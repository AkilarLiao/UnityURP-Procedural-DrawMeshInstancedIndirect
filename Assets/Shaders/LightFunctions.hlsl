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
    out InputData inputData)
{
    inputData = (InputData)0;    
    inputData.positionWS = positionWS;    
    inputData.normalWS = NormalizeNormalPerPixel(normalWS);
    inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceNormalizeViewDir(positionWS));
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);

#if defined(_FOG_FRAGMENT)
    half fogFactor = 0;
#else
    half fogFactor = ComputeFogFactor(output.positionCS.z);
#endif

    inputData.fogCoord = InitializeInputDataFog(float4(positionWS, 1.0), fogFactor);
    inputData.vertexLighting = half3(0, 0, 0);
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, SampleSHVertex(normalWS), normalWS);
    inputData.shadowMask = half4(1, 1, 1, 1);
}

void GetSurfaceData(in half4 albedoColor, out SurfaceData surfaceData)
{
    surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedoColor.rgb;
    surfaceData.alpha = albedoColor.a;
    surfaceData.occlusion = 1.0;
}

half4 CalculateBlinnPhong(in float3 positionWS, in half3 normalWS, in half4 albedoColor)
{
     InputData inputData;
     InitializeInputData(positionWS, normalWS, inputData);
     SurfaceData surfaceData;    
     GetSurfaceData(albedoColor, surfaceData);
     return UniversalFragmentBlinnPhong(inputData, surfaceData);
}

// InputData inputData;
//     InitializeInputData(
//         positionWS, 
//         output.normalWS,
//         inputData);

//     SurfaceData surfaceData;    
//     GetSurfaceData(albedoColor, surfaceData);

//     output.applyLightResult = half4(UniversalFragmentBlinnPhong(inputData, surfaceData).rgb, albedoColor.a);


#endif //LIGHT_FUNCTIONS_INCLUDED