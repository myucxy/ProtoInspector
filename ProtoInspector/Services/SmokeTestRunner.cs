using ProtoInspector.Models;

namespace ProtoInspector.Services;

public static class SmokeTestRunner
{
    public static Task<int> RunAsync(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--serialize", StringComparison.OrdinalIgnoreCase))
        {
            return RunSerializeAsync(args.Skip(1).ToArray());
        }

        return RunDeserializeAsync(args);
    }

    private static Task<int> RunDeserializeAsync(string[] args)
    {
        var protocolFile = args.ElementAtOrDefault(0)
            ?? Path.Combine(WorkspacePaths.GetGeneratedDirectory(), "ProtobufByCTrd.cs");

        var messageType = args.ElementAtOrDefault(1)
            ?? "Ctrd.Protocol.Pb_JysQueryOrderMessage";

        var byteFile = args.ElementAtOrDefault(2)
            ?? WorkspacePaths.GetDefaultByteFile();

        using var session = ProtocolCompiler.Compile(protocolFile);
        var message = FindMessage(session.Messages, messageType);
        var bytes = ByteInputParser.Parse(File.ReadAllText(byteFile));
        var parsed = message.Parser.ParseFrom(bytes);

        WriteUtf8Output(ProtoMessageFormatter.Format(parsed, bytes, message.DisplayName));
        return Task.FromResult(0);
    }

    private static Task<int> RunSerializeAsync(string[] args)
    {
        var protocolFile = args.ElementAtOrDefault(0)
            ?? Path.Combine(WorkspacePaths.GetGeneratedDirectory(), "ProtobufByCTrd.cs");

        var messageType = args.ElementAtOrDefault(1)
            ?? "Ctrd.Protocol.Pb_JysQueryOrderMessage";

        var jsonFile = args.ElementAtOrDefault(2)
            ?? throw new InvalidOperationException("请提供 JSON 输入文件。");

        using var session = ProtocolCompiler.Compile(protocolFile);
        var message = FindMessage(session.Messages, messageType);
        var result = ProtoMessageSerializer.SerializeFromJson(File.ReadAllText(jsonFile), message);

        WriteUtf8Output(ProtoMessageFormatter.FormatSerialized(result.Message, result.Bytes, message.DisplayName));
        return Task.FromResult(0);
    }

    private static ProtocolMessageDefinition FindMessage(IReadOnlyList<ProtocolMessageDefinition> messages, string messageType)
    {
        var match = messages.FirstOrDefault(item =>
            string.Equals(item.DisplayName, messageType, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.FullName, messageType, StringComparison.OrdinalIgnoreCase));

        return match ?? throw new InvalidOperationException($"未找到消息类型: {messageType}");
    }

    private static void WriteUtf8Output(string text)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
        }

        Console.WriteLine(text);
    }
}
