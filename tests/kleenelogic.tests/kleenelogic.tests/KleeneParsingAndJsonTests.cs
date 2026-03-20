#nullable enable
using System;
using System.Globalization;
using System.Text.Json;
using KleeneLogic;
using KleeneLogic.Serialization;
using Xunit;

namespace KleeneLogic.Tests;

public sealed class KleeneParsingAndJsonTests
{
    [Fact]
    public void TryParse_ReturnsFalse_OnNull()
    {
        string? input = null;
        Assert.False(Kleene.TryParse(input, out _));
    }

    [Theory]
    [InlineData("true",  1)]
    [InlineData("false",  -1)]
    [InlineData("unknown", 0)]
    [InlineData("maybe", 0)]
    [InlineData("True",  1)]
    [InlineData(" FALSE ", -1)]
    [InlineData("-1", -1)]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    public void TryParse_AcceptsInvariantAndLegacyInputs(string input, sbyte expectedRaw)
    {
        Assert.True(Kleene.TryParse(input, out var k));
        Assert.Equal(expectedRaw, k.Raw);
    }

    [Theory]
    [InlineData("")] // empty string
    [InlineData("   ")] // whitespace only
    [InlineData("t")]
    [InlineData("f")]
    [InlineData("2")]
    [InlineData("-2")]
    [InlineData("Truee")]
    public void TryParse_RejectsInvalidInputs(string input)
    {
        Assert.False(Kleene.TryParse(input, out _));
    }

    [Fact]
    public void Parse_Throws_OnNull()
    {
        string? input = null;
        Assert.Throws<FormatException>(() => Kleene.Parse(input!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("perhaps")]
    public void Parse_Throws_OnInvalidInputs(string input)
    {
        Assert.Throws<FormatException>(() => Kleene.Parse(input));
    }

    [Theory]
    [InlineData((sbyte)-1, "false")]
    [InlineData((sbyte)0,  "unknown")]
    [InlineData((sbyte)1,  "true")]
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
        var k = Kleene.Unknown; // "unknown" = 7 chars
        Span<char> buf = stackalloc char[6];

        Assert.False(k.TryFormat(buf, out var written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void DefaultMethods_StayInvariant_WhenCurrentCultureDiffers()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");

            Assert.Equal("true", Kleene.True.ToString());
            Assert.True(Kleene.TryParse("true", out var yes) && yes == Kleene.True);
            Assert.False(Kleene.TryParse("ja", out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("en-US", "yes", "no", "unknown")]
    [InlineData("nl-NL", "ja", "no", "onbekend")]
    [InlineData("nl-BE", "ja", "no", "onbekend")]
    [InlineData("fr-FR", "oui", "non", "inconnu")]
    [InlineData("ru-RU", "да", "нет", "неизвестно")]
    [InlineData("de-DE", "ja", "nein", "unbekannt")]
    [InlineData("tr-TR", "evet", "hayır", "bilinmiyor")]
    [InlineData("cs-CZ", "ano", "ne", "neznámé")]
    [InlineData("ar", "نعم", "لا", "غير معروف")]
    [InlineData("fa", "بله", "نه", "نامشخص")]
    [InlineData("fy", "ja", "nee", "ûnbekend")]
    [InlineData("cy", "ie", "na", "anhysbys")]
    [InlineData("ja-JP", "はい", "いいえ", "不明")]
    public void ToString_UsesExplicitCulture(string cultureName, string trueText, string falseText, string unknownText)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);

        Assert.Equal(trueText, Kleene.True.ToString(culture));
        Assert.Equal(falseText, Kleene.False.ToString(culture));
        Assert.Equal(unknownText, Kleene.Unknown.ToString(culture));
    }

    [Theory]
    [InlineData("en-US", "yes", 1)]
    [InlineData("en-US", "no", -1)]
    [InlineData("en-US", "unknown", 0)]
    [InlineData("en-US", "maybe", 0)]
    [InlineData("nl-NL", "ja", 1)]
    [InlineData("nl-BE", "onbekend", 0)]
    [InlineData("fr-FR", "oui", 1)]
    [InlineData("fr-FR", "inconnu", 0)]
    [InlineData("ru-RU", "да", 1)]
    [InlineData("ru-RU", "неизвестно", 0)]
    [InlineData("de-DE", "nein", -1)]
    [InlineData("de-DE", "unbekannt", 0)]
    [InlineData("tr-TR", "hayır", -1)]
    [InlineData("tr-TR", "bilinmiyor", 0)]
    [InlineData("cs-CZ", "neznámé", 0)]
    [InlineData("ar", "نعم", 1)]
    [InlineData("ar", "غير معروف", 0)]
    [InlineData("fa", "بله", 1)]
    [InlineData("fa", "نامشخص", 0)]
    [InlineData("fy", "nee", -1)]
    [InlineData("fy", "ûnbekend", 0)]
    [InlineData("cy", "na", -1)]
    [InlineData("cy", "anhysbys", 0)]
    [InlineData("ja-JP", "いいえ", -1)]
    public void Parse_AndTryParse_UseExplicitCulture(string cultureName, string input, sbyte expectedRaw)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);

        Assert.True(Kleene.TryParse(input, culture, out var parsed));
        Assert.Equal(expectedRaw, parsed.Raw);
        Assert.Equal(expectedRaw, Kleene.Parse(input, culture).Raw);
    }

    [Fact]
    public void Parse_WithExplicitCulture_StillAcceptsLegacyAndNumericAliases()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");

        Assert.Equal(Kleene.True, Kleene.Parse("true", culture));
        Assert.Equal(Kleene.False, Kleene.Parse("false", culture));
        Assert.Equal(Kleene.Unknown, Kleene.Parse("unknown", culture));
        Assert.Equal(Kleene.True, Kleene.Parse("1", culture));
        Assert.Equal(Kleene.False, Kleene.Parse("-1", culture));
        Assert.Equal(Kleene.Unknown, Kleene.Parse("0", culture));
    }

