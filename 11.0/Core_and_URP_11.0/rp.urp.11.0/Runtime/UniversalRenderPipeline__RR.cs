using System;
using Unity.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditor.Rendering.Universal;
#endif

using UnityEngine.Scripting.APIUpdating;
using Lightmapping = UnityEngine.Experimental.GlobalIllumination.Lightmapping;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;

/*
namespace UnityEngine.Rendering.LWRP
{
    [Obsolete("LWRP -> Universal (UnityUpgradable) -> UnityEngine.Rendering.Universal.UniversalRenderPipeline", true)]
    public class LightweightRenderPipeline
    {
        public LightweightRenderPipeline(LightweightRenderPipelineAsset asset)
        {
        }
    }
}
*/

namespace UnityEngine.Rendering.Universal
{
    /*
        全 urp 中唯一一个 "RenderPipeline" 派生类;
    */
    public sealed partial class UniversalRenderPipeline //UniversalRenderPipeline__RR_1
        : RenderPipeline
    {

        // 暂没见被使用
        public const string k_ShaderTagName = "UniversalPipeline";


        private static class Profiling
        {
            private static Dictionary<int, ProfilingSampler> s_HashSamplerCache = new Dictionary<int, ProfilingSampler>();
            public static readonly ProfilingSampler unknownSampler = new ProfilingSampler("Unknown");


            /*
                Specialization for camera loop to avoid allocations.
                --
                将每个 camera 的 (cameraId,ProfilingSampler) 信息存储起来, 以便下次访问时直接使用,
                避免多次分配;
            */
            public static ProfilingSampler TryGetOrAddCameraSampler(Camera camera)// 读完__
            {
                // 暂未在任何地方见到这个 宏 的定义
                #if UNIVERSAL_PROFILING_NO_ALLOC
                    return unknownSampler;
                #else
                    ProfilingSampler ps = null;
                    int cameraId = camera.GetHashCode();
                    bool exists = s_HashSamplerCache.TryGetValue(cameraId, out ps);
                    if (!exists)
                    {
                        // NOTE: camera.name allocates!
                        ps = new ProfilingSampler($"{nameof(UniversalRenderPipeline)}.{nameof(RenderSingleCamera)}: {camera.name}");
                        s_HashSamplerCache.Add(cameraId, ps);
                    }
                    return ps;
                #endif
            }// 函数完__



            public static class Pipeline
            {
                // TODO: Would be better to add Profiling name hooks into RenderPipeline.cs, requires changes outside of Universal.
#if UNITY_2021_1_OR_NEWER
                public static readonly ProfilingSampler beginContextRendering  = new ProfilingSampler($"{nameof(RenderPipeline)}.{nameof(BeginContextRendering)}");// "RenderPipeline.BeginContextRendering"
                public static readonly ProfilingSampler endContextRendering    = new ProfilingSampler($"{nameof(RenderPipeline)}.{nameof(EndContextRendering)}");// "RenderPipeline.EndContextRendering"
#else
                /*   tpr
                public static readonly ProfilingSampler beginFrameRendering  = new ProfilingSampler($"{nameof(RenderPipeline)}.{nameof(BeginFrameRendering)}");
                public static readonly ProfilingSampler endFrameRendering    = new ProfilingSampler($"{nameof(RenderPipeline)}.{nameof(EndFrameRendering)}");
                */
#endif
                public static readonly ProfilingSampler beginCameraRendering = new ProfilingSampler($"{nameof(RenderPipeline)}.{nameof(BeginCameraRendering)}");// "RenderPipeline.BeginCameraRendering"
                public static readonly ProfilingSampler endCameraRendering   = new ProfilingSampler($"{nameof(RenderPipeline)}.{nameof(EndCameraRendering)}");// "RenderPipeline.EndCameraRendering"

                const string k_Name = nameof(UniversalRenderPipeline);
                public static readonly ProfilingSampler initializeCameraData           = new ProfilingSampler($"{k_Name}.{nameof(InitializeCameraData)}");// "UniversalRenderPipeline.InitializeCameraData"
                public static readonly ProfilingSampler initializeStackedCameraData    = new ProfilingSampler($"{k_Name}.{nameof(InitializeStackedCameraData)}");// "UniversalRenderPipeline.InitializeStackedCameraData"
                public static readonly ProfilingSampler initializeAdditionalCameraData = new ProfilingSampler($"{k_Name}.{nameof(InitializeAdditionalCameraData)}");// "UniversalRenderPipeline.InitializeAdditionalCameraData"
                public static readonly ProfilingSampler initializeRenderingData        = new ProfilingSampler($"{k_Name}.{nameof(InitializeRenderingData)}");// "UniversalRenderPipeline.InitializeRenderingData"
                public static readonly ProfilingSampler initializeShadowData           = new ProfilingSampler($"{k_Name}.{nameof(InitializeShadowData)}");// "UniversalRenderPipeline.InitializeShadowData"
                public static readonly ProfilingSampler initializeLightData            = new ProfilingSampler($"{k_Name}.{nameof(InitializeLightData)}");// "UniversalRenderPipeline.InitializeLightData"
                public static readonly ProfilingSampler getPerObjectLightFlags         = new ProfilingSampler($"{k_Name}.{nameof(GetPerObjectLightFlags)}");// "UniversalRenderPipeline.GetPerObjectLightFlags"
                public static readonly ProfilingSampler getMainLightIndex              = new ProfilingSampler($"{k_Name}.{nameof(GetMainLightIndex)}");// "UniversalRenderPipeline.GetMainLightIndex"
                public static readonly ProfilingSampler setupPerFrameShaderConstants   = new ProfilingSampler($"{k_Name}.{nameof(SetupPerFrameShaderConstants)}");// "UniversalRenderPipeline.SetupPerFrameShaderConstants"

                public static class Renderer
                {
                    const string k_Name = nameof(ScriptableRenderer);
                    public static readonly ProfilingSampler setupCullingParameters = new ProfilingSampler($"{k_Name}.{nameof(ScriptableRenderer.SetupCullingParameters)}");// "ScriptableRenderer.SetupCullingParameters"
                    public static readonly ProfilingSampler setup                  = new ProfilingSampler($"{k_Name}.{nameof(ScriptableRenderer.Setup)}");// "ScriptableRenderer.Setup"
                };

                public static class Context
                {
                    const string k_Name = nameof(Context);
                    public static readonly ProfilingSampler submit = new ProfilingSampler($"{k_Name}.{nameof(ScriptableRenderContext.Submit)}");
                };

                public static class XR
                {
                    public static readonly ProfilingSampler mirrorView = new ProfilingSampler("XR Mirror View");
                };
            };//Pipeline end
        }//Profiling end



/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
        internal static XRSystem m_XRSystem = new XRSystem();
#endif
*/
        public static float maxShadowBias
        {
            get => 10.0f;
        }

        public static float minRenderScale
        {
            get => 0.1f;
        }

        public static float maxRenderScale
        {
            get => 2.0f;
        }

        // Amount of Lights that can be shaded per object (in the for loop in the shader)
        public static int maxPerObjectLights
        {
            // No support to bitfield mask and int[] in gles2. Can't index fast more than 4 lights.
            // Check Lighting.hlsl for more details.
            get => (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2) ? 4 : 8;
        }

        // These limits have to match same limits in Input.hlsl
        internal const int k_MaxVisibleAdditionalLightsMobileShaderLevelLessThan45 = 16;
        internal const int k_MaxVisibleAdditionalLightsMobile    = 32;
        internal const int k_MaxVisibleAdditionalLightsNonMobile = 256;


        // 16, 32, or 256
        public static int maxVisibleAdditionalLights
        {
            get
            {
                bool isMobile = Application.isMobilePlatform;
                if( isMobile && 
                    (
                        SystemInfo.graphicsDeviceType==GraphicsDeviceType.OpenGLES2 || 
                        (SystemInfo.graphicsDeviceType==GraphicsDeviceType.OpenGLES3 && Graphics.minOpenGLESVersion<=OpenGLESVersion.OpenGLES30)
                    )
                ){
                    return k_MaxVisibleAdditionalLightsMobileShaderLevelLessThan45; // 16
                }

                // GLES can be selected as platform on Windows (not a mobile platform) but uniform buffer size so we must use a low light count.
                return 
                    (
                        isMobile || 
                        SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore || 
                        SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 || 
                        SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3
                    )
                    ? k_MaxVisibleAdditionalLightsMobile    // 32
                    : k_MaxVisibleAdditionalLightsNonMobile;// 256 
            }
        }

        /*
            构造函数 -----------------------------------------------------------------------:
        */
        public UniversalRenderPipeline(UniversalRenderPipelineAsset asset)
        {
            SetSupportedRenderingFeatures();

            // In QualitySettings.antiAliasing disabled state uses value 0, where in URP 1
            int qualitySettingsMsaaSampleCount = QualitySettings.antiAliasing > 0 ? QualitySettings.antiAliasing : 1;
            bool msaaSampleCountNeedsUpdate = qualitySettingsMsaaSampleCount != asset.msaaSampleCount;

            // Let engine know we have MSAA on for cases where we support MSAA backbuffer
            if (msaaSampleCountNeedsUpdate)
            {
                QualitySettings.antiAliasing = asset.msaaSampleCount;
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
                XRSystem.UpdateMSAALevel(asset.msaaSampleCount);
#endif
*/
            }

/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            XRSystem.UpdateRenderScale(asset.renderScale);
#endif
*/
            // For compatibility reasons we also match old LightweightPipeline tag.
            Shader.globalRenderPipeline = "UniversalPipeline,LightweightPipeline";

            Lightmapping.SetDelegate(lightsDelegate);

            CameraCaptureBridge.enabled = true;

            RenderingUtils.ClearSystemInfoCache();
        }// 函数完__






        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Shader.globalRenderPipeline = "";
            SupportedRenderingFeatures.active = new SupportedRenderingFeatures();
            ShaderData.instance.Dispose();
            DeferredShaderData.instance.Dispose();
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            m_XRSystem?.Dispose();
#endif
*/

#if UNITY_EDITOR
            SceneViewDrawMode.ResetDrawMode();
#endif
            Lightmapping.ResetDelegate();
            CameraCaptureBridge.enabled = false;
        }// 函数完__



