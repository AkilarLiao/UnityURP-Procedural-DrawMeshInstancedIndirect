/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

#ifndef GRASS_IMPL_INCLUDED
#define GRASS_IMPL_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "WeightMapUtility.hlsl"
#include "SimplexNoise.hlsl"

static const float sc_widthWorldScale = 100.0;
static const float sc_heightWorldScale = 200.0;
static const float sc_noiseScale = 500.0;

struct VertexInput
{
    float4 positionOS : POSITION;
    float2 texcoordOS : TEXCOORD0;
};

struct VertexOutput
{
    //real2 baseUV : TEXCOORD0;
    float4 positionCS   : SV_POSITION;    
    half displayWeight  : TEXCOORD0;
    float4 positionSS   : TEXCOORD1;
};

//TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
CBUFFER_START(UnityPerMaterial)
//float4 _MainTex_ST;
StructuredBuffer<float3> _VisibleInstancesTransformBuffer;
float _FadeStartSquareDistance;

//x:baseSize
//y:minSizeOffest
//z:maxSizeOffest
float3 _WidthSizeInfo;
float3 _HeightSizeInfo;
CBUFFER_END

float2 GetDestBaseSize(in float2 position2D, in float2 sourceSize)
{
//#if !defined(PROCESS_SIZE_WITH_WEIGHT_MAP)
    return sourceSize;
//#endif
    /*
    half4 weightColor = SAMPLE_TEXTURE2D_LOD(_WeightMap,
        sampler_WeightMap, GetGroundUV(position2D),0);

    half destWeight = max(weightColor.r, weightColor.g);
    destWeight = max(destWeight, weightColor.b);
    destWeight = max(destWeight, weightColor.a);

    return sourceSize * destWeight;
    */
}

float2 GetDestSize(in float2 position2D//, 
    //x:baseSize
    //y:minSizeOffest
    //z:maxSizeOffest
    // in float3 widthSizeInfo,
    // in float3 heightSizeInfo)
    )
{   
    float2 baseSize = GetDestBaseSize(position2D,
        float2(_WidthSizeInfo.x, _HeightSizeInfo.x));

    float sizeOffest = sc_noiseScale *
        GetSimplexNoise(position2D * sc_widthWorldScale);
    float destWidth = clamp(baseSize.x + sizeOffest,
        baseSize.x - _WidthSizeInfo.y,
        baseSize.x + _WidthSizeInfo.z);

    sizeOffest = sc_noiseScale *
        GetSimplexNoise(position2D * sc_heightWorldScale);
    float destHeight = clamp(baseSize.y + sizeOffest,
        baseSize.y - _HeightSizeInfo.y,
        baseSize.y + _HeightSizeInfo.z);

    return float2(destWidth, destHeight);
}

VertexOutput VertexProgram(VertexInput input, uint instanceID : SV_InstanceID)
{
    VertexOutput output;    

    float3 transform = _VisibleInstancesTransformBuffer[instanceID];
    float3 instancePositoin = float3(transform.x, 0.0, transform.y);

    float3 viewPositionWS = TransformWorldToView(instancePositoin);
    float viewSquareDistance = dot(viewPositionWS, viewPositionWS);

    float clampViewSquareDistance = clamp(viewSquareDistance, _FadeStartSquareDistance, _MaxViewSquareDistance);
    output.displayWeight = 1.0 - (clampViewSquareDistance - _FadeStartSquareDistance) / 
        (_MaxViewSquareDistance - _FadeStartSquareDistance);

    float2 sizeFactor = GetDestSize(instancePositoin.xz);

    // 用 sizeFactor 對局部頂點做 XZ 方向縮放
    float3 scaledOS = float3(
        input.positionOS.x * sizeFactor.x,
        input.positionOS.y * sizeFactor.y,
        input.positionOS.z * sizeFactor.x);
    
    float3 positionWS = instancePositoin + scaledOS;

    output.positionCS = TransformWorldToHClip(positionWS);
    output.positionSS = ComputeScreenPos(output.positionCS);
    
    return output;
}

half4 FragmentProgram(VertexOutput input) : SV_Target
{   
    half3 sceneColor = SampleSceneColor(
        input.positionSS.xy / input.positionSS.w);
    
    return half4(lerp(sceneColor, half3(1.0, 0.0, 0.0),  input.displayWeight), 1.0);
}

#endif //GRASS_IMPL_INCLUDED