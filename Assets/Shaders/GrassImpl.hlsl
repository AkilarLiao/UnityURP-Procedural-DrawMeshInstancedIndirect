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
    //real2 baseUV : TEXCOORD0;
    float4 positionCS       : SV_POSITION;    
    half displayWeight      : TEXCOORD0;
    half3 applyLittingColor : TEXCOORD1;
    float4 positionSS       : TEXCOORD2;
};

TEXTURE2D(_ColorTexture); SAMPLER(sampler_ColorTexture);


CBUFFER_START(UnityPerMaterial)
//float4 _MainTex_ST;
StructuredBuffer<GrassInstanceData> _VisibleInstanceBuffer;
float _FadeStartSquareDistance;

//x:baseSize
//y:minSizeOffest
//z:maxSizeOffest
//float3 _WidthSizeInfo;
//float3 _HeightSizeInfo;
CBUFFER_END

VertexOutput VertexProgram(VertexInput input, uint instanceID : SV_InstanceID)
{
    VertexOutput output;    

    GrassInstanceData grassInstanceData = _VisibleInstanceBuffer[instanceID];

    //float3 transform = _VisibleInstanceBuffer[instanceID];

    float3 transform = float3(grassInstanceData.position2D, grassInstanceData.yawRadian);

    float3 instancePositoin = float3(transform.x, 0.0, transform.y);

    float3 viewPositionWS = TransformWorldToView(instancePositoin);
    float viewSquareDistance = dot(viewPositionWS, viewPositionWS);

    float clampViewSquareDistance = clamp(viewSquareDistance, _FadeStartSquareDistance, _MaxViewSquareDistance);
    output.displayWeight = 1.0 - (clampViewSquareDistance - _FadeStartSquareDistance) / 
        (_MaxViewSquareDistance - _FadeStartSquareDistance);

    //float2 sizeFactor = GetDestSize(instancePositoin.xz);
    float2 sizeFactor = grassInstanceData.sizeFactor;

    // 用 sizeFactor 對局部頂點做 XZ 方向縮放
    float3 destPositionOS = float3(
        input.positionOS.x * sizeFactor.x,
        input.positionOS.y * sizeFactor.y,
        input.positionOS.z * sizeFactor.x);

    // 建立繞 Y 軸的旋轉
    float s = sin(transform.z);
    float c = cos(transform.z);
    destPositionOS = float3(
        destPositionOS.x * c - destPositionOS.z * s,
        destPositionOS.y,
        destPositionOS.x * s + destPositionOS.z * c
        );    

    float3 positionWS = instancePositoin + destPositionOS;

    // 旋轉 normal
    half3 normalOS = input.normalOS;
    half3 normalWS = float3(
        normalOS.x * c - normalOS.z * s,
        normalOS.y,
        normalOS.x * s + normalOS.z * c
        );

    output.applyLittingColor = normalWS * 0.5 + 0.5;

    /*
    // 原本 normal
    float3 normalOS = input.normalOS;

    // 風力方向（xz）
    float2 windDir = normalize(float2(fx, fz));

    // 風力強度
    float windForce = 2.0;

    // 把 normal 稍微「傾斜」一點
    float3 windNormal = normalOS + float3(windDir.x, 0, windDir.y) * windForce * factor;

    // factor 可依據頂點高度（比如頂端影響最大，底部不動）
    float factor = saturate(input.positionOS.y / 草的高度);

    // 最後正規化
    windNormal = normalize(windNormal);
    */

    /*void ApplyWindOffsetToNormal(
        in float localOSYalue,
        in float3 cameraTransformRightWS,
        in float wind,
        inout float3 normalOS)
    {
        // 讓 normal 稍微傾斜一點
        // 0.5是讓法線偏移不要太大，你可以自行調整
        float windNormalFactor = 0.5;

        // normal 主要是Y向上，加一點 wind 方向
        normalOS += cameraTransformRightWS * wind * localOSYalue * windNormalFactor;

        // 最後正規化
        normalOS = normalize(normalOS);
    }*/


    output.positionCS = TransformWorldToHClip(positionWS);
    output.positionSS = ComputeScreenPos(output.positionCS);
    return output;
}

half4 FragmentProgram(VertexOutput input) : SV_Target
{   
    half3 sceneColor = SampleSceneColor(
        input.positionSS.xy / input.positionSS.w);
    
    //return half4(lerp(sceneColor, half3(1.0, 0.0, 0.0),  input.displayWeight), 1.0);
    return half4(lerp(sceneColor, input.applyLittingColor,  input.displayWeight), 1.0);

    //half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
    //half3 VertexLighting(float3 positionWS, half3 normalWS)
    /*if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_VERTEX_LIGHTING))
    {
        lightingColor += lightingData.vertexLightingColor;
    }
    lightingColor *= albedo;*/
}

#endif //GRASS_IMPL_INCLUDED