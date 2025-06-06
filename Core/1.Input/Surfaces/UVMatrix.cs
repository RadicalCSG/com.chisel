using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    /// <summary>A 2x4 matrix to calculate the UV coordinates for the vertices of a <see cref="Chisel.Core.BrushMesh"/>.</summary>
    /// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
    /// <seealso cref="Chisel.Core.BrushMesh"/>
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct UVMatrix
    {
        public const double kScaleStep  = 1.0 / 10000.0;
        public const double kMinScale   = kScaleStep;
        const double kSnapEpsilon       = kScaleStep / 10.0f;

        /// <value>Used to convert a vertex coordinate to a U texture coordinate</value>
        public Vector4 U;

        /// <value>Used to convert a vertex coordinate to a V texture coordinate</value>
        public Vector4 V;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UVMatrix(float4 u, float4 v) 
        { 
            U = u; 
            V = v;
            Validate();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UVMatrix(Matrix4x4 input) 
        { 
            U = input.GetRow(0); 
            V = input.GetRow(1);
            Validate();
        }

        void Validate()
        {
            if (math.any(!math.isfinite(U)) || math.any(!math.isfinite(V)))
            {
                Debug.LogError("Resetting UV values since they are set to invalid floating point numbers.");
                U = new float4(1, 0, 0, 0);
                V = new float4(0, 1, 0, 0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matrix4x4 ToMatrix4x4() { return (Matrix4x4)ToFloat4x4(); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float4x4 ToFloat4x4()
        {
            Validate();
            var w = new float4(PlaneNormal, 0);
            return math.transpose(new float4x4 { c0 = U, c1 = V, c2 = w, c3 = new float4(0, 0, 0, 1) }); 
        }

		public readonly float3 PlaneNormal { get { return math.normalizesafe(math.cross(((float4)U).xyz, ((float4)V).xyz)); } }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Matrix4x4(UVMatrix input) { return input.ToMatrix4x4(); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator UVMatrix(Matrix4x4 input) { return new UVMatrix(input); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator float4x4(UVMatrix input) { return input.ToFloat4x4(); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator UVMatrix(float4x4 input) { return new UVMatrix(input); }

        readonly static UVMatrix kIdentity = new(new float4(1,0,0,0.0f), new float4(0,1,0,0.0f));
        readonly static UVMatrix kCentered = new(new float4(1,0,0,0.5f), new float4(0,1,0,0.5f));
		public static ref readonly UVMatrix Identity => ref kIdentity;
		public static ref readonly UVMatrix Centered => ref kCentered;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UVMatrix TRS(Vector2 translation, Vector3 normal, float rotation, float2 scale)
		{
            var orientation     = Quaternion.LookRotation(normal, Vector3.forward);
			var inv_orientation = Quaternion.Inverse(orientation);
			var rotation2d      = Quaternion.AngleAxis(rotation, Vector3.forward);
            var scale3d         = new Vector3(scale.x, scale.y, 1.0f);

            // TODO: optimize
            return (UVMatrix)
                    (Matrix4x4.TRS(translation, Quaternion.identity, Vector3.one) *
                     Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale3d) *
                     Matrix4x4.TRS(Vector3.zero, rotation2d, Vector3.one) *

                     Matrix4x4.TRS(Vector3.zero, inv_orientation, Vector3.one));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decompose(out Vector2 translation, out float rotation, out Vector2 scale)
        {
            var normal          = PlaneNormal;
            var orientation     = Quaternion.LookRotation(normal, Vector3.forward);
            var inv_orientation = Quaternion.Inverse(orientation);

            var u = inv_orientation * ((float4)U).xyz;
            var v = inv_orientation * ((float4)V).xyz;

            rotation = -Vector3.SignedAngle(Vector3.right, u, Vector3.forward);

            const double min_rotate = 1.0 / 10000.0;
            rotation = (float)(Math.Round(rotation / min_rotate) * min_rotate);

            scale = new Vector2(u.magnitude, v.magnitude);

            const double min_scale = 1.0 / 10000.0;
            scale.x = (float)(Math.Round(scale.x / min_scale) * min_scale);
            scale.y = (float)(Math.Round(scale.y / min_scale) * min_scale);

            translation     = new Vector2(U.w, V.w);

            const double min_translation = 1.0 / 32768.0;
            translation.x = (float)(Math.Round(translation.x / min_translation) * min_translation);
            translation.y = (float)(Math.Round(translation.y / min_translation) * min_translation);


            // TODO: figure out a better way to find if we scale negatively
            var newUvMatrix = UVMatrix.TRS(translation, normal, rotation, scale);
            if (Vector3.Dot(((float4)V).xyz, ((float4)newUvMatrix.V).xyz) < 0) scale.y = -scale.y;
            if (Vector3.Dot(((float4)U).xyz, ((float4)newUvMatrix.U).xyz) < 0) scale.x = -scale.x;

			Validate();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly string ToString() { return $@"{{U: {U}, V: {V}}}"; }

        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(UVMatrix left, UVMatrix right) { return left.U == right.U && left.V == right.V; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(UVMatrix left, UVMatrix right) { return left.U != right.U || left.V != right.V; }

        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly bool Equals(object obj) { if (obj is not UVMatrix) return false; var uv = (UVMatrix)obj; return this == uv; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly int GetHashCode() { unchecked { return (int)math.hash(new uint2(math.hash(U), math.hash(V))); } }
        #endregion
    }
}