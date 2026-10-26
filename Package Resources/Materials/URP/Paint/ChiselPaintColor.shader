// Test shader for a Color paint mode: the base color times the vertex color
Shader "Chisel/Test/Paint Color"
{
    Properties
    {
        _BaseMap   ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _ValueColor ("Value Color", Color) = (1, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex PaintVertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "ChiselPaintCommon.hlsl"

            half4 Fragment(PaintVaryings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * input.color;
                return half4(PaintLighting(albedo.rgb, input.positionWS, input.normalWS), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #include "ChiselPaintCommon.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthNormalsFragment
            #include "ChiselPaintCommon.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            #include "ChiselPaintCommon.hlsl"
            ENDHLSL
        }
    }
}