    // ============================================ Render() =======================================================:
    // 区别仅仅是 参数 cameras 的类型是 array 还是 List<>;

    // 由本类来自定义 Render() 内容
    // 参数中的 context 和 cameras 则由 unity 提供过来
    // 本函数由 unity 自动调用

#if UNITY_2021_1_OR_NEWER
        protected override void Render(ScriptableRenderContext renderContext,  Camera[] cameras)//   读完__
        {
            Render(renderContext, new List<Camera>(cameras));
        }
#endif

#if UNITY_2021_1_OR_NEWER
        protected override void Render(ScriptableRenderContext renderContext, List<Camera> cameras) //  读完__
#else
        protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
        
#endif
        {
            /*
                TODO: Would be better to add Profiling name hooks into RenderPipelineManager.
                C#8 feature, only in >= 2020.2
                --
                (笔记查找 "using var") 此 "未托管资源" 会一直存在, 直到函数体结束 才被释放; (它不是没有用)
            */
            using var profScope = new ProfilingScope(null, ProfilingSampler.Get(URPProfileId.UniversalRenderTotal));

#if UNITY_2021_1_OR_NEWER
            using (new ProfilingScope(null, Profiling.Pipeline.beginContextRendering))
            {
                // --- 回调函数 触发点 ---: 
                // 触发并执行: 所有绑定到委托 "RenderPipelineManager.beginContextRendering 和 beginFrameRendering" 上的 callbacks;
                BeginContextRendering(renderContext, cameras);
            }
#else
            /*   tpr
            using (new ProfilingScope(null, Profiling.Pipeline.beginFrameRendering))
            {
                BeginFrameRendering(renderContext, cameras);
            }
            */
#endif

            // Light intensity 会被乘以 线性color值; 
            GraphicsSettings.lightsUseLinearIntensity = (QualitySettings.activeColorSpace == ColorSpace.Linear);
            GraphicsSettings.useScriptableRenderPipelineBatching = asset.useSRPBatcher; // SRP Batcher

            // 将每一帧的 "shader const global 数据" 写入 shader;
            SetupPerFrameShaderConstants();
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            // Update XR MSAA level per frame.
            XRSystem.UpdateMSAALevel(asset.msaaSampleCount);
#endif
*/
            // cameras 排序, 依照 "camera.depth" 从小到大;
            SortCameras(cameras);


            // ============================== 遍历每个 camera ===============================:
#if UNITY_2021_1_OR_NEWER
            for (int i = 0; i < cameras.Count; ++i)
#else
            /*   tpr
            for (int i = 0; i < cameras.Length; ++i)
            */
#endif
            {
                var camera = cameras[i]; // base or overlay;
                if (IsGameCamera(camera))
                {
                    //  核心 !!!!
                    // 传入的参数如果为 overlay camera, 此函数会立即返回; 这些 overlay cameras 会在后续的 stack 内被渲染;
                    RenderCameraStack(renderContext, camera);
                }
                else
                {// ----- 这些是 editor 中 杂七杂八的 camera -----:
                    using (new ProfilingScope(null, Profiling.Pipeline.beginCameraRendering))
                    {
                        // --- 回调函数 触发点 ---: 
                        // 触发并执行: 所有绑定到委托 "RenderPipelineManager.beginCameraRendering" 上的 callbacks;
                        BeginCameraRendering(renderContext, camera);
                    }
/*        tpr
// 如果 package: "com.unity.visualeffectgraph" 版本大于 0.0.1
#if VISUAL_EFFECT_GRAPH_0_0_1_OR_NEWER
                    //It should be called before culling to prepare material. 
                    //When there isn't any VisualEffect component, this method has no effect.
                    VFX.VFXManager.PrepareCamera(camera);
#endif
*/
                    UpdateVolumeFramework(camera, null);

                    // 调用-1-: 只针对 base camera;
                    RenderSingleCamera(renderContext, camera);

                    using (new ProfilingScope(null, Profiling.Pipeline.endCameraRendering))
                    {
                        // --- 回调函数 触发点 ---: 
                        // 触发并执行: 所有绑定到委托 "RenderPipelineManager.endCameraRendering" 上的 callbacks;
                        EndCameraRendering(renderContext, camera);
                    }
                }
            }// cameras loop end


#if UNITY_2021_1_OR_NEWER
            using (new ProfilingScope(null, Profiling.Pipeline.endContextRendering))
            {
                // --- 回调函数 触发点 ---: 
                // 触发并执行: 所有绑定到委托 "RenderPipelineManager.endContextRendering 和 endFrameRendering" 上的 callbacks;
                EndContextRendering(renderContext, cameras);
            }
#else
            /*   tpr
            using (new ProfilingScope(null, Profiling.Pipeline.endFrameRendering))
            {
                EndFrameRendering(renderContext, cameras);
            }
            */
#endif
        }// 函数完__




        /*
            -1-
            Standalone camera rendering. Use this to render procedural cameras.
            本函数体内不调用 "BeginCameraRendering()" and "EndCameraRendering()" callbacks.

            只有 base camera 可调用本重载; 只是另一个重载 的套壳;

            本重载仅被调用一次, 用来处理 editor 中的 "非 Game cameras"
        */
        public static void RenderSingleCamera(//      读完__
                                            ScriptableRenderContext context, 
                                            Camera camera // 必须是 base camera
        ){
            UniversalAdditionalCameraData additionalCameraData = null;
            if (IsGameCamera(camera))// 不会进入此分支...
                // 尝试获得 camera 所在 go 现成的 additionalCameraData 组件;
                camera.gameObject.TryGetComponent(out additionalCameraData);


            if (additionalCameraData!=null && additionalCameraData.renderType!=CameraRenderType.Base)
            {
                Debug.LogWarning("Only Base cameras can be rendered with standalone RenderSingleCamera. Camera will be skipped.");
                return;
            }

            InitializeCameraData(
                camera,                 // must be base camera
                additionalCameraData,   // 与参数 camera 关联的, 也可能是个 null
                true,                   // 参数 camera 是不是 stack 中的最后一个;
                out var cameraData      // 输出值
            );

/*       tpr
// 如果 package: "com.unity.adaptiveperformance" 版本大于 2.0.0
#if ADAPTIVE_PERFORMANCE_2_0_0_OR_NEWER
            if (asset.useAdaptivePerformance)
                ApplyAdaptivePerformance(ref cameraData);
#endif
*/
            RenderSingleCamera(context, cameraData, cameraData.postProcessEnabled);// -2-
        }// 函数完__ -1-




        
        /// <returns> 如果目标 camera 不能渲染, 返回 false; (empty viewport rectangle, invalid clip plane setup etc.).
        /// </returns>
        static bool TryGetCullingParameters(CameraData cameraData, out ScriptableCullingParameters cullingParams)// 读完__
        {
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            if (cameraData.xr.enabled)
            {
                cullingParams = cameraData.xr.cullingParams;
                // Sync the FOV on the camera to match the projection from the XR device
                if (!cameraData.camera.usePhysicalProperties)
                    cameraData.camera.fieldOfView = Mathf.Rad2Deg * Mathf.Atan(1.0f / cullingParams.stereoProjectionMatrix.m11) * 2.0f;
                return true;
            }
#endif
*/
            return cameraData.camera.TryGetCullingParameters(
                false,              // Generate single-pass stereo aware culling parameters. 非 xr 版
                out cullingParams   // Resultant "culling parameters".  输出端;
            );
        }// 函数完__



