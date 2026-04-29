using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Loader;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ProtoInspector.Models;

namespace ProtoInspector.Services;

public static class ProtocolCompiler
{
    public static ProtocolSession Compile(string sourceFile)
    {
        if (!File.Exists(sourceFile))
        {
            throw new FileNotFoundException($"协议文件不存在: {sourceFile}");
        }

        var sourceText = File.ReadAllText(sourceFile);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, new CSharpParseOptions(LanguageVersion.Latest));

        using var metadataReferences = GetMetadataReferences();
        var assemblyName = $"DynamicProto_{Path.GetFileNameWithoutExtension(sourceFile)}_{Guid.NewGuid():N}";
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            metadataReferences.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

        using var peStream = new MemoryStream();

        var emitResult = compilation.Emit(peStream);
        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.ToString());

            throw new InvalidOperationException("动态编译失败:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        peStream.Position = 0;

        var loadContext = new CollectibleProtocolLoadContext();
        var assembly = loadContext.LoadFromStream(peStream);
        var messages = DiscoverMessages(assembly);
        if (messages.Count == 0)
        {
            loadContext.Unload();
            throw new InvalidOperationException("当前协议文件没有找到任何可解析的 Protobuf 消息类型。");
        }

        return new ProtocolSession(sourceFile, messages, loadContext);
    }

    private static IReadOnlyList<ProtocolMessageDefinition> DiscoverMessages(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IMessage).IsAssignableFrom(type))
            .Select(type =>
            {
                var parserProperty = type.GetProperty("Parser", BindingFlags.Public | BindingFlags.Static);
                if (parserProperty?.GetValue(null) is not MessageParser parser)
                {
                    return null;
                }

                var descriptorProperty = type.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static);
                if (descriptorProperty?.GetValue(null) is not MessageDescriptor descriptor)
                {
                    return null;
                }

                return new ProtocolMessageDefinition
                {
                    FullName = type.FullName ?? type.Name,
                    DisplayName = type.FullName ?? type.Name,
                    Parser = parser,
                    Descriptor = descriptor
                };
            })
            .Where(definition => definition is not null)
            .Cast<ProtocolMessageDefinition>()
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static MetadataReferenceSet GetMetadataReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];

        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        var assemblyMetadata = ImmutableArray.CreateBuilder<AssemblyMetadata>();
        var referencePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referenceIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<Assembly>();
        var visitedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in GetRootAssemblies())
        {
            EnqueueAssembly(queue, visitedAssemblies, assembly);
        }

        while (queue.Count > 0)
        {
            var assembly = queue.Dequeue();
            AddMetadataReference(references, assemblyMetadata, referencePaths, referenceIdentities, trustedPlatformAssemblies, assembly);

            foreach (var referencedAssemblyName in assembly.GetReferencedAssemblies())
            {
                EnqueueAssembly(queue, visitedAssemblies, LoadAssembly(referencedAssemblyName));
            }
        }

        return new MetadataReferenceSet(references.ToImmutable(), assemblyMetadata.ToImmutable());
    }

    private static IEnumerable<Assembly> GetRootAssemblies()
    {
        yield return typeof(object).Assembly;
        yield return typeof(Console).Assembly;
        yield return typeof(Enumerable).Assembly;
        yield return typeof(List<>).Assembly;
        yield return typeof(AssemblyLoadContext).Assembly;
        yield return typeof(IMessage).Assembly;

        foreach (var assemblyName in new[]
                 {
                     "netstandard",
                     "System.Runtime",
                     "System.Collections",
                     "System.Runtime.Extensions",
                     "System.ObjectModel",
                     "System.Memory",
                     "System.Buffers",
                     "System.Runtime.CompilerServices.Unsafe"
                 })
        {
            var assembly = LoadAssembly(new AssemblyName(assemblyName));
            if (assembly is not null)
            {
                yield return assembly;
            }
        }
    }

    private static void EnqueueAssembly(Queue<Assembly> queue, ISet<string> visitedAssemblies, Assembly? assembly)
    {
        if (assembly is null || assembly.IsDynamic)
        {
            return;
        }

        var identity = assembly.FullName ?? assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(identity) || !visitedAssemblies.Add(identity))
        {
            return;
        }

        queue.Enqueue(assembly);
    }

    private static Assembly? LoadAssembly(AssemblyName assemblyName)
    {
        var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => AssemblyName.ReferenceMatchesDefinition(candidate.GetName(), assemblyName));
        if (loadedAssembly is not null)
        {
            return loadedAssembly;
        }

        try
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
        }
        catch
        {
            return null;
        }
    }

    private static void AddMetadataReference(
        ICollection<MetadataReference> references,
        ICollection<AssemblyMetadata> assemblyMetadata,
        ISet<string> referencePaths,
        ISet<string> referenceIdentities,
        IReadOnlyCollection<string> trustedPlatformAssemblies,
        Assembly assembly)
    {
        var identity = assembly.FullName ?? assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(identity))
        {
            return;
        }

        var referencePath = GetAssemblyReferencePath(trustedPlatformAssemblies, assembly);
        if (!string.IsNullOrWhiteSpace(referencePath))
        {
            if (referencePaths.Add(referencePath))
            {
                references.Add(MetadataReference.CreateFromFile(referencePath));
                referenceIdentities.Add(identity);
            }

            return;
        }

        if (!referenceIdentities.Add(identity))
        {
            return;
        }

        if (TryCreateMetadataReference(assembly, out var reference, out var metadata))
        {
            assemblyMetadata.Add(metadata);
            references.Add(reference);
        }
    }

    private static string? GetAssemblyReferencePath(IReadOnlyCollection<string> trustedPlatformAssemblies, Assembly assembly)
    {
        if (!string.IsNullOrWhiteSpace(assembly.Location) && File.Exists(assembly.Location))
        {
            return assembly.Location;
        }

        var assemblyFileName = $"{assembly.GetName().Name}.dll";
        var trustedPath = trustedPlatformAssemblies.FirstOrDefault(path =>
            string.Equals(Path.GetFileName(path), assemblyFileName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(trustedPath) && File.Exists(trustedPath))
        {
            return trustedPath;
        }

        var baseDirectoryPath = Path.Combine(AppContext.BaseDirectory, assemblyFileName);
        return File.Exists(baseDirectoryPath) ? baseDirectoryPath : null;
    }

    private static unsafe bool TryCreateMetadataReference(
        Assembly assembly,
        out PortableExecutableReference reference,
        out AssemblyMetadata metadata)
    {
        reference = null!;
        metadata = null!;

        try
        {
            if (!assembly.TryGetRawMetadata(out var blob, out var length))
            {
                return false;
            }

            var moduleMetadata = ModuleMetadata.CreateFromMetadata((nint)blob, length);
            metadata = AssemblyMetadata.Create(moduleMetadata);
            reference = metadata.GetReference(filePath: $"{assembly.GetName().Name}.dll");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class MetadataReferenceSet : IDisposable
    {
        public MetadataReferenceSet(ImmutableArray<MetadataReference> references, ImmutableArray<AssemblyMetadata> assemblyMetadata)
        {
            References = references;
            this.assemblyMetadata = assemblyMetadata;
        }

        private readonly ImmutableArray<AssemblyMetadata> assemblyMetadata;

        public ImmutableArray<MetadataReference> References { get; }

        public void Dispose()
        {
            foreach (var metadata in assemblyMetadata)
            {
                metadata.Dispose();
            }
        }
    }
}
