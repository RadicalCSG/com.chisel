using System.Numerics;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	internal static class ExactFloatLine
	{
		public static BigInteger Exact(float value)
		{
			var bits  = math.asuint(value);
			var field = (int)((bits >> 23) & 0xFF);
			long whole = field == 0 ? (bits & 0x7FFFFF) : ((bits & 0x7FFFFF) | 0x800000);
			var result = new BigInteger(whole) << (field == 0 ? 0 : field - 1);
			return (bits & 0x80000000u) != 0 ? -result : result;
		}

		// NaN and infinity are on no line
		public static bool OnOneLine(float3 a, float3 b, float3 c)
		{
			if (!math.all(math.isfinite(a)) || !math.all(math.isfinite(b)) || !math.all(math.isfinite(c)))
				return false;
			var ux = Exact(b.x) - Exact(a.x); var uy = Exact(b.y) - Exact(a.y); var uz = Exact(b.z) - Exact(a.z);
			var wx = Exact(c.x) - Exact(a.x); var wy = Exact(c.y) - Exact(a.y); var wz = Exact(c.z) - Exact(a.z);
			return (uy * wz - uz * wy).IsZero && (uz * wx - ux * wz).IsZero && (ux * wy - uy * wx).IsZero;
		}

		// m strictly between a and b, given that the three lie on one line (float comparisons, exact)
		public static bool StrictlyBetween(float3 a, float3 m, float3 b)
		{
			for (int k = 0; k < 3; k++)
			{
				if (a[k] != b[k])
					return (a[k] < m[k] && m[k] < b[k]) || (b[k] < m[k] && m[k] < a[k]);
			}
			return false;
		}

		// The needle a-b-c read from its middle corner: u, m, w in its own winding, its long edge running w -> u; false when
		// the three are no needle (not three distinct positions on one line)
		public static bool AsNeedle(float3 a, float3 b, float3 c, out float3 u, out float3 m, out float3 w)
		{
			u = m = w = default;
			if (a.Equals(b) || b.Equals(c) || c.Equals(a) || !OnOneLine(a, b, c))
				return false;
			if      (StrictlyBetween(c, a, b)) { u = c; m = a; w = b; }
			else if (StrictlyBetween(a, b, c)) { u = a; m = b; w = c; }
			else if (StrictlyBetween(b, c, a)) { u = b; m = c; w = a; }
			else return false;
			return true;
		}
	}
}
