Shader "Custom/WireframeShader"
{
    Properties
    {
        _WireColor ("Wire Color", Color) = (0.0, 1.0, 1.0, 1.0)
        _BaseColor ("Base Color", Color) = (0.0, 0.0, 0.0, 0.5) //Semi transparent
        _WireThickness ("Wire Thickness", Range(0, 0.5)) = 0.01
        _WireSmoothness ("Wire Smoothness", Range(0,1)) = 0.02

        [HDR] _EmissionColor ("Emission Color", Color) = (0.0, 1.0, 1.0, 1.0)
        _EmissionIntensity ("Emission Intensity", Range(0,5)) = 2.0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Culling", Float) = 2 //Back face culling by defaul
        [Enum(Off, 0, On, 1)] _ZWrite ("Depth Write", Float) = 1

    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalRenderPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull [_Cull]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR; //Barycentric coordinates are stored here
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 barycentric : TEXCOORD0; //Passes barycentric coords to fragment shader
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4 _WireColor;
            half4 _BaseColor;
            half _WireThickness;
            half _WireSmoothness;
            half4 _EmissionColor;
            half _EmissionIntensity;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                //Transform vertex position from obejct space to clip space
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);

                //Pass barycentric coordinates (from vertex colors) to the fragment shader
                OUT.barycentric = IN.color;

                return OUT;
            }

            //Fragment shader
            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                //Use screen space derivatives to calculate edge distance
                //Keeps wireframe width consistent regardless of distance
                float3 d = fwidth(IN.barycentric.xyz);

                //Calculate min distance to any triangles edge
                float3 a3 = smoothstep(float3(0.0, 0.0, 0.0), d*(1.0 + _WireSmoothness), IN.barycentric.xyz);
                float min_dist = min(min(a3.x, a3.y), a3.z);

                //Determine wireframe intensity
                float wire = 1.0 - smoothstep(_WireThickness, _WireThickness + 0.01, min_dist);

                //Mix base color and wireframe color based on wire intensit<
                //base color is used for faces of the mesh
                half4 finalColor = lerp(_BaseColor, _WireColor, wire);

                //Apply emission for glow, mainly on wireframe
                half3 emission = _EmissionColor.rgb * _EmissionIntensity * wire;
                finalColor.rgb += emission;

                //Ensure fnal alpha is set correctly for transparency
                finalColor.a = max(finalColor.a, wire);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
