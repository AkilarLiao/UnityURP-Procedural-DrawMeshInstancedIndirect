/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

#ifndef GRASS_IMPL_INCLUDED
#define GRASS_IMPL_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "GrassTransformUtility.hlsl"
#include "LightFunctions.hlsl"

struct VertexInput
{
    float4 positionOS       : POSITION;
    half3 normalOS          : NORMAL;
    float2 texcoordOS       : TEXCOORD0;
};

struct VertexOutput
{   
    float4 positionCS               : SV_POSITION;
    half4 resultColor               : TEXCOORD0;
    float4 positionSS               : TEXCOORD1;
    half3 normalWS                  : TEXCOORD2;
};

static const half sc_alphaCutoff = 0.5;

TEXTURE2D(_ColorTexture); SAMPLER(sampler_ColorTexture);

float _FadeStartSquareDistance;
half2 _WindDirection;
//x:windNormalWeight, y:colorTextureTileScale, z:fadePow, w: interactorAffectWeight
half4 _ShadingParams;
float4 _InteractorCollisionSphere;
float4 _FilterResultRT_TexelSize;

half4 GetAlbedoColor(in half2 worldUV, in float viewDistance, in half affectWeight)
{
    half4 albedoColor;
    albedoColor.rgb = SAMPLE_TEXTURE2D_LOD(_ColorTexture, sampler_ColorTexture,
        worldUV * _ShadingParams.y, 0).rgb;

    float viewSquareDistance = viewDistance * viewDistance;
    float clampViewSquareDistance = clamp(viewSquareDistance, _FadeStartSquareDistance,
        _MaxViewSquareDistance);
    
    albedoColor.a = 1.0 - (clampViewSquareDistance - _FadeStartSquareDistance) /
        (_MaxViewSquareDistance - _FadeStartSquareDistance);

    albedoColor.a *= affectWeight;

    return albedoColor;
}

VertexOutput VertexProgram(VertexInput input, uint instanceID : SV_InstanceID)
{
    VertexOutput output;

    half2 worldUV;
    //float3 instancePosition, positionWS;
    float3 positionWS;
    half affectWeight;
    half3 normalWS;
    float viewDistance;
    GetInstanceTransform(instanceID, _FilterResultRT_TexelSize, input.positionOS, input.normalOS,
        _WindDirection, _InteractorCollisionSphere, _ShadingParams.w, _ShadingParams.x,
        worldUV, positionWS, affectWeight, normalWS, viewDistance);

    output.positionCS = TransformWorldToHClip(positionWS);

    output.positionSS = ComputeScreenPos(output.positionCS);

    float viewSquareDistance = viewDistance * viewDistance;
    float clampViewSquareDistance = clamp(viewSquareDistance, _FadeStartSquareDistance,
        _MaxViewSquareDistance);
    half farFadeWeight = 1.0 - (clampViewSquareDistance - _FadeStartSquareDistance) /
        (_MaxViewSquareDistance - _FadeStartSquareDistance);

    affectWeight *= farFadeWeight;

    half4 albedoColor = half4(SAMPLE_TEXTURE2D_LOD(_ColorTexture, sampler_ColorTexture,
        worldUV * _ShadingParams.y, 0).rgb, affectWeight);
    output.resultColor = CalculateBlinnPhong(positionWS, normalWS, output.positionCS,         
        albedoColor);

    output.normalWS = normalWS;
    return output;
}

half4 FragmentProgram(VertexOutput input) : SV_Target
{
    //return half4(input.normalWS * 0.5 + 0.5, 1.0);

    half4 resultColor = input.resultColor;
    half applyWeight = 1.0 - pow(saturate(1.0 - resultColor.a), _ShadingParams.z);
    half3 sceneColor = SampleSceneColor(
        input.positionSS.xy / input.positionSS.w);
    return half4(lerp(sceneColor, resultColor.rgb, applyWeight), 1.0);
}

#endif //GRASS_IMPL_INCLUDED