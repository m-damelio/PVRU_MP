Shader "Unlit/StripesShader"
{
    Properties
    {
        _Tiling ("Tiling", Range(1,500)) = 10
        _WidthShift ("Width Shift", Range(0,1)) = 0.5
        _TopCut ("Top Cut", Range(0,0.5)) = 0.2
        _BottomCut ("Bottom Cut", Range(0,0.5)) = 0.1
        _Color1 ("Color 1", Color) = (0,0,0,1)
        _Color2 ("Color 2", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

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

            int _Tiling; 
            float _WidthShift;
            float _BottomCut;
            float _TopCut;
            float4 _Color1;
            float4 _Color2;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                //Define regions 
                float startY = _BottomCut;
                float endY = 1.0 - _TopCut;
                float regionHeight = endY - startY;
                float uvy = i.uv.y;
                //Cutoff bottom and top to be white
                if (uvy < startY || uvy > endY)
                {
                    return half4(1,1,1,1);
                }
                //remap uvy to actual stripe region
                float t = (uvy - startY) / regionHeight;
                float pos = t * _Tiling;
                half value = floor(frac(pos) + _WidthShift);
                return lerp(_Color1, _Color2, value);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
