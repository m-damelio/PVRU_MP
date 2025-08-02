Shader "Custom/WireframeShader"
{
    Properties
    {
        _WireColor ("Wire Color", Color) = (0.0, 1.0, 1.0, 1.0)
        _BaseColor ("Base Color", Color) = (0.0, 0.0, 0.0, 0.5)
        _WireThickness ("Wire Thickness", Range(0, 1)) = 0.1
        _WireSmoothness ("Wire Smoothness", Range(0,1)) = 0.1

        [HDR] _EmissionColor ("Emission Color", Color) = (0.0, 1.0, 1.0, 1.0)
        _EmissionIntensity ("Emission Intensity", Range(0,3)) = 1.0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Culling", Float) = 2
        [Enum(Off, 0, On, 1)] _ZWrite ("Depth Write", Float) = 0
        
        [Toggle] _UseSimpleWireframe ("Use Simple Wireframe (Mobile)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalRenderPipeline"}

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull [_Cull]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            // Simplified lighting for mobile
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            
            // VR support
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED
            #pragma multi_compile _ UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO

            // Mobile optimization
            #pragma shader_feature _USESIMPLEWIREFRAME_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR; // Barycentric coordinates
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 barycentric : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
            half4 _WireColor;
            half4 _BaseColor;
            half _WireThickness;
            half _WireSmoothness;
            half4 _EmissionColor;
            half _EmissionIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.barycentric = IN.color;
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half4 finalColor = _BaseColor;
                float wire = 0.0;

                #ifdef _USESIMPLEWIREFRAME_ON
                    // Simple mobile-friendly wireframe calculation
                    // Uses direct barycentric coordinate comparison
                    float3 barycentric = IN.barycentric.xyz;
                    
                    // Find the minimum barycentric coordinate
                    float minBary = min(min(barycentric.x, barycentric.y), barycentric.z);
                    
                    // Create wireframe effect based on proximity to edge
                    wire = step(minBary, _WireThickness);
                    
                    // Optional smoothing (less expensive than fwidth)
                    if(_WireSmoothness > 0.01)
                    {
                        wire = smoothstep(_WireThickness - _WireSmoothness, _WireThickness, minBary);
                        wire = 1.0 - wire;
                    }
                #else
                    // Desktop version with fwidth (keep for PC builds)
                    float3 d = fwidth(IN.barycentric.xyz);
                    float3 a3 = smoothstep(float3(0.0, 0.0, 0.0), d * (1.0 + _WireSmoothness), IN.barycentric.xyz);
                    float min_dist = min(min(a3.x, a3.y), a3.z);
                    wire = 1.0 - smoothstep(_WireThickness, _WireThickness + 0.01, min_dist);
                #endif

                // Mix base and wire colors
                finalColor = lerp(_BaseColor, _WireColor, wire);

                // Apply emission (reduced intensity for mobile)
                half3 emission = _EmissionColor.rgb * _EmissionIntensity * wire * 0.5;
                finalColor.rgb += emission;

                // Set alpha
                finalColor.a = max(finalColor.a, wire * _WireColor.a);

                return finalColor;
            }
            ENDHLSL
        }
    }
}