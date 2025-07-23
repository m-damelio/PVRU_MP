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
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED
            #pragma multi_compile _ UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
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

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.worldPos = mul(unity_ObjectToWorld, v.positionOS).xyz;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
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

                return finalColor;
            }
            ENDHLSL
        }
    }
}
