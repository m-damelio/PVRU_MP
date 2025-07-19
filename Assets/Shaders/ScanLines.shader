Shader "Custom/ScanLines"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScanlineColor ("Scanline Color", Color) = (0,1,1,1)
        _ScanlineFrequency("Scanline Frequency", Range(50,500)) = 200.0
        [PerRendererData] _ScanlineIntensity("Scanline Intensity", Range(0,1))= 0.3
        _ScanlineSpeed("Scanline Speed", Range(-10,10)) = 1.0
        _FlickerSpeed("Flicker Speed", Range(0,20)) = 5.0
        _FlickerIntensity("Flicker Intensity", Range(0,1)) = 0.1
        _EmissionIntensity ("Emission Intensity", Range(0,5)) = 1.0
        
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
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _ScanlineColor;
            float _ScanlineFrequency;
            float _ScanlineIntensity;
            float _ScanlineSpeed;
            float _FlickerSpeed;
            float _FlickerIntensity;
            float _EmissionIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.vertex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // base texture
                fixed4 col = tex2D(_MainTex, i.uv);

                //Screen position for scanlines
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                //moving scanlines
                float4 scanline = sin((screenUV.y + _Time.y * _ScanlineSpeed) * _ScanlineFrequency);
                scanline = scanline * 0.5 + 0.5; //Convert from -1,1 to 0,1

                //Flicker effect
                float flicker = sin(_Time.y * _FlickerSpeed) * _FlickerIntensity + 1.0;

                //Apply scanlines
                float scanlineEffect = lerp(1.0, scanline, _ScanlineIntensity);
                col.rgb *= scanlineEffect * flicker;

                //Add emission glow to scanlines
                float scanlineGlow = smoothstep(0.7, 1.0, scanline) * _ScanlineIntensity;
                col.rgb += _ScanlineColor.rgb * scanlineGlow * _EmissionIntensity;

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);

                return col;
            }
            ENDCG
        }
    }
}
