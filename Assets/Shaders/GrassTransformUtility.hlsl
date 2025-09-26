/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>
#ifndef GRASS_TRANSFORM_UTILITY_INCLUDED
#define GRASS_TRANSFORM_UTILITY_INCLUDED
#include "GrassParams.hlsl"

//triangle flicker
static const float sc_farAwayFlickerRatio = 0.00225;

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
    out half2 worldUV, out float2 position2D, out half2 sizeFactor, out half wind,
#ifdef PROCESS_BILLBOARD
    out half3 normalWS
#else
    out half yawSin,
    out half yawCos
#endif
)
{   
    uint destInstanceID = instanceID * 2;
    half4 data = SampleFilterResultRT(destInstanceID, filterResultRTTexelSize);
    worldUV = data.xy;
    ConverRatioPosition(worldUV, position2D);
    sizeFactor = data.zw;

    data = SampleFilterResultRT(++destInstanceID, filterResultRTTexelSize);
    wind = data.x;
#ifdef PROCESS_BILLBOARD
    normalWS = data.yzw;
#else
    yawSin = data.y;
    yawCos = data.z;
#endif //PROCESS_BILLBOARD
//#ifdef PROCESS_BILLBOARD
//    data = SampleFilterResultRT(++destInstanceID, filterResultRTTexelSize);
//    normalWS = data.xyz;
//#endif
}

void ApplyInteractorOffest(in float3 instancePosition, in float4 collisionSphere,
    in half applyWeight, inout float3 positionWS)
{
    float3 delta = instancePosition - collisionSphere.xyz;
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

void GetBillboardWorldPosition(in float3 positionOS,
    in float3 instancePosition,
    in half2 sizeFactor,    
    in float viewDistance,
    out float3 positionWS)
{
    half3 cameraTransformForwardWS = -UNITY_MATRIX_V[2].xyz;
    half3 cameraTransformUpWS = UNITY_MATRIX_V[1].xyz;
    half3 cameraTransformRightWS = UNITY_MATRIX_V[0].xyz;

    //Expand Billboard (billboard Left+right)
    float3 billboardPositionOS = positionOS.x * cameraTransformRightWS * sizeFactor.x;
    //Expand Billboard (billboard Up)
    billboardPositionOS += positionOS.y * cameraTransformUpWS * sizeFactor.y;

    float3 bendDir = cameraTransformForwardWS;

    //make grass shorter when bending, looks better
    //bendDir.xz *= 0.5;

    //prevent grass become too long if camera forward 
    //is / near parallel to ground
    bendDir.y = min(-0.5, bendDir.y);

    //camera distance scale (make grass width larger if grass is
    //far away to camera, to hide smaller than pixel size triangle flicker)
    //viewWS = _WorldSpaceCameraPos - instancePosition;
    //viewWSLength = length(viewWS);

    billboardPositionOS += cameraTransformRightWS * positionOS.x
        * max(0, viewDistance * sc_farAwayFlickerRatio);

    positionWS = billboardPositionOS + instancePosition;
}

void GetRotateWorldPosition(in float3 positionOS, in float3 instancePosition,
    in half2 sizeFactor, in half yawSin, in half yawCos,
    out float3 positionWS)
{
    float3 destPositionOS = float3(
        positionOS.x * sizeFactor.x,
        positionOS.y * sizeFactor.y,
        positionOS.z * sizeFactor.x);

    destPositionOS = float3(
        destPositionOS.x * yawCos - destPositionOS.z * yawSin,
        destPositionOS.y,
        destPositionOS.x * yawSin + destPositionOS.z * yawCos);

    positionWS = instancePosition + destPositionOS;
}

void GetInstanceTransform(in uint instanceID, in float4 filterResultRTTexelSize, in float4 positionOS,
    in half3 normalOS, in half2 windDirection, in float4 interactorCollisionSphere,
    in half interactorAffectWeight, in half windNormalWeight,
    out half2 worldUV,
    out float3 positionWS,
    out half affectWeight,
    out half3 normalWS,    
    out float viewDistance)
{   
    float2 position2D;
    half2 sizeFactor;
    half wind;
#ifdef PROCESS_BILLBOARD
    GetGrassInstanceData(instanceID, filterResultRTTexelSize, worldUV, position2D, sizeFactor, wind,
        normalWS);    
#else
    half yawSin, yawCos;
    GetGrassInstanceData(instanceID, filterResultRTTexelSize, worldUV, position2D, sizeFactor, wind,
        yawSin,
        yawCos);
#endif //PROCESS_BILLBOARD

    float3 instancePosition = float3(position2D.x, 0.0, position2D.y);
    viewDistance = length(_WorldSpaceCameraPos - instancePosition);

    affectWeight = positionOS.y;
    
#if !defined(PROCESS_BILLBOARD)
    GetRotateWorldPosition(positionOS.xyz, instancePosition, sizeFactor, yawSin, yawCos, positionWS);
#else
    GetBillboardWorldPosition(positionOS.xyz, instancePosition, sizeFactor, viewDistance, positionWS);
#endif

    float2 windOffest = windDirection * wind * affectWeight;
    positionWS.xz += windOffest;

    ApplyInteractorOffest(instancePosition, interactorCollisionSphere,
        step(0.5, affectWeight) * interactorAffectWeight, positionWS);
    
#if !defined(PROCESS_BILLBOARD)
    CalculateNormal(normalOS, yawSin, yawCos, windNormalWeight * affectWeight, windOffest, normalWS);
#endif
}

#endif //GRASS_INSTANCE_INCLUDED