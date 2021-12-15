using UnityEngine.Rendering.Universal.Internal;
using System.Reflection;

namespace UnityEngine.Rendering.Universal
{
    /*
        Rendering modes for Universal renderer.
        enum: Forward, Deferred;
    */
    public enum RenderingMode//RenderingMode__
    {
        /*
            Render all objects and lighting in one pass, 
            with a hard limit on the number of lights that can be applied on an object.
            ---
            和 built-in Forward 不用, urp 中可在 一个 pass 中完成所有 物体和光源的 渲染;
        */
        Forward,//0
        
        /// Render all objects first in a g-buffer pass, 
        /// then apply all lighting in a separate pass using deferred shading.
        Deferred//1
    };




    /*
        Default renderer for urp
        This renderer is supported on all urp supported platforms.
        It uses a "classic forward rendering strategy" with "per-object light culling".
    */ 
    public sealed class ForwardRenderer//ForwardRenderer__RR
        : ScriptableRenderer
    {
        const int k_DepthStencilBufferBits = 32;

        private static class Profiling
        {
            private const string k_Name = nameof(ForwardRenderer);
            public static readonly ProfilingSampler createCameraRenderTarget = new ProfilingSampler($"{k_Name}.{nameof(CreateCameraRenderTarget)}");// "ForwardRenderer.CreateCameraRenderTarget"
        }

        // Rendering mode setup from UI.
        //  enum: Forward, Deferred;
        internal RenderingMode renderingMode { get { return m_RenderingMode;  } }

        /*
            Actual rendering mode, which may be different 
            (ex: wireframe rendering, harware not capable of deferred rendering).
            --
            某一个模式只能支持 前向渲染, 剩余情况则随意;
        */
        internal RenderingMode actualRenderingMode
        {
            get { return    GL.wireframe || 
                            m_DeferredLights == null || 
                            !m_DeferredLights.IsRuntimeSupportedThisFrame() 
                    ? RenderingMode.Forward : this.renderingMode; 
            } 
        }

        internal bool accurateGbufferNormals { get { return m_DeferredLights != null ? m_DeferredLights.AccurateGbufferNormals : false; } }

        DepthOnlyPass m_DepthPrepass;
        DepthNormalOnlyPass m_DepthNormalPrepass;
        MainLightShadowCasterPass m_MainLightShadowCasterPass;
        AdditionalLightsShadowCasterPass m_AdditionalLightsShadowCasterPass;


        GBufferPass m_GBufferPass;//暂时先忽略, 反正 11.0 中也不支持 延迟渲染       tpr
        CopyDepthPass m_GBufferCopyDepthPass;//暂时先忽略, 反正 11.0 中也不支持 延迟渲染       tpr
        TileDepthRangePass m_TileDepthRangePass;//暂时先忽略, 反正 11.0 中也不支持 延迟渲染       tpr
        // TODO use subpass API to hide this pass
        TileDepthRangePass m_TileDepthRangeExtraPass; //暂时先忽略, 反正 11.0 中也不支持 延迟渲染       tpr
        DeferredPass m_DeferredPass;//暂时先忽略, 反正 11.0 中也不支持 延迟渲染       tpr
        DrawObjectsPass m_RenderOpaqueForwardOnlyPass;//暂时先忽略, 反正 11.0 中也不支持 延迟渲染       tpr



        // "渲染不透明物" 的 render pass 
        // 始终创建此 render pass, 就算在 延迟渲染模式 也创建此 render pass, 
        // 因为:(1) editor 中的线框模式, (2) 绘制到 depth render texture 的 camera, 都要用到它;
        DrawObjectsPass m_RenderOpaqueForwardPass;

        DrawSkyboxPass m_DrawSkyboxPass;
        CopyDepthPass m_CopyDepthPass; // 看完
        CopyColorPass m_CopyColorPass;
        TransparentSettingsPass m_TransparentSettingsPass;
        DrawObjectsPass m_RenderTransparentForwardPass;
        InvokeOnRenderObjectCallbackPass m_OnRenderObjectCallbackPass;// 看完
        FinalBlitPass m_FinalBlitPass;
        CapturePass m_CapturePass;
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
        XROcclusionMeshPass m_XROcclusionMeshPass;
        CopyDepthPass m_XRCopyDepthPass;
#endif
*/

#if UNITY_EDITOR
        SceneViewDepthCopyPass m_SceneViewDepthCopyPass;
#endif

        // 要么等于 "_CameraColorTexture", 要么等于 RenderTargetHandle.CameraTarget
        RenderTargetHandle m_ActiveCameraColorAttachment;

        // 要么等于 "_CameraDepthAttachment", 要么等于 RenderTargetHandle.CameraTarget
        RenderTargetHandle m_ActiveCameraDepthAttachment;
        RenderTargetHandle m_CameraColorAttachment;//"_CameraColorTexture"
        RenderTargetHandle m_CameraDepthAttachment;//"_CameraDepthAttachment"
        RenderTargetHandle m_DepthTexture;//"_CameraDepthTexture"
        RenderTargetHandle m_NormalsTexture;//"_CameraNormalsTexture"
        RenderTargetHandle[] m_GBufferHandles;
        RenderTargetHandle m_OpaqueColor;//"_CameraOpaqueTexture"
        // For tiled-deferred shading.
        RenderTargetHandle m_DepthInfoTexture;//"_DepthInfoTexture"
        RenderTargetHandle m_TileDepthInfoTexture;//"_TileDepthInfoTexture"


        ForwardLights m_ForwardLights;
        DeferredLights m_DeferredLights;
        RenderingMode m_RenderingMode; // 初始值 Forward
        StencilState m_DefaultStencilState;// 直接源自 Forward Renderer inspector 中设置的 stencil 部分 的数据;

        // Materials used in URP Scriptable Render Passes
        Material m_BlitMaterial = null;//"Shaders/Utils/Blit.shader"
        Material m_CopyDepthMaterial = null;//"Shaders/Utils/CopyDepth.shader"
        Material m_SamplingMaterial = null;//"Shaders/Utils/Sampling.shader"
        Material m_TileDepthInfoMaterial = null;// 未使用
        Material m_TileDeferredMaterial = null;// 未使用
        Material m_StencilDeferredMaterial = null;//"Shaders/Utils/StencilDeferred.shader"

        PostProcessPasses m_PostProcessPasses;
        internal ColorGradingLutPass colorGradingLutPass { get => m_PostProcessPasses.colorGradingLutPass; }
        internal PostProcessPass postProcessPass { get => m_PostProcessPasses.postProcessPass; }
        internal PostProcessPass finalPostProcessPass { get => m_PostProcessPasses.finalPostProcessPass; }
        internal RenderTargetHandle afterPostProcessColor { get => m_PostProcessPasses.afterPostProcessColor; }
        internal RenderTargetHandle colorGradingLut { get => m_PostProcessPasses.colorGradingLut; }



        
        //    构造函数
        public ForwardRenderer(ForwardRendererData data) : base(data)
        {
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            UniversalRenderPipeline.m_XRSystem.InitializeXRSystemData(data.xrSystemData);
#endif
*/

            m_BlitMaterial = CoreUtils.CreateEngineMaterial(data.shaders.blitPS);//"Shaders/Utils/Blit.shader"
            m_CopyDepthMaterial = CoreUtils.CreateEngineMaterial(data.shaders.copyDepthPS);//"Shaders/Utils/CopyDepth.shader"
            m_SamplingMaterial = CoreUtils.CreateEngineMaterial(data.shaders.samplingPS);//"Shaders/Utils/Sampling.shader"

            // 下面两行是 urp 自己注释掉的:
            //m_TileDepthInfoMaterial = CoreUtils.CreateEngineMaterial(data.shaders.tileDepthInfoPS);
            //m_TileDeferredMaterial = CoreUtils.CreateEngineMaterial(data.shaders.tileDeferredPS);

            m_StencilDeferredMaterial = CoreUtils.CreateEngineMaterial(data.shaders.stencilDeferredPS);//"Shaders/Utils/StencilDeferred.shader"

            // --- 直接源自 Forward Renderer inspector 中设置的 stencil 部分 的数据:
            StencilStateData stencilData = data.defaultStencilState;
            m_DefaultStencilState = StencilState.defaultValue;
            m_DefaultStencilState.enabled = stencilData.overrideStencilState;
            m_DefaultStencilState.SetCompareFunction(stencilData.stencilCompareFunction);
            m_DefaultStencilState.SetPassOperation(stencilData.passOperation);
            m_DefaultStencilState.SetFailOperation(stencilData.failOperation);
            m_DefaultStencilState.SetZFailOperation(stencilData.zFailOperation);


            m_ForwardLights = new ForwardLights();

            //m_DeferredLights.LightCulling = data.lightCulling;   urp 自己注释掉的

            this.m_RenderingMode = data.renderingMode; // Forward

            /*
                Note: Since all custom render passes inject first and we have stable sort, 
                we inject the builtin passes in the before events.
                ---
                鉴于所有 custom render passes 首先注入, 且我们有稳定的排序, 所有我们将 内置paasses 注入 "before events"
            */
            m_MainLightShadowCasterPass = new MainLightShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);
            m_AdditionalLightsShadowCasterPass = new AdditionalLightsShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);

/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            m_XROcclusionMeshPass = new XROcclusionMeshPass(RenderPassEvent.BeforeRenderingOpaques);
            // Schedule XR copydepth right after m_FinalBlitPass(AfterRendering + 1)
            m_XRCopyDepthPass = new CopyDepthPass(RenderPassEvent.AfterRendering + 2, m_CopyDepthMaterial);
#endif
*/
            m_DepthPrepass = new DepthOnlyPass(RenderPassEvent.BeforeRenderingPrePasses, RenderQueueRange.opaque, data.opaqueLayerMask);
            m_DepthNormalPrepass = new DepthNormalOnlyPass(RenderPassEvent.BeforeRenderingPrePasses, RenderQueueRange.opaque, data.opaqueLayerMask);


