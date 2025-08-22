Shader "Custom/WavyGuard"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WaveAmp ("Wave Amplitude", Range(0, 0.2)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define TAU 6.2831853071

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _WaveAmp;

            struct MeshData {
                float4 vertex : POSITION;
                float4 normals : NORMAL;
                float2 uv0 : TEXCOORD0;
            };

            struct Interpolators {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                
            };

            

            Interpolators vert (MeshData v) {
                Interpolators o;

                float3 pos = v.vertex.xyz;
                float wave = sin((pos.x + _Time.y * 0.1) * TAU * 5);
                float wave2 = cos((pos.z + _Time.y * 0.1) * TAU * 5);

                pos.y += wave * wave2 * _WaveAmp;

                v.vertex.xyz = pos;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv0;
                return o;
            }

            float4 frag (Interpolators i) : SV_Target {
                // sample the texture




                float4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}
