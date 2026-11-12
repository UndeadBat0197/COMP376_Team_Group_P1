#ifndef TOON_COMMON_INCLUDED
#define TOON_COMMON_INCLUDED

// Shared code for the Toon/Lit and Toon/Lit Outline shaders (URP).

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

// Every pass uses the same material buffer, which keeps the shader
// compatible with the SRP Batcher.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _ShadeColor;
    half4 _RimColor;
    half4 _OutlineColor;
    half _RampThreshold;
    half _RampSmooth;
    half _AmbientStrength;
    half _RimPower;
    half _RimStrength;
    half _Triplanar;
    half _TriplanarScale;
    half _OutlineWidth;
CBUFFER_END

struct ToonAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ToonVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS   : TEXCOORD2;
    float3 triPos     : TEXCOORD3;
    float3 triNormal  : TEXCOORD4;
    float  fogFactor  : TEXCOORD5;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// Length of each axis of the object, so a stretched cube still gets
// evenly sized texture tiles.
float3 ToonObjectScale()
{
    float4x4 m = GetObjectToWorldMatrix();
    return float3(
        length(float3(m._m00, m._m10, m._m20)),
        length(float3(m._m01, m._m11, m._m21)),
        length(float3(m._m02, m._m12, m._m22)));
}

ToonVaryings ToonVert(ToonAttributes input)
{
    ToonVaryings output = (ToonVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

    output.positionWS = positionWS;
    output.positionCS = TransformWorldToHClip(positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

    float3 scale = max(ToonObjectScale(), float3(0.0001, 0.0001, 0.0001));
    output.triPos = input.positionOS.xyz * scale * _TriplanarScale;
    output.triNormal = input.normalOS / scale;

    output.fogFactor = ComputeFogFactor(output.positionCS.z);

    return output;
}

half4 ToonSampleBase(ToonVaryings input)
{
    // Normal UV mapping.
    half4 uvColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

    // Triplanar mapping: project the texture along the object's three
    // axes. It sticks to the object and tiles at the same size on
    // every face, even on stretched cubes.
    float3 n = abs(normalize(input.triNormal));
    float3 w = n * n * n * n;
    w = w / max(w.x + w.y + w.z, 0.0001);

    float2 tiling = _BaseMap_ST.xy;

    half4 x = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.triPos.zy * tiling);
    half4 y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.triPos.xz * tiling);
    half4 z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.triPos.xy * tiling);

    half4 triColor = x * w.x + y * w.y + z * w.z;

    return lerp(uvColor, triColor, step(0.5, _Triplanar));
}

half4 ToonFrag(ToonVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 albedo = ToonSampleBase(input) * _BaseColor;

    float3 normalWS = normalize(input.normalWS);
    float3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));

    // Main light with shadows.
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    float4 shadowCoord = float4(GetNormalizedScreenSpaceUV(input.positionCS), 0.0, 1.0);
#else
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif

    Light mainLight = GetMainLight(shadowCoord);

    // Two-tone lighting: everything is either "lit" or "shade",
    // with a narrow soft edge between them.
    half halfLambert = dot(normalWS, mainLight.direction) * 0.5 + 0.5;

    half band = smoothstep(
        _RampThreshold - _RampSmooth,
        _RampThreshold + _RampSmooth,
        halfLambert);

    half shadow = smoothstep(0.35, 0.65,
        mainLight.shadowAttenuation * mainLight.distanceAttenuation);

    band *= shadow;

    // The shade side is tinted by the scene's ambient colour; the lit
    // side is just the light colour, so bright textures do not blow
    // out to white.
    half3 ambient = SampleSH(normalWS);
    half3 shade = _ShadeColor.rgb * lerp(half3(1.0, 1.0, 1.0), ambient * 1.6, _AmbientStrength);
    half3 lighting = lerp(shade, min(mainLight.color, half3(1.1, 1.1, 1.1)), band);

    half3 color = albedo.rgb * lighting;

    // Hard rim light on the lit side.
    half rim = 1.0 - saturate(dot(normalWS, viewWS));
    rim = smoothstep(_RimPower - 0.05, _RimPower + 0.05, rim);
    color += rim * band * _RimStrength * _RimColor.rgb;

    color = MixFog(color, input.fogFactor);

    return half4(color, 1.0);
}

// ---------------------------------------------------------------
// Outline (inverted hull)
// ---------------------------------------------------------------

struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float  fogFactor  : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

OutlineVaryings OutlineVert(ToonAttributes input)
{
    OutlineVaryings output = (OutlineVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

    // Grow with distance so the line keeps roughly the same
    // thickness on screen.
    float dist = clamp(distance(GetCameraPositionWS(), positionWS), 2.0, 80.0);
    positionWS += normalWS * (_OutlineWidth * 0.0012 * dist);

    output.positionCS = TransformWorldToHClip(positionWS);
    output.fogFactor = ComputeFogFactor(output.positionCS.z);

    return output;
}

half4 OutlineFrag(OutlineVaryings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half3 color = MixFog(_OutlineColor.rgb, input.fogFactor);
    return half4(color, 1.0);
}

// ---------------------------------------------------------------
// Shadow caster / depth passes
// ---------------------------------------------------------------

float3 _LightDirection;
float3 _LightPosition;

struct SimpleVaryings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS   : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

SimpleVaryings ShadowVert(ToonAttributes input)
{
    SimpleVaryings output = (SimpleVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(
        ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif

    output.positionCS = positionCS;
    output.normalWS = normalWS;
    return output;
}

SimpleVaryings DepthVert(ToonAttributes input)
{
    SimpleVaryings output = (SimpleVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half4 DepthFrag(SimpleVaryings input) : SV_Target
{
    return 0;
}

half4 DepthNormalsFrag(SimpleVaryings input) : SV_Target
{
    return half4(normalize(input.normalWS), 0.0);
}

#endif
