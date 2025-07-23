Shader "Custom/NeonEmission"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NeonColor ("Neon Color", Color) = (1,0,1,1)
        _EmissionIntensity ("Emission Intensity", Range(0,10)) = 3.0
        _PulseSpeed ("Pulse Speed", Range (0,5)) = 2.0
        _PulseIntensity ("Pulse Intensity", Range(0,2)) = 0.5
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.0
        _FresnelIntensity ("Fresnel Intensity", Range(0,5)) = 1.0
        _NoiseScale ("Noise Scale", Range(0,50)) = 10.0
        _NoiseSpeed ("Noise Speed", Range(0,5)) = 1.0
        _NoiseIntensity("Noise Iintensity", Range(0,1)) = 0.2 
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float fogCoord : TEXCOORD3;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO

            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _NeonColor;
            float _EmissionIntensity;
            float _PulseSpeed;
            float _PulseIntensity;
            float _FresnelPower;
            float _FresnelIntensity;
            float _NoiseScale;
            float _NoiseSpeed;
            float _NoiseIntensity;

            float noise(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43210.9876);
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.worldPos = mul(unity_ObjectToWorld, v.positionOS).xyz;
                o.worldNormal = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                //Base texture
                half4 col = tex2D(_MainTex, i.uv);

                //Fresnel effect
                float3 viewDir = normalize(_WorldSpaceCameraPos -i.worldPos);
                float fresnel = 1.0 - dot(normalize(i.worldNormal), viewDir);
                fresnel = pow(abs(fresnel), _FresnelPower) * _FresnelIntensity;

                //Pulse effect 
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseIntensity + 1.0;

                //Create noise
                float2 noiseUV = i.uv * _NoiseScale + _Time.y * _NoiseSpeed;
                float noiseValue = noise(noiseUV) * _NoiseIntensity + 1.0;

                //Cmombine effects
                float emissionMask = fresnel * pulse * noiseValue;
                float3 emission = _NeonColor.rgb * emissionMask * _EmissionIntensity;

                //Final color
                col.rgb += emission;

                return col;
            }
            ENDHLSL
        }
    }
}
