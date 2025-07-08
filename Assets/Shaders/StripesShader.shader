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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            int _Tiling; 
            float _WidthShift;
            float _BottomCut;
            float _TopCut;
            fixed4 _Color1;
            fixed4 _Color2;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                //Define regions 
                float startY = _BottomCut;
                float endY = 1.0 - _TopCut;
                float regionHeight = endY - startY;
                float uvy = i.uv.y;
                //Cutoff bottom and top to be white
                if (uvy < startY || uvy > endY)
                {
                    return fixed4(1,1,1,1);
                }
                //remap uvy to actual stripe region
                float t = (uvy - startY) / regionHeight;
                float pos = t * _Tiling;
                fixed value = floor(frac(pos) + _WidthShift);
                return lerp(_Color1, _Color2, value);
            }
            ENDCG
        }
    }
}
