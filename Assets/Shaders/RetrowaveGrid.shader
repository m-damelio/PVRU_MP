Shader "Custom/RetrowaveGrid"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _GridColor ("Grid Color", Color) = (1,0,1,1)
        _BackgroundColor ("Background Color", Color) = (0,0,0,0)
        _GridScale ("Grid Scale", Float) = 10.0
        _GridWidth ("Grid Width", Range(0.01, 0.1)) = 0.05
        _EmmisionIntensity ("Emission Intensity", Range(0,10)) = 2.0
        _PulseSpeed ("Pulse Speed", Range(0,5)) = 1.0
        _PulseIntensity ("Oulse Intensity", Range(0,2)) = 0.5
        _FadeDistance("Fade Distance", Range(10,200)) = 100.0
        _FadeStart ("Fade Start", Range(0,100)) = 50.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-100" } //Used this shader for the ground therefor put it earlier in the queue
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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
                float3 worldPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _GridColor;
            float4 _BackgroundColor;
            float _GridScale;
            float _GridWidth;
            float _EmmisionIntensity;
            float _PulseSpeed;
            float _PulseIntensity;
            float _FadeDistance;
            float _FadeStart;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                //Scale uv for grid 
                float2 gridUV = i.uv * _GridScale;

                //GridPattern
                float2 grid = abs(frac(gridUV-0.5) -0.5) /fwidth(gridUV);
                float gLine = min(grid.x, grid.y);

                //GridLine
                float gridLine = 1.0 - min(gLine, 1.0);
                gridLine = smoothstep(0.0, _GridWidth, gridLine);

                //Pulse effect
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseIntensity + 1.0;

                //Distance fade
                float3 cameraPos = _WorldSpaceCameraPos;
                float distance = length(i.worldPos - cameraPos);
                float fade = 1.0 - smoothstep(_FadeStart, _FadeDistance, distance);

                //Combine colors
                float4 gridColor = _GridColor * gridLine * pulse * _EmmisionIntensity;
                float4 finalColor = lerp(_BackgroundColor, gridColor, gridLine);

                //Apply fade
                finalColor.a *= fade;

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, finalColor);

                return finalColor;
            }
            ENDCG
        }
    }
}
