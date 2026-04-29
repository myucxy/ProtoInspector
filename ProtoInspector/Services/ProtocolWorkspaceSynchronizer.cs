using System.Diagnostics;
using System.Text;

namespace ProtoInspector.Services;

public static class ProtocolWorkspaceSynchronizer
{
    public static IReadOnlyList<ProtocolGenerationItem> FindMissingGeneratedFiles()
    {
        var protoDirectory = WorkspacePaths.GetProtoDirectory();
        var generatedDirectory = WorkspacePaths.GetGeneratedDirectory();
        if (!Directory.Exists(protoDirectory))
        {
            return Array.Empty<ProtocolGenerationItem>();
        }

        Directory.CreateDirectory(generatedDirectory);

        return Directory.GetFiles(protoDirectory, "*.proto", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(protoFile => new ProtocolGenerationItem(
                protoFile,
                Path.Combine(generatedDirectory, Path.ChangeExtension(Path.GetFileName(protoFile), ".cs")!)))
            .Where(item => !File.Exists(item.GeneratedFile))
            .ToArray();
    }

    public static void GenerateMissingFiles(IReadOnlyList<ProtocolGenerationItem> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var protocPath = WorkspacePaths.FindProtocPath();
        if (string.IsNullOrWhiteSpace(protocPath))
        {
            throw new FileNotFoundException("未找到 protoc.exe，无法自动生成协议代码。");
        }

        var protoDirectory = WorkspacePaths.GetProtoDirectory();
        var generatedDirectory = WorkspacePaths.GetGeneratedDirectory();
        Directory.CreateDirectory(generatedDirectory);

        foreach (var item in items)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = protocPath,
                Arguments = $"--proto_path=\"{protoDirectory}\" --csharp_out=\"{generatedDirectory}\" \"{item.ProtoFile}\"",
                WorkingDirectory = WorkspacePaths.GetWorkspaceRoot(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"无法启动 protoc: {protocPath}");

            var stdOut = process.StandardOutput.ReadToEnd();
            var stdErr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"生成协议失败: {Path.GetFileName(item.ProtoFile)}{Environment.NewLine}{stdOut}{Environment.NewLine}{stdErr}");
            }
        }
    }
}

public sealed record ProtocolGenerationItem(string ProtoFile, string GeneratedFile);
