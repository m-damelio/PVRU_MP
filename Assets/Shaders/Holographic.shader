Shader "Custom/Holographic"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _HologramColor ("Hologram Color", Color) = (0, 1, 1, 1)
        _RimColor ("Rim Color", Color) = (1, 0, 1, 1)
        _RimPower ("Rim Power", Range(0.1, 8.0)) = 2.0
        _RimIntensity ("Rim Intensity", Range(0, 3)) = 1.5
        
        [Header(Simple Glitch)]
        _GlitchIntensity ("Glitch Intensity", Range(0, 0.1)) = 0.02
        _GlitchSpeed ("Glitch Speed", Range(0, 5)) = 3.0
        
        [Header(Transparency)]
        _Alpha ("Alpha", Range(0, 1)) = 0.7
        _FresnelPower ("Fresnel Power", Range(0.1, 3.0)) = 1.0
        
        [Header(Color Pulse)]
        _PulseSpeed ("Pulse Speed", Range(0, 3)) = 1.0
        _PulseIntensity ("Pulse Intensity", Range(0, 0.5)) = 0.2

        [Header(Rendering)]
        [Enum(Off, 0, Front, 1, Back, 2)] _CullMode ("Cull Mode", Float) = 2
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+100" //will be drawn later since i have used this shader for ui
            "RenderPipeline" = "UniversalPipeline"
        }
        
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull [_CullMode]
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED
            #pragma multi_compile _ UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float rimFactor : TEXCOORD3;
                float fogCoord : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _HologramColor;
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                float _GlitchIntensity;
                float _GlitchSpeed;
                float _Alpha;
                float _FresnelPower;
                float _PulseSpeed;
                float _PulseIntensity;
                float _CullMode;
            CBUFFER_END
            
            // Simplified noise function
            float simpleNoise(float x)
            {
                return frac(sin(x * 12.9898) * 43758.5453);
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionHCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                
                // Pre-calculate view direction and normal in vertex shader
                output.viewDirWS = normalize(GetCameraPositionWS() - vertexInput.positionWS);
                output.normalWS = normalInput.normalWS;
                
                // Pre-calculate rim factor in vertex shader for better performance
                float NdotV = dot(normalInput.normalWS, output.viewDirWS);
                output.rimFactor = 1.0 - saturate(NdotV);
                
                output.fogCoord = ComputeFogFactor(output.positionHCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                
                // Lightweight glitch effect
                float glitchTime = _Time.y * _GlitchSpeed;
                float glitchNoise = simpleNoise(floor(glitchTime * 10.0));
                float glitchOffset = (glitchNoise - 0.5) * _GlitchIntensity;
                uv.x += glitchOffset * step(0.98, glitchNoise); // Only glitch occasionally
                
                // Base texture
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                
                // Use pre-calculated rim factor from vertex shader
                float rim = pow(abs(input.rimFactor), _RimPower) * _RimIntensity;
                
                // Simple pulse effect instead of complex color shifting
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseIntensity + 1.0;
                
                // Combine colors efficiently
                float3 hologramTint = _HologramColor.rgb * pulse;
                float3 finalColor = baseColor.rgb * hologramTint + rim * _RimColor.rgb;
                
                // Fresnel for transparency (using pre-calculated rim factor)
                float fresnel = pow(abs(input.rimFactor), _FresnelPower);
                float alpha = _Alpha * (fresnel * 0.5 + 0.5 + rim * 0.3);
                
                half4 color = half4(finalColor, saturate(alpha));
                
                // Apply fog
                color.rgb = MixFog(color.rgb, input.fogCoord);
                
                return color;
            }
            ENDHLSL
        }
    }
    
    Fallback "Universal Render Pipeline/Unlit"
}