            /* 暂时先忽略, 反正 11.0 中也不支持 延迟渲染          tpr
            if (this.renderingMode == RenderingMode.Deferred)
            {
                m_DeferredLights = new DeferredLights(m_TileDepthInfoMaterial, m_TileDeferredMaterial, m_StencilDeferredMaterial);
                m_DeferredLights.AccurateGbufferNormals = data.accurateGbufferNormals;
                //m_DeferredLights.TiledDeferredShading = data.tiledDeferredShading;
                m_DeferredLights.TiledDeferredShading = false;

                m_GBufferPass = new GBufferPass(RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask, m_DefaultStencilState, stencilData.stencilReference, m_DeferredLights);
                // Forward-only pass only runs if deferred renderer is enabled.
                // It allows specific materials to be rendered in a forward-like pass.
                // We render both gbuffer pass and forward-only pass before the deferred lighting pass so we can minimize copies of depth buffer and
                // benefits from some depth rejection.
                // - If a material can be rendered either forward or deferred, then it should declare a UniversalForward and a UniversalGBuffer pass.
                // - If a material cannot be lit in deferred (unlit, bakedLit, special material such as hair, skin shader), then it should declare UniversalForwardOnly pass
                // - Legacy materials have unamed pass, which is implicitely renamed as SRPDefaultUnlit. In that case, they are considered forward-only too.
                // TO declare a material with unnamed pass and UniversalForward/UniversalForwardOnly pass is an ERROR, as the material will be rendered twice.
                StencilState forwardOnlyStencilState = DeferredLights.OverwriteStencil(m_DefaultStencilState, (int)StencilUsage.MaterialMask);
                ShaderTagId[] forwardOnlyShaderTagIds = new ShaderTagId[]
                {
                    new ShaderTagId("UniversalForwardOnly"),
                    new ShaderTagId("SRPDefaultUnlit"), // Legacy shaders (do not have a gbuffer pass) are considered forward-only for backward compatibility
                    new ShaderTagId("LightweightForward") // Legacy shaders (do not have a gbuffer pass) are considered forward-only for backward compatibility
                };
                int forwardOnlyStencilRef = stencilData.stencilReference | (int)StencilUsage.MaterialUnlit;

                m_RenderOpaqueForwardOnlyPass = new DrawObjectsPass(
                    "Render Opaques Forward Only", 
                    forwardOnlyShaderTagIds, 
                    true, 
                    RenderPassEvent.BeforeRenderingOpaques + 1, 
                    RenderQueueRange.opaque, 
                    data.opaqueLayerMask, 
                    forwardOnlyStencilState, 
                    forwardOnlyStencilRef
                );
                
                m_GBufferCopyDepthPass = new CopyDepthPass(RenderPassEvent.BeforeRenderingOpaques + 2, m_CopyDepthMaterial);
                m_TileDepthRangePass = new TileDepthRangePass(RenderPassEvent.BeforeRenderingOpaques + 3, m_DeferredLights, 0);
                m_TileDepthRangeExtraPass = new TileDepthRangePass(RenderPassEvent.BeforeRenderingOpaques + 4, m_DeferredLights, 1);
                m_DeferredPass = new DeferredPass(RenderPassEvent.BeforeRenderingOpaques + 5, m_DeferredLights);
            }
            */



            // Always create this pass even in deferred because we use it for
            // wireframe rendering in the Editor, or offscreen depth texture rendering.
            // --
            // "渲染不透明物" 的 render pass 
            // 始终创建此 render pass, 就算在 延迟渲染模式 也创建此 render pass, 
            // 因为:(1) editor 中的线框模式, (2) 绘制到 depth render texture 的 camera, 都要用到它;
            m_RenderOpaqueForwardPass = new DrawObjectsPass(
                URPProfileId.DrawOpaqueObjects,         // 分析代码块的 name
                // ----------                           // 参数2被省略, 使用一组预定义 ShaderTagIds
                true,                                   // 本 render pass 渲染的是 不透明物体
                RenderPassEvent.BeforeRenderingOpaques, // 设置 render pass 执行时间: 在渲染 prepasses 之后, 在渲染不透明物体之前
                RenderQueueRange.opaque,                // renderQueue 值位于此区间内的物体, 可被渲染, 比如 [0,2000]
                data.opaqueLayerMask,                   // Forward Renderer inspector 中设置
                m_DefaultStencilState,                  // 替换 render state 中的 stencil 部分
                stencilData.stencilReference            // stencil ref 值
            );

            m_CopyDepthPass = new CopyDepthPass(
                RenderPassEvent.AfterRenderingSkybox, // 在所有 不透明物体之后, 执行本pass
                m_CopyDepthMaterial
            );
            
