using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class SourceConcretePadTests
	{
		// solid 4838, all 14 sides
		static readonly float4[] kPad =
		{
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(+0f, +1f, +0f, -25.4675f),
			new float4(+0.259973473f, +0f, +0.965615759f, +40.3935641f),
			new float4(+0.707106781f, +0f, +0.707106781f, +78.9882469f),
			new float4(+0.965615759f, +0f, +0.259973473f, +95.4901138f),
			new float4(+0.965615759f, +0f, -0.259973473f, +85.3407493f),
			new float4(+0.707106781f, +0f, -0.707106781f, +51.3827981f),
			new float4(+0.259973473f, +0f, -0.965615759f, +2.69592492f),
			new float4(-0.259973473f, +0f, -0.965615759f, -48.0508971f),
			new float4(-0.707106781f, +0f, -0.707106781f, -86.6444455f),
			new float4(-0.965615759f, +0f, -0.259973473f, -103.147447f),
			new float4(-0.965615759f, +0f, +0.259973473f, -92.9980823f),
			new float4(-0.707106781f, +0f, +0.707106781f, -59.0389968f),
			new float4(-0.259973473f, +0f, +0.965615759f, -10.3532579f),
		};

		// side 35132, the face that went missing: +Y at -25.467500
		static readonly float4 kTopFace = new float4(+0f, +1f, +0f, -25.4675f);

		static readonly float4[] kNeighbour3982 =
		{
			new float4(+0f, +1f, +0f, -25.08625f),
			new float4(+0.965965867f, +0f, -0.258669563f, +85.0399165f),
			new float4(-0.96594981f, +0f, +0.258729519f, -85.3963103f),
			new float4(+0f, -0f, +1f, +19.52f),
			new float4(+0f, -1f, -0f, +24.8575f),
			new float4(+0.148435645f, +0f, -0.988922069f, -6.29192715f),
		};

		static readonly float4[] kNeighbour3984 =
		{
			new float4(+0f, +1f, -0f, -25.08625f),
			new float4(+0.965973954f, +0f, +0.258639365f, +95.1385409f),
			new float4(-0.965968646f, +0f, -0.258659185f, -95.4975015f),
			new float4(+0f, +0f, -1f, -19.52f),
			new float4(+0f, -1f, +0f, +24.8575f),
			new float4(+0.155691004f, -0f, +0.987805807f, +32.9919113f),
		};

		static readonly float4[] kNeighbour3986 =
		{
			new float4(+0f, +1f, -0f, -24.62875f),
			new float4(+0.965948052f, -0f, +0.258736085f, +94.696092f),
			new float4(-0.966485127f, +0f, -0.256722612f, -95.5080602f),
			new float4(+0f, +0f, -1f, -19.52f),
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(+0.173167881f, -0f, +0.984892322f, +34.2942212f),
		};

		static readonly float4[] kNeighbour3988 =
		{
			new float4(+0f, +1f, -0f, -24.8575f),
			new float4(+0.965935975f, +0f, +0.258781167f, +94.916809f),
			new float4(-0.965944189f, +0f, -0.258750504f, -95.496994f),
			new float4(+0f, +0f, -1f, -19.52f),
			new float4(+0f, -1f, +0f, +24.62875f),
			new float4(+0.173666379f, -0f, +0.984804543f, +34.4864163f),
		};

		static readonly float4[] kNeighbour3990 =
		{
			new float4(+0f, +1f, +0f, -24.8575f),
			new float4(+0.965935975f, +0f, -0.258781167f, +84.8139923f),
			new float4(-0.965944189f, +0f, +0.258750504f, -85.3953743f),
			new float4(+0f, -0f, +1f, +19.52f),
			new float4(+0f, -1f, -0f, +24.62875f),
			new float4(+0.173666379f, +0f, -0.984804543f, -3.9603531f),
		};

		static readonly float4[] kNeighbour3992 =
		{
			new float4(+0f, +1f, +0f, -24.62875f),
			new float4(+0.965948052f, +0f, -0.258736085f, +84.5950352f),
			new float4(-0.966485127f, +0f, +0.256722612f, -85.4856095f),
			new float4(+0f, -0f, +1f, +19.52f),
			new float4(+0f, -1f, -0f, +24.4f),
			new float4(+0.173167881f, +0f, -0.984892322f, -4.15597502f),
		};

		static readonly float4[] kNeighbour4054 =
		{
			new float4(+0f, -1f, +0f, +23.79f),
			new float4(+0f, +1f, -0f, -24.4f),
			new float4(+0.498964445f, +0f, -0.866622457f, +31.7884667f),
			new float4(-0.866622457f, +0f, +0.498964445f, -74.8365586f),
			new float4(+0.707106781f, +0f, +0.707106781f, +76.1845685f),
			new float4(-0.707106781f, +0f, -0.707106781f, -78.9882469f),
		};

		static readonly float4[] kNeighbour4055 =
		{
			new float4(-0f, -1f, +0f, +23.79f),
			new float4(+0f, +1f, +0f, -24.4f),
			new float4(+0.866622457f, +0f, -0.498964445f, +74.8365586f),
			new float4(-1f, +0f, +0f, -97.6f),
			new float4(+0.257662651f, +0f, +0.96623494f, +37.3779934f),
			new float4(-0.259973473f, +0f, -0.965615759f, -40.3935641f),
		};

		static readonly float4[] kNeighbour4056 =
		{
			new float4(+0f, -1f, +0f, +23.79f),
			new float4(+0f, +1f, +0f, -24.4f),
			new float4(+1f, +0f, +0f, +97.6f),
			new float4(-0.866622457f, +0f, -0.498964445f, -94.328145f),
			new float4(-0.257662651f, +0f, +0.96623494f, -12.9177559f),
			new float4(+0.259973473f, +0f, -0.965615759f, +10.3532579f),
		};

		static readonly float4[] kNeighbour4057 =
		{
			new float4(+0f, -1f, -0f, +23.79f),
			new float4(+0f, +1f, +0f, -24.4f),
			new float4(+0.866622457f, +0f, +0.498964445f, +94.328145f),
			new float4(-0.498964445f, +0f, -0.866622457f, -65.6093929f),
			new float4(-0.707106781f, +0f, +0.707106781f, -61.8426752f),
			new float4(+0.707106781f, +0f, -0.707106781f, +59.0389968f),
		};

		static readonly float4[] kNeighbour4058 =
		{
			new float4(+0f, -1f, +0f, +23.79f),
			new float4(+0f, +1f, -0f, -24.4f),
			new float4(+0f, -0f, -1f, -19.52f),
			new float4(-0.498964445f, +0f, +0.866622457f, -31.7884667f),
			new float4(+0.96623494f, +0f, +0.257662651f, +92.7033178f),
			new float4(-0.965615759f, +0f, -0.259973473f, -95.4901138f),
		};

		static readonly float4[] kNeighbour4059 =
		{
			new float4(+0f, -1f, -0f, +23.79f),
			new float4(+0f, +1f, +0f, -24.4f),
			new float4(-0.498964445f, +0f, -0.866622457f, -65.6214074f),
			new float4(+0f, -0f, +1f, +19.52f),
			new float4(+0.96623494f, +0f, -0.257662651f, +82.6441679f),
			new float4(-0.965615759f, +0f, +0.259973473f, -85.3407493f),
		};

		static readonly float4[] kNeighbour4060 =
		{
			new float4(+0f, -1f, +0f, +23.79f),
			new float4(+0f, +1f, +0f, -24.4f),
			new float4(-0.866622457f, -0f, -0.498964445f, -94.3161305f),
			new float4(+0.498964445f, -0f, +0.866622457f, +65.6214074f),
			new float4(+0.707106781f, +0f, -0.707106781f, +48.5791198f),
			new float4(-0.707106781f, +0f, +0.707106781f, -51.3827981f),
		};

		static readonly float4[] kNeighbour4061 =
		{
			new float4(+0f, -1f, +0f, +23.79f),
			new float4(-0f, +1f, +0f, -24.4f),
			new float4(-1f, +0f, +0f, -97.6f),
			new float4(+0.866622457f, -0f, +0.498964445f, +94.3161305f),
			new float4(+0.257662651f, +0f, -0.96623494f, -0.343818599f),
			new float4(-0.259973473f, +0f, +0.965615759f, -2.69592492f),
		};

		static readonly float4[] kNeighbour4062 =
		{
			new float4(+0f, -1f, +0f, +23.79f),
			new float4(+0f, +1f, +0f, -24.4f),
			new float4(-0.866622457f, +0f, +0.498964445f, -74.8485731f),
			new float4(+1f, +0f, +0f, +97.6f),
			new float4(-0.257662651f, +0f, -0.96623494f, -50.639568f),
			new float4(+0.259973473f, +0f, +0.965615759f, +48.0508971f),
		};

		static readonly float4[] kNeighbour4063 =
		{
			new float4(+0f, -1f, +0f, +23.79f),
			new float4(+0f, +1f, +0f, -24.4f),
			new float4(-0.498964445f, +0f, +0.866622457f, -31.7764522f),
			new float4(+0.866622457f, +0f, -0.498964445f, +74.8485731f),
			new float4(-0.707106781f, +0f, -0.707106781f, -89.4481239f),
			new float4(+0.707106781f, +0f, +0.707106781f, +86.6444455f),
		};

		static readonly float4[] kNeighbour4064 =
		{
			new float4(+0f, -1f, +0f, +23.79f),
			new float4(+0f, +1f, +0f, -24.4f),
			new float4(+0f, +0f, +1f, +19.52f),
			new float4(+0.498964445f, +0f, -0.866622457f, +31.7764522f),
			new float4(-0.96623494f, +0f, -0.257662651f, -105.964892f),
			new float4(+0.965615759f, +0f, +0.259973473f, +103.147447f),
		};

		static readonly float4[] kNeighbour4065 =
		{
			new float4(+0f, -1f, -0f, +23.79f),
			new float4(+0f, +1f, +0f, -24.4f),
			new float4(+0.498964445f, +0f, +0.866622457f, +65.6093929f),
			new float4(+0f, +0f, -1f, -19.52f),
			new float4(-0.96623494f, +0f, +0.257662651f, -95.9057424f),
			new float4(+0.965615759f, +0f, -0.259973473f, +92.9980823f),
		};

		static readonly float4[] kNeighbour4312 =
		{
			new float4(+0f, -1f, +0f, +25.4675f),
			new float4(-0f, +1f, +0f, -26.0775f),
			new float4(+0.928476691f, +0f, -0.371390676f, +83.4016374f),
			new float4(-0.707106781f, +0f, +0.707106781f, -55.2108975f),
			new float4(-0.558376369f, +0f, -0.829587748f, -73.6808479f),
			new float4(+0.540757591f, +0f, +0.841178475f, +71.594972f),
		};

		static readonly float4[] kNeighbour4314 =
		{
			new float4(-0f, -1f, +0f, +25.4675f),
			new float4(+0f, +1f, +0f, -26.0775f),
			new float4(-0.928476691f, +0f, +0.371390676f, -83.4016374f),
			new float4(-0.193055343f, +0f, -0.981187869f, -40.9876521f),
			new float4(+0.199883224f, +0f, +0.979819727f, +41.0253692f),
			new float4(+1f, +0f, -0f, +98.0575f),
		};

		static readonly float4[] kNeighbour4315 =
		{
			new float4(+0f, -1f, +0f, +25.4675f),
			new float4(-0f, +1f, +0f, -26.0775f),
			new float4(+0.707106781f, +0f, -0.707106781f, +55.2108975f),
			new float4(-0.371390676f, +0f, +0.928476691f, -18.0920066f),
			new float4(-0.829587748f, +0f, -0.558376369f, -94.8570323f),
			new float4(+0.841178475f, +0f, +0.540757591f, +95.0518346f),
		};

		static readonly float4[] kNeighbour4317 =
		{
			new float4(-0f, -1f, +0f, +25.4675f),
			new float4(+0f, +1f, +0f, -26.0775f),
			new float4(+0.371390676f, +0f, -0.928476691f, +18.0920066f),
			new float4(-0.981187869f, +0f, -0.193055343f, -102.52504f),
			new float4(+0.979819727f, +0f, +0.199883224f, +101.922811f),
			new float4(+0f, +0f, +1f, +19.9775f),
		};

		static readonly float4[] kNeighbour4839 =
		{
			new float4(+0f, -1f, -0f, +24.4f),
			new float4(+0f, +1f, +0f, -25.315f),
			new float4(+0f, +0f, -1f, -19.52f),
			new float4(-0.496138938f, +0f, +0.868243142f, -31.4939695f),
			new float4(+0.965788228f, +0f, +0.259332024f, +95.3464591f),
			new float4(-0.965615759f, +0f, -0.259973473f, -95.4901138f),
		};

		static readonly float4[] kNeighbour4840 =
		{
			new float4(-0f, -1f, +0f, +24.4f),
			new float4(+0f, +1f, -0f, -25.315f),
			new float4(-0.496138938f, +0f, -0.868243142f, -65.3901818f),
			new float4(+0f, +0f, +1f, +19.52f),
			new float4(+0.965788228f, +0f, -0.259332024f, +85.2221369f),
			new float4(-0.965615759f, +0f, +0.259973473f, -85.3407493f),
		};

		static readonly float4[] kNeighbour4841 =
		{
			new float4(-0f, -1f, +0f, +24.4f),
			new float4(+0f, +1f, -0f, -25.315f),
			new float4(-0.868243142f, +0f, -0.496138938f, -94.4062475f),
			new float4(+0.496138938f, +0f, +0.868243142f, +65.3901818f),
			new float4(+0.707106781f, +0f, -0.707106781f, +51.2345267f),
			new float4(-0.707106781f, +0f, +0.707106781f, -51.3827981f),
		};

		static readonly float4[] kNeighbour4842 =
		{
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(+0f, +1f, +0f, -25.315f),
			new float4(-1f, +0f, +0f, -97.6f),
			new float4(+0.868243142f, +0f, +0.496138938f, +94.4062475f),
			new float4(+0.259332024f, +0f, -0.965788228f, +2.48198632f),
			new float4(-0.259973473f, +0f, +0.965615759f, -2.69592492f),
		};

		static readonly float4[] kNeighbour4843 =
		{
			new float4(+0f, -1f, -0f, +24.4f),
			new float4(-0f, +1f, +0f, -25.315f),
			new float4(-0.868243142f, +0f, +0.496138938f, -75.0748139f),
			new float4(+1f, +0f, +0f, +97.6f),
			new float4(-0.259332024f, +0f, -0.965788228f, -48.1396248f),
			new float4(+0.259973473f, +0f, +0.965615759f, +48.0508971f),
		};

		static readonly float4[] kNeighbour4844 =
		{
			new float4(+0f, -1f, -0f, +24.4f),
			new float4(-0f, +1f, +0f, -25.315f),
			new float4(-0.496138938f, +0f, +0.868243142f, -31.456139f),
			new float4(+0.868243142f, +0f, -0.496138938f, +75.0748139f),
			new float4(-0.707106781f, +0f, -0.707106781f, -86.792717f),
			new float4(+0.707106781f, +0f, +0.707106781f, +86.6444455f),
		};

		static readonly float4[] kNeighbour4845 =
		{
			new float4(-0f, -1f, -0f, +24.4f),
			new float4(-0f, +1f, +0f, -25.315f),
			new float4(+0f, +0f, +1f, +19.52f),
			new float4(+0.496138938f, +0f, -0.868243142f, +31.456139f),
			new float4(-0.965788228f, +0f, -0.259332024f, -103.299725f),
			new float4(+0.965615759f, +0f, +0.259973473f, +103.147447f),
		};

		static readonly float4[] kNeighbour4846 =
		{
			new float4(-0f, -1f, +0f, +24.4f),
			new float4(+0f, +1f, +0f, -25.315f),
			new float4(+0.496138938f, +0f, +0.868243142f, +65.3523512f),
			new float4(+0f, +0f, -1f, -19.52f),
			new float4(-0.965788228f, +0f, +0.259332024f, -93.175403f),
			new float4(+0.965615759f, +0f, -0.259973473f, +92.9980823f),
		};

		static readonly float4[] kNeighbour4847 =
		{
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(+0f, +1f, +0f, -25.315f),
			new float4(+0.868243142f, +0f, +0.496138938f, +94.444078f),
			new float4(-0.496138938f, +0f, -0.868243142f, -65.3523512f),
			new float4(-0.707106781f, +0f, +0.707106781f, -59.1872683f),
			new float4(+0.707106781f, +0f, -0.707106781f, +59.0389968f),
		};

		static readonly float4[] kNeighbour4848 =
		{
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(+0f, +1f, +0f, -25.315f),
			new float4(+1f, +0f, +0f, +97.6f),
			new float4(-0.868243142f, +0f, -0.496138938f, -94.444078f),
			new float4(-0.259332024f, +0f, +0.965788228f, -10.4352524f),
			new float4(+0.259973473f, +0f, -0.965615759f, +10.3532579f),
		};

		static readonly float4[] kNeighbour4849 =
		{
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(+0f, +1f, +0f, -25.315f),
			new float4(+0.868243142f, +0f, -0.496138938f, +75.0369833f),
			new float4(-1f, +0f, +0f, -97.6f),
			new float4(+0.259332024f, +0f, +0.965788228f, +40.1863587f),
			new float4(-0.259973473f, +0f, -0.965615759f, -40.3935641f),
		};

		static readonly float4[] kNeighbour4850 =
		{
			new float4(+0f, -1f, -0f, +24.4f),
			new float4(+0f, +1f, +0f, -25.315f),
			new float4(+0.496138938f, +0f, -0.868243142f, +31.4939695f),
			new float4(-0.868243142f, +0f, +0.496138938f, -75.0369833f),
			new float4(+0.707106781f, +0f, +0.707106781f, +78.8399754f),
			new float4(-0.707106781f, +0f, -0.707106781f, -78.9882469f),
		};

		static readonly float4[] kNeighbour4861 =
		{
			new float4(-0.862578431f, +0f, +0.505923365f, -71.4302668f),
			new float4(+0f, -1f, -0f, +24.4f),
			new float4(-0.383747758f, +0f, -0.923437956f, -56.3047135f),
			new float4(+0.606382618f, +0f, +0.795173013f, +73.8854488f),
			new float4(+0.707750483f, +0f, -0.706462493f, +51.1607863f),
			new float4(+0.965708999f, +0f, -0.259626902f, +85.0594455f),
			new float4(+0f, +1f, +0f, -29.89f),
		};

		static readonly float4[] kNeighbour4862 =
		{
			new float4(-0.499992958f, +0f, +0.866029469f, -29.0152437f),
			new float4(+0f, -1f, -0f, +24.4f),
			new float4(-0.794010974f, +0f, -0.607903424f, -90.1879994f),
			new float4(+0.923387019f, +0f, +0.383870308f, +96.7897889f),
			new float4(+0.259626902f, +0f, -0.965708999f, +2.36281333f),
			new float4(+0.706462493f, +0f, -0.707750483f, +51.0091289f),
			new float4(+0f, +1f, +0f, -29.89f),
		};

		static readonly float4[] kNeighbour4863 =
		{
			new float4(+0.499992958f, +0f, +0.866029469f, +68.5824609f),
			new float4(-0f, -1f, +0f, +24.4f),
			new float4(-0.923437956f, +0f, +0.383747758f, -83.4632816f),
			new float4(+0.794080342f, +0f, -0.607812809f, +64.8122288f),
			new float4(-0.70614057f, +0f, -0.708071674f, -86.8666214f),
			new float4(-0.259788525f, +0f, -0.965665533f, -48.3303993f),
			new float4(+0f, +1f, +0f, -29.89f),
		};

		static readonly float4[] kNeighbour4864 =
		{
			new float4(+0.866029469f, +0f, +0.499992958f, +97.162522f),
			new float4(-0f, -1f, +0f, +24.4f),
			new float4(-0.607834063f, +0f, +0.794064073f, -44.6497879f),
			new float4(+0.383819372f, +0f, -0.923408192f, +18.6093169f),
			new float4(-0.965547314f, +0f, -0.260227562f, -103.442773f),
			new float4(-0.708072553f, +0f, -0.706139689f, -87.0177408f),
			new float4(+0f, +1f, +0f, -29.89f),
		};

		static readonly float4[] kNeighbour4866 =
		{
			new float4(+0.866034677f, +0f, -0.499983937f, +77.6434765f),
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(+0.383794865f, +0f, +0.923418378f, +54.6569056f),
			new float4(-0.607843123f, +0f, -0.794057138f, -75.6508453f),
			new float4(-0.708201023f, -4.90285308e-06f, +0.706010843f, -59.4651006f),
			new float4(-0.965521128f, +9.03905221e-07f, +0.260324704f, -93.2791399f),
			new float4(+0f, +1f, +0f, -29.89f),
		};

		static readonly float4[] kNeighbour4867 =
		{
			new float4(+0.499992958f, +0f, -0.866029469f, +34.7721241f),
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(+0.79413343f, +0f, +0.607743445f, +88.5448228f),
			new float4(-0.923459117f, +0f, -0.383696832f, -98.4453885f),
			new float4(-0.260227562f, +0f, +0.965547314f, -10.6761419f),
			new float4(-0.706139689f, -2.45858525e-06f, +0.708072553f, -59.2228027f),
			new float4(+0f, +1f, +0f, -29.89f),
		};

		static readonly float4[] kNeighbour4868 =
		{
			new float4(+0f, +0f, -1f, -16.6415625f),
			new float4(+0.991600411f, +0f, +0.129339184f, +98.4789491f),
			new float4(-0.991600411f, +0f, +0.129339184f, -95.0814512f),
			new float4(+0.257662651f, +0f, +0.96623494f, +39.8829575f),
			new float4(-0.257662651f, +0f, +0.96623494f, -10.4127919f),
			new float4(+0f, +1f, +0f, -29.89f),
			new float4(+0f, -1f, +0f, +24.4f),
		};

		static readonly float4[] kNeighbour4869 =
		{
			new float4(-0.499977333f, +0f, -0.86603849f, -62.8236605f),
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(+0.923384901f, +0f, -0.383875402f, +81.803216f),
			new float4(-0.794005663f, +0f, +0.607910361f, -66.454832f),
			new float4(+0.706430232f, +4.91515752e-06f, +0.707782683f, +78.6370175f),
			new float4(+0.259626902f, +0f, +0.965708999f, +40.0640927f),
			new float4(+0f, +1f, -0f, -29.89f),
		};

		static readonly float4[] kNeighbour4870 =
		{
			new float4(-0.862481839f, +0f, -0.506088013f, -91.1753923f),
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(+0.606400884f, +0f, -0.795159084f, +42.8438846f),
			new float4(-0.383709087f, +3.20643759e-06f, +0.923454025f, -20.2498586f),
			new float4(+0.965726363f, +1.80251603e-06f, +0.259562308f, +95.1957135f),
			new float4(+0.707557557f, -2.45366569e-06f, +0.706655718f, +78.7263043f),
			new float4(+0f, +1f, +0f, -29.89f),
		};

		static readonly float4[] kNeighbour4871 =
		{
			new float4(+0f, +0f, +1f, +22.3984375f),
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(-0.991602214f, +0f, -0.129325362f, -100.130702f),
			new float4(+0.991596804f, +0f, -0.129366837f, +93.4285405f),
			new float4(-0.257662651f, +0f, -0.96623494f, -48.1346039f),
			new float4(+0.257662651f, +0f, -0.96623494f, +2.16114548f),
			new float4(+1f, +0f, +0f, +97.2378125f),
			new float4(-1f, +0f, +0f, -97.9621875f),
			new float4(+0f, +1f, +0f, -25.62f),
		};

		static readonly float4[] kNeighbour4872 =
		{
			new float4(+0f, +0f, +1f, +22.3984375f),
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(-0.991598969f, +0f, -0.129350244f, -100.130959f),
			new float4(+0f, +1f, +0f, -29.89f),
			new float4(+1f, +0f, +0f, +97.9621875f),
		};

		static readonly float4[] kNeighbour4873 =
		{
			new float4(+0f, -0f, +1f, +22.3984375f),
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(+0.991603295f, +4.49017605e-06f, -0.12931707f, +93.4302132f),
			new float4(+0f, +1f, +0f, -29.89f),
			new float4(-1f, +0f, +0f, -97.2378125f),
		};

		static readonly float4[] kNeighbour4874 =
		{
			new float4(+1f, +0f, +0f, +100.478437f),
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(-0.129408338f, +4.04401055e-05f, +0.991591388f, +5.89838401f),
			new float4(-0.129366837f, +0f, -0.991596804f, -32.8082398f),
			new float4(-0.96623494f, +0f, +0.257662651f, -93.4007784f),
			new float4(-0.96623494f, +0f, -0.257662651f, -103.459928f),
			new float4(+0f, +0f, -1f, -19.8821875f),
			new float4(+0f, +0f, +1f, +19.1578125f),
			new float4(+0f, +1f, -0f, -25.62f),
		};

		static readonly float4[] kNeighbour4875 =
		{
			new float4(+1f, +0f, +0f, +100.478437f),
			new float4(-0f, -1f, -0f, +24.4f),
			new float4(-0.129350244f, +0f, -0.991598969f, -32.8065788f),
			new float4(-0f, +1f, +0f, -29.89f),
			new float4(+0f, +0f, +1f, +19.8821875f),
		};

		static readonly float4[] kNeighbour4876 =
		{
			new float4(+1f, +0f, +0f, +100.478437f),
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(-0.129350244f, +0f, +0.991598969f, +5.90544496f),
			new float4(+0f, +1f, -0f, -29.89f),
			new float4(+0f, +0f, -1f, -19.1578125f),
		};

		static readonly float4[] kNeighbour4894 =
		{
			new float4(+0f, +1f, +0f, -28.975f),
			new float4(+0.9701425f, +0f, +0.242535625f, +95.7215351f),
			new float4(-0.9701425f, +0f, -0.242535625f, -95.8694819f),
			new float4(+0f, -0f, +1f, +17.69f),
			new float4(+0f, -1f, +0f, +25.4675f),
			new float4(+0f, +0f, -1f, -18.4525f),
		};

		static readonly float4[] kNeighbour4897 =
		{
			new float4(+0f, +1f, +0f, -28.975f),
			new float4(+0.9701425f, +0f, -0.242535625f, +86.2529443f),
			new float4(-0.9701425f, +0f, +0.242535625f, -86.4008911f),
			new float4(+0f, +0f, -1f, -21.35f),
			new float4(+0f, -1f, +0f, +25.4675f),
			new float4(+0f, -0f, +1f, +20.5875f),
		};

		static readonly float4[] kNeighbour4898 =
		{
			new float4(+0f, -1f, +0f, +25.4675f),
			new float4(+0.695081302f, +0f, -0.71893114f, +50.0733608f),
			new float4(-0.69513353f, +0f, +0.718880641f, -50.2273469f),
			new float4(+0.866081536f, +0f, +0.499902763f, +92.4811717f),
			new float4(-0.865768711f, -0f, -0.500444342f, -94.2935524f),
			new float4(+0f, +1f, +0f, -26.0775f),
		};

		static readonly float4[] kNeighbour4905 =
		{
			new float4(+0f, -1f, +0f, +25.4675f),
			new float4(+0.242535625f, +0f, -0.9701425f, +1.03562712f),
			new float4(-0.242535625f, +0f, +0.9701425f, -1.18357385f),
			new float4(+1f, +0f, +0f, +95.77f),
			new float4(-1f, +0f, +0f, -97.6f),
			new float4(+0f, +1f, +0f, -26.0775f),
		};

		static readonly float4[] kNeighbour4906 =
		{
			new float4(+0f, -1f, +0f, +25.4675f),
			new float4(-0.242535625f, -0f, -0.9701425f, -46.3073269f),
			new float4(+0.242535625f, +0f, +0.9701425f, +46.1593802f),
			new float4(-1f, +0f, +0f, -99.43f),
			new float4(+1f, +0f, +0f, +97.6f),
			new float4(+0f, +1f, +0f, -26.0775f),
		};

		static readonly float4[] kNeighbour4911 =
		{
			new float4(+0f, -1f, +0f, +25.4675f),
			new float4(-0.695081302f, -0f, -0.71893114f, -85.5791f),
			new float4(+0.69513353f, +0f, +0.718880641f, +85.4353108f),
			new float4(-0.866081536f, +0f, +0.499902763f, -76.597003f),
			new float4(+0.865768711f, +0f, -0.500444342f, +74.7235794f),
			new float4(+0f, +1f, +0f, -26.0775f),
		};

		static readonly float4[] kNeighbour4914 =
		{
			new float4(+0f, -1f, +0f, +25.4675f),
			new float4(-0.9701425f, -0f, -0.242535625f, -103.118872f),
			new float4(+0.9701425f, +0f, +0.242535625f, +102.970925f),
			new float4(+0f, -0f, -1f, -21.35f),
			new float4(+0f, +0f, +1f, +19.52f),
			new float4(+0f, +1f, +0f, -26.0775f),
		};

		static readonly float4[] kNeighbour4921 =
		{
			new float4(+0f, -1f, +0f, +25.4675f),
			new float4(-0.9701425f, +0f, +0.242535625f, -93.6502809f),
			new float4(+0.9701425f, +0f, -0.242535625f, +93.5023342f),
			new float4(+0f, +0f, +1f, +17.69f),
			new float4(+0f, -0f, -1f, -19.52f),
			new float4(+0f, +1f, +0f, -26.0775f),
		};

		static readonly float4[] kNeighbour4925 =
		{
			new float4(+0f, -1f, +0f, +25.4675f),
			new float4(-0.718885524f, +0f, +0.69512848f, -60.3038613f),
			new float4(+0.718926257f, +0f, -0.695086352f, +60.1607413f),
			new float4(+0.500444342f, +0f, +0.865768711f, +63.922401f),
			new float4(-0.499902763f, -0f, -0.866081536f, -65.7035683f),
			new float4(+0f, +1f, +0f, -26.0775f),
		};

		static readonly float4[] kNeighbour4926 =
		{
			new float4(+0f, -1f, +0f, +25.4675f),
			new float4(-0.242535625f, +0f, +0.9701425f, -8.43296368f),
			new float4(+0.242535625f, +0f, -0.9701425f, +8.28501695f),
			new float4(-1f, +0f, +0f, -99.43f),
			new float4(+1f, +0f, +0f, +97.6f),
			new float4(+0f, +1f, +0f, -26.0775f),
		};

		static readonly float4[] kNeighbour4930 =
		{
			new float4(+0f, -1f, +0f, +25.4675f),
			new float4(+0.242535625f, +0f, +0.9701425f, +38.9099903f),
			new float4(-0.242535625f, -0f, -0.9701425f, -39.0579371f),
			new float4(+1f, +0f, +0f, +95.77f),
			new float4(-1f, +0f, +0f, -97.6f),
			new float4(+0f, +1f, +0f, -26.0775f),
		};

		static readonly float4[] kNeighbour4934 =
		{
			new float4(+0f, -1f, +0f, +25.4675f),
			new float4(+0.695128307f, +0f, +0.718885691f, +78.1562334f),
			new float4(-0.695081302f, -0f, -0.71893114f, -78.3004455f),
			new float4(+0.865737373f, +0f, -0.500498553f, +72.8914821f),
			new float4(-0.866081536f, +0f, +0.499902763f, -74.7644048f),
			new float4(+0f, +1f, +0f, -26.0775f),
		};

		static readonly float4[] kNeighbour5188 =
		{
			new float4(+0f, +1f, -0f, -25.01f),
			new float4(-0f, -1f, +0f, +24.4f),
			new float4(-0.868243142f, +0f, -0.496138938f, -94.3967898f),
			new float4(+0.496138938f, +0f, +0.868243142f, +65.3996395f),
			new float4(+0.707106781f, +0f, -0.707106781f, +48.5791198f),
			new float4(-0.707106781f, +0f, +0.707106781f, -48.7273912f),
		};

		static readonly float4[] kNeighbour5191 =
		{
			new float4(-0f, +1f, +0f, -25.01f),
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(+0.868243142f, +0f, -0.496138938f, +75.0842715f),
			new float4(-0.496138938f, +0f, +0.868243142f, -31.4466813f),
			new float4(-0.707106781f, +0f, -0.707106781f, -89.4481239f),
			new float4(+0.707106781f, +0f, +0.707106781f, +89.2998525f),
		};

		static readonly float4[] kNeighbour5193 =
		{
			new float4(+0f, +1f, +0f, -25.01f),
			new float4(+0f, -1f, -0f, +24.4f),
			new float4(-0.868243142f, +0f, +0.496138938f, -75.0275256f),
			new float4(+0.496138938f, +0f, -0.868243142f, +31.5034272f),
			new float4(+0.707106781f, +0f, +0.707106781f, +76.1845685f),
			new float4(-0.707106781f, +0f, -0.707106781f, -76.3328399f),
		};

		static readonly float4[] kNeighbour5195 =
		{
			new float4(+0f, +1f, +0f, -25.01f),
			new float4(+0f, -1f, +0f, +24.4f),
			new float4(+0.868243142f, +0f, +0.496138938f, +94.4535357f),
			new float4(-0.496138938f, +0f, -0.868243142f, -65.3428936f),
			new float4(-0.707106781f, +0f, +0.707106781f, -61.8426752f),
			new float4(+0.707106781f, +0f, -0.707106781f, +61.6944037f),
		};

		// the ones that rest ON the face - what the first version of this fixture used, and it passed
		static readonly float4[][] kResting = { kNeighbour4312, kNeighbour4314, kNeighbour4315, kNeighbour4317, kNeighbour4894, kNeighbour4897, kNeighbour4898, kNeighbour4905, kNeighbour4906, kNeighbour4911, kNeighbour4914, kNeighbour4921, kNeighbour4925, kNeighbour4926, kNeighbour4930, kNeighbour4934 };
		// everything that shares the pad's volume
		internal static readonly float4[][] kNeighbourhood = { kNeighbour3982, kNeighbour3984, kNeighbour3986, kNeighbour3988, kNeighbour3990, kNeighbour3992, kNeighbour4054, kNeighbour4055, kNeighbour4056, kNeighbour4057, kNeighbour4058, kNeighbour4059, kNeighbour4060, kNeighbour4061, kNeighbour4062, kNeighbour4063, kNeighbour4064, kNeighbour4065, kNeighbour4312, kNeighbour4314, kNeighbour4315, kNeighbour4317, kNeighbour4839, kNeighbour4840, kNeighbour4841, kNeighbour4842, kNeighbour4843, kNeighbour4844, kNeighbour4845, kNeighbour4846, kNeighbour4847, kNeighbour4848, kNeighbour4849, kNeighbour4850, kNeighbour4861, kNeighbour4862, kNeighbour4863, kNeighbour4864, kNeighbour4866, kNeighbour4867, kNeighbour4868, kNeighbour4869, kNeighbour4870, kNeighbour4871, kNeighbour4872, kNeighbour4873, kNeighbour4874, kNeighbour4875, kNeighbour4876, kNeighbour4894, kNeighbour4897, kNeighbour4898, kNeighbour4905, kNeighbour4906, kNeighbour4911, kNeighbour4914, kNeighbour4921, kNeighbour4925, kNeighbour4926, kNeighbour4930, kNeighbour4934, kNeighbour5188, kNeighbour5191, kNeighbour5193, kNeighbour5195 };
		internal static readonly string[] kNeighbourhoodNames = { "detail 3982", "detail 3984", "detail 3986", "detail 3988", "detail 3990", "detail 3992", "detail 4054", "detail 4055", "detail 4056", "detail 4057", "detail 4058", "detail 4059", "detail 4060", "detail 4061", "detail 4062", "detail 4063", "detail 4064", "detail 4065", "detail 4312", "detail 4314", "detail 4315", "detail 4317", "detail 4839", "detail 4840", "detail 4841", "detail 4842", "detail 4843", "detail 4844", "detail 4845", "detail 4846", "detail 4847", "detail 4848", "detail 4849", "detail 4850", "detail 4861", "detail 4862", "detail 4863", "detail 4864", "detail 4866", "detail 4867", "detail 4868", "detail 4869", "detail 4870", "detail 4871", "detail 4872", "detail 4873", "detail 4874", "detail 4875", "detail 4876", "detail 4894", "detail 4897", "detail 4898", "detail 4905", "detail 4906", "detail 4911", "detail 4914", "detail 4921", "detail 4925", "detail 4926", "detail 4930", "detail 4934", "detail 5188", "detail 5191", "detail 5193", "detail 5195" };
		internal static readonly float4[] kPadPlanes = kPad;
		internal static readonly float4 kTop = kTopFace;

		/// <summary>The area of the pad's top face in a scene's output. Public so the shrink survey can use the
		/// same measurement rather than a second one that might disagree with it.</summary>
		internal static double MeasureTopFaceOf(ContentsScene scene)
		{
			using (var harness = ContentsTreeHarness.Build(scene))
			{
				if (!harness.Update())
					return -1;
				return TopFaceArea(harness);
			}
		}

		internal static ContentsScene BuildPadWith(params float4[][] others) { return PadWith(others); }

		// The area of output triangles that lie on the top face's plane and face the same way.
		static double TopFaceArea(ContentsTreeHarness harness)
		{
			double area = 0;
			foreach (var triangle in harness.Triangles)
			{
				if (math.dot(triangle.Normal, kTopFace.xyz) < 0.999f)
					continue;
				if (math.abs(math.dot(kTopFace.xyz, triangle.Center) + kTopFace.w) > 1e-3f)
					continue;
				area += 0.5 * math.length(math.cross(triangle.b - triangle.a, triangle.c - triangle.a));
			}
			return area;
		}

		static ContentsScene PadWith(params float4[][] others)
		{
			var scene = new ContentsScene().Add(ContentsSceneNode.Brush(kPad, name: "pad 4838"));
			for (int i = 0; i < others.Length; i++)
				scene.Add(ContentsSceneNode.Brush(others[i], name: kNeighbourhoodNames[i]));
			return scene;
		}

		static double MeasureTopFace(ContentsScene scene, string what)
		{
			using (var harness = ContentsTreeHarness.Build(scene))
			{
				Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
				if (CompactHierarchyManager.TreeUpdate.kExactCSG)
					ContentsComparison.AssertMatchesOracle(harness, what);
				return TopFaceArea(harness);
			}
		}

		static void AssertArea(double area, double expected, double tolerance, string message)
		{
			if (CompactHierarchyManager.TreeUpdate.kExactCSG)
				return;
			Assert.That(area, Is.EqualTo(expected).Within(tolerance), message);
		}

		[Test]
		public void ThePadOnItsOwn_ShowsItsWholeTopFace()
		{
			var area = MeasureTopFace(PadWith(), "the pad alone");
			AssertArea(area, 47.143552, 0.992871, $"the pad's top face is {area:0.00} m2");
		}

		[Test]
		public void TheBrushesRestingOnThePad_TakeOnlyWhatTheyCover()
		{
			var area = MeasureTopFace(PadWith(kResting), "the brushes resting on the pad");
			AssertArea(area, 42.105717, 0.892114, $"the pad's top face is {area:0.00} m2");
		}

		[Test]
		public void APadWithOneBrushRestingOnItAndOnePassingThrough_KeepsTheRestOfItsTopFace()
		{
			var area = MeasureTopFace(PadWith(kNeighbour4312, kNeighbour4863), "the shrunk pair");
			AssertArea(area, 45.792030, 0.965841,
					   $"the pad's top face is {area:0.00} m2 of the 45.79 m2 that nothing covers");
		}

		// THE MAP'S OWN NEIGHBOURHOOD: every detail brush that shares the pad's volume. This is what Sander is
		// looking at, and in the scene only 0.50 m2 of this face survives.
		[Test]
		public void ThePadInItsNeighbourhood_KeepsTheConcreteNothingCovers()
		{
			var area = MeasureTopFace(PadWith(kNeighbourhood), "the map's own neighbourhood");
			AssertArea(area, 34.464447, 0.739289,
					   $"the pad's top face is {area:0.00} m2 of the 34.46 m2 that nothing covers");
		}
	}
}
