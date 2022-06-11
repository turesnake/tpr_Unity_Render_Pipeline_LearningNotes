// CoC: circle of confusion, 模糊圈;
//  点光源经过镜头, 然后打到 画布平面上, 在画布上形成的一个 模糊的圆形的圈;
//  如果这个圈是一个清晰的点, 就说明整个系统对焦成功了(在这个画布上); 否则, 就会出现焦散, 出现 dof


Shader "Hidden/Universal Render Pipeline/BokehDepthOfField"
{
    HLSLINCLUDE
        #pragma exclude_renderers gles
        #pragma multi_compile_local_fragment _ _USE_FAST_SRGB_LINEAR_CONVERSION

        // XR;  目前看来确实只有 加载了 xr/vr package 的程序, 才能启用此 keyword
        #pragma multi_compile _ _USE_DRAW_PROCEDURAL

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        // Do not change this without changing PostProcessPass.PrepareBokehKernel()
        #define SAMPLE_COUNT            42


        // Toggle this to reduce flickering - note that it will reduce overall bokeh energy and add
        // a small cost to the pre-filtering pass
        // 切换此选项以减少闪烁; 请注意，这将减少总体bokeh能量，并为预过滤过程增加一小部分成本
        #define COC_LUMA_WEIGHTING      0

        TEXTURE2D_X(_SourceTex);
        TEXTURE2D_X(_DofTexture);
        TEXTURE2D_X(_FullCoCTexture);

        half4 _SourceSize;  // {width, height, 1.0f / width, 1.0f / height}   (m_Descriptor 的)
        //half4 _HalfSourceSize; // 没有用到... 可删... 
        half4 _DownSampleScaleFactor; // {1/2f, 1/2f, 2f, 2f} downSample = 2;
        half4 _CoCParams;    // { focusDistance, maxCoC, maxRadius, rcpAspect }
        half4 _BokehKernel[SAMPLE_COUNT];
        half4 _BokehConstants;    // {uvMargin, uvMargin * 2.0f, 0, 0 }

        #define FocusDist       _CoCParams.x  // 焦点深度值
        #define MaxCoC          _CoCParams.y  // 
        #define MaxRadius       _CoCParams.z  //
        //#define RcpAspect       _CoCParams.w  // 大概就是 h/w;  没被用到...


        // ----------------------------------------------------------------------------------------------------- // 
        // -1-

        // 向 tgt 写入 coc 值; [0,1]; 原始值为 [-1,1]
        half FragCoC(Varyings input) : SV_Target
        {
            //UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = UnityStereoTransformScreenSpaceTex(input.uv); // 直接等于 input.uv
            float depth = LOAD_TEXTURE2D_X(_CameraDepthTexture, _SourceSize.xy * uv).x;
            float linearEyeDepth = LinearEyeDepth(depth, _ZBufferParams);

            half coc = (1.0 - FocusDist / linearEyeDepth) * MaxCoC; // 完全对焦时为 0, 近时为负, 远时为正;

            
            half nearCoC = clamp(coc, -1.0, 0.0); // 约束到 [-1,0];
            half farCoC = saturate(coc);          // 约束到 [0,1]

            return saturate((farCoC + nearCoC + 1.0) * 0.5); // 从 [-1,1] 映射到 [0,1]
        }

        // ----------------------------------------------------------------------------------------------------- // 
        // -2-

        half4 FragPrefilter(Varyings input) : SV_Target
        {
            //UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.uv); // 直接等于 input.uv

        #if SHADER_TARGET >= 45 && defined(PLATFORM_SUPPORT_GATHER)
            // 高消耗, 建议彻底不使用; 暂时不看

            // Sample source colors
            half4 cr = GATHER_RED_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, uv);
            half4 cg = GATHER_GREEN_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, uv);
            half4 cb = GATHER_BLUE_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, uv);

            half3 c0 = half3(cr.x, cg.x, cb.x);
            half3 c1 = half3(cr.y, cg.y, cb.y);
            half3 c2 = half3(cr.z, cg.z, cb.z);
            half3 c3 = half3(cr.w, cg.w, cb.w);

            // Sample CoCs
            half4 cocs = GATHER_TEXTURE2D_X(_FullCoCTexture, sampler_LinearClamp, uv) * 2.0 - 1.0;
            half coc0 = cocs.x;
            half coc1 = cocs.y;
            half coc2 = cocs.z;
            half coc3 = cocs.w;

        #else
            // 低消耗

            // _SourceSize.zwz: {1/w, 1/h, 1/w}; // 第三个值仅用来快速获得 负值;
            float3 duv = _SourceSize.zwz * float3(0.5, 0.5, -0.5);
            float2 uv0 = uv - duv.xy; // { -1/w, -1/h } // 差值为 两像素间距
            float2 uv1 = uv - duv.zy; // { +1/w, -1/h }
            float2 uv2 = uv + duv.zy; // { -1/w, +1/h }
            float2 uv3 = uv + duv.xy; // { +1/w, +1/h }

            // Sample source colors
            // 对角线的4个像素 颜色值;
            half3 c0 = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, uv0).xyz;
            half3 c1 = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, uv1).xyz;
            half3 c2 = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, uv2).xyz;
            half3 c3 = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, uv3).xyz;

            // Sample CoCs
            // 采样这对应的 4 个像素点的 coc 值;
            half coc0 = SAMPLE_TEXTURE2D_X(_FullCoCTexture, sampler_LinearClamp, uv0).x * 2.0 - 1.0; // 还原回 [-1,1] 区间;
            half coc1 = SAMPLE_TEXTURE2D_X(_FullCoCTexture, sampler_LinearClamp, uv1).x * 2.0 - 1.0;
            half coc2 = SAMPLE_TEXTURE2D_X(_FullCoCTexture, sampler_LinearClamp, uv2).x * 2.0 - 1.0;
            half coc3 = SAMPLE_TEXTURE2D_X(_FullCoCTexture, sampler_LinearClamp, uv3).x * 2.0 - 1.0;

        #endif


        #if COC_LUMA_WEIGHTING // 不为 0 时

            // Apply CoC and luma weights to reduce bleeding and flickering  减少溢出和闪烁
            half w0 = abs(coc0) / (Max3(c0.x, c0.y, c0.z) + 1.0);
            half w1 = abs(coc1) / (Max3(c1.x, c1.y, c1.z) + 1.0);
            half w2 = abs(coc2) / (Max3(c2.x, c2.y, c2.z) + 1.0);
            half w3 = abs(coc3) / (Max3(c3.x, c3.y, c3.z) + 1.0);

            // Weighted average of the color samples
            half3 avg = c0 * w0 + c1 * w1 + c2 * w2 + c3 * w3;
            avg /= max(w0 + w1 + w2 + w3, 1e-5);

        #else // 默认状态, 为 0 时;
            half3 avg = (c0 + c1 + c2 + c3) / 4.0; // 颜色均值
        #endif


            // Select the largest CoC value
            half cocMin = min(coc0, Min3(coc1, coc2, coc3));
            half cocMax = max(coc0, Max3(coc1, coc2, coc3));
            half coc = (-cocMin > cocMax ? cocMin : cocMax) * MaxRadius; // 选绝对值最大的那个, 且保留符号

            // Premultiply CoC
            // 颜色均值, 乘以一个 受 max coc 影响的 [0,1] 值; 也就是适当变暗;
            // 检查 frame debugger 即可看到, 非常明显;
            avg *= smoothstep( 0, _SourceSize.w*2.0, abs(coc) );

            /*  tpr
            #if defined(UNITY_COLORSPACE_GAMMA)
                avg = GetSRGBToLinear(avg);
            #endif
            */

            return half4(avg, coc);
        }

        

        // ----------------------------------------------------------------------------------------------------- // 
        // -3-

        void Accumulate(
                half4 samp0, 
                float2 uv, 
                half4 disp, 
                inout half4 farAcc, 
                inout half4 nearAcc
        ){

            half4 samp = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, uv + disp.wy);

            // 上一pass 的 a值:  coc 值, 存储为 [0,1], 原值为 [-1,1]; 

            // Compare CoC of the current sample and the center sample and select smaller one
            half farCoC = max(min(samp0.a, samp.a), 0.0);

            // Compare the CoC to the sample distance & add a small margin to smooth out
            half farWeight = saturate( (farCoC - disp.z + _BokehConstants.y) / _BokehConstants.y );
            half nearWeight = saturate( (-samp.a - disp.z + _BokehConstants.y) / _BokehConstants.y );

            // Cut influence from focused areas because they're darkened by CoC premultiplying. This is only needed for near field
            // 削减聚焦区域的影响，因为它们被CoC预乘变暗。这仅适用于近场
            nearWeight *= step(_BokehConstants.x, -samp.a);

            // Accumulation
            farAcc += half4(samp.rgb, 1.0h) * farWeight;
            nearAcc += half4(samp.rgb, 1.0h) * nearWeight;
        }


        half4 FragBlur(Varyings input) : SV_Target
        {
            //UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.uv); // 直接等于 input.uv

            // 采样 "_PingTexture"; 就是上一个 pass 最终写入的图;
            half4 samp0 = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, uv);

            half4 farAcc = 0.0;  // Background: far field bokeh
            half4 nearAcc = 0.0; // Foreground: near field bokeh

            // Center sample isn't in the kernel array, accumulate it separately
            Accumulate(samp0, uv, 0.0, farAcc, nearAcc);

            UNITY_LOOP
            for (int si = 0; si < SAMPLE_COUNT; si++)
            {
                Accumulate(samp0, uv, _BokehKernel[si], farAcc, nearAcc);
            }

            // Get the weighted average
            farAcc.rgb /= farAcc.a + (farAcc.a == 0.0);     // Zero-div guard
            nearAcc.rgb /= nearAcc.a + (nearAcc.a == 0.0);

            // Normalize the total of the weights for the near field
            nearAcc.a *= PI / (SAMPLE_COUNT + 1);

            // Alpha premultiplying
            half alpha = saturate(nearAcc.a);
            half3 rgb = lerp(farAcc.rgb, nearAcc.rgb, alpha);

            return half4(rgb, alpha);
        }


        // ----------------------------------------------------------------------------------------------------- // 
        // -4-

        half4 FragPostBlur(Varyings input) : SV_Target
        {
            //UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.uv); // 直接等于 input.uv

            // 9-tap tent filter with 4 bilinear samples
            // 猜测: 用 4 次 双线性插值, 来获得 3x3 tent 滤波器
            float4 duv = _SourceSize.zwzw * _DownSampleScaleFactor.zwzw * float4(0.5, 0.5, -0.5, 0);
            half4 acc;
            acc  = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, uv - duv.xy);
            acc += SAMPLE_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, uv - duv.zy);
            acc += SAMPLE_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, uv + duv.zy);
            acc += SAMPLE_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, uv + duv.xy);
            return acc * 0.25;
        }

        // ----------------------------------------------------------------------------------------------------- // 
        // -5-

        half4 FragComposite(Varyings input) : SV_Target
        {
            //UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.uv); // 直接等于 input.uv

            half4 dof = SAMPLE_TEXTURE2D_X(_DofTexture, sampler_LinearClamp, uv);
            half coc = SAMPLE_TEXTURE2D_X(_FullCoCTexture, sampler_LinearClamp, uv).r;
            coc = (coc - 0.5) * 2.0 * MaxRadius;

            // Convert CoC to far field alpha value
            float ffa = smoothstep(_SourceSize.w * 2.0, _SourceSize.w * 4.0, coc);

            half4 color = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, uv);

            /* tpr
            #if defined(UNITY_COLORSPACE_GAMMA)
                color = GetSRGBToLinear(color);
            #endif
            */ 

            half alpha = Max3(dof.r, dof.g, dof.b);
            color = lerp(color, half4(dof.rgb, alpha), ffa + dof.a - ffa * dof.a);

            /*  tpr
            #if defined(UNITY_COLORSPACE_GAMMA)
                color = GetLinearToSRGB(color);
            #endif
            */

            return color;
        }


    ENDHLSL



    // ##################################################################################################### //
    // 按顺序 依次执行如下的每一个 pass; 共 5 个;

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "Bokeh Depth Of Field CoC"

            HLSLPROGRAM
                #pragma vertex      FullscreenVert
                #pragma fragment    FragCoC
                #pragma target 4.5
            ENDHLSL
        }

        Pass
        {
            Name "Bokeh Depth Of Field Prefilter"

            HLSLPROGRAM
                #pragma vertex      FullscreenVert
                #pragma fragment    FragPrefilter
                #pragma target 4.5
            ENDHLSL
        }

        Pass
        {
            Name "Bokeh Depth Of Field Blur"

            HLSLPROGRAM
                #pragma vertex      FullscreenVert
                #pragma fragment    FragBlur
                #pragma target 4.5
            ENDHLSL
        }

        Pass
        {
            Name "Bokeh Depth Of Field Post Blur"

            HLSLPROGRAM
                #pragma vertex      FullscreenVert
                #pragma fragment    FragPostBlur
                #pragma target 4.5
            ENDHLSL
        }

        Pass
        {
            Name "Bokeh Depth Of Field Composite"

            HLSLPROGRAM
                #pragma vertex      FullscreenVert
                #pragma fragment    FragComposite
                #pragma target 4.5
            ENDHLSL
        }
    }



    // 仅将 #pragma target 设置为 3.5
    // 为方便阅读, 暂注释掉

    /*

    // SM3.5 fallbacks - needed because of the use of Gather
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "Bokeh Depth Of Field CoC"

            HLSLPROGRAM
                #pragma vertex      FullscreenVert
                #pragma fragment    FragCoC
                #pragma target 3.5
            ENDHLSL
        }

        Pass
        {
            Name "Bokeh Depth Of Field Prefilter"

            HLSLPROGRAM
                #pragma vertex FullscreenVert
                #pragma fragment FragPrefilter
                #pragma target 3.5
            ENDHLSL
        }

        Pass
        {
            Name "Bokeh Depth Of Field Blur"

            HLSLPROGRAM
                #pragma vertex FullscreenVert
                #pragma fragment FragBlur
                #pragma target 3.5
            ENDHLSL
        }

        Pass
        {
            Name "Bokeh Depth Of Field Post Blur"

            HLSLPROGRAM
                #pragma vertex FullscreenVert
                #pragma fragment FragPostBlur
                #pragma target 3.5
            ENDHLSL
        }

        Pass
        {
            Name "Bokeh Depth Of Field Composite"

            HLSLPROGRAM
                #pragma vertex FullscreenVert
                #pragma fragment FragComposite
                #pragma target 3.5
            ENDHLSL
        }
    }

    */ 

}