            m_DrawSkyboxPass = new DrawSkyboxPass(RenderPassEvent.BeforeRenderingSkybox);
            m_CopyColorPass = new CopyColorPass(
                RenderPassEvent.AfterRenderingSkybox, // skybox 之后, 就意味着 所有 不透明物体都渲染完毕了
                m_SamplingMaterial, // samplingMaterial
                m_BlitMaterial      // copyColorMaterial; //"Shaders/Utils/Blit.shader"
            );


// 如果 package: "com.unity.adaptiveperformance" 版本大于等于 2.1.0
#if ADAPTIVE_PERFORMANCE_2_1_0_OR_NEWER
            if (!UniversalRenderPipeline.asset.useAdaptivePerformance || AdaptivePerformance.AdaptivePerformanceRenderSettings.SkipTransparentObjects == false)
#endif
            {
                m_TransparentSettingsPass = new TransparentSettingsPass(
                    RenderPassEvent.BeforeRenderingTransparents, 
                    data.shadowTransparentReceive
                );
                
                m_RenderTransparentForwardPass = new DrawObjectsPass(
                    URPProfileId.DrawTransparentObjects,        // 分析代码块的 name
                    // ----------                               // 参数2被省略, 使用一组预定义 ShaderTagIds
                    false,                                      // 本 render pass 渲染的是 半透明物体
                    RenderPassEvent.BeforeRenderingTransparents,// 设置 render pass 执行时间: 在渲染 Transparents 之前,
                    RenderQueueRange.transparent,               // renderQueue 值位于此区间内的物体, 可被渲染, 比如 [2000,2500]
                    data.transparentLayerMask,                  // Forward Renderer inspector 中设置
                    m_DefaultStencilState,                      // 替换 render state 中的 stencil 部分
                    stencilData.stencilReference                // stencil ref 值
                );
            }

            m_OnRenderObjectCallbackPass = new InvokeOnRenderObjectCallbackPass(RenderPassEvent.BeforeRenderingPostProcessing);

            m_PostProcessPasses = new PostProcessPasses(
                data.postProcessData, 
                m_BlitMaterial//"Shaders/Utils/Blit.shader"
            );

            m_CapturePass = new CapturePass(RenderPassEvent.AfterRendering);
            m_FinalBlitPass = new FinalBlitPass(
                RenderPassEvent.AfterRendering + 1, 
                m_BlitMaterial //"Shaders/Utils/Blit.shader"
            );

#if UNITY_EDITOR
            m_SceneViewDepthCopyPass = new SceneViewDepthCopyPass(RenderPassEvent.AfterRendering + 9, m_CopyDepthMaterial);
#endif

            // RenderTexture format depends on camera and pipeline (HDR, non HDR, etc)
            // Samples (MSAA) depend on camera and pipeline
            m_CameraColorAttachment.Init("_CameraColorTexture");
            m_CameraDepthAttachment.Init("_CameraDepthAttachment");
            m_DepthTexture.Init("_CameraDepthTexture");
            m_NormalsTexture.Init("_CameraNormalsTexture");


            /* 暂时先忽略, 反正 11.0 中也不支持 延迟渲染          tpr
            if (this.renderingMode == RenderingMode.Deferred)
            {
                m_GBufferHandles = new RenderTargetHandle[(int)DeferredLights.GBufferHandles.Count];
                m_GBufferHandles[(int)DeferredLights.GBufferHandles.DepthAsColor].Init("_GBufferDepthAsColor");
                m_GBufferHandles[(int)DeferredLights.GBufferHandles.Albedo].Init("_GBuffer0");
                m_GBufferHandles[(int)DeferredLights.GBufferHandles.SpecularMetallic].Init("_GBuffer1");
                m_GBufferHandles[(int)DeferredLights.GBufferHandles.NormalSmoothness].Init("_GBuffer2");
                m_GBufferHandles[(int)DeferredLights.GBufferHandles.Lighting] = new RenderTargetHandle();
                m_GBufferHandles[(int)DeferredLights.GBufferHandles.ShadowMask].Init("_GBuffer4");
            }
            */


            m_OpaqueColor.Init("_CameraOpaqueTexture");
            m_DepthInfoTexture.Init("_DepthInfoTexture");
            m_TileDepthInfoTexture.Init("_TileDepthInfoTexture");

            supportedRenderingFeatures = new RenderingFeatures()
            {
                cameraStacking = true,
            };

            /* 暂时先忽略, 反正 11.0 中也不支持 延迟渲染          tpr
            if (this.renderingMode == RenderingMode.Deferred)
            {
                // Deferred rendering does not support MSAA.
                this.supportedRenderingFeatures.msaa = false;

                // Avoid legacy platforms: use vulkan instead.
                unsupportedGraphicsDeviceTypes = new GraphicsDeviceType[]
                {
                    GraphicsDeviceType.OpenGLCore,
                    GraphicsDeviceType.OpenGLES2,
                    GraphicsDeviceType.OpenGLES3
                };
            }
            */

        }// 函数完__




        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            m_PostProcessPasses.Dispose();

            CoreUtils.Destroy(m_BlitMaterial);
            CoreUtils.Destroy(m_CopyDepthMaterial);
            CoreUtils.Destroy(m_SamplingMaterial);
            CoreUtils.Destroy(m_TileDepthInfoMaterial);
            CoreUtils.Destroy(m_TileDeferredMaterial);
            CoreUtils.Destroy(m_StencilDeferredMaterial);
        }// 函数完__



        /*
            ======================================= Setup =================================================:
            一次只处理一个 camera, (而不是一个 camera stack), base / overlay camera 皆可;
        */
        /// <inheritdoc />
        public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
// 如果 package: "com.unity.adaptiveperformance" 版本大于等于 2.1.0
#if ADAPTIVE_PERFORMANCE_2_1_0_OR_NEWER
            bool needTransparencyPass = !UniversalRenderPipeline.asset.useAdaptivePerformance || !AdaptivePerformance.AdaptivePerformanceRenderSettings.SkipTransparentObjects;
#endif

            Camera camera = renderingData.cameraData.camera; // base / overlay camera 皆可;
            ref CameraData cameraData = ref renderingData.cameraData;
            RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            /*
                ---------------------------------------------------------------------:
                Special path for "depth only offscreen cameras". Only write opaques + transparents.
                如果相机有自己得 rt, 且只渲染 depth 值;
                此处所谓的 "offscreen", 就是 "并不渲染到屏幕上, 而是渲染到 rt 上" 的意思;
            */
            bool isOffscreenDepthTexture = cameraData.targetTexture!=null && cameraData.targetTexture.format == RenderTextureFormat.Depth;
            if (isOffscreenDepthTexture)
            {
                ConfigureCameraTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget);
                AddRenderPasses(ref renderingData);
                EnqueuePass(m_RenderOpaqueForwardPass);

                // TODO: Do we need to inject transparents and skybox when rendering depth only camera? They don't write to depth.
                EnqueuePass(m_DrawSkyboxPass);
// 如果 package: "com.unity.adaptiveperformance" 版本大于等于 2.1.0
#if ADAPTIVE_PERFORMANCE_2_1_0_OR_NEWER
                if (!needTransparencyPass)
                    return;
#endif

                EnqueuePass(m_RenderTransparentForwardPass);
                return;
            }



            if (m_DeferredLights != null)
                m_DeferredLights.ResolveMixedLightingMode(ref renderingData);

            // Assign the camera color target early in case it is needed during AddRenderPasses.
            bool isPreviewCamera = cameraData.isPreviewCamera;
            bool isRunningHololens = false;