        /*
            -2-
            Renders a single camera. This method will do culling, setup and execution of the renderer.

            base / overlay camera 皆可
        */
        /// <param name="cameraData">Camera rendering data. This might contain data inherited from a base camera.</param>
        /// <param name="anyPostProcessingEnabled">
        ///     base camera 以及 stack 中的所有 overlay camera, 任意一个支持后处理, 此值即为 true;
        ///     (若此 base camera 启用了 stack 的话, 才会考虑 overlay cameras)
        /// </param>
        static void RenderSingleCamera(//   读完__
                                        ScriptableRenderContext context, 
                                        CameraData cameraData, // base / overlay camera 皆可
                                        bool anyPostProcessingEnabled
        ){
            Camera camera = cameraData.camera; // base / overlay camera 皆可
            var renderer = cameraData.renderer; // 比如 "Forward Renderer", or "Renderer2D"
            if (renderer == null)
            {
                Debug.LogWarning(string.Format("Trying to render {0} with an invalid renderer. Camera rendering will be skipped.", 
                    camera.name));
                return;
            }

            // 获得 cullingParameters:
            if( !TryGetCullingParameters(
                    cameraData, 
                    out ScriptableCullingParameters cullingParameters)
            )
                // 说明目标 camera 无法渲染,  直接放弃
                return;

            ScriptableRenderer.current = renderer;

            // 是否为 editor: scene 窗口使用的 camera;
            bool isSceneViewCamera = cameraData.isSceneViewCamera;

            /*
                NOTE: Do NOT mix ScriptableCullingParameters with named CommandBuffers i.e. CommandBufferPool.Get("name").
                Currently there's an issue which results in mismatched markers.
                The named CommandBuffer will close its "profiling scope" on execution.
                That will orphan ProfilingScope markers as the named CommandBuffer markers are their parents.
                Resulting in following pattern:
                exec(cmd.start, scope.start, cmd.end) and exec(cmd.start, scope.end, cmd.end)
                ---
                注意:
                不要将 "ScriptableCullingParameters" 和 "命名版 CommandBuffers" (比如 CommandBufferPool.Get("name")) 混淆;
                当前存在一个问题, 它会得到不必配的 markers;
                在运行时, "命名版 CommandBuffers" 会关闭自己的 "profiling scope"; 这将孤立 ProfilingScope markers，
                因为命名的 CommandBuffer markers 是它们的父级。从而导致以下 pattern:
                exec(cmd.start, scope.start, cmd.end) and exec(cmd.start, scope.end, cmd.end)
            */
            CommandBuffer cmd = CommandBufferPool.Get();

            /*
                TODO: move skybox code from C++ to URP in order to remove the call to context.Submit() inside DrawSkyboxPass
                Until then, we can't use nested profiling scopes with XR multipass
                --
                TODO: 将 Skybox 代码从 C++ 移至 URP，以删除对 DrawSkyboxPass 内的 context.Submit() 的调用;
                在此之前，我们不能将 "nested profiling scopes" 与 "XR multipass" 一起使用
            */
            CommandBuffer cmdScope = cameraData.xr.enabled ? null : cmd; // 非 xr 程序, 得到 cmd

            ProfilingSampler sampler = Profiling.TryGetOrAddCameraSampler(camera);
            // Enqueues a "BeginSample" command into the CommandBuffer cmd
            // 等同于调用 "cmdScope.BeginSample()"
            using (new ProfilingScope(cmdScope, sampler)) 
            {

                renderer.Clear(cameraData.renderType);

                using (new ProfilingScope(cmd, Profiling.Pipeline.Renderer.setupCullingParameters))
                {
                    /*
                        改写 "参数 cullingParameters" 中的一部分数据;
                        -1- 是否 cull "shadow caster objs"
                        -2- 设置 maximumVisibleLights 数量 ( main light + add lights 数量 )
                        -3- 设置 shadowDistance
                    */ 
                    renderer.SetupCullingParameters(ref cullingParameters, ref cameraData);
                }

                // Send all the commands enqueued so far in the CommandBuffer cmd, to the context;
                context.ExecuteCommandBuffer(cmd); 
                cmd.Clear();

#if UNITY_EDITOR
                // Emit scene view UI
                if (isSceneViewCamera)// 是否为 editor: scene 窗口 使用的 camera;
                {
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
                }
#endif

                // 执行真正的 Cull !!!!!
                CullingResults cullResults = context.Cull(ref cullingParameters);

                // 初始化 "参数 renderingData" 中的全部数据;
                InitializeRenderingData(asset, ref cameraData, ref cullResults, anyPostProcessingEnabled, out RenderingData renderingData);

/*         tpr
// 如果 package: "com.unity.adaptiveperformance" 版本大于 2.0.0
#if ADAPTIVE_PERFORMANCE_2_0_0_OR_NEWER
                if (asset.useAdaptivePerformance)
                    ApplyAdaptivePerformance(ref renderingData);  
#endif
*/
                using (new ProfilingScope(cmd, Profiling.Pipeline.Renderer.setup))
                {
                    renderer.Setup(context, ref renderingData);
                }

                // Timing scope inside
                renderer.Execute(context, ref renderingData);
            }// 等同于调用 "cmdScope.EndSample()"


            cameraData.xr.EndCamera(cmd, cameraData);// xr

            // Sends to ScriptableRenderContext all the commands enqueued since cmd.Clear, i.e the "EndSample" command
            context.ExecuteCommandBuffer(cmd); 
            CommandBufferPool.Release(cmd);

            using (new ProfilingScope(cmd, Profiling.Pipeline.Context.submit))
            {
                // Actually execute the commands that we previously sent to the ScriptableRenderContext context
                // 抛开 xr 代码, 这是全局唯一一个 "Submit()" 调用;
                context.Submit();
            }

            ScriptableRenderer.current = null;
        }// 函数完__ -2-




