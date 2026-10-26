// What the Chisel paint test shaders share: simple lighting (the main light and the ambient), and the passes URP's
// deferred and forward renderers need from an opaque shader
#ifndef CHISEL_PAINT_COMMON_INCLUDED
#define CHISEL_PAINT_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half4  _ValueColor;
CBUFFER_END

TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);

struct PaintAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    float4 color      : COLOR;
    float4 paint      : TEXCOORD2;
};

struct PaintVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    float2 uv         : TEXCOORD2;
    float4 color      : TEXCOORD3;
    float4 paint      : TEXCOORD4;
};

PaintVaryings PaintVertex(PaintAttributes input)
{
    PaintVaryings output;
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
    output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
    output.color      = input.color;
    output.paint      = input.paint;
    return output;
}

half3 PaintLighting(half3 albedo, float3 positionWS, float3 normalWS)
{
    float3 normal = normalize(normalWS);
    Light  light  = GetMainLight(TransformWorldToShadowCoord(positionWS));
    half3  direct = light.color * saturate(dot(normal, light.direction)) * light.shadowAttenuation;
    return albedo * (direct + SampleSH(normal));
}

// Depth, depth-normals and shadows: the position and normal only
struct DepthVaryings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS   : TEXCOORD0;
};

DepthVaryings DepthVertex(PaintAttributes input)
{
    DepthVaryings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half4 DepthFragment(DepthVaryings input) : SV_Target { return 0; }

half4 DepthNormalsFragment(DepthVaryings input) : SV_Target
{
    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
}

float3 _LightDirection;

DepthVaryings ShadowVertex(PaintAttributes input)
{
    DepthVaryings output;
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    output.positionCS = positionCS;
    output.normalWS   = normalWS;
    return output;
}

#endif
