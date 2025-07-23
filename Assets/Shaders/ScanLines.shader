Shader "Custom/VR_ScanLines"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ScanlineColor ("Scanline Color", Color) = (0,1,1,1)
        _ScanlineFrequency("Scanline Frequency", Range(50,500)) = 200.0
        _ScanlineIntensity("Scanline Intensity", Range(0,1))= 0.3
        _ScanlineSpeed("Scanline Speed", Range(-10,10)) = 1.0
        _FlickerSpeed("Flicker Speed", Range(0,20)) = 5.0
        _FlickerIntensity("Flicker Intensity", Range(0,1)) = 0.1
        _EmissionIntensity ("Emission Intensity", Range(0,5)) = 1.0
        
        // URP properties
        [Toggle] _AlphaClip("Alpha Clipping", Float) = 0.0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // URP keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            
            // VR keywords 
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED
            #pragma multi_compile _ UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float fogCoord : TEXCOORD3;
                
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Texture and sampler declarations (URP style)
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Property declarations in CBUFFER for SRP Batcher compatibility
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ScanlineColor;
                float _ScanlineFrequency;
                half _ScanlineIntensity;
                float _ScanlineSpeed;
                float _FlickerSpeed;
                half _FlickerIntensity;
                half _EmissionIntensity;
                half _AlphaClip;
                half _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // VR setup
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Transform positions
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                
                // UV transformation
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                
                // Screen position for VR-aware sampling
                output.screenPos = ComputeScreenPos(output.positionHCS);
                
                // Fog
                output.fogCoord = ComputeFogFactor(output.positionHCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // VR setup
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                // Sample base texture
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                baseColor *= _BaseColor;

                // Screen-space UV calculation for post-processing style effect
                // This makes scanlines move consistently across the screen regardless of object rotation
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                
                // Use screen space Y coordinate for consistent horizontal scanlines
                // This creates the post-processing effect you want
                float scanlinePos = (screenUV.y + _Time.y * _ScanlineSpeed) * _ScanlineFrequency;
                float scanline = sin(scanlinePos);
                scanline = scanline * 0.5 + 0.5; // Convert from [-1,1] to [0,1]

                // Flicker effect
                float flicker = sin(_Time.y * _FlickerSpeed) * _FlickerIntensity + 1.0;

                // Apply scanline darkening
                float scanlineEffect = lerp(1.0, scanline, _ScanlineIntensity);
                baseColor.rgb *= scanlineEffect * flicker;

                // Add emission glow to bright scanlines
                float scanlineGlow = smoothstep(0.7, 1.0, scanline) * _ScanlineIntensity;
                half3 emission = _ScanlineColor.rgb * scanlineGlow * _EmissionIntensity;
                baseColor.rgb += emission;

                // Alpha clipping if enabled
                #if defined(_ALPHATEST_ON)
                    clip(baseColor.a - _Cutoff);
                #endif

                // Apply fog
                baseColor.rgb = MixFog(baseColor.rgb, input.fogCoord);

                return baseColor;
            }
            ENDHLSL
        }
        

    }
}