/*   tpr
#if ENABLE_VR && ENABLE_VR_MODULE
            isRunningHololens = UniversalRenderPipeline.IsRunningHololens(cameraData);
#endif
*/
            var createColorTexture = (rendererFeatures.Count!=0 && !isRunningHololens) && !isPreviewCamera;// bool
            if (createColorTexture)
            {
                m_ActiveCameraColorAttachment = m_CameraColorAttachment;//"_CameraColorTexture"
                var activeColorRenderTargetId = m_ActiveCameraColorAttachment.Identifier();
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
                if (cameraData.xr.enabled) activeColorRenderTargetId = new RenderTargetIdentifier(activeColorRenderTargetId, 0, CubemapFace.Unknown, -1);
#endif
*/
                ConfigureCameraColorTarget(activeColorRenderTargetId);
            }

            // Add render passes and gather the input requirements
            isCameraColorTargetValid = true;
            AddRenderPasses(ref renderingData);
            isCameraColorTargetValid = false;
            RenderPassInputSummary renderPassInputs = GetRenderPassInputs(ref renderingData);

            // Should apply post-processing after rendering this camera?
            bool applyPostProcessing = cameraData.postProcessEnabled && m_PostProcessPasses.isCreated;

            // There's at least a camera in the camera stack that applies post-processing
            bool anyPostProcessing = renderingData.postProcessingEnabled && m_PostProcessPasses.isCreated;

            // TODO: We could cache and generate the LUT before rendering the stack
            bool generateColorGradingLUT = cameraData.postProcessEnabled && m_PostProcessPasses.isCreated;

            bool isSceneViewCamera = cameraData.isSceneViewCamera;// 是否为 editor: scene 窗口 使用的 camera;

            bool requiresDepthTexture = cameraData.requiresDepthTexture || 
                                        renderPassInputs.requiresDepthTexture || // 存在某个 render pass, 需要提前计算好 depth texture 当作 input;
                                        this.actualRenderingMode == RenderingMode.Deferred;

            bool mainLightShadows = m_MainLightShadowCasterPass.Setup(ref renderingData);
            
            bool additionalLightShadows = m_AdditionalLightsShadowCasterPass.Setup(ref renderingData);

            // 如果 半透明物体 "不能接收 shadow", 此值为 true;
            bool transparentsNeedSettingsPass = m_TransparentSettingsPass.Setup(ref renderingData);

            /*
                "Depth prepass" is generated in the following cases:
                    - If game or offscreen camera(离屏:渲染到一个 rt 上去) requires it 
                        we check if we can copy the depth from the "rendering opaques pass" and use that instead.
                    - Scene or preview cameras always require a depth texture. 
                        We do a depth pre-pass to simplify it and it shouldn't matter much for editor.
                    - Render passes require it
                --------
                以下情况时, 需要运行一个 Depth prepass:
                    -- 如果 game camera 或 离屏camera 需要 depth 数据, 
                        我们会尝试从 "rendering opaques pass" 中复制出 depth 数据, 并直接使用这个数据
                    -- scene camera, preview camera 总是需要 depth texture;
                    -- 当 Render passes 明确需要 Depth prepass 时;
            */
            bool requiresDepthPrepass = requiresDepthTexture &&
                !CanCopyDepth(ref renderingData.cameraData);

            requiresDepthPrepass |= isSceneViewCamera;// 是否为 editor: scene 窗口 使用的 camera;
            requiresDepthPrepass |= isPreviewCamera; // 是否为 editor: 预览窗口使用的 camera;
            requiresDepthPrepass |= renderPassInputs.requiresDepthPrepass;// 存在某个 render pass, 它在 "渲染不透明物" 之前执行,
                                                                          // 同时它需要 depth 或 normal texture 作为 input;
            requiresDepthPrepass |= renderPassInputs.requiresNormalsTexture;//存在某个 render pass, 需要提前计算好 normal texture 当作 input;


            // The copying of depth should normally happen after rendering opaques.
            // But if we only require it for post processing or the "scene camera"(editor) then we do it after rendering transparent objects
            m_CopyDepthPass.renderPassEvent = (!requiresDepthTexture && (applyPostProcessing || isSceneViewCamera)) ? 
                            RenderPassEvent.AfterRenderingTransparents : 
                            RenderPassEvent.AfterRenderingOpaques;
            
            createColorTexture |= RequiresIntermediateColorTexture(ref cameraData);
            createColorTexture |= renderPassInputs.requiresColorTexture;// 存在某个 render pass, 需要提前计算好 color texture 当作 input;
            createColorTexture &= !isPreviewCamera; // 不能是 预览窗口


            // TODO: There's an issue in multiview and depth copy pass. Atm forcing a depth prepass on XR until we have a proper fix.
            /*      tpr
            if (cameraData.xr.enabled && requiresDepthTexture)
                requiresDepthPrepass = true;
            */


            /*
                If camera requires depth and there's no depth pre-pass we create a depth texture 
                "that can be read later by effect requiring it".

                ---
                When deferred renderer is enabled, we must always create a depth texture 
                and CANNOT use BuiltinRenderTextureType.CameraTarget. 
                This is to get around a bug where during gbuffer pass (MRT pass), 
                the camera depth attachment is correctly bound, but during deferred pass ("camera color" + "camera depth"), 
                the implicit depth surface of "camera color" is used instead of "camera depth",
                because BuiltinRenderTextureType.CameraTarget for depth means there is no explicit depth attachment...
            */

            // 如果 需要在渲染完 skybox 之后, 将 camera 的 depth 写入 "_CameraDepthTexture"; 但又没启用 "depth prepass";
            // 那么我们就需要创建一个 depth texture;
            bool createDepthTexture = 
                cameraData.requiresDepthTexture && // 在渲染完 skybox 之后, 需要将 camera 的 depth buffer 复制一份到 "_CameraDepthTexture"
                !requiresDepthPrepass; // 没有启用 "depth prepass";

            // 添加: 当前 camera 是 base, 且不属于 stack 中最后一个;
            createDepthTexture |= (cameraData.renderType==CameraRenderType.Base && !cameraData.resolveFinalTarget);

            // Deferred renderer always need to access depth buffer.
            // 添加: 延迟渲染 始终要开启;
            createDepthTexture |= this.actualRenderingMode == RenderingMode.Deferred;

/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            if (cameraData.xr.enabled)
            {
                // URP can't handle msaa/size mismatch between depth RT and color RT(for now we create intermediate textures to ensure they match)
                createDepthTexture |= createColorTexture;
                createColorTexture = createDepthTexture;
            }
#endif
*/

// 安卓, webGL
#if UNITY_ANDROID || UNITY_WEBGL
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
            {
                // GLES can not use render texture's depth buffer with the color buffer of the backbuffer
                // in such case we create a color texture for it too.
                createColorTexture |= createDepthTexture;
            }
