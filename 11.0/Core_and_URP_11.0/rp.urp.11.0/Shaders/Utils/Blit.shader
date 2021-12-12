Shader "Hidden/Universal Render Pipeline/Blit"
{
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "Blit"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex      FullscreenVert
            #pragma fragment    Fragment

            #pragma multi_compile_fragment _ _LINEAR_TO_SRGB_CONVERSION

            // xr 才启用
            #pragma multi_compile _ _USE_DRAW_PROCEDURAL

            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Fullscreen.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"


            TEXTURE2D_X(_SourceTex); // 等于: TEXTURE2D(_SourceTex)
            SAMPLER(sampler_SourceTex);


            half4 Fragment(Varyings input) : SV_Target
            {
                /*UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);    tpr */

                //常规的采样
                half4 col = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_SourceTex, input.uv);


            // 如果启用了 linear 工作流, 且 backbuffer 不支持 "自动执行 linear->sRGB 转换",
            // 那么当把 backbuffer 定位一次 Blit() 的目的地时, 
            // 需要启用此 keyword, 并在 shader 中手动执行 "linear->sRGB" 转换;
             #ifdef _LINEAR_TO_SRGB_CONVERSION
             
                col = LinearToSRGB(col);
             #endif

                return col;
            }
            ENDHLSL
        }
    }
}
