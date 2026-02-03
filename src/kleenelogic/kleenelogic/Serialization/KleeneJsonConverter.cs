using System.Text.Json;
using System.Text.Json.Serialization;

namespace KleeneLogic.Serialization;

public sealed class KleeneJsonConverter : JsonConverter<Kleene>
{
    public override Kleene Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return Kleene.True;
            case JsonTokenType.False:
                return Kleene.False;
            case JsonTokenType.Null:
                return Kleene.Unknown;
            case JsonTokenType.String:
            {
                var s = reader.GetString();
                if (Kleene.TryParse(s, out var value))
                    return value;
                throw new JsonException($"Invalid Kleene string value: '{s}'.");
            }
            case JsonTokenType.Number:
            {
                var raw = GetRawNumberText(ref reader);
                if (raw.IndexOfAny(['.', 'e', 'E']) >= 0)
                    throw new JsonException($"Invalid Kleene numeric value: '{raw}'.");

                if (!reader.TryGetInt32(out var i) || (i != -1 && i != 0 && i != 1))
                    throw new JsonException($"Invalid Kleene numeric value: '{raw}'.");

                return Kleene.FromRaw((sbyte)i);
            }
            default:
                throw new JsonException($"Invalid token for Kleene: {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, Kleene value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    private static string GetRawNumberText(ref Utf8JsonReader reader)
    {
        if (!reader.HasValueSequence)
            return System.Text.Encoding.UTF8.GetString(reader.ValueSpan);

        var seq = reader.ValueSequence;
        var bytes = new byte[(int)seq.Length];
        var offset = 0;
        foreach (var segment in seq)
        {
            segment.Span.CopyTo(bytes.AsSpan(offset));
            offset += segment.Length;
        }
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
