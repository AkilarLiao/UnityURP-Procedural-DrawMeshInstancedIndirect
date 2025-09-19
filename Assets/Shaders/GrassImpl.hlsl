/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

#ifndef GRASS_IMPL_INCLUDED
#define GRASS_IMPL_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct VertexInput
{
    float4 positionOS : POSITION;
    float2 texcoordOS : TEXCOORD0;
};

struct VertexOutput
{
    //real2 baseUV : TEXCOORD0;
    float4 positionCS : SV_POSITION;
};

//TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
CBUFFER_START(UnityPerMaterial)
//float4 _MainTex_ST;
CBUFFER_END

VertexOutput VertexProgram(VertexInput input)
{
    VertexOutput output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    //output.baseUV = TRANSFORM_TEX(input.texcoordOS, _MainTex);
    return output;
}

half4 FragmentProgram(VertexOutput input) : SV_Target
{   
    //return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.baseUV);
    return half4(1.0, 0.0, 0.0, 1.0);
}

#endif //GRASS_IMPL_INCLUDED