        /*
            ======================================< RenderCameraStack >============================================:
            Renders a camera stack. This method calls "RenderSingleCamera()" for each valid camera in the stack.
            The last camera resolves the final target to screen.
        */
        /// <param name="baseCamera"> 传入的 camera 可能是 base 或 overlay; 只不过, overlay camera 会在本函数内被剔除掉; </param>
        static void RenderCameraStack(//        读完__
                                ScriptableRenderContext context, 
                                Camera baseCamera 
        ){
            using var profScope = new ProfilingScope(null, ProfilingSampler.Get(URPProfileId.RenderCameraStack));

            // urp 中, camera 一定自带一个 camera add data 组件;
            // 只不过, 通常我们不不会手动改写这个 组件中的内容; (当然你可以改写它)
            baseCamera.TryGetComponent<UniversalAdditionalCameraData>(out var baseCameraAdditionalData);

            // Overlay cameras will be rendered stacked while rendering base cameras
            // 若参数 baseCamera 为 overlay, 直接返回;  因为他们将在各个 camera stack 内被渲染;
            if (baseCameraAdditionalData != null && baseCameraAdditionalData.renderType == CameraRenderType.Overlay)
                return;

            // ----- 接下来, baseCamera 一定代表一个 base camera !!! -----:

            // renderer contains a stack if it has additional data and the renderer supports stacking
            var renderer = baseCameraAdditionalData?.scriptableRenderer;// 如 "Forward Renderer"
            
            // ForwardRenderer 中, 此值为 true;
            bool supportsCameraStacking = renderer != null && renderer.supportedRenderingFeatures.cameraStacking;

            // 原则上此处得到的 stack, 里面存储的都是 overlay camera; (猜测就是 inspector 中手动绑定的那堆)
            List<Camera> cameraStack = (supportsCameraStacking) ? baseCameraAdditionalData?.cameraStack : null;

            // base camera 或 stack 中的任意一个 overlay camera 支持后处理,  此值即为 true 
            bool anyPostProcessingEnabled = baseCameraAdditionalData != null && baseCameraAdditionalData.renderPostProcessing;

            /*
                We need to know the "last active camera" in the stack to be able to resolve rendering to screen when rendering it. 
                The last camera in the stack is not necessarily the last active one as it users might disable it.
                ---
                查找 stack 中最后一个 "active camera" 的 idx; 它一定是个 overlay camera;
                同时把 stack 中所有值为 null 的元素剔除掉;
                ---
                tpr:
                    但是, 此处有个 bug: 在寻找到 lastActiveOverlayCameraIndex 之后, 还可能执行一次 "UpdateCameraStack()", 
                    它可能删除 stack 中的元素, 进而导致 lastActiveOverlayCameraIndex 不再准确; 到 12.1 中仍未修正;
            */

            int lastActiveOverlayCameraIndex = -1;
            if (cameraStack != null)
            {
                // Object.GetType(), 得到调用者的 Type 类型实例; 通常为: "ForwardRenderer"
                var baseCameraRendererType = baseCameraAdditionalData?.scriptableRenderer.GetType();

                // 若在 cameraStack 中找到值为 null 的元素, 此值为 true;
                bool shouldUpdateCameraStack = false;

                for (int i = 0; i < cameraStack.Count; ++i)// 遍历每个 stack 中的 overlay camera
                {
                    Camera currCamera = cameraStack[i];
                    if (currCamera == null)
                    {
                        shouldUpdateCameraStack = true;
                        continue;
                    }

                    if (currCamera.isActiveAndEnabled)
                    {
                        currCamera.TryGetComponent<UniversalAdditionalCameraData>(out var data);

                        if (data == null || data.renderType != CameraRenderType.Overlay)
                        {
                            // 仅仅是警告并 忽略掉这个 base camera;
                            Debug.LogWarning(string.Format("Stack can only contain Overlay cameras. {0} will skip rendering.", currCamera.name));
                            continue;
                        }

                        // Object.GetType() 通常为: "ForwardRenderer"
                        var currCameraRendererType = data?.scriptableRenderer.GetType();

                        // 如果 base camera 和 stack中的 overlay camera 两者绑定的 renderer 类型不同; 
                        // 如果两者都是 "ForwardRenderer", 或都是 "Renderer2D", 都能允许; 
                        // 如果两者中有一个为 "Renderer2D", 另一个未知, 也允许,
                        // 如果两者 即 "都不是ForwardRenderer", 也 "都不是Renderer2D": 就会曝出警告, 并忽略这个 overlay camera;
                        if (currCameraRendererType != baseCameraRendererType)
                        {
                            var renderer2DType = typeof(Experimental.Rendering.Universal.Renderer2D);
                            if (currCameraRendererType != renderer2DType && baseCameraRendererType != renderer2DType)
                            {
                                Debug.LogWarning(string.Format("Only cameras with compatible renderer types can be stacked. {0} will skip rendering", currCamera.name));
                                continue;
                            }
                        }

                        // 现在能确认, currCamera 是一个 active overlay camera;

                        anyPostProcessingEnabled |= data.renderPostProcessing;
                        lastActiveOverlayCameraIndex = i;
                    }
                }
                if (shouldUpdateCameraStack)
                {
                    // 此处对 stack 元素的修改, 可能导致 lastActiveOverlayCameraIndex 失效;
                    // 但是这个 bug 在 12.1 中仍存在;
                    baseCameraAdditionalData.UpdateCameraStack();// 把 camera stack 中所有值为 null 的元素剔除掉
                }
            }

            // Post-processing not supported in GLES2.
            anyPostProcessingEnabled &= SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES2;

            bool isStackedRendering = lastActiveOverlayCameraIndex != -1;// 如果 stack 不为空, 此值为 true

            using (new ProfilingScope(null, Profiling.Pipeline.beginCameraRendering))
            {
                // --- 回调函数 触发点 ---: 
                // 触发并执行: 所有绑定到委托 "RenderPipelineManager.beginCameraRendering" 上的 callbacks;
                BeginCameraRendering(context, baseCamera);
            }

            // Update volumeframework before initializing additional camera data
            // 配置 base camera 的 Volume: "layerMask 和 trigger(Transform)" 两个信息, 并写入 VolumeManager.instance;
            UpdateVolumeFramework(baseCamera, baseCameraAdditionalData);

            // 新建并初始化 base camera 的 CameraData 实例;
            InitializeCameraData(
                baseCamera, 
                baseCameraAdditionalData, 
                !isStackedRendering,    // 只要 stack 中存在有效元素, 本 base camera 就不是最后一个
                out var baseCameraData  // 要装配的 实例;
            );

/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            var originalTargetDesc = baseCameraData.cameraTargetDescriptor;
            var xrActive = false;
            var xrPasses = m_XRSystem.SetupFrame(baseCameraData);
            foreach (XRPass xrPass in xrPasses)
            {
                baseCameraData.xr = xrPass;
                // XRTODO: remove isStereoEnabled in 2021.x
#pragma warning disable 0618
                baseCameraData.isStereoEnabled = xrPass.enabled;
#pragma warning restore 0618
                if (baseCameraData.xr.enabled)
                {
                    xrActive = true;
                    // Helper function for updating cameraData with xrPass Data
                    m_XRSystem.UpdateCameraData(ref baseCameraData, baseCameraData.xr);

                    // Update volume manager to use baseCamera's settings for XR multipass rendering.
                    if (baseCameraData.xr.multipassId > 0)
                    {
                        UpdateVolumeFramework(baseCamera, baseCameraAdditionalData);
                    }
                }
#endif
*/

/*      tpr
// 如果 package: "com.unity.visualeffectgraph" 版本大于 0.0.1
#if VISUAL_EFFECT_GRAPH_0_0_1_OR_NEWER
            //It should be called before culling to prepare material. When there isn't any VisualEffect component, this method has no effect.
            VFX.VFXManager.PrepareCamera(baseCamera);
#endif
*/

/*       tpr
// 如果 package: "com.unity.adaptiveperformance" 版本大于 2.0.0
#if ADAPTIVE_PERFORMANCE_2_0_0_OR_NEWER
            if (asset.useAdaptivePerformance)
                ApplyAdaptivePerformance(ref baseCameraData);
#endif
*/
            // 调用 -2-:
            RenderSingleCamera(context, baseCameraData, anyPostProcessingEnabled);

            using (new ProfilingScope(null, Profiling.Pipeline.endCameraRendering))
            {
                // --- 回调函数 触发点 ---: 
                // 触发并执行: 所有绑定到委托 "RenderPipelineManager.endCameraRendering" 上的 callbacks;
                EndCameraRendering(context, baseCamera);
            }

            if (isStackedRendering)
            {
                for (int i = 0; i < cameraStack.Count; ++i)// 每个 stack 中的 overlay camera
                {
                    var currCamera = cameraStack[i];// overlay camera
                    if (!currCamera.isActiveAndEnabled)
                        continue;

                    currCamera.TryGetComponent<UniversalAdditionalCameraData>(out var currCameraData);
                    // Camera is overlay and enabled
                    if (currCameraData != null)
                    {
                        // Copy base settings from base camera data and initialize initialize remaining specific settings for this camera type.
                        CameraData overlayCameraData = baseCameraData;
                        bool lastCamera = i == lastActiveOverlayCameraIndex;

                        using (new ProfilingScope(null, Profiling.Pipeline.beginCameraRendering))
                        {
                            // --- 回调函数 触发点 ---: 
                            // 触发并执行: 所有绑定到委托 "RenderPipelineManager.beginCameraRendering" 上的 callbacks;
                            BeginCameraRendering(context, currCamera);
                        }
/*     tpr
// 如果 package: "com.unity.visualeffectgraph" 版本大于 0.0.1
#if VISUAL_EFFECT_GRAPH_0_0_1_OR_NEWER
                        //It should be called before culling to prepare material. When there isn't any VisualEffect component, this method has no effect.
                        VFX.VFXManager.PrepareCamera(currCamera);
#endif
*/
                        // 配置 overlay camera 的 Volume: "layerMask 和 trigger(Transform)" 两个信息, 并写入 VolumeManager.instance;
                        UpdateVolumeFramework(currCamera, currCameraData);

                        // 使用 任意 camera (base 或 overlay) 和它的 add data 去初始化最好一个参数 cameraData 中的数据;
                        // -- 仅初始化 非通用数据, (camera stack 中每个camera 都各自独立的数据 )
                        InitializeAdditionalCameraData(
                            currCamera,             // 当前的 overlay camera
                            currCameraData,         // camera add data
                            lastCamera,             // 是否为 stack 中最后一个 camera
                            ref overlayCameraData   // "CameraData" 
                        );
/*   tpr                      
#if ENABLE_VR && ENABLE_XR_MODULE
                        if (baseCameraData.xr.enabled)
                            m_XRSystem.UpdateFromCamera(ref overlayCameraData.xr, overlayCameraData);
#endif
*/
                        // 调用-2-:
                        RenderSingleCamera(context, overlayCameraData, anyPostProcessingEnabled);

                        using (new ProfilingScope(null, Profiling.Pipeline.endCameraRendering))
                        {
                            // --- 回调函数 触发点 ---: 
                            // 触发并执行: 所有绑定到委托 "RenderPipelineManager.endCameraRendering" 上的 callbacks;
                            EndCameraRendering(context, currCamera);
                        }
                    }
                }
            }
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            if (baseCameraData.xr.enabled)
                baseCameraData.cameraTargetDescriptor = originalTargetDesc;
        }

        if (xrActive)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, Profiling.Pipeline.XR.mirrorView))
            {
                m_XRSystem.RenderMirrorView(cmd, baseCamera);
            }

            context.ExecuteCommandBuffer(cmd);
            context.Submit();
            CommandBufferPool.Release(cmd);
        }

        m_XRSystem.ReleaseFrame();
