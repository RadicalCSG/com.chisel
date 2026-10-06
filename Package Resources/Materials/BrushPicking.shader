Shader "Hidden/Chisel/Brush-Picking"
{
    Properties
    {
        [HideInInspector] [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [HideInInspector] [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [HideInInspector] _Offset("Offset", Integer) = 0
    }
    SubShader
    {
        Pass
        {
            Name "ScenePickingPass"
            Tags { "LightMode" = "Picking" }

            // Standard back-face culling, stated explicitly. Verified in-editor: the picking pass's
            // front-face convention matches the mesh winding, and the winding matches the render
            // normals (Cull Front culled exactly the true front faces). Back faces must not be
            // pickable - a brush the camera stands just inside/behind would otherwise win the pick.
            Cull Back

            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma multi_compile_instancing
                #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
                #pragma editor_sync_compilation
                
                #define SCENEPICKINGPASS

                #include "UnityCG.cginc"
                #include "UnityShaderVariables.cginc"
                #include "UnityStandardConfig.cginc"
                #include "UnityStandardUtils.cginc"
                
                struct VertexInput
                {
                    float4 vertex       : POSITION;
                    float4 _SelectionID : TEXCOORD0;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                struct VertexOutput
                {
                    float4 positionCS : SV_POSITION;
                    float4 _SelectionID : TEXCOORD0;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                int _Offset;

                
	            float4 EncodeSelectionId(int pickingIndex)
	            {
                    float a = float((pickingIndex      ) & 0xFF);
                    float b = float((pickingIndex >>  8) & 0xFF);
                    float c = float((pickingIndex >> 16) & 0xFF);
                    float d = float((pickingIndex >> 24) & 0xFF);
		            return float4(saturate(a / 255), 
                                  saturate(b / 255), 
                                  saturate(c / 255), 
                                  saturate(d / 255));
	            }

	            int DecodeSelectionId(float4 selectionId)
	            {
                    // MUST round, not truncate. The id arrives as byte/255 in a float channel, and that
                    // round-trip is not exact: 1/255*255 evaluates to 0.99999997, which int() truncates
                    // to 0. Every byte that lands just below its integer decodes one too low, so the
                    // pick resolves to a NEIGHBOURING selection id - a different brush, which is why
                    // clicking selected the wrong object (and only sometimes, since ids whose
                    // round-trip lands exactly still decoded correctly).
                    int a = int(saturate(selectionId.x) * 255.0 + 0.5);
                    int b = int(saturate(selectionId.y) * 255.0 + 0.5);
                    int c = int(saturate(selectionId.z) * 255.0 + 0.5);
                    int d = int(saturate(selectionId.w) * 255.0 + 0.5);
		            return (a      ) +
                           (b <<  8) +
                           (c << 16) +
                           (d << 24);
	            }

                VertexOutput vert(VertexInput input)
                {
                    VertexOutput output = (VertexOutput)0;
                    UNITY_SETUP_INSTANCE_ID(input);
                    UNITY_TRANSFER_INSTANCE_ID(input, output);
                    
                    output.positionCS = UnityObjectToClipPos(input.vertex);
                    output._SelectionID =
                        EncodeSelectionId(DecodeSelectionId(input._SelectionID) + _Offset);

                    return output;
                }

                float4 frag(VertexOutput input) : SV_Target
                {
                    UNITY_SETUP_INSTANCE_ID(input);
                    return input._SelectionID;
                }
            ENDCG
        }
    }
    FallBack Off
}
