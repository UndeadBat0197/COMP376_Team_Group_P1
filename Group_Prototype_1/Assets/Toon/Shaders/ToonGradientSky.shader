// Simple three-colour gradient sky. Assign a material using this
// shader in Window > Rendering > Lighting > Environment > Skybox Material.
Shader "Toon/Gradient Sky"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.25, 0.55, 0.95, 1)
        _HorizonColor ("Horizon Color", Color) = (0.75, 0.9, 1, 1)
        _BottomColor ("Bottom Color", Color) = (0.95, 0.85, 0.7, 1)
        _Exponent ("Gradient Sharpness", Range(0.2, 4)) = 0.8
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half _Exponent;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float height = normalize(input.direction).y;

                half up = pow(saturate(height), _Exponent);
                half down = pow(saturate(-height), _Exponent);

                half3 color = lerp(_HorizonColor.rgb, _TopColor.rgb, up);
                color = lerp(color, _BottomColor.rgb, down);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
