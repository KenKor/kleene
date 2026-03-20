#nullable enable
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace KleeneLogic
{
    /// <summary>
    /// Kleene's strong three-valued logic (K3).
    ///
    /// Values are encoded as:
    ///   False   = -1
    ///   Unknown =  0
    ///   True    = +1
    ///
    /// The encoding makes the core operators simple:
    ///   NOT(x)   = -x
    ///   AND(a,b) = min(a,b)
    ///   OR(a,b)  = max(a,b)
    ///
    /// XOR is a chosen lift for K3:
    ///   - Unknown propagates
    ///   - Otherwise behaves like boolean XOR
    ///   XOR(a,b) = -(a*b)
    ///
    /// Control-flow semantics:
    ///   - operator true returns true only for True
    ///   - operator false returns true only for False
    ///   - Unknown is neither
    ///
    /// So:
    ///   if (k) executes only for True
    ///   if (!k) executes only for False
    ///   else means "not definitively true"
    ///
    /// Short-circuiting (&&, ||) happens only for definitive values.
    /// Unknown evaluates the RHS.
    ///
    /// NOTE:
    ///   Using 'else' after 'if (Kleene)' merges False and Unknown.
    ///   For three-way branching, use IsTrue / IsFalse / IsUnknown explicitly.
    /// </summary>
    public readonly struct Kleene : IEquatable<Kleene>, IComparable<Kleene>, IComparable, IFormattable
    {
        // Backing storage: must always be -1, 0, or +1.
        private readonly sbyte _v;

        private Kleene(sbyte v) => _v = v;

        // Canonical singleton values
        public static readonly Kleene False   = new(-1);
        public static readonly Kleene Unknown = new(0);
        public static readonly Kleene True    = new(1);

        private const string LanguageTermsFileName = "kleene.language-terms.json";
        private static readonly JsonSerializerOptions TermsJsonOptions = new() { PropertyNameCaseInsensitive = true };

        private static readonly LocalizedTerms FallbackInvariantTerms = new(
            trueDisplay: "true",
            falseDisplay: "false",
            unknownDisplay: "unknown",
            trueTokens: ["true"],
            falseTokens: ["false"],
            unknownTokens: ["unknown", "maybe"]);

        private static readonly LocalizedTerms FallbackEnglishTerms = new(
            trueDisplay: "yes",
            falseDisplay: "no",
            unknownDisplay: "unknown",
            trueTokens: ["yes"],
            falseTokens: ["no"],
            unknownTokens: ["unknown", "maybe"]);

        private static readonly LocalizedTerms InvariantTerms = new(
            FallbackInvariantTerms.TrueDisplay,
            FallbackInvariantTerms.FalseDisplay,
            FallbackInvariantTerms.UnknownDisplay,
            FallbackInvariantTerms.TrueTokens,
            FallbackInvariantTerms.FalseTokens,
            FallbackInvariantTerms.UnknownTokens);

        private static readonly Dictionary<string, LocalizedTerms> LanguageTermsByIsoCode = new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = FallbackEnglishTerms,
        };

        static Kleene()
        {
            if (TryLoadLanguageTerms(out var loadedInvariantTerms, out var loadedByIsoCode))
            {
                InvariantTerms = loadedInvariantTerms;
                LanguageTermsByIsoCode = loadedByIsoCode;
            }
        }

        /// <summary>
        /// The raw encoded value (-1, 0, +1).
        /// Exposed for interop, debugging, and very hot paths.
        /// </summary>
        public sbyte Raw => _v;

        public bool IsTrue    => _v > 0;
        public bool IsFalse   => _v < 0;
        public bool IsUnknown => _v == 0;

        /// <summary>
        /// Returns this value if it is definitively True or False;
        /// otherwise returns the provided default when the value is Unknown.
        ///
        /// This mirrors Nullable&lt;T&gt;.GetValueOrDefault for Kleene values.
        /// </summary>
        public Kleene Default(Kleene defaultValue)
            => _v == 0 ? defaultValue : this;

        /// <summary>
        /// Returns this value as a boolean if it is definitively True or False;
        /// otherwise returns the provided default when the value is Unknown.
        ///
        /// This is the explicit, intentional way to collapse Kleene logic to bool.
        /// </summary>
        public bool Default(bool defaultValue)
            => _v == 0 ? defaultValue : _v > 0;

        /// <summary>
        /// Parses a Kleene value using invariant culture tokens by default.
        /// Accepted invariant tokens (case-insensitive, trimmed):
        ///   - "true", "false", "unknown"
        ///   - alias for unknown: "maybe"
        ///   - "-1", "0", "1"
        /// </summary>
        public static bool TryParse(string? input, out Kleene value)
            => TryParse(input, CultureInfo.InvariantCulture, out value);

        /// <summary>
        /// Parses a Kleene value using localized tokens for the provided culture,
        /// with invariant/legacy aliases and numeric forms always accepted.
        /// </summary>
        public static bool TryParse(string? input, IFormatProvider? provider, out Kleene value)
        {
            value = default;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            var s = input.Trim();
            var primaryTerms = GetTerms(provider);

            if (TryParseLocalizedToken(s, primaryTerms, out value))
                return true;

            if (!ReferenceEquals(primaryTerms, InvariantTerms) &&
                TryParseLocalizedToken(s, InvariantTerms, out value))
                return true;

            return TryParseNumericToken(s, out value);
        }

        /// <summary>
        /// Parses a Kleene value using invariant culture tokens by default.
        /// </summary>
        public static Kleene Parse(string input)
            => Parse(input, CultureInfo.InvariantCulture);

        /// <summary>
        /// Parses a Kleene value using localized tokens for the provided culture,
        /// with invariant/legacy aliases and numeric forms always accepted.
        /// </summary>
        public static Kleene Parse(string input, IFormatProvider? provider)
        {
            if (!TryParse(input, provider, out var value))
            {
                var terms = GetTerms(provider);
                throw new FormatException(
                    $"Invalid Kleene value: '{input}'. Expected {terms.TrueDisplay}/{terms.FalseDisplay}/{terms.UnknownDisplay} " +
                    "(or invariant true/false/unknown), maybe, or -1/0/1.");
            }

            return value;
        }

        /// <summary>
        /// Fast invariant formatting that writes "true", "false", or "unknown"
        /// into the destination. This is allocation-free and suitable for hot paths.
        /// </summary>
        public bool TryFormat(Span<char> destination, out int charsWritten)
        {
            var text = _v switch
            {
                -1 => InvariantTerms.FalseDisplay,
                 0 => InvariantTerms.UnknownDisplay,
                 1 => InvariantTerms.TrueDisplay,
                _  => "invalid"
            };

            if (destination.Length < text.Length)
            {
                charsWritten = 0;
                return false;
            }

            text.AsSpan().CopyTo(destination);
            charsWritten = text.Length;
            return true;
        }

        /// <summary>
        /// Invariant-culture text formatting.
        /// </summary>
        public override string ToString()
        {
            Span<char> buf = stackalloc char[7]; // "invalid" is 7 chars
            return TryFormat(buf, out var written)
                ? new string(buf[..written])
                : _v switch
                {
                    -1 => InvariantTerms.FalseDisplay,
                     0 => InvariantTerms.UnknownDisplay,
                     1 => InvariantTerms.TrueDisplay,
                    _  => $"invalid({_v})"
                };
        }

        /// <summary>
        /// Localized text formatting for the provided culture.
        /// </summary>
        public string ToString(IFormatProvider? provider)
        {
            var terms = GetTerms(provider);
            return _v switch
            {
                -1 => terms.FalseDisplay,
                 0 => terms.UnknownDisplay,
                 1 => terms.TrueDisplay,
                _  => $"invalid({_v})"
            };
        }

        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            if (!string.IsNullOrEmpty(format) &&
                !format.Equals("G", StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"The '{format}' format string is not supported.");

            return ToString(formatProvider);
        }

        /// <summary>
        /// Creates a Kleene value from a raw value.
        /// Values are clamped to the valid domain {-1,0,+1}.
        /// </summary>
        public static Kleene FromRaw(sbyte raw)
        {
            if (raw < -1) raw = -1;
            else if (raw > 1) raw = 1;

            return raw switch
            {
                -1 => False,
                 0 => Unknown,
                 1 => True,
                _  => throw new ArgumentOutOfRangeException(nameof(raw))
            };
        }

        /// <summary>Explicit conversion from bool: false → False, true → True.</summary>
        public static explicit operator Kleene(bool value) => value ? True : False;

        /// <summary>Explicit conversion from nullable bool: null → Unknown.</summary>
        public static explicit operator Kleene(bool? value)
            => value is null ? Unknown : (value.Value ? True : False);

        /// <summary>Collapse to nullable bool: Unknown → null.</summary>
        public bool? ToNullableBool()
            => _v == 0 ? (bool?)null : _v > 0;

        // -----------------------------
        // Core logical operators (K3)
        // -----------------------------

        /// <summary>
        /// Kleene strong negation.
        /// Algebraically: NOT(x) = -x.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Kleene operator !(Kleene x)
            => FromRaw((sbyte)(-x._v));

        /// <summary>
        /// Kleene AND.
        /// Interpreted as min(a,b) under the ordering:
        /// False &lt; Unknown &lt; True.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Kleene operator &(Kleene a, Kleene b)
            => FromRaw((sbyte)Math.Min(a._v, b._v));

        /// <summary>
        /// Kleene OR.
        /// Interpreted as max(a,b) under the ordering:
        /// False &lt; Unknown &lt; True.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Kleene operator |(Kleene a, Kleene b)
            => FromRaw((sbyte)Math.Max(a._v, b._v));

        /// <summary>
        /// Lifted XOR (chosen semantics).
        /// Unknown propagates; otherwise behaves like boolean XOR.
        ///
        /// Algebraically: XOR(a,b) = -(a*b).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Kleene operator ^(Kleene a, Kleene b)
            => FromRaw((sbyte)(-(a._v * b._v)));

        // -----------------------------
        // Control-flow operators (C#)
        // -----------------------------

        /// <summary>
        /// Enables: if (k), &&, ||.
        /// Returns true only when the value is definitively True (+1).
        /// Unknown is NOT true.
        /// </summary>
        public static bool operator true(Kleene k) => k._v == 1;

        /// <summary>
        /// Enables: if (k), &&, ||.
        /// Returns true only when the value is definitively False (-1).
        /// Unknown is NOT false.
        /// </summary>
        public static bool operator false(Kleene k) => k._v == -1;

        // -----------------------------
        // Equality and ordering
        // -----------------------------

        public bool Equals(Kleene other) => _v == other._v;
        public override bool Equals(object? obj) => obj is Kleene k && Equals(k);
        public override int GetHashCode() => _v.GetHashCode();

        public static bool operator ==(Kleene a, Kleene b) => a._v == b._v;
        public static bool operator !=(Kleene a, Kleene b) => a._v != b._v;

        /// <summary>
        /// Natural ordering: False (-1) &lt; Unknown (0) &lt; True (+1).
        /// Useful when treating Kleene values as a lattice.
        /// </summary>
        public int CompareTo(Kleene other) => _v.CompareTo(other._v);

        /// <summary>
        /// Natural ordering via non-generic comparison.
        /// Null is considered less than any instance.
        /// </summary>
        public int CompareTo(object? obj)
        {
            if (obj is null)
                return 1;

            if (obj is Kleene other)
                return CompareTo(other);

            throw new ArgumentException($"Object must be of type {nameof(Kleene)}.", nameof(obj));
        }

        private static LocalizedTerms GetTerms(IFormatProvider? provider)
        {
            if (provider is CultureInfo culture)
                return GetTerms(culture);

            if (provider?.GetFormat(typeof(CultureInfo)) is CultureInfo formattedCulture)
                return GetTerms(formattedCulture);

            return InvariantTerms;
        }

        private static LocalizedTerms GetTerms(CultureInfo culture)
        {
            if (culture.Equals(CultureInfo.InvariantCulture))
                return InvariantTerms;

            return LanguageTermsByIsoCode.TryGetValue(culture.TwoLetterISOLanguageName, out var terms)
                ? terms
                : InvariantTerms;
        }

        private static bool TryLoadLanguageTerms(
            out LocalizedTerms invariantTerms,
            out Dictionary<string, LocalizedTerms> languageTermsByIsoCode)
        {
            invariantTerms = FallbackInvariantTerms;
            languageTermsByIsoCode = new Dictionary<string, LocalizedTerms>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = FallbackEnglishTerms
            };

            foreach (var candidatePath in GetLanguageTermsFileCandidates())
            {
                if (!File.Exists(candidatePath))
                    continue;

                try
                {
                    using var stream = File.OpenRead(candidatePath);
                    var fileModel = JsonSerializer.Deserialize<LanguageTermsFile>(stream, TermsJsonOptions);
                    if (fileModel is null)
                        continue;

                    var loaded = new Dictionary<string, LocalizedTerms>(StringComparer.OrdinalIgnoreCase);

                    if (fileModel.Languages is not null)
                    {
                        foreach (var pair in fileModel.Languages)
                        {
                            var normalizedCode = NormalizeLanguageCode(pair.Key);
                            if (normalizedCode is null)
                                continue;

                            if (!TryCreateLocalizedTerms(pair.Value, out var terms))
                                continue;

                            loaded[normalizedCode] = terms;
                        }
                    }

                    if (!TryCreateLocalizedTerms(fileModel.Invariant, out var loadedInvariantTerms))
                        loadedInvariantTerms = FallbackInvariantTerms;

                    if (loaded.Count == 0 && fileModel.Invariant is null)
                        continue;

                    if (!loaded.TryGetValue("en", out var loadedEnglishTerms) || loadedEnglishTerms is null)
                        loaded["en"] = FallbackEnglishTerms;

                    invariantTerms = loadedInvariantTerms;
                    languageTermsByIsoCode = loaded;
                    return true;
                }
                catch
                {
                    // Best-effort loading only; never fail type initialization.
                }
            }

            return false;
        }

        private static IEnumerable<string> GetLanguageTermsFileCandidates()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
            {
                var appBasePath = Path.Combine(AppContext.BaseDirectory, LanguageTermsFileName);
                if (seen.Add(appBasePath))
                    yield return appBasePath;
            }

            var assemblyLocation = typeof(Kleene).Assembly.Location;
            if (!string.IsNullOrWhiteSpace(assemblyLocation))
            {
                var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                if (!string.IsNullOrWhiteSpace(assemblyDirectory))
                {
                    var assemblyPath = Path.Combine(assemblyDirectory, LanguageTermsFileName);
                    if (seen.Add(assemblyPath))
                        yield return assemblyPath;
                }
            }
        }

        private static string? NormalizeLanguageCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var trimmed = code.Trim();
            var separatorIndex = trimmed.IndexOfAny(['-', '_']);
            if (separatorIndex > 0)
                trimmed = trimmed[..separatorIndex];

            return trimmed.ToLowerInvariant();
        }

        private static bool TryCreateLocalizedTerms(LanguageTermsEntry? entry, out LocalizedTerms terms)
        {
            terms = default!;
            if (entry is null)
                return false;

            if (string.IsNullOrWhiteSpace(entry.TrueDisplay) ||
                string.IsNullOrWhiteSpace(entry.FalseDisplay) ||
                string.IsNullOrWhiteSpace(entry.UnknownDisplay))
                return false;

            var trueDisplay = entry.TrueDisplay.Trim();
            var falseDisplay = entry.FalseDisplay.Trim();
            var unknownDisplay = entry.UnknownDisplay.Trim();

            var trueTokens = NormalizeTokens(entry.TrueTokens, trueDisplay);
            var falseTokens = NormalizeTokens(entry.FalseTokens, falseDisplay);
            var unknownTokens = NormalizeTokens(entry.UnknownTokens, unknownDisplay);

            terms = new LocalizedTerms(
                trueDisplay: trueDisplay,
                falseDisplay: falseDisplay,
                unknownDisplay: unknownDisplay,
                trueTokens: trueTokens,
                falseTokens: falseTokens,
                unknownTokens: unknownTokens);
            return true;
        }

        private static string[] NormalizeTokens(IEnumerable<string>? tokens, string displayValue)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalized = new List<string>();

            void Add(string? token)
            {
                if (string.IsNullOrWhiteSpace(token))
                    return;

                var trimmed = token.Trim();
                if (seen.Add(trimmed))
                    normalized.Add(trimmed);
            }

            Add(displayValue);
            if (tokens is not null)
            {
                foreach (var token in tokens)
                    Add(token);
            }

            return normalized.ToArray();
        }

        private static bool TryParseLocalizedToken(string token, LocalizedTerms terms, out Kleene value)
        {
            if (ContainsToken(token, terms.TrueTokens))
            {
                value = True;
                return true;
            }

            if (ContainsToken(token, terms.FalseTokens))
            {
                value = False;
                return true;
            }

            if (ContainsToken(token, terms.UnknownTokens))
            {
                value = Unknown;
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryParseNumericToken(string token, out Kleene value)
        {
            // canonical numeric strings (culture-invariant)
            if (token == "1")  { value = True; return true; }
            if (token == "0")  { value = Unknown; return true; }
            if (token == "-1") { value = False; return true; }

            value = default;
            return false;
        }

        private static bool ContainsToken(string candidate, string[] tokens)
        {
            for (var i = 0; i < tokens.Length; i++)
            {
                if (candidate.Equals(tokens[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private sealed class LocalizedTerms(
            string trueDisplay,
            string falseDisplay,
            string unknownDisplay,
            string[] trueTokens,
            string[] falseTokens,
            string[] unknownTokens)
        {
            public string TrueDisplay { get; } = trueDisplay;
            public string FalseDisplay { get; } = falseDisplay;
            public string UnknownDisplay { get; } = unknownDisplay;
            public string[] TrueTokens { get; } = trueTokens;
            public string[] FalseTokens { get; } = falseTokens;
            public string[] UnknownTokens { get; } = unknownTokens;
        }

        private sealed class LanguageTermsFile
        {
            public LanguageTermsEntry? Invariant { get; init; }
            public Dictionary<string, LanguageTermsEntry>? Languages { get; init; }
        }

        private sealed class LanguageTermsEntry
        {
            public string? TrueDisplay { get; init; }
            public string? FalseDisplay { get; init; }
            public string? UnknownDisplay { get; init; }
            public string[]? TrueTokens { get; init; }
            public string[]? FalseTokens { get; init; }
            public string[]? UnknownTokens { get; init; }
        }
    }
}
