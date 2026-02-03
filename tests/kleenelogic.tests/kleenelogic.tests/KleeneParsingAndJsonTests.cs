#nullable enable
using System;
using System.Text.Json;
using KleeneLogic;
using KleeneLogic.Serialization;
using Xunit;

namespace KleeneLogic.Tests;

public sealed class KleeneParsingAndJsonTests
{
    [Theory]
    [InlineData("True",  1)]
    [InlineData("true",  1)]
    [InlineData(" FALSE ", -1)]
    [InlineData("unknown", 0)]
    [InlineData("-1", -1)]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    public void TryParse_AcceptsCanonicalInputs(string input, sbyte expectedRaw)
    {
        Assert.True(Kleene.TryParse(input, out var k));
        Assert.Equal(expectedRaw, k.Raw);
    }

    [Theory]
    [InlineData("")] // empty string
    [InlineData("   ")] // whitespace only
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("t")]
    [InlineData("f")]
    [InlineData("maybe")]
    [InlineData("2")]
    [InlineData("-2")]
    [InlineData("Truee")]
    public void TryParse_RejectsNonCanonicalInputs(string input)
    {
        Assert.False(Kleene.TryParse(input, out _));
    }

    [Theory]
    [InlineData((sbyte)-1, "False")]
    [InlineData((sbyte)0,  "Unknown")]
    [InlineData((sbyte)1,  "True")]
    public void TryFormat_WritesExpectedText(sbyte raw, string expected)
    {
        var k = Kleene.FromRaw(raw);

        Span<char> buf = stackalloc char[7];
        Assert.True(k.TryFormat(buf, out var written));

        var s = new string(buf[..written]);
        Assert.Equal(expected, s);
    }

    [Fact]
    public void TryFormat_ReturnsFalse_WhenBufferTooSmall()
    {
        var k = Kleene.Unknown; // "Unknown" = 7 chars
        Span<char> buf = stackalloc char[6];

        Assert.False(k.TryFormat(buf, out var written));
        Assert.Equal(0, written);
    }

    [Theory]
    [InlineData((sbyte)-1)]
    [InlineData((sbyte)0)]
    [InlineData((sbyte)1)]
    public void RoundTrip_ToString_Parse(sbyte raw)
    {
        var k1 = Kleene.FromRaw(raw);
        var text = k1.ToString();
        var k2 = Kleene.Parse(text);

        Assert.Equal(k1, k2);
    }

    [Fact]
    public void Json_Reads_Bool_Null_String_Number_AndWritesString()
    {
        var opts = new JsonSerializerOptions();
        opts.Converters.Add(new KleeneJsonConverter());

        // Read: bool
        Assert.Equal(Kleene.True,  JsonSerializer.Deserialize<Kleene>("true", opts));
        Assert.Equal(Kleene.False, JsonSerializer.Deserialize<Kleene>("false", opts));

        // Read: null -> Unknown
        Assert.Equal(Kleene.Unknown, JsonSerializer.Deserialize<Kleene>("null", opts));

        // Read: string tokens
        Assert.Equal(Kleene.True,    JsonSerializer.Deserialize<Kleene>("\"true\"", opts));
        Assert.Equal(Kleene.False,   JsonSerializer.Deserialize<Kleene>("\"FALSE\"", opts));
        Assert.Equal(Kleene.Unknown, JsonSerializer.Deserialize<Kleene>("\"unknown\"", opts));
        Assert.Equal(Kleene.False,   JsonSerializer.Deserialize<Kleene>("\"-1\"", opts));
        Assert.Equal(Kleene.Unknown, JsonSerializer.Deserialize<Kleene>("\"0\"", opts));
        Assert.Equal(Kleene.True,    JsonSerializer.Deserialize<Kleene>("\"1\"", opts));

        // Read: numeric tokens
        Assert.Equal(Kleene.False,   JsonSerializer.Deserialize<Kleene>("-1", opts));
        Assert.Equal(Kleene.Unknown, JsonSerializer.Deserialize<Kleene>("0", opts));
        Assert.Equal(Kleene.True,    JsonSerializer.Deserialize<Kleene>("1", opts));

        // Write: always a JSON string
        Assert.Equal("\"True\"",    JsonSerializer.Serialize(Kleene.True, opts));
        Assert.Equal("\"False\"",   JsonSerializer.Serialize(Kleene.False, opts));
        Assert.Equal("\"Unknown\"", JsonSerializer.Serialize(Kleene.Unknown, opts));
    }

    [Theory]
    [InlineData("\"yes\"")]
    [InlineData("\"\"")]
    [InlineData("\"maybe\"")]
    [InlineData("2")]
    [InlineData("-2")]
    [InlineData("1.0")] // reject non-integer JSON numbers
    [InlineData("{}")]
    [InlineData("[]")]
    public void Json_RejectsInvalidInputs(string json)
    {
        var opts = new JsonSerializerOptions();
        opts.Converters.Add(new KleeneJsonConverter());

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Kleene>(json, opts));
    }
}
