#nullable enable
namespace KleeneLogic.Tests;

using KleeneLogic;

public sealed class KleeneTests
{
        // --------
        // Constants / basic properties
        // --------

        [Fact]
        public void Singletons_HaveExpectedRawValues_AndFlags()
        {
            Assert.Equal((sbyte)-1, Kleene.False.Raw);
            Assert.Equal((sbyte)0,  Kleene.Unknown.Raw);
            Assert.Equal((sbyte)1,  Kleene.True.Raw);

            Assert.True(Kleene.True.IsTrue);
            Assert.False(Kleene.True.IsFalse);
            Assert.False(Kleene.True.IsUnknown);

            Assert.False(Kleene.False.IsTrue);
            Assert.True(Kleene.False.IsFalse);
            Assert.False(Kleene.False.IsUnknown);

            Assert.False(Kleene.Unknown.IsTrue);
            Assert.False(Kleene.Unknown.IsFalse);
            Assert.True(Kleene.Unknown.IsUnknown);
        }

        [Theory]
        [InlineData(-1, "False")]
        [InlineData(0,  "Unknown")]
        [InlineData(1,  "True")]
        public void ToString_ReturnsExpectedText(sbyte raw, string expected)
        {
            Assert.Equal(expected, Kleene.FromRaw(raw).ToString());
        }

        // --------
        // FromRaw clamping
        // --------

        [Theory]
        [InlineData((sbyte)-128, (sbyte)-1)]
        [InlineData((sbyte)-2,   (sbyte)-1)]
        [InlineData((sbyte)-1,   (sbyte)-1)]
        [InlineData((sbyte)0,    (sbyte)0)]
        [InlineData((sbyte)1,    (sbyte)1)]
        [InlineData((sbyte)2,    (sbyte)1)]
        [InlineData((sbyte)127,  (sbyte)1)]
        public void FromRaw_ClampsToValidDomain(sbyte raw, sbyte expectedRaw)
        {
            var k = Kleene.FromRaw(raw);
            Assert.Equal(expectedRaw, k.Raw);
        }

        // --------
        // Conversions
        // --------

        [Fact]
        public void ExplicitConversion_FromBool()
        {
            Assert.Equal(Kleene.True, (Kleene)true);
            Assert.Equal(Kleene.False, (Kleene)false);
        }

        [Fact]
        public void ExplicitConversion_FromNullableBool()
        {
            Assert.Equal(Kleene.True, (Kleene)(bool?)true);
            Assert.Equal(Kleene.False, (Kleene)(bool?)false);
            Assert.Equal(Kleene.Unknown, (Kleene)(bool?)null);
        }

        [Fact]
        public void ToNullableBool_CollapsesUnknownToNull()
        {
            Assert.Equal(true,  Kleene.True.ToNullableBool());
            Assert.Equal(false, Kleene.False.ToNullableBool());
            Assert.Null(Kleene.Unknown.ToNullableBool());
        }

        [Fact]
        public void Default_Kleene_UsesFallbackOnlyForUnknown()
        {
            Assert.Equal(Kleene.True,    Kleene.True.Default(Kleene.False));
            Assert.Equal(Kleene.False,   Kleene.False.Default(Kleene.True));
            Assert.Equal(Kleene.True,    Kleene.Unknown.Default(Kleene.True));
        }

        [Fact]
        public void Default_Bool_UsesFallbackOnlyForUnknown()
        {
            Assert.True(Kleene.True.Default(false));
            Assert.False(Kleene.False.Default(true));
            Assert.True(Kleene.Unknown.Default(true));
            Assert.False(Kleene.Unknown.Default(false));
        }

        // --------
        // Core logical operators
        // --------

        [Theory]
        [InlineData(-1,  1)]
        [InlineData(0,   0)]
        [InlineData(1,  -1)]
        public void Not_IsArithmeticNegation(sbyte raw, sbyte expectedRaw)
        {
            var k = Kleene.FromRaw(raw);
            Assert.Equal(expectedRaw, (!k).Raw);
        }

        [Theory]
        // AND = min
        [InlineData(-1, -1, -1)]
        [InlineData(-1,  0, -1)]
        [InlineData(-1,  1, -1)]
        [InlineData(0,  -1, -1)]
        [InlineData(0,   0,  0)]
        [InlineData(0,   1,  0)]
        [InlineData(1,  -1, -1)]
        [InlineData(1,   0,  0)]
        [InlineData(1,   1,  1)]
        public void And_IsMin(sbyte aRaw, sbyte bRaw, sbyte expectedRaw)
        {
            var a = Kleene.FromRaw(aRaw);
            var b = Kleene.FromRaw(bRaw);
            Assert.Equal(expectedRaw, (a & b).Raw);
        }

