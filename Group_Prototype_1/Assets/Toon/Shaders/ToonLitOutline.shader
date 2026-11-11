// Two-tone cartoon lighting for URP, plus a dark outline around the object.
Shader "Toon/Lit Outline"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)

        [Header(Toon lighting)]
        _ShadeColor ("Shade Color", Color) = (0.45, 0.5, 0.7, 1)
        _RampThreshold ("Light / Shade Split", Range(0, 1)) = 0.5
        _RampSmooth ("Edge Softness", Range(0.001, 0.5)) = 0.03
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.35

        [Header(Rim light)]
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Size", Range(0, 1)) = 0.7
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.15

        [Header(Texture mapping)]
        [Toggle] _Triplanar ("Triplanar (ignore UVs)", Float) = 0
        _TriplanarScale ("Triplanar Tiles Per Unit", Float) = 0.25

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.1, 0.08, 0.12, 1)
        _OutlineWidth ("Outline Width", Range(0, 10)) = 2

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ToonVert
            #pragma fragment ToonFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "ToonCommon.hlsl"
            ENDHLSL
        }

        Pass
        {
            // Second colour pass: the mesh is pushed outwards and only
            // its back faces are drawn, which leaves a dark line around
            // the object.
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "ToonCommon.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "ToonCommon.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #pragma multi_compile_instancing

            #include "ToonCommon.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthNormalsFrag

            #pragma multi_compile_instancing

            #include "ToonCommon.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
