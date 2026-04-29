using System.Globalization;
using System.Text.RegularExpressions;

namespace ProtoInspector.Services;

public static class ByteInputParser
{
    public static byte[] Parse(string text)
    {
        var format = NormalizeByteText(text, out var normalized);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("字节输入为空。");
        }

        if (format == ByteInputFormat.Hex)
        {
            return ParseHexText(normalized);
        }

        if (format == ByteInputFormat.Base64)
        {
            return TryParseBase64(normalized, out var explicitBase64Bytes)
                ? explicitBase64Bytes
                : throw new FormatException("Base64 字节格式不正确。");
        }

        var tokens = Regex.Matches(normalized, @"0x[0-9A-Fa-f]+|-?\d+|[0-9A-Fa-f]+")
            .Select(match => match.Value)
            .ToArray();

        if (format == ByteInputFormat.Decimal)
        {
            return tokens.Length == 0
                ? throw new InvalidOperationException("未从输入中解析出任何字节。")
                : ParseDecimalTokens(tokens);
        }

        if (LooksLikeHexInput(normalized, tokens))
        {
            return ParseHexText(normalized);
        }

        if (TryParseBase64(normalized, out var base64Bytes))
        {
            return base64Bytes;
        }

        if (LooksLikeBase64(normalized))
        {
            throw new FormatException("Base64 字节格式不正确。");
        }

        if (tokens.Length == 0)
        {
            throw new InvalidOperationException("未从输入中解析出任何字节。");
        }

        return ShouldParseAsHex(normalized, tokens)
            ? ParseHexText(normalized)
            : ParseDecimalTokens(tokens);
    }

    private static ByteInputFormat NormalizeByteText(string text, out string normalized)
    {
        normalized = RemoveComments(text).Trim();
        var match = Regex.Match(normalized, @"(?im)^\s*(Hex|Base64|Decimal)\s*:\s*(.+)$");
        if (!match.Success)
        {
            return ByteInputFormat.Auto;
        }

        normalized = match.Groups[2].Value.Trim();
        return match.Groups[1].Value.ToUpperInvariant() switch
        {
            "HEX" => ByteInputFormat.Hex,
            "BASE64" => ByteInputFormat.Base64,
            "DECIMAL" => ByteInputFormat.Decimal,
            _ => ByteInputFormat.Auto
        };
    }

    private static bool LooksLikeHexInput(string text, IReadOnlyCollection<string> tokens)
    {
        if (text.Contains("0x", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var compact = Regex.Replace(text, @"[\s,;_\-]", string.Empty);
        return compact.Length > 0
               && compact.Length % 2 == 0
               && Regex.IsMatch(compact, @"\A[0-9A-Fa-f]+\z")
               && string.Equals(string.Concat(tokens), compact, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseBase64(string text, out byte[] bytes)
    {
        bytes = [];
        var compact = Regex.Replace(text, @"\s+", string.Empty);
        if (!LooksLikeBase64(compact))
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(compact);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool LooksLikeBase64(string text)
    {
        var compact = Regex.Replace(text, @"\s+", string.Empty);
        if (compact.Length < 8 || compact.Length % 4 != 0)
        {
            return false;
        }

        if (!Regex.IsMatch(compact, @"^[A-Za-z0-9+/]*={0,2}$"))
        {
            return false;
        }

        return Regex.IsMatch(compact, "[G-Zg-z+/=]");
    }

    private static string RemoveComments(string text)
    {
        var noSlashComments = Regex.Replace(text, @"//.*?$", string.Empty, RegexOptions.Multiline);
        return Regex.Replace(noSlashComments, @"#.*?$", string.Empty, RegexOptions.Multiline);
    }

    private static bool ShouldParseAsHex(string text, string[] tokens)
    {
        if (text.Contains("0x", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(text, "[A-Fa-f]"))
        {
            return true;
        }

        return tokens.Length == 1 && tokens[0].Length > 2 && tokens[0].Length % 2 == 0;
    }

    private static byte[] ParseHexText(string text)
    {
        var compact = Regex.Replace(text, @"0x", string.Empty, RegexOptions.IgnoreCase);
        compact = Regex.Replace(compact, @"[\s,;_\-]", string.Empty);
        if (compact.Length == 0 || !Regex.IsMatch(compact, @"\A[0-9A-Fa-f]+\z"))
        {
            throw new FormatException("十六进制字节格式不正确。");
        }

        if (compact.Length % 2 != 0)
        {
            throw new FormatException($"十六进制字节格式不正确: {compact}");
        }

        var bytes = new byte[compact.Length / 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
        }

        return bytes;
    }

    private static byte[] ParseHexTokens(IEnumerable<string> tokens)
    {
        var bytes = new List<byte>();
        foreach (var token in tokens)
        {
            var hex = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? token[2..] : token;
            if (hex.Length % 2 != 0)
            {
                throw new FormatException($"十六进制字节格式不正确: {token}");
            }

            for (var index = 0; index < hex.Length; index += 2)
            {
                bytes.Add(Convert.ToByte(hex.Substring(index, 2), 16));
            }
        }

        return bytes.ToArray();
    }

    private static byte[] ParseDecimalTokens(IEnumerable<string> tokens)
    {
        return tokens.Select(token =>
        {
            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException($"十进制字节格式不正确: {token}");
            }

            if (value is >= 0 and <= 255)
            {
                return (byte)value;
            }

            if (value is >= -128 and <= -1)
            {
                return unchecked((byte)(sbyte)value);
            }

            throw new FormatException($"十进制字节超出范围(-128~255): {token}");
        }).ToArray();
    }
}

internal enum ByteInputFormat
{
    Auto,
    Hex,
    Base64,
    Decimal
}
