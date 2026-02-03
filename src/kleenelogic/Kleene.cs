#nullable enable
using System.Runtime.CompilerServices;

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
    public readonly struct Kleene : IEquatable<Kleene>, IComparable<Kleene>
    {
        // Backing storage: must always be -1, 0, or +1.
        private readonly sbyte _v;

        private Kleene(sbyte v) => _v = v;

        // Canonical singleton values
        public static readonly Kleene False   = new(-1);
        public static readonly Kleene Unknown = new(0);
        public static readonly Kleene True    = new(1);

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
        /// Parses a Kleene value from text. Accepted (case-insensitive, trimmed):
        ///   - "true", "false", "unknown"
        ///   - "-1", "0", "1"
        /// </summary>
        public static bool TryParse(string? input, out Kleene value)
        {
            value = default;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            var s = input.Trim();

            // canonical words (culture-invariant)
            if (s.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                value = True;
                return true;
            }

            if (s.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                value = False;
                return true;
            }

            if (s.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                value = Unknown;
                return true;
            }

            // canonical numeric strings (avoid culture-dependent parsing)
            if (s == "1")  { value = True; return true; }
            if (s == "0")  { value = Unknown; return true; }
            if (s == "-1") { value = False; return true; }

            return false;
        }

        public static Kleene Parse(string input)
        {
            if (!TryParse(input, out var value))
                throw new FormatException($"Invalid Kleene value: '{input}'. Expected true/false/unknown or -1/0/1.");
            return value;
        }

        /// <summary>
        /// Fast formatting that writes "True", "False", or "Unknown" into the destination.
        /// This is allocation-free and suitable for hot paths.
        /// </summary>
        public bool TryFormat(Span<char> destination, out int charsWritten)
        {
            ReadOnlySpan<char> text = _v switch
            {
                -1 => "False",
                 0 => "Unknown",
                 1 => "True",
                _  => "Invalid"
            };

            if (destination.Length < text.Length)
            {
                charsWritten = 0;
                return false;
            }

            text.CopyTo(destination);
            charsWritten = text.Length;
            return true;
        }

        /// <summary>
        /// Convenience allocation-free formatting. Uses TryFormat internally.
        /// </summary>
        public override string ToString()
        {
            Span<char> buf = stackalloc char[7]; // "Unknown" is 7 chars
            return TryFormat(buf, out var written)
                ? new string(buf[..written])
                : _v switch
                {
                    -1 => "False",
                     0 => "Unknown",
                     1 => "True",
                    _  => $"Invalid({_v})"
                };
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
    }
}