#endif
*/
        }// 函数完__




        // 配置 参数camera 的 Volume: "layerMask 和 trigger(Transform)" 两个信息, 并写入 VolumeManager.instance;
        static void UpdateVolumeFramework(//   读完__
                            Camera camera, // base or overlay 皆可
                            UniversalAdditionalCameraData additionalCameraData// 此数据与 参数 camera 相互绑定
        ){
            using var profScope = new ProfilingScope(null, ProfilingSampler.Get(URPProfileId.UpdateVolumeFramework));

            // Default values when there's no additional camera data available
            LayerMask layerMask = 1; // "Default"
            Transform trigger = camera.transform;

            if (additionalCameraData != null)
            {// ----- 主要分支 -----:
                // defines which Volumes affect this Camera. 对应 inspector 中 Volumes Mask 一项;
                layerMask = additionalCameraData.volumeLayerMask;
                // 对应 inspector 中 Volumes Trigger 一栏; 一般为空 此时本函数返回 null
                trigger = additionalCameraData.volumeTrigger != null
                    ? additionalCameraData.volumeTrigger    // 选用用户设置的 trigger transform
                    : trigger;                              // 选用 camera transform
            }
            else if (camera.cameraType == CameraType.SceneView)
            {// ----- editor 分支 -----:
                // Try to mirror the MainCamera volume layer mask for the scene view - do not mirror the target
                var mainCamera = Camera.main;
                UniversalAdditionalCameraData mainAdditionalCameraData = null;

                if (mainCamera != null && mainCamera.TryGetComponent(out mainAdditionalCameraData))
                    layerMask = mainAdditionalCameraData.volumeLayerMask;

                trigger = mainAdditionalCameraData != null && mainAdditionalCameraData.volumeTrigger != null ? mainAdditionalCameraData.volumeTrigger : trigger;
            }

            VolumeManager.instance.Update(trigger, layerMask);
        }// 函数完__



        
        // SMAA, DepthOfField, MotionBlur 这几种后处理 需要用到 depth buffer, 
        // 若启用任意一种, 本函数返回 true;
        static bool CheckPostProcessForDepth(in CameraData cameraData)// 读完__ 2遍
        {
            if (!cameraData.postProcessEnabled)
                return false;

            if (cameraData.antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing)//SMAA
                return true;

            var stack = VolumeManager.instance.stack;

            if (stack.GetComponent<DepthOfField>().IsActive())
                return true;

            if (stack.GetComponent<MotionBlur>().IsActive())
                return true;

            return false;
        }// 函数完__



        static void SetSupportedRenderingFeatures()
        {
#if UNITY_EDITOR
            SupportedRenderingFeatures.active = new SupportedRenderingFeatures()
            {
                reflectionProbeModes = SupportedRenderingFeatures.ReflectionProbeModes.None,
                defaultMixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeModes.Subtractive,
                mixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeModes.Subtractive | SupportedRenderingFeatures.LightmapMixedBakeModes.IndirectOnly | SupportedRenderingFeatures.LightmapMixedBakeModes.Shadowmask,
                lightmapBakeTypes = LightmapBakeType.Baked | LightmapBakeType.Mixed,
                lightmapsModes = LightmapsMode.CombinedDirectional | LightmapsMode.NonDirectional,
                lightProbeProxyVolumes = false,
                motionVectors = false,
                receiveShadows = false,
                reflectionProbes = true,
                particleSystemInstancing = true
            };
            SceneViewDrawMode.SetupDrawMode();
#endif
        }// 函数完__



        // 为一个 base camera, 新建并初始化它的 CameraData 实例;
        static void InitializeCameraData(   // 读完__  第2遍
                                Camera camera, // must be base camera
                                UniversalAdditionalCameraData additionalCameraData, // 和参数 camera 一起的
                                bool resolveFinalTarget, // 参数 camera 是不是 stack 中的最后一个;
                                out CameraData cameraData
        ){
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeCameraData);

            cameraData = new CameraData();

            // 使用 Base camera 和它的 add data 去初始化参数 cameraData 中的数据;
            // -- 仅初始化 通用数据, (camera stack 中每个camera 都相同的数据)
            InitializeStackedCameraData(camera, additionalCameraData, ref cameraData);

            // 使用 任意 camera (base 或 overlay) 和它的 add data 去初始化参数 cameraData 中的数据;
            // -- 仅初始化 非通用数据, (camera stack 中每个camera 都各自独立的数据 )
            InitializeAdditionalCameraData(camera, additionalCameraData, resolveFinalTarget, ref cameraData);

            ///////////////////////////////////////////////////////////////////
            // RenderTextureDescriptor settings                                            /
            ///////////////////////////////////////////////////////////////////

            var renderer = additionalCameraData?.scriptableRenderer; //如 "Forward Renderer"
            bool rendererSupportsMSAA = renderer != null && renderer.supportedRenderingFeatures.msaa;

            int msaaSamples = 1;
            if (camera.allowMSAA && asset.msaaSampleCount > 1 && rendererSupportsMSAA)
                msaaSamples = (camera.targetTexture != null) ? 
                    camera.targetTexture.antiAliasing : // camera 绑定了 rt, 就使用 rt 的配置
                    asset.msaaSampleCount;              // 没绑定 rt, 就是用 asset 的数据
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            // Use XR's MSAA if camera is XR camera. XR MSAA needs special handle here because it is not per Camera.
            // Multiple cameras could render into the same XR display and they should share the same MSAA level.
            if (cameraData.xrRendering && rendererSupportsMSAA)
                msaaSamples = XRSystem.GetMSAALevel();
#endif
*/
            // 右侧原值意思: 是否保留 framebuffer 的 alpha 通道信息 (readonly).
            // 此处左侧变量含义: 选择合适的 texture/render texture 的 GraphicsFormat 类型, 比如是否携带 alpha 通道;
            bool needsAlphaChannel = Graphics.preserveFramebufferAlpha; 
            
            cameraData.cameraTargetDescriptor = CreateRenderTextureDescriptor(
                camera, 
                cameraData.renderScale,
                cameraData.isHdrEnabled, 
                msaaSamples, 
                needsAlphaChannel, 
                cameraData.requiresOpaqueTexture
            );
        }// 函数完__



        /*
            -----------------------------------------------------------------------------:
            Initialize camera data settings common for all cameras in the stack.
            "Overlay cameras" will inherit settings from "base camera".
            --
            使用 Base camera 和它的 add data 去初始化 参数 cameraData 中的数据;
            仅初始化 通用数据, (camera stack 中每个camera 都相同的数据)
        */
        /// <param name="baseCamera"> "Base camera" to inherit settings from.</param>
        /// <param name="baseAdditionalCameraData">Component that contains additional "base camera" data.</param>
        /// <param name="cameraData">Camera data to initialize setttings.</param>
        static void InitializeStackedCameraData( // 读完__ _第2遍_
                Camera baseCamera, 
                UniversalAdditionalCameraData baseAdditionalCameraData, 
                ref CameraData cameraData
        ){
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeStackedCameraData);

            var settings = asset; // "UniversalRenderPipelineAsset"

            cameraData.targetTexture = baseCamera.targetTexture;
            cameraData.cameraType = baseCamera.cameraType;
            bool isSceneViewCamera = cameraData.isSceneViewCamera;// 是否为 editor: scene 窗口 使用的 camera;

            ///////////////////////////////////////////////////////////////////
            // Environment and Post-processing settings                       /
            ///////////////////////////////////////////////////////////////////
            if (isSceneViewCamera)
            {// base camera 是 editor: Scene camera;
                cameraData.volumeLayerMask = 1; // "Default"
                cameraData.volumeTrigger = null;
                cameraData.isStopNaNEnabled = false;
                cameraData.isDitheringEnabled = false;
                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
                cameraData.xrRendering = false;
#endif
*/
            }
            else if (baseAdditionalCameraData != null)
            {// base camera 是 game camera;  且 add camera data 存在, 直接延用它的数据:
                cameraData.volumeLayerMask = baseAdditionalCameraData.volumeLayerMask;
                cameraData.volumeTrigger = baseAdditionalCameraData.volumeTrigger == null ? baseCamera.transform : baseAdditionalCameraData.volumeTrigger;
                cameraData.isStopNaNEnabled = baseAdditionalCameraData.stopNaN && SystemInfo.graphicsShaderLevel >= 35;
                cameraData.isDitheringEnabled = baseAdditionalCameraData.dithering;
                cameraData.antialiasing = baseAdditionalCameraData.antialiasing;
                cameraData.antialiasingQuality = baseAdditionalCameraData.antialiasingQuality;
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
                cameraData.xrRendering = baseAdditionalCameraData.allowXRRendering && m_XRSystem.RefreshXrSdk();
#endif
*/
            }
            else
            {// base camera 是 game camera; 且 add camera data 不存在,;
                cameraData.volumeLayerMask = 1; // "Default"
                cameraData.volumeTrigger = null; // 使用 camera 自己的 transform 充当
                cameraData.isStopNaNEnabled = false;
                cameraData.isDitheringEnabled = false;
                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
                cameraData.xrRendering = m_XRSystem.RefreshXrSdk();
#endif
*/
            }

            ///////////////////////////////////////////////////////////////////
            // Settings that control output of the camera                     /
            ///////////////////////////////////////////////////////////////////

            cameraData.isHdrEnabled = baseCamera.allowHDR && settings.supportsHDR;

            Rect cameraRect = baseCamera.rect;
            cameraData.pixelRect = baseCamera.pixelRect;
            cameraData.pixelWidth = baseCamera.pixelWidth;
            cameraData.pixelHeight = baseCamera.pixelHeight;
            cameraData.aspectRatio = (float)cameraData.pixelWidth / (float)cameraData.pixelHeight;

            // 猜测: 只有当 viewport 为 全屏时, 才算是 default 的, 此值才为 true;
            cameraData.isDefaultViewport = ( !(
                Math.Abs(cameraRect.x) > 0.0f || 
                Math.Abs(cameraRect.y) > 0.0f ||
                Math.Abs(cameraRect.width) < 1.0f || 
                Math.Abs(cameraRect.height) < 1.0f) );

            // Discard variations lesser than kRenderScaleThreshold. 丢弃小于 kRenderScaleThreshold 的变化;
            // Scale is only enabled for gameview.
            // ---
            // 如果 asset.renderScale 十分接近 1.0, 那就设为 1.0;  否则, 沿用 asset.renderScale 的值;
            const float kRenderScaleThreshold = 0.05f;
            cameraData.renderScale = (Mathf.Abs(1.0f - settings.renderScale) < kRenderScaleThreshold) ? 1.0f : settings.renderScale;

#if ENABLE_VR && ENABLE_XR_MODULE
            /*   tpr
            cameraData.xr = m_XRSystem.emptyPass;
            XRSystem.UpdateRenderScale(cameraData.renderScale);
            */
#else
            cameraData.xr = XRPass.emptyPass;
