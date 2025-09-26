/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

Shader "Hidden/SB/Grass"
{
    Properties
    {   
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-1"
        }

        Pass
        {   
            HLSLPROGRAM
            #pragma vertex VertexProgram
            #pragma fragment FragmentProgram

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT            
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_local _ PROCESS_BILLBOARD
            #include "GrassImpl.hlsl"
            ENDHLSL
        }        
        
    }
}
