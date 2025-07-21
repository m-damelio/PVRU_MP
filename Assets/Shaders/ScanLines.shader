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
            // VR stereo rendering support
            #pragma multi_compile_fog
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
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
                
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.vertex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                // base texture
                fixed4 col = tex2D(_MainTex, i.uv);

                // Use UV coordinates instead of screen position for VR compatibility
                // This ensures scanlines appear consistently in both eyes
                float2 scanlineUV = i.uv;

                // moving scanlines - use UV.y instead of screen coordinates
                float scanline = sin((scanlineUV.y + _Time.y * _ScanlineSpeed) * _ScanlineFrequency);
                scanline = scanline * 0.5 + 0.5; // Convert from -1,1 to 0,1

                // Flicker effect
                float flicker = sin(_Time.y * _FlickerSpeed) * _FlickerIntensity + 1.0;

                // Apply scanlines
                float scanlineEffect = lerp(1.0, scanline, _ScanlineIntensity);
                col.rgb *= scanlineEffect * flicker;

                // Add emission glow to scanlines
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