#endif

            // Configure all settings require to start a new camera stack (base camera only)
            if (cameraData.renderType == CameraRenderType.Base)
            {
                // 对于 非xr camera来说, 就等于 "CameraTarget", 值为 -1
                RenderTargetHandle cameraTargetHandle = RenderTargetHandle.GetCameraTarget(cameraData.xr);

                m_ActiveCameraColorAttachment = (createColorTexture) ? m_CameraColorAttachment : cameraTargetHandle;
                m_ActiveCameraDepthAttachment = (createDepthTexture) ? m_CameraDepthAttachment : cameraTargetHandle;

                // 是否需要创建 "intermediate Render Texture"
                bool intermediateRenderTexture = createColorTexture || createDepthTexture;

                // Doesn't create texture for Overlay cameras as they are already overlaying on top of created textures.
                // 根据需求创建 color render texture 和 depth render texture, 分别绑定到:
                //      m_ActiveCameraColorAttachment
                //      m_ActiveCameraDepthAttachment
                if (intermediateRenderTexture)
                    CreateCameraRenderTarget(context, ref cameraTargetDescriptor, createColorTexture, createDepthTexture);
            }
            else
            {
                m_ActiveCameraColorAttachment = m_CameraColorAttachment;//"_CameraColorTexture"
                m_ActiveCameraDepthAttachment = m_CameraDepthAttachment;//"_CameraDepthAttachment"
            }


            // Assign camera targets (color and depth)
            {
                var activeColorRenderTargetId = m_ActiveCameraColorAttachment.Identifier();// rtid
                var activeDepthRenderTargetId = m_ActiveCameraDepthAttachment.Identifier();// rtid

/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
                if (cameraData.xr.enabled)
                {
                    activeColorRenderTargetId = new RenderTargetIdentifier(activeColorRenderTargetId, 0, CubemapFace.Unknown, -1);
                    activeDepthRenderTargetId = new RenderTargetIdentifier(activeDepthRenderTargetId, 0, CubemapFace.Unknown, -1);
                }
#endif
*/

                ConfigureCameraTarget(activeColorRenderTargetId, activeDepthRenderTargetId);
            }


            // activeRenderPassQueue 中, 存在 "晚于 PostProcessing" 的 render pass;
            bool hasPassesAfterPostProcessing = 
                // Find(...): 根据谓词去查找所有元素, 返回第一个符合条件的元素, 若没找到, 返回类型 T 的默认值;
                activeRenderPassQueue.Find(x => x.renderPassEvent==RenderPassEvent.AfterRendering) != null;


            if (mainLightShadows)
                EnqueuePass(m_MainLightShadowCasterPass);
            if (additionalLightShadows)
                EnqueuePass(m_AdditionalLightsShadowCasterPass);


            // "Depth prepass" + "Normal prepass"
            if (requiresDepthPrepass)
            {
                if (renderPassInputs.requiresNormalsTexture)// 存在某个 render pass, 需要提前计算好 normal texture 当作 input;
                {
                    // 本 render pass 在开始执行前, 同时需要计算好的 depth texture 和 normal texture
                    // 想要在 prepass 阶段运行 DepthNormalOnlyPass, 可以在自定义 render pass 的 Setup() 中写入:
                    //    ConfigureInput( ScriptableRenderPassInput.Normal );
                    m_DepthNormalPrepass.Setup(cameraTargetDescriptor, m_DepthTexture, m_NormalsTexture);
                    EnqueuePass(m_DepthNormalPrepass);
                }
                else
                {// 本 render pass 在开始执行前, 仅需要计算好的 depth texture
                    m_DepthPrepass.Setup(cameraTargetDescriptor, m_DepthTexture);
                    EnqueuePass(m_DepthPrepass);
                }
            }


            if (generateColorGradingLUT)
            {
                colorGradingLutPass.Setup(colorGradingLut);
                EnqueuePass(colorGradingLutPass);
            }

/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            if (cameraData.xr.hasValidOcclusionMesh)
                EnqueuePass(m_XROcclusionMeshPass);
#endif
*/

            if (this.actualRenderingMode == RenderingMode.Deferred){
                /*  暂时先忽略,  反正 11.0 也不支持 延迟渲染    tpr
                EnqueueDeferred(ref renderingData, requiresDepthPrepass, mainLightShadows, additionalLightShadows);
                */
            }
            else
                EnqueuePass(m_RenderOpaqueForwardPass);


            Skybox cameraSkybox;
            // 只有在 camera go 上显式绑定一个 Skybox 组件时, 才能访问到; (通常情况下不绑定)
            cameraData.camera.TryGetComponent<Skybox>(out cameraSkybox);
            bool isOverlayCamera = cameraData.renderType == CameraRenderType.Overlay;
            if( camera.clearFlags == CameraClearFlags.Skybox && 
                (RenderSettings.skybox != null || cameraSkybox?.material != null) && 
                !isOverlayCamera
            )
                EnqueuePass(m_DrawSkyboxPass);

            /*
                If a depth texture was created we necessarily need to copy it, otherwise we could have render it to a renderbuffer.
                If deferred rendering path was selected, it has already made a copy.
            */
            bool requiresDepthCopyPass = 
                    !requiresDepthPrepass &&    // 没有开启 depth prepass
                    renderingData.cameraData.requiresDepthTexture && //在渲染完 skybox 之后, 要求将 camera 的 depth buffer 复制一份到 "_CameraDepthTexture";
                    createDepthTexture &&       // 已经创建了 depth render texture;
                    this.actualRenderingMode != RenderingMode.Deferred; // 前向渲染

            if (requiresDepthCopyPass)
            {
                m_CopyDepthPass.Setup(m_ActiveCameraDepthAttachment, m_DepthTexture);
                EnqueuePass(m_CopyDepthPass);
            }



            // For Base Cameras: Set the depth texture to the far Z if we do not have a depth prepass or copy depth
            if( cameraData.renderType==CameraRenderType.Base && 
                !requiresDepthPrepass &&  // 没有开启 depth prepass
                !requiresDepthCopyPass // 没有开启 depth copy pass
            ){
                Shader.SetGlobalTexture(m_DepthTexture.id, //"_CameraDepthTexture"
                    SystemInfo.usesReversedZBuffer ? 
                        Texture2D.blackTexture : // depth:[1->0]; 故设置默认值为 0, (最远值)
                        Texture2D.whiteTexture   // depth:[0->1]; 故设置默认值为 1, (最远值)
                );
            }


            if( renderingData.cameraData.requiresOpaqueTexture || //在渲染完 skybox 之后, 是否将 camera 的不透明物的 color buffer 复制一份到 "_CameraOpaqueTexture"
                renderPassInputs.requiresColorTexture // 存在某个 render pass, 需要提前计算好 color texture 当作 input;
            ){
                /*
                    TODO: Downsampling(多个 texel 放在一个像素里) method should be store in the renderer instead of in the asset.
                    We need to migrate(迁徙) this data to renderer. For now, we query the method in the active asset.
                */
                // enum: None,_2xBilinear,_4xBox,_4xBilinear
                Downsampling downsamplingMethod = UniversalRenderPipeline.asset.opaqueDownsampling;
                m_CopyColorPass.Setup(
                    m_ActiveCameraColorAttachment.Identifier(),//"_CameraColorTexture" 或 camera target
                    m_OpaqueColor, 
                    downsamplingMethod
                );
                EnqueuePass(m_CopyColorPass);
            }

// 如果 package: "com.unity.adaptiveperformance" 版本大于等于 2.1.0
#if ADAPTIVE_PERFORMANCE_2_1_0_OR_NEWER
            if (needTransparencyPass)
