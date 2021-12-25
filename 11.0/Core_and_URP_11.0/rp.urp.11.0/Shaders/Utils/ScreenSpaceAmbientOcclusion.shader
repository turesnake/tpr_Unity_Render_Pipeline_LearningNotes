Shader "Hidden/Universal Render Pipeline/ScreenSpaceAmbientOcclusion"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


        struct Attributes
        {
            float4 positionHCS   : POSITION;// 实际上是 posOS
            float2 uv           : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };


        struct Varyings
        {
            float4  positionCS  : SV_POSITION;
            float2  uv          : TEXCOORD0;
            /*UNITY_VERTEX_OUTPUT_STEREO   tpr  */
        };


        // -- 绘制 full screen quad
        Varyings VertDefault(Attributes input)//   读完__
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            /*UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);   tpr */

            // Note: The pass is setup with a mesh already in CS; Therefore, we can just output vertex position
            // 因为是 full screen quad, 所以直接把 posOS 当成 posCS 来用;
            output.positionCS = float4(input.positionHCS.xyz, 1.0);

            // flip-uv-y
            #if UNITY_UV_STARTS_AT_TOP
                output.positionCS.y *= -1;
            #endif

            output.uv = input.uv;

            // Add a small epsilon to avoid artifacts when reconstructing the normals
            // 避免 0 值;
            output.uv += 1.0e-6;

            return output;
        }

    ENDHLSL




    SubShader
    {
        Tags{ 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off 
        ZWrite Off 
        ZTest Always

        // ------------------------------------------------------------------
        // Depth only passes
        // ------------------------------------------------------------------

        // --------------------------------------------------:
        // 0 - Occlusion estimation with CameraDepthTexture
        Pass
        {
            Name "SSAO_Occlusion"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex      VertDefault
                #pragma fragment    SSAO

                // 对应 SSAO inspector: "Source"; normal 信息 从哪来; (是自己计算, 还是直接使用预先计算好的)
                #pragma multi_compile_local _SOURCE_DEPTH _SOURCE_DEPTH_NORMALS _SOURCE_GBUFFER
                // 如果 normal 信息需要现在计算, 那么计算何种质量的;
                #pragma multi_compile_local _RECONSTRUCT_NORMAL_LOW _RECONSTRUCT_NORMAL_MEDIUM _RECONSTRUCT_NORMAL_HIGH
                // 是否为 正交透视
                #pragma multi_compile_local _ _ORTHOGRAPHIC

                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SSAO.hlsl"
            ENDHLSL
        }

        // --------------------------------------------------:
        // 1 - Horizontal Blur
        Pass
        {
            Name "SSAO_HorizontalBlur"

            HLSLPROGRAM
                #pragma vertex      VertDefault
                #pragma fragment    HorizontalBlur

                #define BLUR_SAMPLE_CENTER_NORMAL
                // 是否为 正交透视
                #pragma multi_compile_local _ _ORTHOGRAPHIC
                // 对应 SSAO inspector: "Source"; normal 信息 从哪来; (是自己计算, 还是直接使用预先计算好的)
                #pragma multi_compile_local _SOURCE_DEPTH _SOURCE_DEPTH_NORMALS _SOURCE_GBUFFER

                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SSAO.hlsl"
            ENDHLSL
        }

        // --------------------------------------------------:
        // 2 - Vertical Blur
        Pass
        {
            Name "SSAO_VerticalBlur"

            HLSLPROGRAM
                #pragma vertex      VertDefault
                #pragma fragment    VerticalBlur

                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SSAO.hlsl"
            ENDHLSL
        }

        // --------------------------------------------------:
        // 3 - Final Blur
        Pass
        {
            Name "SSAO_FinalBlur"

            HLSLPROGRAM
                #pragma vertex      VertDefault
                #pragma fragment    FinalBlur

                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SSAO.hlsl"
            ENDHLSL
        }
    }
}
