#nullable enable
using System.Runtime.CompilerServices;

namespace KleeneLogic
{
    /// <summary>
    /// Kleene's strong three-valued logic (K3).
    ///
    /// Truth values are represented using the ordered set:
    ///
    ///     False   = -1
    ///     Unknown =  0
    ///     True    = +1
    ///
    /// This numeric encoding is intentional and algebraically meaningful:
    ///
    ///   NOT(x)   = -x
    ///   AND(a,b) = min(a,b)
    ///   OR(a,b)  = max(a,b)
    ///
    /// These operators obey the standard K3 truth tables and De Morgan laws.
    ///
    /// XOR is not canonical in K3; the chosen semantics are:
    ///   - Unknown propagates
    ///   - Otherwise behaves like boolean XOR
    ///
    /// With the {-1,0,+1} encoding:
    ///   XOR(a,b) = -(a*b)
    ///
    /// ----------------------------------------
    /// Control-flow semantics (IMPORTANT)
    /// ----------------------------------------
    ///
    /// This type intentionally defines:
    ///
    ///     operator true
    ///     operator false
    ///
    /// with the following meaning:
    ///
    ///   - true  only when value == True   (+1)
    ///   - false only when value == False  (-1)
    ///   - Unknown is neither true nor false
    ///
    /// Consequences:
    ///
    ///   if (k)         executes only for True
    ///   if (!k)        executes only for False
    ///   else           means: "not definitely true"
    ///
    /// Short-circuiting operators (&&, ||) are enabled and behave as follows:
    ///
    ///   - Short-circuit happens only for fully-resolved values
    ///   - Unknown does NOT short-circuit and evaluates the RHS
    ///
    /// This matches the design intent:
    ///   - Unknown does not control flow
    ///   - Only definitive knowledge does
    ///
    /// This behavior is deliberate and suitable for domains such as:
    ///   - unit systems
    ///   - dimensional analysis
    ///   - constraint propagation
    ///   - staged validation pipelines
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

        public override string ToString() => _v switch
        {
            -1 => "False",
             0 => "Unknown",
             1 => "True",
            _  => $"Invalid({_v})"
        };

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
