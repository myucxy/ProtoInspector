namespace ProtoInspector.Models;

public sealed class ProtocolFileOption
{
    public required string DisplayName { get; init; }
    public required string FullPath { get; init; }

    public override string ToString() => DisplayName;
}