#endif

            var commonOpaqueFlags = SortingCriteria.CommonOpaque; // 不透明物使用的一组排序方式;
            // 在 "CommonOpaque" 的基础上, 少了一种 "QuantizedFrontToBack" 排序; 还是用于 不透明物体的
            var noFrontToBackOpaqueFlags = SortingCriteria.SortingLayer | SortingCriteria.RenderQueue | SortingCriteria.OptimizeStateChanges | SortingCriteria.CanvasOrder;
            
            // 是否是支持 "hidden surface removal". (隐藏面去除) 的 gpu; 
            // 有些 gpu 在渲染 不透明物体时, 支持 "hidden surface removal" 功能;
            // 在这样的 gpu 上运行的程序, 就不必对 不透明物体 执行 "front-to-back" 排序工作了, 以提供性能;
            bool hasHSRGPU = SystemInfo.hasHiddenSurfaceRemovalOnGPU;
            bool canSkipFrontToBackSorting = (baseCamera.opaqueSortMode==OpaqueSortMode.Default && hasHSRGPU) || baseCamera.opaqueSortMode==OpaqueSortMode.NoDistanceSort;

            cameraData.defaultOpaqueSortFlags = canSkipFrontToBackSorting ? noFrontToBackOpaqueFlags : commonOpaqueFlags;

            // 似乎和 camera 录屏相关, 暂时忽视此功能;
            cameraData.captureActions = CameraCaptureBridge.GetCaptureActions(baseCamera);
        }// 函数完__




        /*
            ----------------------------------------------------------------------------:
            Initialize settings that can be different for each camera in the stack.
            ---
            使用 任意 camera (base 或 overlay) 和它的 add data 去初始化 参数 cameraData 中的数据;
            仅初始化 非通用数据, (camera stack 中每个camera 都各自不同的 数据 )
        */
        /// <param name="resolveFinalTarget">True if this is the last camera in the stack and rendering should resolve to camera target.</param>
        /// <param name="cameraData">Settings to be initilized.</param>
        static void InitializeAdditionalCameraData(  //   读完__   第2遍
                                            Camera camera, 
                                            UniversalAdditionalCameraData additionalCameraData, 
                                            bool resolveFinalTarget,  // 参数 camera 是不是 stack 中的最后一个;
                                            ref CameraData cameraData // 设置的对象
        ){
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeAdditionalCameraData);

            var settings = asset;
            cameraData.camera = camera;

            bool anyShadowsEnabled = settings.supportsMainLightShadows || settings.supportsAdditionalLightShadows;
            cameraData.maxShadowDistance = Mathf.Min(settings.shadowDistance, camera.farClipPlane);
            cameraData.maxShadowDistance = (anyShadowsEnabled && cameraData.maxShadowDistance >= camera.nearClipPlane) 
                                            ? cameraData.maxShadowDistance : 0.0f;

            // Getting the background color from preferences to add to the preview camera
#if UNITY_EDITOR
            if (cameraData.camera.cameraType == CameraType.Preview)
            {
                camera.backgroundColor = CoreRenderPipelinePreferences.previewBackgroundColor;
            }
#endif

            // 是否为 editor: scene 窗口 使用的 camera;
            bool isSceneViewCamera = cameraData.isSceneViewCamera;
            if (isSceneViewCamera)
            {// camera 是 editor: Scene camera;
             // 部分数据沿用 asset 的;
                cameraData.renderType = CameraRenderType.Base;
                cameraData.clearDepth = true;
                cameraData.postProcessEnabled = CoreUtils.ArePostProcessesEnabled(camera);
                cameraData.requiresDepthTexture = settings.supportsCameraDepthTexture;
                cameraData.requiresOpaqueTexture = settings.supportsCameraOpaqueTexture;
                cameraData.renderer = asset.scriptableRenderer;
            }
            else if (additionalCameraData != null)
            {// camera 是 game camera;  且 add camera data 存在, 直接延用它的数据:
                cameraData.renderType = additionalCameraData.renderType;
                // base camera 一定设为 true, overlay camera 则使用 inspector 中配置的值;
                cameraData.clearDepth = (additionalCameraData.renderType != CameraRenderType.Base) ? additionalCameraData.clearDepth : true;
                cameraData.postProcessEnabled = additionalCameraData.renderPostProcessing;
                cameraData.maxShadowDistance = (additionalCameraData.renderShadows) ? cameraData.maxShadowDistance : 0.0f;
                cameraData.requiresDepthTexture = additionalCameraData.requiresDepthTexture;
                cameraData.requiresOpaqueTexture = additionalCameraData.requiresColorTexture;
                cameraData.renderer = additionalCameraData.scriptableRenderer;
            }
            else
            {// camera 是 game camera; 且 add camera data 不存在;
                cameraData.renderType = CameraRenderType.Base;
                cameraData.clearDepth = true;
                cameraData.postProcessEnabled = false;
                cameraData.requiresDepthTexture = settings.supportsCameraDepthTexture;
                cameraData.requiresOpaqueTexture = settings.supportsCameraOpaqueTexture;
                cameraData.renderer = asset.scriptableRenderer;
            }

            // Disables post if GLes2;   GLes2 不支持 后处理;
            cameraData.postProcessEnabled &= SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES2;

            // 以下两者情况 一定需要 depth texture
            // -- editor scene 窗口使用的 camera, 
            // -- 或启用以下一种后处理中: SMAA, DepthOfField, MotionBlur;
            cameraData.requiresDepthTexture |= isSceneViewCamera || CheckPostProcessForDepth(cameraData);


            cameraData.resolveFinalTarget = resolveFinalTarget;

            // Disable depth and color copy. We should add it in the renderer instead to avoid performance pitfalls (陷阱)
            // of camera stacking breaking render pass execution implicitly.
            // ---
            // 直观看就是: overlay camera 不支持 depth buffer 和 opaque buffer 的复制
            bool isOverlayCamera = (cameraData.renderType == CameraRenderType.Overlay);
            if (isOverlayCamera)
            {
                cameraData.requiresDepthTexture = false;
                cameraData.requiresOpaqueTexture = false;
            }
            
            /*
                Overlay cameras inherit viewport from base.
                If the viewport is different between them we might need to patch the projection to adjust aspect ratio
                matrix to prevent squishing when rendering objects in overlay cameras.
                --
                专门处理 overlay camera:
                如果 overlay camera 不是正交透视, 同时它的 viewport 和 base camera 不同, 
                那就应该调整这个 overlay camera 的 投影矩阵 的 横纵比, 以防止 overlay camera 渲染出来的画面 是拉伸过的;
            */
            Matrix4x4 projectionMatrix = camera.projectionMatrix;
            if (isOverlayCamera && !camera.orthographic && cameraData.pixelRect != camera.pixelRect)
            {
                /*
                    m11 同样含有 cotangent, 但是此处只修改 m00, 最终只会影响 posHCS.x, 而不会影响 y分量;
                    camera.aspect 是 overlay camera 自己的
                    cameraData.aspectRatio 是 base camera 的;
                    这个计算没有彻底懂, 需要未来实践下;
                */

                // -------
                // m00 = (cotangent / aspect), therefore m00 * aspect gives us cotangent.
                // 可以查书, 此值为 cot(FOV/2)
                float cotangent = camera.projectionMatrix.m00 * camera.aspect;

                // Get new m00 by dividing by base camera aspectRatio.
                float newCotangent = cotangent / cameraData.aspectRatio;
                projectionMatrix.m00 = newCotangent;
            }

            cameraData.SetViewAndProjectionMatrix(camera.worldToCameraMatrix, projectionMatrix);
        }// 函数完__



        /*
            -1- 寻找合适的 main light
            -2- 分别确定 main light / add lights 是否能 cast shadow;
            -3- 初始化 "参数 renderingData" 中的全部数据;
        */
        static void InitializeRenderingData( //   读完__
                                    UniversalRenderPipelineAsset settings, 
                                    ref CameraData cameraData, 
                                    ref CullingResults cullResults,
                                    bool anyPostProcessingEnabled, 
                                    out RenderingData renderingData
        ){
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeRenderingData);

            var visibleLights = cullResults.visibleLights;// NativeArray<VisibleLight>

            // 若找到合适的 main light, 就返回它在 visibleLights 中的 idx;  没找到就返回 -1;
            int mainLightIndex = GetMainLightIndex(settings, visibleLights);
            bool mainLightCastShadows = false;
            bool additionalLightsCastShadows = false;

            if (cameraData.maxShadowDistance > 0.0f)
            {
                mainLightCastShadows = (mainLightIndex!=-1 && // 找到了 main light
                                        visibleLights[mainLightIndex].light!=null && // 找到了 main light
                                        visibleLights[mainLightIndex].light.shadows!=LightShadows.None);

                // If additional lights are not shaded per-pixel they cannot cast shadows
                if (settings.additionalLightsRenderingMode == LightRenderingMode.PerPixel)
                {
                    for (int i = 0; i < visibleLights.Length; ++i)
                    {
                        if (i == mainLightIndex)
                            continue;

                        Light light = visibleLights[i].light;

                        // urp doesn't support additional directional light shadows yet
                        // urp 暂时不支持 "add 平行光" 的阴影;
                        if( (visibleLights[i].lightType==LightType.Spot || visibleLights[i].lightType==LightType.Point) && 
                            light!=null && 
                            light.shadows!=LightShadows.None
                        ){
                            additionalLightsCastShadows = true;
                            break;
                        }
                    }
                }
            }

            renderingData.cullResults = cullResults;
            renderingData.cameraData = cameraData;
            InitializeLightData(settings, visibleLights, mainLightIndex, out renderingData.lightData);
            InitializeShadowData(
                settings, visibleLights, mainLightCastShadows, 
                additionalLightsCastShadows && !renderingData.lightData.shadeAdditionalLightsPerVertex, 
                out renderingData.shadowData
            );
            InitializePostProcessingData(settings, out renderingData.postProcessingData);
            renderingData.supportsDynamicBatching = settings.supportsDynamicBatching;
            renderingData.perObjectData = GetPerObjectLightFlags(renderingData.lightData.additionalLightsCount);
            renderingData.postProcessingEnabled = anyPostProcessingEnabled;
        }// 函数完__




        // 初始化 "参数 shadowData" 中的全部数据, ( 它其实是 renderingData.shadowData )
        static void InitializeShadowData(      //    读完__   第二遍没看细
                                    UniversalRenderPipelineAsset settings, 
                                    NativeArray<VisibleLight> visibleLights, 
                                    bool mainLightCastShadows, 
                                    bool additionalLightsCastShadows, 
                                    out ShadowData shadowData
        ){
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeShadowData);

            m_ShadowBiasData.Clear();
            m_ShadowResolutionData.Clear();

            for (int i = 0; i < visibleLights.Length; ++i)
            {
                Light light = visibleLights[i].light;

                // 一个可以绑定到 light go 上的组件, 用户可在脚本中改写这个 组件中的数据;
                // 这个组件 很可能是不存在的; (用户没有绑定它)
                UniversalAdditionalLightData data = null;
                if (light != null)
                {
                    light.gameObject.TryGetComponent(out data);
                }

                if (data && !data.usePipelineSettings)
                    // 使用 light 的配置数据
                    m_ShadowBiasData.Add(new Vector4(light.shadowBias, light.shadowNormalBias, 0.0f, 0.0f));
                else
                    // 使用 asset 的配置数据
                    m_ShadowBiasData.Add(new Vector4(settings.shadowDepthBias, settings.shadowNormalBias, 0.0f, 0.0f));


                if( data && (data.additionalLightsShadowResolutionTier==UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierCustom))
                {   
                    // native code does not clamp light.shadowResolution between -1 and 3
                    // 很奇怪, 原值是个 enum, 怎么没有转换为 pix 为单位的
                    m_ShadowResolutionData.Add((int)light.shadowResolution); 
                }
                else if( data && (data.additionalLightsShadowResolutionTier!=UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierCustom))
                {
                    int resolutionTier = Mathf.Clamp(
                        data.additionalLightsShadowResolutionTier, 
                        UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow, //0
                        UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh //2
                    );
                    // 此处写入的是分辨率, 以 pix 为单位
                    m_ShadowResolutionData.Add(settings.GetAdditionalLightsShadowResolution(resolutionTier));
                }
                else
                {
                    // 此处写入的是分辨率, 以 pix 为单位
                    m_ShadowResolutionData.Add(settings.GetAdditionalLightsShadowResolution(UniversalAdditionalLightData.AdditionalLightsShadowDefaultResolutionTier));
                }
            }

            shadowData.bias = m_ShadowBiasData;
            shadowData.resolution = m_ShadowResolutionData;

            shadowData.supportsMainLightShadows = SystemInfo.supportsShadows && settings.supportsMainLightShadows && mainLightCastShadows;

            // We no longer use screen space shadows in URP.
            // This change allows us to have particles & transparent objects receive shadows.
