/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

#ifndef GRASS_IMPL_INCLUDED
#define GRASS_IMPL_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "GrassInstance.hlsl"

struct VertexInput
{
    float4 positionOS : POSITION;
    half3 normalOS : NORMAL;
    float2 texcoordOS : TEXCOORD0;
};

struct VertexOutput
{   
    float4 positionCS       : SV_POSITION;
    half displayWeight      : TEXCOORD0;
    half3 applyLittingColor : TEXCOORD1;
    float4 positionSS       : TEXCOORD2;
};

TEXTURE2D(_ColorTexture); SAMPLER(sampler_ColorTexture);


CBUFFER_START(UnityPerMaterial)
StructuredBuffer<GrassInstanceData> _VisibleInstanceBuffer;
float _FadeStartSquareDistance;
half2 _WindDirection;
half _FadeGroundPow;
CBUFFER_END

void CalculateNormal(in VertexInput input, in float sinValue, in float cosValue, in half2 windOffest,
    out half3 normalWS)
{   
    normalWS = half3(
        input.normalOS.x * cosValue - input.normalOS.z * sinValue,
        input.normalOS.y,
        input.normalOS.x * sinValue + input.normalOS.z * cosValue);

    half windNormalFactor = 1.0;
    half2 scaleWindOffest = windOffest * windNormalFactor;
    normalWS += half3(scaleWindOffest.x, 0.0, scaleWindOffest.y);
    normalWS = normalize(normalWS);
}

VertexOutput VertexProgram(VertexInput input, uint instanceID : SV_InstanceID)
{
    VertexOutput output;    

    GrassInstanceData grassInstanceData = _VisibleInstanceBuffer[instanceID];
    
    float2 position2D = grassInstanceData.position2D;

    float3 instancePositoin = float3(position2D.x, 0.0, position2D.y);

    float3 viewPositionWS = TransformWorldToView(instancePositoin);
    float viewSquareDistance = dot(viewPositionWS, viewPositionWS);

    float clampViewSquareDistance = clamp(viewSquareDistance, _FadeStartSquareDistance, _MaxViewSquareDistance);
    output.displayWeight = 1.0 - (clampViewSquareDistance - _FadeStartSquareDistance) /
        (_MaxViewSquareDistance - _FadeStartSquareDistance);
    output.displayWeight *= input.positionOS.y;

    float2 sizeFactor = grassInstanceData.sizeFactor;
    
    float3 destPositionOS = float3(
        input.positionOS.x * sizeFactor.x,
        input.positionOS.y * sizeFactor.y,
        input.positionOS.z * sizeFactor.x);
    
    float sinValue = grassInstanceData.yawSin;
    float cosValue = grassInstanceData.yawCos;

    destPositionOS = float3(
        destPositionOS.x * cosValue - destPositionOS.z * sinValue,
        destPositionOS.y,
        destPositionOS.x * sinValue + destPositionOS.z * cosValue
        );
    
    float3 positionWS = instancePositoin + destPositionOS;

    float2 windOffest = _WindDirection * grassInstanceData.wind * input.positionOS.y;
    positionWS.xz += windOffest;

    half3 normalWS;
    CalculateNormal(input, sinValue, cosValue, windOffest, normalWS);
    
    output.applyLittingColor = normalWS * 0.5 + 0.5;
    output.positionCS = TransformWorldToHClip(positionWS);
    output.positionSS = ComputeScreenPos(output.positionCS);
    return output;
}

half4 FragmentProgram(VertexOutput input) : SV_Target
{   
    half3 sceneColor = SampleSceneColor(
        input.positionSS.xy / input.positionSS.w);    
    
    return half4(lerp(sceneColor, input.applyLittingColor, input.displayWeight), 1.0);
}

#endif //GRASS_IMPL_INCLUDED