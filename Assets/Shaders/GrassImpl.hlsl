/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

#ifndef GRASS_IMPL_INCLUDED
#define GRASS_IMPL_INCLUDED

//#define _SPECULAR_COLOR
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
};

TEXTURE2D(_ColorTexture); SAMPLER(sampler_ColorTexture);

CBUFFER_START(UnityPerMaterial)
float _FadeStartSquareDistance;
half2 _WindDirection;
half4 _SpecularColor;
half4 _ShadingParams;
float4 _InteractorCollisionSphere;
float4 _FilterResultRT_TexelSize;
CBUFFER_END

half4 GetAlbedoColor(in half2 worldUV, in float3 instancePositoin, in half affectWeight)
{
    half4 albedoColor;
    albedoColor.rgb = SAMPLE_TEXTURE2D_LOD(_ColorTexture, sampler_ColorTexture,
        worldUV * _ShadingParams.y, 0).rgb;

    float3 viewPositionWS = TransformWorldToView(instancePositoin);
    float viewSquareDistance = dot(viewPositionWS, viewPositionWS);
    float clampViewSquareDistance = clamp(viewSquareDistance, _FadeStartSquareDistance, _MaxViewSquareDistance);

    albedoColor.a = 1.0 - (clampViewSquareDistance - _FadeStartSquareDistance) /
        (_MaxViewSquareDistance - _FadeStartSquareDistance);
    albedoColor.a *= affectWeight;

    return albedoColor;
}

VertexOutput VertexProgram(VertexInput input, uint instanceID : SV_InstanceID)
{
    VertexOutput output;

    half2 worldUV;
    float3 instancePositoin, positionWS;
    half affectWeight;
    half3 normalWS;
    GetInstanceTransform(instanceID, _FilterResultRT_TexelSize, input.positionOS, input.normalOS,
        _WindDirection, _InteractorCollisionSphere, _ShadingParams.w, _ShadingParams.x,
        worldUV, instancePositoin, positionWS, affectWeight, normalWS);

    output.positionCS = TransformWorldToHClip(positionWS);
    output.positionSS = ComputeScreenPos(output.positionCS);

    half4 albedoColor = GetAlbedoColor(worldUV, instancePositoin, affectWeight);

    output.resultColor = CalculateBlinnPhong(positionWS, normalWS, albedoColor);

    return output;
}

half4 FragmentProgram(VertexOutput input) : SV_Target
{
    half3 sceneColor = SampleSceneColor(
        input.positionSS.xy / input.positionSS.w);    
    
    half4 resultColor = input.resultColor;

    half applyWeight = 1.0 - pow(1.0 - resultColor.a, _ShadingParams.z);

    return half4(lerp(sceneColor, resultColor.rgb, applyWeight), 1.0);
}

#endif //GRASS_IMPL_INCLUDED