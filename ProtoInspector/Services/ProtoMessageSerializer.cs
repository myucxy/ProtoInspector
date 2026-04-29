using Google.Protobuf;
using ProtoInspector.Models;

namespace ProtoInspector.Services;

public static class ProtoMessageSerializer
{
    public static ProtoSerializationResult SerializeFromJson(string json, ProtocolMessageDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("JSON 输入为空。");
        }

        var message = JsonParser.Default.Parse(json, definition.Descriptor);
        var bytes = message.ToByteArray();
        return new ProtoSerializationResult(message, bytes);
    }
}

public sealed record ProtoSerializationResult(IMessage Message, byte[] Bytes);
