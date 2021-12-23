using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal.Internal
{
    /*
        Note: this pass can't be done at the same time as post-processing as it needs to be done in advance 
        in case we're doing on-tile color grading.
        ---
        本 render pass 不能和 post-processing 在同一时间执行, 而是要先于 post-processing, 
        以防我们正在执行 "on-tile color grading"

        暂时只关心在 ForwardRenderer 中的使用;

        在 "BeforeRenderingPrePasses" 时刻执行;

    */

    // Renders a color grading LUT texture.
    public class ColorGradingLutPass //ColorGradingLutPass__
        : ScriptableRenderPass
    {
        
        // 此二 materials 都是在 代码中当场新建的; (所有也需要释放之)
        readonly Material m_LutBuilderLdr;//"Shaders/PostProcessing/LutBuilderLdr.shader"
        readonly Material m_LutBuilderHdr;//"Shaders/PostProcessing/LutBuilderHdr.shader"

        readonly GraphicsFormat m_HdrLutFormat;//R16G16B16A16_SFloat, 或 B10G11R11_UFloatPack32, 或 R8G8B8A8_UNorm
        readonly GraphicsFormat m_LdrLutFormat;//R8G8B8A8_UNorm

        RenderTargetHandle m_InternalLut;//"_InternalGradingLut"


        // 构造函数
        public ColorGradingLutPass(//      读完__  第二遍
                            RenderPassEvent evt, // 设置 render pass 何时执行; "BeforeRenderingPrePasses"
                            PostProcessData data // PostProcess 要使用到的 资源对象: shaders, textures
        ){
            base.profilingSampler = new ProfilingSampler(nameof(ColorGradingLutPass));
            renderPassEvent = evt;// base class 中的

            // 此值为 true 意味着: 用户要调用 "ConfigureTarget()" 重新设置了 camera 的 color 和 depth target, 
            overrideCameraTarget = true;// base class 中的

            // 这是个 "局部函数";
            Material Load(Shader shader)
            {
                if (shader == null)
                {
                    // "DeclaringType" 要访问本 class
                    Debug.LogError($"Missing shader. {GetType().DeclaringType.Name} render pass will not execute. Check for missing reference in the renderer resources.");
                    return null;
                }
                return CoreUtils.CreateEngineMaterial(shader);
            }

            // 调用 "局部函数"
            m_LutBuilderLdr = Load(data.shaders.lutBuilderLdrPS);//"Shaders/PostProcessing/LutBuilderLdr.shader"
            m_LutBuilderHdr = Load(data.shaders.lutBuilderHdrPS);//"Shaders/PostProcessing/LutBuilderHdr.shader"

            // Warm up lut format as IsFormatSupported adds GC pressure...

            /*
                HDR texture 需要支持的功能:
                -----
                FormatUsage: "GraphicsFormat" (如:B8G8R8A8_SRGB) 在本平台上所支持的 "功能":
                -- Linear: Use this to sample textures with a linear filter
                -- Render: Use this to create and render to a render texture.
            */
            const FormatUsage kFlags = FormatUsage.Linear | FormatUsage.Render;

            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, kFlags))
                m_HdrLutFormat = GraphicsFormat.R16G16B16A16_SFloat;
    
            else if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, kFlags))
                m_HdrLutFormat = GraphicsFormat.B10G11R11_UFloatPack32;
            else
                /*
                    Obviously using this for log lut encoding is a very bad idea for precision but we
                    need it for compatibility reasons and avoid black screens on platforms that don't
                    support floating point formats. 
                    Expect banding and posterization artifact if this ends up being used.
                    ---
                    若不得不选择此 format, 可能会出现 带状伪影 或 分色伪影;
                */
                m_HdrLutFormat = GraphicsFormat.R8G8B8A8_UNorm;

            m_LdrLutFormat = GraphicsFormat.R8G8B8A8_UNorm;
        }//  函数完__


        // ForwardRenderer 中被调用
        public void Setup(in RenderTargetHandle internalLut)//"_InternalGradingLut"
        {
            m_InternalLut = internalLut;
        }


        /*
            ------------------------------------------------------------------- +++
            可在本函数体内编写: 渲染逻辑本身, 也就是 用户希望本 render pass 要做的那些工作;
            使用参数 context 来发送 绘制指令, 执行 commandbuffers;
            不需要在本函数实现体内 调用 "ScriptableRenderContext.submit()", 渲染管线会在何时的时间点自动调用它;
        */
        /// <param name="context">Use this render context to issue(发射) any draw commands during execution</param>
        /// <param name="renderingData">Current rendering state information</param>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)//   读完__
        {

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.ColorGradingLUT)))
            {
                // Fetch all color grading settings
                var stack = VolumeManager.instance.stack;// 后处理容器, 用户启用的后处理模块, 都存于此

                // 下面这些变量, 很可能是 null; (用户未启用对应模块)
                var channelMixer = stack.GetComponent<ChannelMixer>(); // 调整 rgb 三通道的 混合;
                var colorAdjustments = stack.GetComponent<ColorAdjustments>();// 调整画面的整体颜色
                var curves = stack.GetComponent<ColorCurves>(); // 使用一组曲线来控制 整个画面的颜色
                var liftGammaGain = stack.GetComponent<LiftGammaGain>(); // Lift, Gamma, Gain; 分别控制 暗/中/亮 三个区域的颜色
                var shadowsMidtonesHighlights = stack.GetComponent<ShadowsMidtonesHighlights>();
                var splitToning = stack.GetComponent<SplitToning>(); // 分别控制 阴影/亮区 的颜色;
                var tonemapping = stack.GetComponent<Tonemapping>(); // 控制 hdr 颜色是符合转换到 sRGB 颜色的;
                var whiteBalance = stack.GetComponent<WhiteBalance>(); // 白平衡


                ref var postProcessingData = ref renderingData.postProcessingData;

                // gradingMode:
                // 颜色渐变模式; enum: LowDynamicRange, HighDynamicRange
                // 若 asset 支持 hdr, 则沿用 asset inspector 中配置值; 否则使用 LowDynamicRange;

                bool hdr = postProcessingData.gradingMode == ColorGradingMode.HighDynamicRange;

                // ----- Prepare texture & material -----:
                int lutHeight = postProcessingData.lutSize; // 沿用 asset inspector 中配置值; 通常为 32
                int lutWidth = lutHeight * lutHeight; // 32*32 = 1024
                var format = hdr ? m_HdrLutFormat : m_LdrLutFormat;
                var material = hdr ? 
                    m_LutBuilderHdr : //"Shaders/PostProcessing/LutBuilderHdr.shader"
                    m_LutBuilderLdr;  //"Shaders/PostProcessing/LutBuilderLdr.shader"

                var desc = new RenderTextureDescriptor(
                    lutWidth, lutHeight, 
                    format, // colorFormat
                    0       // The number of bits to use for the depth buffer.
                );
                desc.vrUsage = VRTextureUsage.None; // We only need one for both eyes in VR

                // 分配一个 render texture; (w=1024,h=32)
                cmd.GetTemporaryRT(
                    m_InternalLut.id, //"_InternalGradingLut"
                    desc, 
                    FilterMode.Bilinear
                );

                // ----- Prepare data -----:

                // Converts white balancing parameter to LMS coefficients.
                var lmsColorBalance = ColorUtils.ColorBalanceToLMSCoeffs(whiteBalance.temperature.value, whiteBalance.tint.value);

                var hueSatCon = new Vector4(
                    colorAdjustments.hueShift.value / 360f, 
                    colorAdjustments.saturation.value / 100f + 1f, 
                    colorAdjustments.contrast.value / 100f + 1f, 
                    0f
                );
                var channelMixerR = new Vector4(
                    channelMixer.redOutRedIn.value / 100f, 
                    channelMixer.redOutGreenIn.value / 100f, 
                    channelMixer.redOutBlueIn.value / 100f, 
                    0f
                );
                var channelMixerG = new Vector4(
                    channelMixer.greenOutRedIn.value / 100f, 
                    channelMixer.greenOutGreenIn.value / 100f, 
                    channelMixer.greenOutBlueIn.value / 100f, 
                    0f
                );
                var channelMixerB = new Vector4(
                    channelMixer.blueOutRedIn.value / 100f, 
                    channelMixer.blueOutGreenIn.value / 100f, 
                    channelMixer.blueOutBlueIn.value / 100f, 
                    0f
                );

                var shadowsHighlightsLimits = new Vector4(
                    shadowsMidtonesHighlights.shadowsStart.value,
                    shadowsMidtonesHighlights.shadowsEnd.value,
                    shadowsMidtonesHighlights.highlightsStart.value,
                    shadowsMidtonesHighlights.highlightsEnd.value
                );

                var(shadows, midtones, highlights) = ColorUtils.PrepareShadowsMidtonesHighlights(
                    shadowsMidtonesHighlights.shadows.value,
                    shadowsMidtonesHighlights.midtones.value,
                    shadowsMidtonesHighlights.highlights.value
                );

                var(lift, gamma, gain) = ColorUtils.PrepareLiftGammaGain(
                    liftGammaGain.lift.value,
                    liftGammaGain.gamma.value,
                    liftGammaGain.gain.value
                );

                var(splitShadows, splitHighlights) = ColorUtils.PrepareSplitToning(
                    splitToning.shadows.value,
                    splitToning.highlights.value,
                    splitToning.balance.value
                );

                var lutParameters = new Vector4(
                    lutHeight,       // 像素个数, 比如为 32;
                    0.5f / lutWidth, 
                    0.5f / lutHeight,
                    lutHeight / (lutHeight - 1f)
                );

                // ----- Fill in constants -----:
                material.SetVector(ShaderConstants._Lut_Params, lutParameters);
                material.SetVector(ShaderConstants._ColorBalance, lmsColorBalance);
                material.SetVector(ShaderConstants._ColorFilter, colorAdjustments.colorFilter.value.linear);
                material.SetVector(ShaderConstants._ChannelMixerRed, channelMixerR);
                material.SetVector(ShaderConstants._ChannelMixerGreen, channelMixerG);
                material.SetVector(ShaderConstants._ChannelMixerBlue, channelMixerB);
                material.SetVector(ShaderConstants._HueSatCon, hueSatCon);
                material.SetVector(ShaderConstants._Lift, lift);
                material.SetVector(ShaderConstants._Gamma, gamma);
                material.SetVector(ShaderConstants._Gain, gain);
                material.SetVector(ShaderConstants._Shadows, shadows);
                material.SetVector(ShaderConstants._Midtones, midtones);
                material.SetVector(ShaderConstants._Highlights, highlights);
                material.SetVector(ShaderConstants._ShaHiLimits, shadowsHighlightsLimits);
                material.SetVector(ShaderConstants._SplitShadows, splitShadows);
                material.SetVector(ShaderConstants._SplitHighlights, splitHighlights);

                // YRGB curves
                // 将曲线转换为 texture2d, 传入 shader
                material.SetTexture(ShaderConstants._CurveMaster, curves.master.value.GetTexture());
                material.SetTexture(ShaderConstants._CurveRed, curves.red.value.GetTexture());
                material.SetTexture(ShaderConstants._CurveGreen, curves.green.value.GetTexture());
                material.SetTexture(ShaderConstants._CurveBlue, curves.blue.value.GetTexture());

                // Secondary curves
                // 将曲线转换为 texture2d, 传入 shader
                material.SetTexture(ShaderConstants._CurveHueVsHue, curves.hueVsHue.value.GetTexture());
                material.SetTexture(ShaderConstants._CurveHueVsSat, curves.hueVsSat.value.GetTexture());
                material.SetTexture(ShaderConstants._CurveLumVsSat, curves.lumVsSat.value.GetTexture());
                material.SetTexture(ShaderConstants._CurveSatVsSat, curves.satVsSat.value.GetTexture());

                // Tonemapping (baked into the lut for HDR)
                if (hdr)
                {
                    // An array containing the "names of the local shader keywords" that are currently enabled for this material.
                    material.shaderKeywords = null;

                    switch (tonemapping.mode.value)
                    {
                        case TonemappingMode.Neutral: 
                            material.EnableKeyword(ShaderKeywordStrings.TonemapNeutral); //"_TONEMAP_NEUTRAL"
                            break;
                        case TonemappingMode.ACES: 
                            material.EnableKeyword(ShaderKeywordStrings.TonemapACES); // "_TONEMAP_NEUTRAL"
                            break;
                        default: break; // None
                    }
                }

                renderingData.cameraData.xr.StopSinglePass(cmd);// xr

                // Render the lut
                // 本质上就是在绘制一个 (w=1024,h=32) 的 quad, 一共4个顶点;
                cmd.Blit(
                    null,               // src:  猜测: 不依赖 src数据, 仅使用 参数 material 的 shader pass 去生成数据
                                        //       最后写入 参数 dest 中;
                    m_InternalLut.id,   // dest: "_InternalGradingLut"
                    material            // "Shaders/PostProcessing/LutBuilderHdr.shader"
                                        // "Shaders/PostProcessing/LutBuilderLdr.shader"
                );

                renderingData.cameraData.xr.StartSinglePass(cmd);// xr
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }//  函数完__


        /*
            ------------------------------------------------------------------- +++
            覆写 "ScriptableRenderPass" 的虚函数;
            ===
            在完成一个 camera stack 的渲染后, 本函数会被调用;
            可在本函数实现体内释放由本 render pass 新建的任何资源;

            如果一个 camera 没有显式的 camera stack, 它也被认为是一个 camera stack, 不过这个 stack 内只有它一个 camera;
        */
        /// <inheritdoc/>
        /// <param name="cmd">Use this CommandBuffer to cleanup any generated data</param>
        public override void OnFinishCameraStackRendering(CommandBuffer cmd)//  读完__
        {
            cmd.ReleaseTemporaryRT(m_InternalLut.id);
        }


        public void Cleanup()//  读完__
        {
            // 释放 materials
            CoreUtils.Destroy(m_LutBuilderLdr);
            CoreUtils.Destroy(m_LutBuilderHdr);
        }


        // Precomputed shader ids to same some CPU cycles (mostly affects mobile)
        static class ShaderConstants
        {
            public static readonly int _Lut_Params        = Shader.PropertyToID("_Lut_Params");
            public static readonly int _ColorBalance      = Shader.PropertyToID("_ColorBalance");
            public static readonly int _ColorFilter       = Shader.PropertyToID("_ColorFilter");
            public static readonly int _ChannelMixerRed   = Shader.PropertyToID("_ChannelMixerRed");
            public static readonly int _ChannelMixerGreen = Shader.PropertyToID("_ChannelMixerGreen");
            public static readonly int _ChannelMixerBlue  = Shader.PropertyToID("_ChannelMixerBlue");
            public static readonly int _HueSatCon         = Shader.PropertyToID("_HueSatCon");
            public static readonly int _Lift              = Shader.PropertyToID("_Lift");
            public static readonly int _Gamma             = Shader.PropertyToID("_Gamma");
            public static readonly int _Gain              = Shader.PropertyToID("_Gain");
            public static readonly int _Shadows           = Shader.PropertyToID("_Shadows");
            public static readonly int _Midtones          = Shader.PropertyToID("_Midtones");
            public static readonly int _Highlights        = Shader.PropertyToID("_Highlights");
            public static readonly int _ShaHiLimits       = Shader.PropertyToID("_ShaHiLimits");
            public static readonly int _SplitShadows      = Shader.PropertyToID("_SplitShadows");
            public static readonly int _SplitHighlights   = Shader.PropertyToID("_SplitHighlights");
            public static readonly int _CurveMaster       = Shader.PropertyToID("_CurveMaster");
            public static readonly int _CurveRed          = Shader.PropertyToID("_CurveRed");
            public static readonly int _CurveGreen        = Shader.PropertyToID("_CurveGreen");
            public static readonly int _CurveBlue         = Shader.PropertyToID("_CurveBlue");
            public static readonly int _CurveHueVsHue     = Shader.PropertyToID("_CurveHueVsHue");
            public static readonly int _CurveHueVsSat     = Shader.PropertyToID("_CurveHueVsSat");
            public static readonly int _CurveLumVsSat     = Shader.PropertyToID("_CurveLumVsSat");
            public static readonly int _CurveSatVsSat     = Shader.PropertyToID("_CurveSatVsSat");
        }
    }
}
