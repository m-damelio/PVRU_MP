Shader "Custom/Healthbar"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        //_StartColor ("Start Color", Color) = (0,1,0,1)
        //_EndColor ("End Color", Color) = (1,0,0,1)
        _Amount ("Health amount", Range(0,1)) = 1
        //_StartThreshold ("Start Threshold", Range(0,1)) = 0.8
        //_EndThreshold ("End Threshold", Range(0,1)) = 0.2
        _PulseSpeed ("Pulse Speed", Range(1,5)) = 1
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
            float4 _StartColor;
            float4 _EndColor;
            float _Amount;
            float _StartThreshold;
            float _EndThreshold;
            float _PulseSpeed;


            struct MeshData
            {
                float4 vertex : POSITION;
                float2 uv0 : TEXCOORD0;
            };

            struct Interpolators
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0; 
            };

            Interpolators vert (MeshData v)
            {
                Interpolators o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv0;
                return o;
            }

            float InverseLerp(float a, float b, float v){
                return (v-a)/(b-a);
            }

            float4 frag (Interpolators i) : SV_Target
            {
                //float t = saturate(InverseLerp(_EndThreshold, _StartThreshold, _Amount));
                //float4 healthColor = lerp(_EndColor, _StartColor, t);

                //float offset = _EndThreshold;
                //float fillAmount = _Amount * 1 - i.uv.x + offset;
                //return fillAmount.xxxx;

                //float4 finalColor = (healthColor * fillAmount);
                //clip(finalColor);
                //return finalColor;

                float2 lookupColor = float2(_Amount, i.uv.y);
                float4 col = tex2D(_MainTex, lookupColor);
                float fillAmount = _Amount * 1 - i.uv.x;
                clip(fillAmount);

                if(_Amount < 0.2)
                {
                    float pulse = cos(_Time.y * TAU * 0.1 * _PulseSpeed) * 0.5 + 0.55;
                    return pulse * col;
                }
                return col;
            }
            ENDCG
        }
    }
}