#pragma warning disable 0618
            shadowData.requiresScreenSpaceShadowResolve = false;
#pragma warning restore 0618

            // cascade 有几层, 区间[1,4]; (比如: 4个重叠的球体) 
            shadowData.mainLightShadowCascadesCount = settings.shadowCascadeCount;
            shadowData.mainLightShadowmapWidth = settings.mainLightShadowmapResolution;
            shadowData.mainLightShadowmapHeight = settings.mainLightShadowmapResolution;

            switch (shadowData.mainLightShadowCascadesCount)
            {
                case 1:
                    // (1, 0, 0)
                    shadowData.mainLightShadowCascadesSplit = new Vector3(1.0f, 0.0f, 0.0f);
                    break;

                case 2:
                    // ( 0.25, 1, 0 )
                    shadowData.mainLightShadowCascadesSplit = new Vector3(settings.cascade2Split, 1.0f, 0.0f);
                    break;

                case 3:
                    // ( 0.1, 0.3, 0 )
                    shadowData.mainLightShadowCascadesSplit = new Vector3(settings.cascade3Split.x, settings.cascade3Split.y, 0.0f);
                    break;

                default:
                    // (0.067f, 0.2f, 0.467f)
                    shadowData.mainLightShadowCascadesSplit = settings.cascade4Split;
                    break;
            }

            shadowData.supportsAdditionalLightShadows = SystemInfo.supportsShadows && settings.supportsAdditionalLightShadows && additionalLightsCastShadows;
            shadowData.additionalLightsShadowmapWidth = shadowData.additionalLightsShadowmapHeight = settings.additionalLightsShadowmapResolution;
            shadowData.supportsSoftShadows = settings.supportsSoftShadows && (shadowData.supportsMainLightShadows || shadowData.supportsAdditionalLightShadows);
            shadowData.shadowmapDepthBufferBits = 16;
        }// 函数完__



        // 初始化 "参数 postProcessingData" 中的全部数据, ( 它其实是 renderingData.postProcessingData )
        static void InitializePostProcessingData(   // 读完__  第二遍
                                            UniversalRenderPipelineAsset settings, 
                                            out PostProcessingData postProcessingData
        ){
            // 颜色渐变模式
            postProcessingData.gradingMode = settings.supportsHDR
                ? settings.colorGradingMode
                : ColorGradingMode.LowDynamicRange;

            postProcessingData.lutSize = settings.colorGradingLutSize;
            postProcessingData.useFastSRGBLinearConversion = settings.useFastSRGBLinearConversion;
        }// 函数完__



        // 初始化 参数 lightData;  (它是 renderingData.lightData )
        static void InitializeLightData( // 读完__  第二遍
                                    UniversalRenderPipelineAsset settings, 
                                    NativeArray<VisibleLight> visibleLights, 
                                    int mainLightIndex, 
                                    out LightData lightData
        ){
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeLightData);

            int maxPerObjectAdditionalLights = UniversalRenderPipeline.maxPerObjectLights;// 4 or 8
            int maxVisibleAdditionalLights = UniversalRenderPipeline.maxVisibleAdditionalLights;// 16, 32, or 256

            lightData.mainLightIndex = mainLightIndex;

            if (settings.additionalLightsRenderingMode != LightRenderingMode.Disabled)
            {// mode: PerVertex or PerPixel
                lightData.additionalLightsCount = Math.Min(   
                    (mainLightIndex!=-1) ? visibleLights.Length-1 : visibleLights.Length,
                    maxVisibleAdditionalLights // 16, 32, or 256
                );

                lightData.maxPerObjectAdditionalLightsCount = Math.Min( 
                    settings.maxAdditionalLightsCount, // [0,8], 默认为4
                    maxPerObjectAdditionalLights       // 4 or 8
                );
            }
            else
            {// mode: Disabled
                lightData.additionalLightsCount = 0;
                lightData.maxPerObjectAdditionalLightsCount = 0;
            }

            lightData.shadeAdditionalLightsPerVertex = settings.additionalLightsRenderingMode==sLightRenderingMode.PerVertex;
            lightData.visibleLights = visibleLights;
            lightData.supportsMixedLighting = settings.supportsMixedLighting;
        }// 函数完__


        /*
            配置一个 PerObjectData 实例 并返回之;
        */
        /// <param name="additionalLightsCount"> add light 的数量 </param>
        /// <returns> 配置好的 PerObjectData 实例 </returns>
        static PerObjectData GetPerObjectLightFlags(int additionalLightsCount)//   读完__
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.getPerObjectLightFlags);

            var configuration = PerObjectData.ReflectionProbes | 
                                PerObjectData.Lightmaps | 
                                PerObjectData.LightProbe | 
                                PerObjectData.LightData |       // 逐物体-光信息
                                PerObjectData.OcclusionProbe |  // 即: light probe(occlusion)
                                PerObjectData.ShadowMask;

            if (additionalLightsCount > 0)
            {
                configuration |= PerObjectData.LightData; // 感觉重复了...

                // In this case we also need per-object indices (unity_LightIndices)
                //  "per-object light indices" 是一个 unity 自带系统, 可在笔记中搜索此关键词;
                //  catlike: 此系统存在问题, 最好别用;
                if (!RenderingUtils.useStructuredBuffer) // 成立
                    configuration |= PerObjectData.LightIndices;
            }

            return configuration;
        }// 函数完__




        /*
            Main Light is always a directional light
            --
            寻找 visibleLights 中的 main light; 返回其在 visibleLights 中的 idx:

            --  如果 "RenderSettings.sun" 是一个位于 visibleLights 中的平行光, (潜在地说明它是 enable 的)
                那就选用 sun, 哪怕它并不是 候选者中 最亮的平行光

            --  如果 visibleLights 中存在平行光, 但 "RenderSettings.sun" 并不在里面 
                (要么是 disable 的, 要么不是平行光)
                就从这些 可选的平行光中, 选择最亮的那栈;

            -- 如果以上都不符合, 就说明没找到合适的 main light, 此时返回 -1;
                比如: 没有手动设置 "sun", 且 visibleLights 中没有平行光;
        */
        static int GetMainLightIndex(UniversalRenderPipelineAsset settings, NativeArray<VisibleLight> visibleLights)// 读完__ 第二遍
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.getMainLightIndex);

            int totalVisibleLights = visibleLights.Length;

            // 失败
            if (totalVisibleLights == 0 || settings.mainLightRenderingMode!=LightRenderingMode.PerPixel)
                return -1;

            // 要么是用户显式设置的(此时可以是任意类型, 用户绑定啥就是啥, 哪怕此光不是 平行光, 哪怕是 disable 的), 
            // 要么是 unity自动找到的 "场景中最亮的平行光";
            Light sunLight = RenderSettings.sun; 
            int brightestDirectionalLightIndex = -1;
            float brightestLightIntensity = 0.0f;
            for (int i = 0; i < totalVisibleLights; ++i)
            {
                VisibleLight currVisibleLight = visibleLights[i];
                Light currLight = currVisibleLight.light;

                /*
                    Particle system lights have the light property as null. 
                    We sort lights so all particles lights come last. 
                    Therefore, if first light is particle light then all lights are particle lights.
                    In this case we either have no main light or already found it.
                    ---
                    粒子系统的 visibleLight, 其 light 成员值为 null;
                    按照这里的说法, 这些 lights 是经过排序的 ?, 如果访问到了 null, 就说明后面的都是 粒子系统光了;
                */
                if (currLight == null)
                    break;

                if (currVisibleLight.lightType == LightType.Directional)
                {
                    // Sun source needs be a directional light
                    // 只有位于 visibleLights 中的 "sun", 才是有效的, 立即使用
                    if (currLight == sunLight)
                        return i;

                    // In case no sun light is present we will return the brightest directional light
                    if (currLight.intensity > brightestLightIntensity)
                    {
                        brightestLightIntensity = currLight.intensity;
                        brightestDirectionalLightIndex = i;
                    }
                }
            }
            return brightestDirectionalLightIndex;
        }// 函数完__




        // 将每一帧的 "shader const global 数据" 写入 shader;
        static void SetupPerFrameShaderConstants()
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.setupPerFrameShaderConstants);

            /*
                ---------------------------------------------------------------------------------:
                When glossy reflections are OFF in the shader we set a constant color to use as indirect specular
                --
                如果用户关闭了 material inspector: "Environment Reflections" 选项,
                将不能接收 反射探针 和 lightprobe 信息, 而是改用一个 constant color: "_GlossyEnvironmentColor"
                不管用户是否关闭, 此处都计算了这个 颜色值, 并传入 shader 中;
            */
            SphericalHarmonicsL2 ambientSH = RenderSettings.ambientProbe;// Custom or skybox ambient lighting data.
            // 分别取了球谐系数中 rgb 三通道的 0号系数 (就是那个常数值)
            Color linearGlossyEnvColor = new Color(ambientSH[0, 0], ambientSH[1, 0], ambientSH[2, 0]) * RenderSettings.reflectionIntensity;
            Color glossyEnvColor = CoreUtils.ConvertLinearToActiveColorSpace(linearGlossyEnvColor);// get linear or gamma color
            Shader.SetGlobalVector(ShaderPropertyId.glossyEnvironmentColor, glossyEnvColor);// "_GlossyEnvironmentColor"

            /*
                ---------------------------------------------------------------------------------:
                Ambient 环境光: 顶光, 赤道光, 底光 (暂未看到被 urp 使用)
                当 "RenderSettings.ambientMode" 选择 "Trilight" 模式时, 
                或 Lighint inspector: Environment Lighting Source 选择 "Gradient" 时(我猜)
                系统就是使用这组数据 去计算 环境光;
                但目前在 urp 中, 没发现这组数据 被 shader 使用;
            */
            Shader.SetGlobalVector(ShaderPropertyId.ambientSkyColor, CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.ambientSkyColor));//"unity_AmbientSky"
            Shader.SetGlobalVector(ShaderPropertyId.ambientEquatorColor, CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.ambientEquatorColor));//"unity_AmbientEquator"
            Shader.SetGlobalVector(ShaderPropertyId.ambientGroundColor, CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.ambientGroundColor));//"unity_AmbientGround"

            /*
                ---------------------------------------------------------------------------------:
                当 Lighting inspector: Scene Lighting Mode 选择 "Subtractive" 时, 本数据被使用
            */
            Shader.SetGlobalVector(ShaderPropertyId.subtractiveShadowColor, CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.subtractiveShadowColor));//"_SubtractiveShadowColor"

            /*
                ---------------------------------------------------------------------------------:
                被 2D Unlit Shadergraph master node 使用, 因为它当前不支持 hidden properties.
                2D 系统的, 直接不关心;
            */
            Shader.SetGlobalColor(ShaderPropertyId.rendererColor, Color.white);//"_RendererColor"
        }// 函数完__




