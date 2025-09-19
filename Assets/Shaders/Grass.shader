/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

Shader "Hidden/SB/Grass"
{
    Properties
    {
        //_MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags 
        {            
            //"Queue" = "Geometry-1"
            //"RenderPipeline" = "UniversalPipeline"
        }
        //LOD 100

        Pass
        {            
            //Blend SrcAlpha OneMinusSrcAlpha
            //ZWrite Off
            HLSLPROGRAM
            #pragma vertex VertexProgram
            #pragma fragment FragmentProgram
            #include "GrassImpl.hlsl"
            ENDHLSL
        }
    }
}
