using System.Text.Json;
using Google.Protobuf.Reflection;
using ProtoInspector.Models;

namespace ProtoInspector.Services;

public static class ProtoJsonSampleGenerator
{
    private const int MaxDepth = 3;

    public static string Generate(ProtocolMessageDefinition definition)
    {
        var sample = CreateMessageSample(definition.Descriptor, 0, new HashSet<string>());
        return JsonSerializer.Serialize(sample, new JsonSerializerOptions { WriteIndented = true });
    }

    private static Dictionary<string, object?> CreateMessageSample(
        MessageDescriptor descriptor,
        int depth,
        ISet<string> descriptorStack)
    {
        if (depth >= MaxDepth || !descriptorStack.Add(descriptor.FullName))
        {
            return [];
        }

        var sample = new Dictionary<string, object?>();
        foreach (var field in descriptor.Fields.InFieldNumberOrder())
        {
            sample[field.JsonName] = CreateFieldSample(field, depth, descriptorStack);
        }

        descriptorStack.Remove(descriptor.FullName);
        return sample;
    }

    private static object? CreateFieldSample(FieldDescriptor field, int depth, ISet<string> descriptorStack)
    {
        if (field.IsMap)
        {
            var valueField = field.MessageType.FindFieldByName("value");
            return new Dictionary<string, object?>
            {
                ["sampleKey"] = valueField is null ? null : CreateSingularFieldSample(valueField, depth + 1, descriptorStack)
            };
        }

        if (field.IsRepeated)
        {
            return new[] { CreateSingularFieldSample(field, depth, descriptorStack) };
        }

        return CreateSingularFieldSample(field, depth, descriptorStack);
    }

    private static object? CreateSingularFieldSample(FieldDescriptor field, int depth, ISet<string> descriptorStack)
    {
        return field.FieldType switch
        {
            FieldType.Double => 1.23,
            FieldType.Float => 1.23f,
            FieldType.Int64 or FieldType.SInt64 or FieldType.SFixed64 => "1",
            FieldType.UInt64 or FieldType.Fixed64 => "1",
            FieldType.Int32 or FieldType.SInt32 or FieldType.SFixed32 => 1,
            FieldType.UInt32 or FieldType.Fixed32 => 1,
            FieldType.Bool => true,
            FieldType.String => $"sample_{field.JsonName}",
            FieldType.Bytes => "AQID",
            FieldType.Enum => field.EnumType.Values.FirstOrDefault()?.Name,
            FieldType.Message or FieldType.Group => CreateMessageSample(field.MessageType, depth + 1, descriptorStack),
            _ => null
        };
    }
}
