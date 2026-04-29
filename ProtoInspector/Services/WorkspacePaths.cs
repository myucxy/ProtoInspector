namespace ProtoInspector.Services;

public static class WorkspacePaths
{
    private const string WorkspaceFolderName = "ProtocolWorkspace";
    private const string ProtocFileName = "protoc.exe";

    public static string GetWorkspaceRoot()
    {
        foreach (var basePath in GetCandidateBasePaths())
        {
            var current = new DirectoryInfo(basePath);
            while (current is not null)
            {
                var workspacePath = Path.Combine(current.FullName, WorkspaceFolderName);
                if (Directory.Exists(workspacePath))
                {
                    return workspacePath;
                }

                current = current.Parent;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, WorkspaceFolderName);
    }

    public static string GetGeneratedDirectory() => Path.Combine(GetWorkspaceRoot(), "generated");

    public static string GetProtoDirectory() => Path.Combine(GetWorkspaceRoot(), "proto");

    public static string GetBytesDirectory() => Path.Combine(GetWorkspaceRoot(), "bytes");

    public static string GetDefaultByteFile() => Path.Combine(GetBytesDirectory(), "byte.txt");

    public static string? FindProtocPath()
    {
        foreach (var basePath in GetCandidateBasePaths())
        {
            var current = new DirectoryInfo(basePath);
            while (current is not null)
            {
                var protocPath = Path.Combine(current.FullName, ProtocFileName);
                if (File.Exists(protocPath))
                {
                    return protocPath;
                }

                current = current.Parent;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidateBasePaths()
    {
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
    }
}