// 如果 package: "com.unity.adaptiveperformance" 版本大于 2.0.0
#if ADAPTIVE_PERFORMANCE_2_0_0_OR_NEWER
    /*       tpr
        static void ApplyAdaptivePerformance(ref CameraData cameraData)
        {
            var noFrontToBackOpaqueFlags = SortingCriteria.SortingLayer | SortingCriteria.RenderQueue | SortingCriteria.OptimizeStateChanges | SortingCriteria.CanvasOrder;
            if (AdaptivePerformance.AdaptivePerformanceRenderSettings.SkipFrontToBackSorting)
                cameraData.defaultOpaqueSortFlags = noFrontToBackOpaqueFlags;

            var MaxShadowDistanceMultiplier = AdaptivePerformance.AdaptivePerformanceRenderSettings.MaxShadowDistanceMultiplier;
            cameraData.maxShadowDistance *= MaxShadowDistanceMultiplier;

            var RenderScaleMultiplier = AdaptivePerformance.AdaptivePerformanceRenderSettings.RenderScaleMultiplier;
            cameraData.renderScale *= RenderScaleMultiplier;

            // TODO
            if (!cameraData.xr.enabled)// 非 xr
            {
                cameraData.cameraTargetDescriptor.width = (int)(cameraData.camera.pixelWidth * cameraData.renderScale);
                cameraData.cameraTargetDescriptor.height = (int)(cameraData.camera.pixelHeight * cameraData.renderScale);
            }

            var antialiasingQualityIndex = (int)cameraData.antialiasingQuality - AdaptivePerformance.AdaptivePerformanceRenderSettings.AntiAliasingQualityBias;
            if (antialiasingQualityIndex < 0)
                cameraData.antialiasing = AntialiasingMode.None;
            cameraData.antialiasingQuality = (AntialiasingQuality)Mathf.Clamp(antialiasingQualityIndex, (int)AntialiasingQuality.Low, (int)AntialiasingQuality.High);
        }

        static void ApplyAdaptivePerformance(ref RenderingData renderingData)
        {
            if (AdaptivePerformance.AdaptivePerformanceRenderSettings.SkipDynamicBatching)
                renderingData.supportsDynamicBatching = false;

            var MainLightShadowmapResolutionMultiplier = AdaptivePerformance.AdaptivePerformanceRenderSettings.MainLightShadowmapResolutionMultiplier;
            renderingData.shadowData.mainLightShadowmapWidth = (int)(renderingData.shadowData.mainLightShadowmapWidth * MainLightShadowmapResolutionMultiplier);
            renderingData.shadowData.mainLightShadowmapHeight = (int)(renderingData.shadowData.mainLightShadowmapHeight * MainLightShadowmapResolutionMultiplier);

            var MainLightShadowCascadesCountBias = AdaptivePerformance.AdaptivePerformanceRenderSettings.MainLightShadowCascadesCountBias;
            
            // cascade 有几层, 区间[1,4]; (比如: 4个重叠的球体) 
            renderingData.shadowData.mainLightShadowCascadesCount = Mathf.Clamp(renderingData.shadowData.mainLightShadowCascadesCount - MainLightShadowCascadesCountBias, 0, 4);

            var shadowQualityIndex = AdaptivePerformance.AdaptivePerformanceRenderSettings.ShadowQualityBias;
            for (int i = 0; i < shadowQualityIndex; i++)
            {
                if (renderingData.shadowData.supportsSoftShadows)
                {
                    renderingData.shadowData.supportsSoftShadows = false;
                    continue;
                }

                if (renderingData.shadowData.supportsAdditionalLightShadows)
                {
                    renderingData.shadowData.supportsAdditionalLightShadows = false;
                    continue;
                }

                if (renderingData.shadowData.supportsMainLightShadows)
                {
                    renderingData.shadowData.supportsMainLightShadows = false;
                    continue;
                }

                break;
            }

            if (AdaptivePerformance.AdaptivePerformanceRenderSettings.LutBias >= 1 && renderingData.postProcessingData.lutSize == 32)
                renderingData.postProcessingData.lutSize = 16;
        }
    */
#endif
    }
}
