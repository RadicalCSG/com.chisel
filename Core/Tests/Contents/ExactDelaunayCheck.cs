using System;
using System.Collections.Generic;
using BigInteger = System.Numerics.BigInteger;

namespace Chisel.Core.Tests
{
	internal static class ExactDelaunayCheck
	{
		internal struct Result
		{
			public int    innerEdges;       // edges with a triangle on each side
			public int    violations;       // of those, the ones that should have been flipped
			public int    floatUncertain;   // triangles that do not certainly turn left in the drawn floats
			public string first;            // the first violation, described
		}

		// vertices: exact; positions: x, y, z per vertex; triangles: counter-clockwise seen from the plane, in its projection
		// (axisU, axisV) as ExactPredicates.ProjectionAxes gives it.
		internal static Result Check(ExactVertex[] vertices, float[] positions, int[] triangles, int axisU, int axisV)
		{
			var result = new Result();
			var directed = new Dictionary<long, int>();     // (from, to) -> how many triangles have that edge
			var slotOf   = new Dictionary<long, int>();     // (from, to) -> the slot of one of them
			for (int slot = 0; slot < triangles.Length; slot++)
			{
				int triangle = slot / 3;
				long key = Key(triangles[slot], triangles[triangle * 3 + (slot % 3 == 2 ? 0 : slot % 3 + 1)]);
				directed.TryGetValue(key, out int count);
				directed[key] = count + 1;
				slotOf[key] = slot;
			}
			for (int t = 0; t + 2 < triangles.Length; t += 3)
				if (!TurnsLeftCertainly(positions, triangles[t], triangles[t + 1], triangles[t + 2], axisU, axisV))
					result.floatUncertain++;

			for (int slot = 0; slot < triangles.Length; slot++)
			{
				int t = slot / 3, k = slot % 3;
				int a = triangles[slot], b = triangles[t * 3 + (k + 1) % 3], c = triangles[t * 3 + (k + 2) % 3];
				if (a > b)
					continue;   // each inner edge once
				if (directed[Key(a, b)] != 1 || !directed.TryGetValue(Key(b, a), out int reverse) || reverse != 1)
					continue;
				int twin = slotOf[Key(b, a)];
				int s = twin / 3, j = twin % 3;
				int d = triangles[s * 3 + (j + 2) % 3];
				result.innerEdges++;

				if (!InCircleCertainly(positions, a, b, c, d, axisU, axisV))
					continue;
				if (ExactPredicates.Orientation(vertices[a], vertices[d], vertices[c], axisU, axisV) <= 0 ||
					ExactPredicates.Orientation(vertices[d], vertices[b], vertices[c], axisU, axisV) <= 0)
					continue;
				if (!TurnsLeftCertainly(positions, a, d, c, axisU, axisV) || !TurnsLeftCertainly(positions, d, b, c, axisU, axisV))
					continue;
				result.violations++;
				if (result.first == null)
					result.first = $"the edge {Describe(positions, a)} - {Describe(positions, b)} between {Describe(positions, c)} and " +
								   $"{Describe(positions, d)}: the last lies inside the circle through the other three, and the " +
								   "other diagonal would make two valid triangles";
			}
			return result;
		}

		static long Key(int from, int to) => ((long)from << 32) | (uint)to;

		static string Describe(float[] positions, int v)
		{
			return $"({positions[v * 3]:R}, {positions[v * 3 + 1]:R}, {positions[v * 3 + 2]:R})";
		}

		// A float times 2^149, which is an integer for every float.
		internal static BigInteger Scaled(float value)
		{
			int bits     = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
			int exponent = (bits >> 23) & 0xFF;
			int mantissa = bits & 0x7FFFFF;
			BigInteger scaled = exponent == 0 ? mantissa : (mantissa | 0x800000);
			if (exponent > 1)
				scaled <<= exponent - 1;
			return bits < 0 ? -scaled : scaled;
		}

		static BigInteger U(float[] positions, int v, int axisU) => Scaled(positions[v * 3 + axisU]);

		// Whether the determinant exceeds 2^-45 of its permanent.
		static bool Certainly(BigInteger determinant, BigInteger permanent) => (determinant << 45) > permanent;

		internal static bool TurnsLeftCertainly(float[] positions, int a, int b, int c, int axisU, int axisV)
		{
			BigInteger acu = U(positions, a, axisU) - U(positions, c, axisU), bcu = U(positions, b, axisU) - U(positions, c, axisU);
			BigInteger acv = U(positions, a, axisV) - U(positions, c, axisV), bcv = U(positions, b, axisV) - U(positions, c, axisV);
			BigInteger left = acu * bcv, right = acv * bcu;
			return Certainly(left - right, BigInteger.Abs(left) + BigInteger.Abs(right));
		}

		// Whether d lies inside the circle through a, b, c (when those turn left; its sign the other way when they don't).
		internal static bool InCircleCertainly(float[] positions, int a, int b, int c, int d, int axisU, int axisV)
		{
			BigInteger du = U(positions, d, axisU), dv = U(positions, d, axisV);
			BigInteger adu = U(positions, a, axisU) - du, adv = U(positions, a, axisV) - dv;
			BigInteger bdu = U(positions, b, axisU) - du, bdv = U(positions, b, axisV) - dv;
			BigInteger cdu = U(positions, c, axisU) - du, cdv = U(positions, c, axisV) - dv;
			BigInteger aLift = adu * adu + adv * adv, bLift = bdu * bdu + bdv * bdv, cLift = cdu * cdu + cdv * cdv;
			BigInteger determinant = aLift * (bdu * cdv - cdu * bdv) + bLift * (cdu * adv - adu * cdv) + cLift * (adu * bdv - bdu * adv);
			BigInteger permanent   = (BigInteger.Abs(bdu * cdv) + BigInteger.Abs(cdu * bdv)) * aLift
								   + (BigInteger.Abs(cdu * adv) + BigInteger.Abs(adu * cdv)) * bLift
								   + (BigInteger.Abs(adu * bdv) + BigInteger.Abs(bdu * adv)) * cLift;
			return Certainly(determinant, permanent);
		}
	}
}
