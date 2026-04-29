using ProtoInspector.Services;

namespace ProtoInspector.Models;

public sealed class ProtocolSession : IDisposable
{
    private readonly CollectibleProtocolLoadContext _loadContext;

    public ProtocolSession(string sourceFile, IReadOnlyList<ProtocolMessageDefinition> messages, CollectibleProtocolLoadContext loadContext)
    {
        SourceFile = sourceFile;
        Messages = messages;
        _loadContext = loadContext;
    }

    public string SourceFile { get; }

    public IReadOnlyList<ProtocolMessageDefinition> Messages { get; }

    public void Dispose()
    {
        _loadContext.Unload();
    }
}
