/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

#ifndef LIGHT_FUNCTIONS_INCLUDED
#define LIGHT_FUNCTIONS_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

void InitializeInputData(
    in float3 positionWS,
    in float3 normalWS,
    in float4 positionCS,
    out InputData inputData)
{
    inputData = (InputData)0;    
    inputData.positionWS = positionWS;    
    inputData.normalWS = NormalizeNormalPerPixel(normalWS);
    inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceNormalizeViewDir(positionWS));
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
    inputData.fogCoord = InitializeInputDataFog(float4(positionWS, 1.0), ComputeFogFactor(positionCS.z));
    inputData.vertexLighting = half3(0, 0, 0);
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, SampleSHVertex(normalWS), normalWS);
    inputData.shadowMask = half4(1, 1, 1, 1);
    //還不是screen座標…
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(ComputeScreenPos(positionCS));
}

void GetSurfaceData(in half4 albedoColor, out SurfaceData surfaceData)
{
    surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedoColor.rgb;
    surfaceData.alpha = albedoColor.a;
    surfaceData.occlusion = 1.0;
}

half4 CalculateBlinnPhong(in float3 positionWS, in half3 normalWS, in float4 positionCS, in half4 albedoColor)
{
     InputData inputData;
     InitializeInputData(positionWS, normalWS, positionCS, inputData);
     SurfaceData surfaceData;    
     GetSurfaceData(albedoColor, surfaceData);
     half4 applyLightResult = UniversalFragmentBlinnPhong(inputData, surfaceData);
     applyLightResult.rgb = MixFog(applyLightResult.rgb, inputData.fogCoord);
     return applyLightResult;
}
#endif //LIGHT_FUNCTIONS_INCLUDED