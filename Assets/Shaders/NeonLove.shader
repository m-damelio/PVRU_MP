Shader "Custom/NeonHeart"
{
    Properties
    {
        _Speed ("Speed", Float) = -0.5
        _Length ("Length", Float) = 0.25
        _Scale ("Scale", Float) = 0.012
        _Intensity ("Intensity", Float) = 1.3
        _Radius ("Radius", Float) = 0.012
        _Thickness ("Thickness", Float) = 0.0035
        _Color1 ("Color One", Color) = (1.0, 0.05, 0.3, 1.0)
        _Color2 ("Color Two", Color) = (0.1, 0.4, 1.0, 1.0)
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            //Blend One One 
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED
            #pragma multi_compile _ UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            #define POINT_COUNT 8
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv: TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            float _Speed;
            float _Length;
            float _Scale;
            float _Intensity;
            float _Radius;
            float _Thickness;
            float4 _Color1;
            float4 _Color2;
            
            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }
            
            // Signed distance to a quadratic bezier
            float sdBezier(float2 pos, float2 A, float2 B, float2 C)
            {    
                float2 a = B - A;
                float2 b = A - 2.0 * B + C;
                float2 c = a * 2.0;
                float2 d = A - pos;

                float kk = 1.0 / dot(b, b);
                float kx = kk * dot(a, b);
                float ky = kk * (2.0 * dot(a, a) + dot(d, b)) / 3.0;
                float kz = kk * dot(d, a);      

                float res = 0.0;

                float p = ky - kx * kx;
                float p3 = p * p * p;
                float q = kx * (2.0 * kx * kx - 3.0 * ky) + kz;
                float h = q * q + 4.0 * p3;
                
                if(h >= 0.0)
                { 
                    h = sqrt(h);
                    float2 x = (float2(h, -h) - q) / 2.0;
                    float2 uv = sign(x) * pow(abs(x), float2(1.0/3.0, 1.0/3.0));
                    float t = uv.x + uv.y - kx;
                    t = clamp(t, 0.0, 1.0);

                    // 1 root
                    float2 qos = d + (c + b * t) * t;
                    res = length(qos);
                }
                else
                {
                    float z = sqrt(-p);
                    float v = acos(q / (p * z * 2.0)) / 3.0;
                    float m = cos(v);
                    float n = sin(v) * 1.732050808;
                    float3 t = float3(m + m, -n - m, n - m) * z - kx;
                    t = clamp(t, 0.0, 1.0);

                    // 3 roots
                    float2 qos = d + (c + b * t.x) * t.x;
                    float dis = dot(qos, qos);
                    
                    res = dis;

                    qos = d + (c + b * t.y) * t.y;
                    dis = dot(qos, qos);
                    res = min(res, dis);

                    qos = d + (c + b * t.z) * t.z;
                    dis = dot(qos, qos);
                    res = min(res, dis);

                    res = sqrt(res);
                }
                
                return res;
            }

            // Heart curve equation
            float2 getHeartPosition(float t)
            {
                return float2(16.0 * sin(t) * sin(t) * sin(t),
                            -(13.0 * cos(t) - 5.0 * cos(2.0 * t)
                            - 2.0 * cos(3.0 * t) - cos(4.0 * t)));
            }

            float getGlow(float dist, float radius, float intensity)
            {
                return pow(radius / dist, intensity);
            }

            float getSegment(float t, float2 pos, float offset)
            {
                float2 points[POINT_COUNT];
                
                // Fill points array
                for(int i = 0; i < POINT_COUNT; i++)
                {
                    points[i] = getHeartPosition(offset + float(i) * _Length + frac(_Speed * t) * 6.28);
                }
                
                float2 c = (points[0] + points[1]) / 2.0;
                float2 c_prev;
                float light = 0.0;
                const float eps = 1e-10;
                
                for(int i = 0; i < POINT_COUNT - 1; i++)
                {
                    c_prev = c;
                    c = (points[i] + points[i + 1]) / 2.0;
                    
                    // Distance from bezier segment
                    float d = sdBezier(pos, _Scale * c_prev, _Scale * points[i], _Scale * c);
                    // Distance from endpoint (except from first point)
                    float e = i > 0 ? distance(pos, _Scale * c_prev) : 1000.0;
                    
                    // Convert the distance to light and accumulate
                    light += 1.0 / max(d - _Thickness, eps);
                    // Convert the endpoint as well and subtract
                    light -= 1.0 / max(e - _Thickness, eps);
                }
                
                return max(0.0, light);
            }
            
            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.uv;
                float widthHeightRatio = _ScreenParams.x / _ScreenParams.y;
                float2 centre = float2(0.5, 0.5);
                float2 pos = centre - uv;
                pos.y /= widthHeightRatio;
                // Shift upwards to centre heart
                pos.y += 0.03;
                
                float t = _Time.y;
                
                // Get raw glow contributions
                float dist1 = getSegment(t, pos, 0.0);
                float glow1 = getGlow(dist1, _Radius, _Intensity);
                float dist2 = getSegment(t, pos, 3.4);
                float glow2 = getGlow(dist2, _Radius, _Intensity);
                float glowSum = glow1 + glow2;
                float alpha = step(0.001, glowSum);

                float glowThreshold = 0.1;
                if(glowSum < glowThreshold) {
                    discard;
                }

                //Farbe builden
                float3 col = glow1 * _Color1.xyz + glow2 * _Color2.xyz;
                // Tone mapping
                col = 1 - exp(-col);
                
                // Gamma correction
                col = pow(col, float3(0.4545, 0.4545, 0.4545));

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}