        [Theory]
        // OR = max
        [InlineData(-1, -1, -1)]
        [InlineData(-1,  0,  0)]
        [InlineData(-1,  1,  1)]
        [InlineData(0,  -1,  0)]
        [InlineData(0,   0,  0)]
        [InlineData(0,   1,  1)]
        [InlineData(1,  -1,  1)]
        [InlineData(1,   0,  1)]
        [InlineData(1,   1,  1)]
        public void Or_IsMax(sbyte aRaw, sbyte bRaw, sbyte expectedRaw)
        {
            var a = Kleene.FromRaw(aRaw);
            var b = Kleene.FromRaw(bRaw);
            Assert.Equal(expectedRaw, (a | b).Raw);
        }

        [Theory]
        // XOR = -(a*b)
        [InlineData(-1, -1, -1)] // -(+1) = -1
        [InlineData(-1,  0,  0)] // -(0)  = 0
        [InlineData(-1,  1,  1)] // -(-1) = +1
        [InlineData(0,  -1,  0)]
        [InlineData(0,   0,  0)]
        [InlineData(0,   1,  0)]
        [InlineData(1,  -1,  1)]
        [InlineData(1,   0,  0)]
        [InlineData(1,   1, -1)]
        public void Xor_IsNegativeProduct(sbyte aRaw, sbyte bRaw, sbyte expectedRaw)
        {
            var a = Kleene.FromRaw(aRaw);
            var b = Kleene.FromRaw(bRaw);
            Assert.Equal(expectedRaw, (a ^ b).Raw);
        }

        // --------
        // Control-flow operators true/false
        // --------

        [Fact]
        public void OperatorTrue_IsTrueOnlyForTrue()
        {
            Assert.True(OperatorTrue(Kleene.True));
            Assert.False(OperatorTrue(Kleene.False));
            Assert.False(OperatorTrue(Kleene.Unknown));
        }

        [Fact]
        public void OperatorFalse_IsTrueOnlyForFalse()
        {
            Assert.True(OperatorFalse(Kleene.False));
            Assert.False(OperatorFalse(Kleene.True));
            Assert.False(OperatorFalse(Kleene.Unknown));
        }

        [Fact]
        public void IfSemantics_UnknownIsNeitherTrueNorFalse()
        {
            int hit = 0;

            // if (Unknown) should NOT execute
            if (Kleene.Unknown) hit++;

            // if (!Unknown) should also NOT execute (because !Unknown == Unknown)
            if (!Kleene.Unknown) hit++;

            Assert.Equal(0, hit);
        }

        [Fact]
        public void ShortCircuiting_WithUnknown_EvaluatesRhs()
        {
            var called = 0;

            Kleene Rhs()
            {
                called++;
                return Kleene.True;
            }

            // Unknown should not short-circuit for &&
            _ = Kleene.Unknown && Rhs();
            Assert.Equal(1, called);

            // Unknown should not short-circuit for ||
            _ = Kleene.Unknown || Rhs();
            Assert.Equal(2, called);

            // False should short-circuit for &&
            _ = Kleene.False && Rhs();
            Assert.Equal(2, called);

            // True should short-circuit for ||
            _ = Kleene.True || Rhs();
            Assert.Equal(2, called);

            // True should not short-circuit for &&
            _ = Kleene.True && Rhs();
            Assert.Equal(3, called);

            // False should not short-circuit for ||
            _ = Kleene.False || Rhs();
            Assert.Equal(4, called);
        }

        // Helper methods to explicitly invoke operator true/false in a test.
        // (C# doesn't let you call them by name from user code.)
        private static bool OperatorTrue(Kleene k) => k ? true : false; // uses operator true/false
        private static bool OperatorFalse(Kleene k) => (!k) ? true : false; // true only when k is False

        // --------
        // Equality / hash / ordering
        // --------

        [Fact]
        public void Equality_AndHashCode_AreRawBased()
        {
            Assert.True(Kleene.True == Kleene.FromRaw(1));
            Assert.True(Kleene.False == Kleene.FromRaw(-1));
            Assert.True(Kleene.Unknown == Kleene.FromRaw(0));

            Assert.False(Kleene.True != Kleene.FromRaw(1));
            Assert.NotEqual(Kleene.True, Kleene.False);

            Assert.Equal(Kleene.True.GetHashCode(), Kleene.FromRaw(1).GetHashCode());
        }

        [Fact]
        public void CompareTo_UsesNaturalOrdering()
        {
            Assert.True(Kleene.False.CompareTo(Kleene.Unknown) < 0);
            Assert.True(Kleene.Unknown.CompareTo(Kleene.True) < 0);
            Assert.Equal(0, Kleene.True.CompareTo(Kleene.FromRaw(1)));
    }
}