    [Fact]
    public void MaybeAliases_AreAcceptedAsUnknown()
    {
        var dutchCulture = CultureInfo.GetCultureInfo("nl-NL");

        Assert.Equal(Kleene.Unknown, Kleene.Parse("maybe"));
        Assert.Equal(Kleene.Unknown, Kleene.Parse("misschien", dutchCulture));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("ja")]
    [InlineData("ar")]
    [InlineData("fa")]
    [InlineData("fy")]
    [InlineData("cy")]
    public void InvariantTokens_AreAccepted_RegardlessOfExplicitCulture(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);

        Assert.True(Kleene.TryParse("true", culture, out var parsedTrue));
        Assert.Equal(Kleene.True, parsedTrue);
        Assert.True(Kleene.TryParse("false", culture, out var parsedFalse));
        Assert.Equal(Kleene.False, parsedFalse);
        Assert.True(Kleene.TryParse("maybe", culture, out var parsedMaybe));
        Assert.Equal(Kleene.Unknown, parsedMaybe);

        Assert.Equal(Kleene.True, Kleene.Parse("true", culture));
        Assert.Equal(Kleene.False, Kleene.Parse("false", culture));
        Assert.Equal(Kleene.Unknown, Kleene.Parse("maybe", culture));
    }

    [Fact]
    public void UnsupportedCulture_FallsBackToInvariantTerms()
    {
        var culture = CultureInfo.GetCultureInfo("af-ZA");

        Assert.Equal("true", Kleene.True.ToString(culture));
        Assert.True(Kleene.TryParse("true", culture, out var yes) && yes == Kleene.True);
        Assert.False(Kleene.TryParse("ja", culture, out _));
    }

    [Fact]
    public void LanguageTermsJson_IsPublishedNextToAssembly()
    {
        var assemblyPath = typeof(Kleene).Assembly.Location;
        var assemblyDir = Path.GetDirectoryName(assemblyPath);
        Assert.NotNull(assemblyDir);

        var termsPath = Path.Combine(assemblyDir!, "kleene.language-terms.json");
        Assert.True(File.Exists(termsPath));
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
    public void Json_Reads_Bool_Null_String_Number_AndWritesBoolStyleTokens()
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
        Assert.Equal(Kleene.False,   JsonSerializer.Deserialize<Kleene>("\"false\"", opts));
        Assert.Equal(Kleene.Unknown, JsonSerializer.Deserialize<Kleene>("\"unknown\"", opts));
        Assert.Equal(Kleene.Unknown, JsonSerializer.Deserialize<Kleene>("\"maybe\"", opts));
        Assert.Equal(Kleene.True,    JsonSerializer.Deserialize<Kleene>("\"TRUE\"", opts));
        Assert.Equal(Kleene.False,   JsonSerializer.Deserialize<Kleene>("\"FALSE\"", opts));
        Assert.Equal(Kleene.False,   JsonSerializer.Deserialize<Kleene>("\"-1\"", opts));
        Assert.Equal(Kleene.Unknown, JsonSerializer.Deserialize<Kleene>("\"0\"", opts));
        Assert.Equal(Kleene.True,    JsonSerializer.Deserialize<Kleene>("\"1\"", opts));

        // Read: numeric tokens
        Assert.Equal(Kleene.False,   JsonSerializer.Deserialize<Kleene>("-1", opts));
        Assert.Equal(Kleene.Unknown, JsonSerializer.Deserialize<Kleene>("0", opts));
        Assert.Equal(Kleene.True,    JsonSerializer.Deserialize<Kleene>("1", opts));

        // Write: bool? compatible tokens
        Assert.Equal("true",  JsonSerializer.Serialize(Kleene.True, opts));
        Assert.Equal("false", JsonSerializer.Serialize(Kleene.False, opts));
        Assert.Equal("null",  JsonSerializer.Serialize(Kleene.Unknown, opts));
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"oui\"")]
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

    [Theory]
    [InlineData("1.0")]
    [InlineData("-1.0")]
    [InlineData("1e0")]
    [InlineData("1E0")]
    public void Json_RejectsNonIntegerNumbers(string json)
    {
        var opts = new JsonSerializerOptions();
        opts.Converters.Add(new KleeneJsonConverter());

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Kleene>(json, opts));
    }
}
