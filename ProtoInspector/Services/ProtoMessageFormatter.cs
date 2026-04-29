using System.Collections;
using System.Globalization;
using System.Text;
using Google.Protobuf;

namespace ProtoInspector.Services;

public static class ProtoMessageFormatter
{
    public static string Format(IMessage message, byte[] bytes, string title)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Type: {title}");
        builder.AppendLine($"ByteLength: {bytes.Length}");
        builder.AppendLine($"Hex: {Convert.ToHexString(bytes)}");
        builder.AppendLine("FieldDump:");
        AppendMessage(builder, message, 0, message.Descriptor.Name);
        return builder.ToString();
    }

    public static string FormatSerialized(IMessage message, byte[] bytes, string title)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Type: {title}");
        builder.AppendLine($"ByteLength: {bytes.Length}");
        builder.AppendLine($"Hex: {Convert.ToHexString(bytes)}");
        builder.AppendLine($"Base64: {Convert.ToBase64String(bytes)}");
        builder.AppendLine($"Decimal: [{string.Join(", ", bytes.Select(item => item.ToString(CultureInfo.InvariantCulture)))}]");
        builder.AppendLine($"CSharpBytes: new byte[] {{ {string.Join(", ", bytes.Select(item => $"0x{item:X2}"))} }}");
        builder.AppendLine("FieldDump:");
        AppendMessage(builder, message, 0, message.Descriptor.Name);
        return builder.ToString();
    }

    private static void AppendMessage(StringBuilder builder, IMessage message, int indent, string title)
    {
        var pad = new string(' ', indent * 2);
        builder.AppendLine($"{pad}{title}");

        foreach (var field in message.Descriptor.Fields.InFieldNumberOrder())
        {
            if (field.IsRepeated)
            {
                var value = field.Accessor.GetValue(message);
                if (value is not IEnumerable items)
                {
                    continue;
                }

                var index = 0;
                foreach (var item in items)
                {
                    if (index == 0)
                    {
                        builder.AppendLine($"{pad}  {field.Name}:");
                    }

                    if (item is IMessage childMessage)
                    {
                        AppendMessage(builder, childMessage, indent + 2, $"[{index}] {childMessage.Descriptor.Name}");
                    }
                    else
                    {
                        builder.AppendLine($"{pad}    [{index}] = {FormatScalar(item)}");
                    }

                    index++;
                }

                continue;
            }

            if (!field.Accessor.HasValue(message))
            {
                continue;
            }

            var fieldValue = field.Accessor.GetValue(message);
            if (fieldValue is IMessage child)
            {
                AppendMessage(builder, child, indent + 1, field.Name);
                continue;
            }

            builder.AppendLine($"{pad}  {field.Name}: {FormatScalar(fieldValue)}");
        }
    }

    private static string FormatScalar(object? value)
    {
        return value switch
        {
            null => "<null>",
            ByteString byteString => Convert.ToHexString(byteString.ToByteArray()),
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }
}
