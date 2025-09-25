/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>
#ifndef GRASS_TRANSFORM_UTILITY_INCLUDED
#define GRASS_TRANSFORM_UTILITY_INCLUDED
#include "GrassParams.hlsl"

TEXTURE2D(_FilterResultRT); SAMPLER(sampler_FilterResultRT);

half4 SampleFilterResultRT(in uint destInstanceID, in float4 filterResultRTTexelSize)
{
    uint columnIndex = destInstanceID % filterResultRTTexelSize.z;
    uint rowIndex = destInstanceID / filterResultRTTexelSize.z;

    half2 uv = float2((float)columnIndex / (float)filterResultRTTexelSize.z,
        (float)rowIndex / (float)filterResultRTTexelSize.w);

    return SAMPLE_TEXTURE2D_LOD(_FilterResultRT,
        sampler_FilterResultRT, uv, 0);
}

void ConverRatioPosition(in half2 worldUV, out float2 position2D)
{
    position2D.x = _WorldMinMax.x + worldUV.x * (_WorldMinMax.z - _WorldMinMax.x);
    position2D.y = _WorldMinMax.y + worldUV.y * (_WorldMinMax.w - _WorldMinMax.y);
}

void GetGrassInstanceData(in uint instanceID, in float4 filterResultRTTexelSize,
    out half2 worldUV, out float2 position2D, out half2 sizeFactor, out half yawSin,
    out half yawCos, out half wind)
{
    uint destInstanceID = instanceID * 2;
    half4 data = SampleFilterResultRT(destInstanceID, filterResultRTTexelSize);
    worldUV = data.xy;
    ConverRatioPosition(worldUV, position2D);
    sizeFactor = data.zw;

    data = SampleFilterResultRT(++destInstanceID, filterResultRTTexelSize);
    yawSin = data.x;
    yawCos = data.y;
    wind = data.z;
}

void ApplyInteractorOffest(in float3 instancePositoin, in float4 collisionSphere,
    in half applyWeight, inout float3 positionWS)
{
    float3 delta = instancePositoin - collisionSphere.xyz;
    float squareDistance = dot(delta, delta);
    float maxCheckRadius = _MaxInstanceSize + collisionSphere.w;
    float maxCheckSquareRadius = maxCheckRadius * maxCheckRadius;
    if (squareDistance > maxCheckSquareRadius)
        return;

    // linear falloff (1 at center, 0 at radius) ¡X can be changed to smoothstep/pow/exp
    float falloff = saturate(1.0 - (squareDistance / maxCheckSquareRadius));

    // Direction: from interactor to instance (avoid divide-by-zero)
    float3 dir = (squareDistance > 1e-5) ? normalize(delta) : float3(0.0, 0.0, 0.0);

    // Use interactor.w as base strength (multiply global scale here if needed)
    float magnitude = falloff * collisionSphere.w;// *_InteractorAffectWeight;

    // Storage format: float4(dx, dy, dz, strength)
    float3 displacement = dir * magnitude;

    positionWS.xz += applyWeight * displacement.xz;
}

void CalculateNormal(
    in half3 normalOS,
    in float sinValue,
    in float cosValue,
    in half windNormalWeight,
    in half2 windOffest,
    out half3 normalWS)
{
    normalWS = half3(
        normalOS.x * cosValue - normalOS.z * sinValue,
        normalOS.y,
        normalOS.x * sinValue + normalOS.z * cosValue);

    half2 scaleWindOffest = windOffest * windNormalWeight;
    normalWS += half3(scaleWindOffest.x, 0.0, scaleWindOffest.y);
    normalWS = normalize(normalWS);
}

void GetInstanceTransform(in uint instanceID, in float4 filterResultRTTexelSize, in float4 positionOS,
    in half3 normalOS, in half2 windDirection, in float4 interactorCollisionSphere,
    in half interactorAffectWeight, in half windNormalWeight,
    out half2 worldUV,
    out float3 instancePositoin,
    out float3 positionWS,
    out half affectWeight,
    out half3 normalWS)
{   
    float2 position2D;
    half2 sizeFactor;
    half yawSin, yawCos, wind;

    GetGrassInstanceData(instanceID, filterResultRTTexelSize, worldUV, position2D, sizeFactor, yawSin, yawCos, wind);

    instancePositoin = float3(position2D.x, 0.0, position2D.y);

    float3 viewPositionWS = TransformWorldToView(instancePositoin);
    float viewSquareDistance = dot(viewPositionWS, viewPositionWS);

    float3 destPositionOS = float3(
        positionOS.x * sizeFactor.x,
        positionOS.y * sizeFactor.y,
        positionOS.z * sizeFactor.x);

    destPositionOS = float3(
        destPositionOS.x * yawCos - destPositionOS.z * yawSin,
        destPositionOS.y,
        destPositionOS.x * yawSin + destPositionOS.z * yawCos);

    positionWS = instancePositoin + destPositionOS;

    affectWeight = positionOS.y;

    float2 windOffest = windDirection * wind * affectWeight;
    positionWS.xz += windOffest;

    ApplyInteractorOffest(instancePositoin, interactorCollisionSphere,
        step(0.5, affectWeight) * interactorAffectWeight, positionWS);
    
    CalculateNormal(normalOS, yawSin, yawCos, windNormalWeight * affectWeight, windOffest, normalWS);
}

#endif //GRASS_INSTANCE_INCLUDED