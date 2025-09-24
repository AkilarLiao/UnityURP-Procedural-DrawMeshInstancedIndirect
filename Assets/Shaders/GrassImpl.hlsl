/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

#ifndef GRASS_IMPL_INCLUDED
#define GRASS_IMPL_INCLUDED

//#define _SPECULAR_COLOR

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

#include "GrassInstance.hlsl"

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
    half4 applyLightResult          : TEXCOORD0;    
    float4 positionSS               : TEXCOORD1;
};

TEXTURE2D(_ColorTexture); SAMPLER(sampler_ColorTexture);

TEXTURE2D(_FilterResultRT); SAMPLER(sampler_FilterResultRT);

CBUFFER_START(UnityPerMaterial)
float _FadeStartSquareDistance;
half2 _WindDirection;
half _FadeGroundPow;
half4 _SpecularColor;
half _WindNormalWeight;
//xyz:position
//w:radius
float4 _InteractorCollisionSphere;
half _InteractorAffectWeight;
float4 _FilterResultRT_TexelSize;
CBUFFER_END

void CalculateNormal(in VertexInput input, in float sinValue, in float cosValue, in half2 windOffest,
    out half3 normalWS)
{   
    normalWS = half3(
        input.normalOS.x * cosValue - input.normalOS.z * sinValue,
        input.normalOS.y,
        input.normalOS.x * sinValue + input.normalOS.z * cosValue);
    
    half2 scaleWindOffest = windOffest * _WindNormalWeight * input.positionOS.y;
    normalWS += half3(scaleWindOffest.x, 0.0, scaleWindOffest.y);
    normalWS = normalize(normalWS);
}

void ApplyInteractorOffest(in float3 instancePositoin, in half applyWeight, inout float3 positionWS)
{   
    float3 delta = instancePositoin - _InteractorCollisionSphere.xyz;
    float squareDistance = dot(delta, delta);
    float maxCheckRadius = _MaxInstanceSize + _InteractorCollisionSphere.w;
    float maxCheckSquareRadius = maxCheckRadius * maxCheckRadius;
    if (squareDistance > maxCheckSquareRadius)
        return;

    // linear falloff (1 at center, 0 at radius) — can be changed to smoothstep/pow/exp
    float falloff = saturate(1.0 - (squareDistance / maxCheckSquareRadius));

    // Direction: from interactor to instance (avoid divide-by-zero)
    float3 dir = (squareDistance > 1e-5) ? normalize(delta) : float3(0.0, 0.0, 0.0);

    // Use interactor.w as base strength (multiply global scale here if needed)
    float magnitude = falloff * _InteractorCollisionSphere.w * _InteractorAffectWeight;

    // Storage format: float4(dx, dy, dz, strength)
    float3 displacement = dir * magnitude;

    //positionWS.xz += applyWeight * displacement.xz;
    positionWS.xz += applyWeight * displacement.xz;
}

half4 SampleFilterResultRT(in uint destInstanceID)
{
    uint columnIndex = destInstanceID % _FilterResultRT_TexelSize.z;
    uint rowIndex = destInstanceID / _FilterResultRT_TexelSize.z;

    half2 uv = float2((float)columnIndex / (float)_FilterResultRT_TexelSize.z,
        (float)rowIndex / (float)_FilterResultRT_TexelSize.w);

    return SAMPLE_TEXTURE2D_LOD(_FilterResultRT,
        sampler_FilterResultRT, uv, 0);
}

void ConverRatioPosition(inout float2 position2D)
{
    position2D.x = _WorldMinMax.x + position2D.x * (_WorldMinMax.z - _WorldMinMax.x);
    position2D.y = _WorldMinMax.y + position2D.y * (_WorldMinMax.w - _WorldMinMax.y);
}

void GetGrassInstanceData(in uint instanceID, out float2 position2D, out half2 sizeFactor, out half yawSin, out half yawCos,
    out half wind)
{
    uint destInstanceID = instanceID * 2;
    half4 data = SampleFilterResultRT(destInstanceID);
    position2D = data.xy;
    ConverRatioPosition(position2D);
    sizeFactor = data.zw;

    data = SampleFilterResultRT(++destInstanceID);
    yawSin = data.x;
    yawCos = data.y;
    wind = data.z;
}

VertexOutput VertexProgram(VertexInput input, uint instanceID : SV_InstanceID)
{
    VertexOutput output;

    float2 position2D;
    half2 sizeFactor;
    half yawSin, yawCos, wind;

    GetGrassInstanceData(instanceID, position2D, sizeFactor, yawSin, yawCos, wind);
    
    float3 instancePositoin = float3(position2D.x, 0.0, position2D.y);

    float3 viewPositionWS = TransformWorldToView(instancePositoin);
    float viewSquareDistance = dot(viewPositionWS, viewPositionWS);

    float clampViewSquareDistance = clamp(viewSquareDistance, _FadeStartSquareDistance, _MaxViewSquareDistance);

    half4 albedoColor;

    albedoColor.rgb = SAMPLE_TEXTURE2D_LOD(_ColorTexture, sampler_ColorTexture,
        position2D, 0).rgb;

    albedoColor.a = 1.0 - (clampViewSquareDistance - _FadeStartSquareDistance) /
        (_MaxViewSquareDistance - _FadeStartSquareDistance);    

    albedoColor.a *= input.positionOS.y;
    
    float3 destPositionOS = float3(
        input.positionOS.x * sizeFactor.x,
        input.positionOS.y * sizeFactor.y,
        input.positionOS.z * sizeFactor.x);

    destPositionOS = float3(
        destPositionOS.x * yawCos - destPositionOS.z * yawSin,
        destPositionOS.y,
        destPositionOS.x * yawSin + destPositionOS.z * yawCos);
    
    float3 positionWS = instancePositoin + destPositionOS;   

    float2 windOffest = _WindDirection * wind * input.positionOS.y;
    positionWS.xz += windOffest;

    ApplyInteractorOffest(instancePositoin, step(0.5, input.positionOS.y), positionWS);
    
    half3 normalWS;
    CalculateNormal(input, yawSin, yawCos, windOffest, normalWS);

    output.positionCS = TransformWorldToHClip(positionWS);
    output.positionSS = ComputeScreenPos(output.positionCS);
    
    output.applyLightResult = CalculateBlinnPhong(positionWS, normalWS, albedoColor);

    return output;
}

half4 FragmentProgram(VertexOutput input) : SV_Target
{
    half3 sceneColor = SampleSceneColor(
        input.positionSS.xy / input.positionSS.w);    
    
    half4 applyLightResult = input.applyLightResult;

    half applyWeight = 1.0 - pow(1.0 - applyLightResult.a, 5.0);

    return half4(lerp(sceneColor, applyLightResult.rgb, applyWeight), 1.0);
}

#endif //GRASS_IMPL_INCLUDED