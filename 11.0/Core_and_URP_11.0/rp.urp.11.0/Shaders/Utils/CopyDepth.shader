Shader "Hidden/Universal Render Pipeline/CopyDepth"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}

        Pass
        {
            Name "CopyDepth"
            ZTest Always 
            ZWrite On 
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex      vert
            #pragma fragment    frag

            // 每像素采样次数: 1,2,4,8
            #pragma multi_compile _ _DEPTH_MSAA_2 _DEPTH_MSAA_4 _DEPTH_MSAA_8
            
            // xr 才启用
            #pragma multi_compile _ _USE_DRAW_PROCEDURAL

            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/CopyDepthPass.hlsl"

            ENDHLSL
        }
    }
}
