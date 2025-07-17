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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float3 worldNormal : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
                float4 vertex : SV_POSITION;
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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                //Base texture
                fixed4 col = tex2D(_MainTex, i.uv);

                //Fresnel effect
                float3 viewDir = normalize(_WorldSpaceCameraPos -i.worldPos);
                float fresnel = 1.0 - dot(normalize(i.worldNormal), viewDir);
                fresnel = pow(fresnel, _FresnelPower) * _FresnelIntensity;

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

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);

                return col;
            }
            ENDCG
        }
    }
}