#endif

            {
                // 只有当 半透明物体 "不能接收 shadow" 时, 此 render pass 才会被渲染;
                if (transparentsNeedSettingsPass)
                {
                    EnqueuePass(m_TransparentSettingsPass);
                }
                EnqueuePass(m_RenderTransparentForwardPass);
            }

            // 在 "BeforeRenderingPostProcessing" 时执行, 
            // 调用所有 "MonoBehaviour.OnRenderObject()" callbacks;
            EnqueuePass(m_OnRenderObjectCallbackPass);



            bool lastCameraInTheStack = cameraData.resolveFinalTarget;

            // 存在 actions 且是 stack 中最后一个 camera;
            bool hasCaptureActions = renderingData.cameraData.captureActions != null && lastCameraInTheStack;


            // 可否理解为: 
            bool applyFinalPostProcessing = 
                anyPostProcessing && 
                lastCameraInTheStack &&
                renderingData.cameraData.antialiasing == AntialiasingMode.FastApproximateAntialiasing;// FXAA 自己就是个 PostProcessing

            /*
                When post-processing is enabled we can use the stack to resolve rendering to camera target (screen or RT).
                However when there are render passes executing after post we avoid resolving to screen so rendering continues 
                (before sRGBConvertion etc)
                ---
                (前略), 如果在 post-processing 之后还存在 render passes, 那么就不允许 post-processing 将自己的结果渲染进 screen,
                必须渲染进 render texture, 以供后续 render pass 继续渲染;
            */
            // 需要将 PostProcessing 解析到 cameraTarget, (而不是到 rt)
            bool resolvePostProcessingToCameraTarget = 
                !hasCaptureActions &&               // 没捕捉到 actions
                !hasPassesAfterPostProcessing &&    // 不存在晚于 PostProcessing 的 render pass
                !applyFinalPostProcessing;          // 


            if (lastCameraInTheStack)
            {
                // Post-processing will resolve to final target. No need for final blit pass.
                if (applyPostProcessing)
                {
                    var destination = resolvePostProcessingToCameraTarget ? RenderTargetHandle.CameraTarget : afterPostProcessColor;

                    // if resolving to screen we need to be able to perform sRGBConvertion in post-processing if necessary
                    bool doSRGBConvertion = resolvePostProcessingToCameraTarget;

                    postProcessPass.Setup(
                        cameraTargetDescriptor, 
                        m_ActiveCameraColorAttachment, 
                        destination, 
                        m_ActiveCameraDepthAttachment, 
                        colorGradingLut, 
                        applyFinalPostProcessing, 
                        doSRGBConvertion
                    );
                    EnqueuePass(postProcessPass);
                }


                // if we applied post-processing for this camera it means current active texture is m_AfterPostProcessColor
                var sourceForFinalPass = (applyPostProcessing) ? 
                        afterPostProcessColor : 
                        m_ActiveCameraColorAttachment;

                // Do FXAA or any other final post-processing effect that might need to run after AA.
                if (applyFinalPostProcessing)
                {
                    finalPostProcessPass.SetupFinalPass(sourceForFinalPass);
                    EnqueuePass(finalPostProcessPass);
                }

                if (renderingData.cameraData.captureActions != null)
                {
                    m_CapturePass.Setup(sourceForFinalPass);
                    EnqueuePass(m_CapturePass);
                }

                // if post-processing then we already resolved to camera target while doing post.
                // Also only do final blit if camera is not rendering to RT.
                bool cameraTargetResolved =
                    // final PP always blit to camera target
                    applyFinalPostProcessing ||
                    // no final PP but we have PP stack. In that case it blit unless there are render pass after PP
                    (applyPostProcessing && !hasPassesAfterPostProcessing) ||
                    // offscreen camera rendering to a texture, we don't need a blit pass to resolve to screen
                    m_ActiveCameraColorAttachment == RenderTargetHandle.GetCameraTarget(cameraData.xr);

                // We need final blit to resolve to screen
                if (!cameraTargetResolved)
                {
                    m_FinalBlitPass.Setup(
                        cameraTargetDescriptor, // 此参数似乎没有被使用
                        sourceForFinalPass
                    );
                    EnqueuePass(m_FinalBlitPass);
                }

/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
                bool depthTargetResolved =
                    // active depth is depth target, we don't need a blit pass to resolve
                    m_ActiveCameraDepthAttachment == RenderTargetHandle.GetCameraTarget(cameraData.xr);

                if (!depthTargetResolved && cameraData.xr.copyDepth)
                {
                    m_XRCopyDepthPass.Setup(m_ActiveCameraDepthAttachment, RenderTargetHandle.GetCameraTarget(cameraData.xr));
                    EnqueuePass(m_XRCopyDepthPass);
                }
#endif
*/
            }
            // stay in RT so we resume rendering on stack after post-processing
            else if (applyPostProcessing)
            {
                postProcessPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, afterPostProcessColor, m_ActiveCameraDepthAttachment, colorGradingLut, false, false);
                EnqueuePass(postProcessPass);
            }

#if UNITY_EDITOR
            if (isSceneViewCamera)
            {
                // Scene view camera should always resolve target (not stacked)
                Assertions.Assert.IsTrue(lastCameraInTheStack, "Editor camera must resolve target upon finish rendering.");
                m_SceneViewDepthCopyPass.Setup(m_DepthTexture);
                EnqueuePass(m_SceneViewDepthCopyPass);
            }
