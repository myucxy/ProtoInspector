using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace ProtoInspector.Models;

public sealed class ProtocolMessageDefinition
{
    public required string FullName { get; init; }
    public required string DisplayName { get; init; }
    public required MessageParser Parser { get; init; }
    public required MessageDescriptor Descriptor { get; init; }

    public override string ToString() => DisplayName;
}