#endif
        }// 函数 Setup() 完__




        /// <inheritdoc />
        public override void SetupLights(ScriptableRenderContext context, ref RenderingData renderingData)//  读完__
        {
            m_ForwardLights.Setup(context, ref renderingData);

            /*  暂时先忽略,  反正 11.0 也不支持 延迟渲染    tpr
            // Perform per-tile light culling on CPU
            if (this.actualRenderingMode == RenderingMode.Deferred)
                m_DeferredLights.SetupLights(context, ref renderingData);
            */

        }// 函数完__



        /*
            为 "参数 cullingParameters" 设置一部分成员数据;
        */
        /// <inheritdoc />
        public override void SetupCullingParameters( //   读完__
                        ref ScriptableCullingParameters cullingParameters,
                        ref CameraData cameraData
        ){

            /*
            // TODO: PerObjectCulling also affect reflection probes. Enabling it for now.
            // if (asset.additionalLightsRenderingMode == LightRenderingMode.Disabled ||
            //     asset. == 0)
            // {
            //     cullingParameters.cullingOptions |= CullingOptions.DisablePerObjectCulling;
            // }
                ====== 官方把这段代码 注释掉了
            */

            // -------------
            // 不再对 shader caster objs 执行 cull 操作, 如果:
            //   --"shadow casting modes are turned off"
            //   --"the shadow distance has been turned down to zero"
            bool isShadowCastingDisabled = !UniversalRenderPipeline.asset.supportsMainLightShadows && !UniversalRenderPipeline.asset.supportsAdditionalLightShadows;
            bool isShadowDistanceZero = Mathf.Approximately(cameraData.maxShadowDistance, 0.0f);
            if (isShadowCastingDisabled || isShadowDistanceZero)
            {
                cullingParameters.cullingOptions &= ~CullingOptions.ShadowCasters;// 关闭这个 flag
            }

            if (this.actualRenderingMode == RenderingMode.Deferred){
                /*  暂时先忽略,  反正 11.0 也不支持 延迟渲染    tpr
                // 延迟渲染理论上支持: 每个物体 无数盏光
                cullingParameters.maximumVisibleLights = 0xFFFF;// 65535
                */
            }
            else
            {
                // We set the number of maximum visible lights allowed and we add one for the mainlight...
                //
                // Note: 
                // However "ScriptableRenderContext.Cull()" does not differentiate between light types.
                // If there is no active main light in the scene, "ScriptableRenderContext.Cull()" might return  
                // ( cullingParameters.maximumVisibleLights )  visible additional lights.
                // i.e "ScriptableRenderContext.Cull()" might return  
                // ( UniversalRenderPipeline.maxVisibleAdditionalLights + 1 ) visible additional lights !
                // --------
                // (16, 32, or 256) + 1;
                cullingParameters.maximumVisibleLights = UniversalRenderPipeline.maxVisibleAdditionalLights + 1;
            }
            cullingParameters.shadowDistance = cameraData.maxShadowDistance;
        }// 函数完__




        /// <inheritdoc />
        public override void FinishRendering(CommandBuffer cmd)
        {
            if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(m_ActiveCameraColorAttachment.id);
                m_ActiveCameraColorAttachment = RenderTargetHandle.CameraTarget;
            }

            if (m_ActiveCameraDepthAttachment != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(m_ActiveCameraDepthAttachment.id);
                m_ActiveCameraDepthAttachment = RenderTargetHandle.CameraTarget;
            }
        }// 函数完__



        /* 暂时先忽略, 反正 11.0 中也不支持 延迟渲染       tpr
        void EnqueueDeferred(ref RenderingData renderingData, bool hasDepthPrepass, bool applyMainShadow, bool applyAdditionalShadow)
        {
            // the last slice is the lighting buffer created in DeferredRenderer.cs
            m_GBufferHandles[(int)DeferredLights.GBufferHandles.Lighting] = m_ActiveCameraColorAttachment;

            m_DeferredLights.Setup(
                ref renderingData,
                applyAdditionalShadow ? m_AdditionalLightsShadowCasterPass : null,
                hasDepthPrepass,
                renderingData.cameraData.renderType == CameraRenderType.Overlay,
                m_DepthTexture,
                m_DepthInfoTexture,
                m_TileDepthInfoTexture,
                m_ActiveCameraDepthAttachment, m_GBufferHandles
            );

            EnqueuePass(m_GBufferPass);

            EnqueuePass(m_RenderOpaqueForwardOnlyPass);

            //Must copy depth for deferred shading: TODO wait for API fix to bind depth texture as read-only resource.
            if (!hasDepthPrepass)
            {
                m_GBufferCopyDepthPass.Setup(m_CameraDepthAttachment, m_DepthTexture);
                EnqueuePass(m_GBufferCopyDepthPass);
            }

            // Note: DeferredRender.Setup is called by UniversalRenderPipeline.RenderSingleCamera (overrides ScriptableRenderer.Setup).
            // At this point, we do not know if m_DeferredLights.m_Tilers[x].m_Tiles actually contain any indices of lights intersecting tiles (If there are no lights intersecting tiles, we could skip several following passes) : this information is computed in DeferredRender.SetupLights, which is called later by UniversalRenderPipeline.RenderSingleCamera (via ScriptableRenderer.Execute).
            // However HasTileLights uses m_HasTileVisLights which is calculated by CheckHasTileLights from all visibleLights. visibleLights is the list of lights that have passed camera culling, so we know they are in front of the camera. So we can assume m_DeferredLights.m_Tilers[x].m_Tiles will not be empty in that case.
            // m_DeferredLights.m_Tilers[x].m_Tiles could be empty if we implemented an algorithm accessing scene depth information on the CPU side, but this (access depth from CPU) will probably not happen.
            if (m_DeferredLights.HasTileLights())
            {
                // Compute for each tile a 32bits bitmask in which a raised bit means "this 1/32th depth slice contains geometry that could intersect with lights".
                // Per-tile bitmasks are obtained by merging together the per-pixel bitmasks computed for each individual pixel of the tile.
                EnqueuePass(m_TileDepthRangePass);

                // On some platform, splitting the bitmasks computation into two passes:
                //   1/ Compute bitmasks for individual or small blocks of pixels
                //   2/ merge those individual bitmasks into per-tile bitmasks
                // provides better performance that doing it in a single above pass.
                if (m_DeferredLights.HasTileDepthRangeExtraPass())
                    EnqueuePass(m_TileDepthRangeExtraPass);
            }

            EnqueuePass(m_DeferredPass);
        }// 函数完__
        */


        /*
            汇总了所有 active render pass 对 input 数据的需求
            (即: 在这些 render passes 开始执行前, 需要一些特定的 input 数据, 这些数据需要已经被计算好)
        */
        private struct RenderPassInputSummary//RenderPassInputSummary__
        {
            internal bool requiresDepthTexture; // 存在某个 render pass, 需要提前计算好 depth texture 当作 input;

            // 存在某个 render pass, 它在 "渲染不透明物" 之前执行, (通常是 shadow, 或 prepass 阶段)
            // 同时它需要计算好的 depth 或 normal texture 作为 input;
            internal bool requiresDepthPrepass; 

            internal bool requiresNormalsTexture; // 存在某个 render pass, 需要提前计算好 normal texture 当作 input;
            internal bool requiresColorTexture; // 存在某个 render pass, 需要提前计算好 color texture 当作 input;
        }


        /*
            遍历每一个 active render pass, 收集他们对 inputs 数据的需求;
            并将这些需求汇总为一份 单一的需求表;
        */
        private RenderPassInputSummary GetRenderPassInputs(ref RenderingData renderingData)// 读完__
        {
            RenderPassInputSummary inputSummary = new RenderPassInputSummary();
            for (int i = 0; i < activeRenderPassQueue.Count; ++i)
            {
                ScriptableRenderPass pass = activeRenderPassQueue[i];
                // 如果这个 render pass 的 input 变量中, 写明了需要某种数据, 那么就设置对应的 "need" flag;
                bool needsDepth   = (pass.input & ScriptableRenderPassInput.Depth) != ScriptableRenderPassInput.None;
                bool needsNormals = (pass.input & ScriptableRenderPassInput.Normal) != ScriptableRenderPassInput.None;
                bool needsColor   = (pass.input & ScriptableRenderPassInput.Color) != ScriptableRenderPassInput.None;

                // 本 render pass 是在 "渲染不透明物" 之前执行的, (通常是 shadow, 或 prepass 阶段)
                bool eventBeforeOpaque = pass.renderPassEvent <= RenderPassEvent.BeforeRenderingOpaques;

                // 不断累加的过程:
                inputSummary.requiresDepthTexture   |= needsDepth;
                inputSummary.requiresDepthPrepass   |= needsNormals || needsDepth && eventBeforeOpaque;
                inputSummary.requiresNormalsTexture |= needsNormals;
                inputSummary.requiresColorTexture   |= needsColor;
            }
            return inputSummary;
        }// 函数完__




        /*
            根据需求创建 color render texture 和 depth render texture, 分别绑定到:
                m_ActiveCameraColorAttachment 
                m_ActiveCameraDepthAttachment
        */
        void CreateCameraRenderTarget(//   读完__
                                    ScriptableRenderContext context, 
                                    ref RenderTextureDescriptor descriptor, //包含用来创建 RenderTexture 所需的一切信息
                                    bool createColor, 
                                    bool createDepth
        ){
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, Profiling.createCameraRenderTarget))
            {
                if (createColor)
                {
                    
                    bool useDepthRenderBuffer = m_ActiveCameraDepthAttachment == RenderTargetHandle.CameraTarget;

                    var colorDescriptor = descriptor;
                    colorDescriptor.useMipMap = false;
                    colorDescriptor.autoGenerateMips = false;
                    colorDescriptor.depthBufferBits = (useDepthRenderBuffer) ? k_DepthStencilBufferBits : 0;
                    cmd.GetTemporaryRT(
                        // 不用担心它会成为 RenderTargetHandle.CameraTarget (即-1)
                        // 当此函数被调用时, 此值一定等于 "_CameraColorTexture"
                        m_ActiveCameraColorAttachment.id, 
                        colorDescriptor, 
                        FilterMode.Bilinear
                    );
                }

                if (createDepth)
                {
                    var depthDescriptor = descriptor;
                    depthDescriptor.useMipMap = false;
                    depthDescriptor.autoGenerateMips = false;
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
                    // XRTODO: Enabled this line for non-XR pass? URP copy depth pass is already capable of handling MSAA.
                    depthDescriptor.bindMS = depthDescriptor.msaaSamples > 1 && !SystemInfo.supportsMultisampleAutoResolve && (SystemInfo.supportsMultisampledTextures != 0);
#endif
*/
                    depthDescriptor.colorFormat = RenderTextureFormat.Depth;
                    depthDescriptor.depthBufferBits = k_DepthStencilBufferBits;//3-bits
                    cmd.GetTemporaryRT(
                        // 不用担心它会成为 RenderTargetHandle.CameraTarget (即-1)
                        // 当此函数被调用时, 此值一定等于 "_CameraDepthAttachment"
                        m_ActiveCameraDepthAttachment.id, 
                        depthDescriptor, 
                        FilterMode.Point
                    );
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }// 函数完__




        // 如果目标平台需要 用户手动实现 msaa 的解析工作, 本函数就返回 true;
        bool PlatformRequiresExplicitMsaaResolve()//   读完__
        {
            #if UNITY_EDITOR
                // In the editor play-mode we use a "Game View Render Texture", 
                // with samples count forced to 1 so we always need to do an explicit MSAA resolve.
                return true;
            #else
                /*
                    On Metal/iOS the MSAA resolve is done implicitly as part of the renderpass, 
                    so we do not need an extra intermediate pass for the explicit autoresolve.
                    ---
                    在 Metal/iOS 上，MSAA 解析是作为 render pass 的一部分隐式完成的，
                    因此我们不需要额外的 intermediate pass 来进行显式自动解析。
                    ---
                    代码解读: 如果目标平台不支持 "msaa 自动解析", 同时驱动也不属于 Metal;
                    就意味着这个平台 需要手动实现 msaa 解析, 所以要返回 true;
                */
                return !SystemInfo.supportsMultisampleAutoResolve && 
                    SystemInfo.graphicsDeviceType != GraphicsDeviceType.Metal;
            #endif
        }// 函数完__



       
        /*
            Checks if the pipeline needs to create a intermediate render texture.
            ---
            如果需要创建一个 "intermediate render texture", 本函数返回 true;
        */
        bool RequiresIntermediateColorTexture(ref CameraData cameraData)//   读完__
        {
            /*
                When rendering a camera stack we always create an intermediate render texture to composite camera results.
                We create it upon rendering the Base camera.
                ---
                如果是 base camera. 且它 不是 stack 中最后一个 (说明这个 stack 中有数个 camera)
                那么一定要 创建 "intermediate render texture"
            */
            if (cameraData.renderType == CameraRenderType.Base && !cameraData.resolveFinalTarget)
                return true;


            /*  暂时先忽略,  反正 11.0 也不支持 延迟渲染       tpr
            // Always force rendering into intermediate color texture if deferred rendering mode is selected.
            // Reason: without intermediate color texture, the target camera texture is y-flipped.
            // However, the target camera texture is bound during gbuffer pass and deferred pass.
            // Gbuffer pass will not be y-flipped because it is MRT (see ScriptableRenderContext implementation),
            // while deferred pass will be y-flipped, which breaks rendering.
            // This incurs an extra blit into at the end of rendering.
            if (this.actualRenderingMode == RenderingMode.Deferred)
                return true;
            */


            bool isSceneViewCamera = cameraData.isSceneViewCamera;// 是否为 editor 中 scene窗口 使用的 camera;
            // 此 struct 包含用来创建 RenderTexture 所需的一切信息。
            // 关于这个 变量:
            // -- 要么根据 context 当场新建一个
            // -- 要么沿用 camera.targetTexture 中的数据, 并做适当调整
            var cameraTargetDescriptor = cameraData.cameraTargetDescriptor;
            int msaaSamples = cameraTargetDescriptor.msaaSamples;// 单像素采样次数
            bool isScaledRender = !Mathf.Approximately(cameraData.renderScale, 1.0f);// 需要 scale render

            // camera target 和 backbuffer 在 dimension 上 是否是兼容的;
            bool isCompatibleBackbufferTextureDimension = cameraTargetDescriptor.dimension == TextureDimension.Tex2D;

            // 启用了 msaa, 且目标平台需要用户手动实现 "msaa 的解析" 时,  本值为 true;
            bool requiresExplicitMsaaResolve = msaaSamples > 1 && PlatformRequiresExplicitMsaaResolve();

            // 是否属于"离屏": (是否要渲染进一个 rt 中)
            bool isOffscreenRender = cameraData.targetTexture != null && !isSceneViewCamera;

            // 是否捕获到了 actions;
            bool isCapturing = cameraData.captureActions != null;

/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            if (cameraData.xr.enabled)
                isCompatibleBackbufferTextureDimension = cameraData.xr.renderTargetDesc.dimension == cameraTargetDescriptor.dimension;
#endif
*/
            
            bool requiresBlitForOffscreenCamera = 
                        cameraData.postProcessEnabled || 
                        cameraData.requiresOpaqueTexture || 
                        requiresExplicitMsaaResolve || 
                        !cameraData.isDefaultViewport;// 不是全屏, 意味着需要一道 pass 来从 "部分viewport" blit 到 "全屏"

            // 这句感觉和下面的 return 重复了...
            if (isOffscreenRender)
                return requiresBlitForOffscreenCamera;


            return  requiresBlitForOffscreenCamera || 
                    isSceneViewCamera || 
                    isScaledRender || 
                    cameraData.isHdrEnabled ||
                    !isCompatibleBackbufferTextureDimension || // 当 camera target 和 backbuffer 在 dimension 上 是不兼容的时
                    isCapturing || 
                    cameraData.requireSrgbConversion;
        }// 函数完__





        bool CanCopyDepth(ref CameraData cameraData)//  读完__
        {
            bool msaaEnabledForCamera = cameraData.cameraTargetDescriptor.msaaSamples > 1;
            bool supportsTextureCopy = SystemInfo.copyTextureSupport != CopyTextureSupport.None;
            bool supportsDepthTarget = RenderingUtils.SupportsRenderTextureFormat(RenderTextureFormat.Depth);
            // 此 camera 不能开启 msaa
            bool supportsDepthCopy = !msaaEnabledForCamera && (supportsDepthTarget || supportsTextureCopy);

            /*
                TODO:  We don't have support to highp Texture2DMS currently and this breaks depth precision.
                currently disabling it until shader changes kick in.
                bool msaaDepthResolve = msaaEnabledForCamera && SystemInfo.supportsMultisampledTextures != 0;
                ---
                暂时还不支持 multi-sample texture2d 来存储 depth, 这会导致 depth 精度丢失;
                未来版本再补上;
            */
            bool msaaDepthResolve = false;
            return supportsDepthCopy || msaaDepthResolve;
        }// 函数完__
    }
}
