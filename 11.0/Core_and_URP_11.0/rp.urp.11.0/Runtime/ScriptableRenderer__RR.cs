using System;
using System.Diagnostics;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.Profiling;

namespace UnityEngine.Rendering.Universal
{
    
    /* 
        最直观理解:
            urp 默认项目中, Assets-Settings 存在一个实例: "Forward Renderer";
            本class "ScriptableRenderer" 描述的就是这个东西;

            (以下会将本类实例简称为 renderer);

            本类负责以下工作:
            -- describes how culling and lighting works and the effects supported
            -- 被哪些 camera 使用
            -- 定义了一组 "ScriptableRenderPass", 它们能在一帧被被执行;
            -- 可以绑定很多个 "ScriptableRendererFeature", 以实现额外的视觉效果;
            -- 将本 renderer 的数据存储在一个 "ScriptableRendererData" 实例中;

        ========================================
        旧信息:
        本类实现了一个 rendering strategy (渲染策略); 
        It describes how culling and lighting works and the effects supported.
        A renderer can be used for all cameras or "be overridden on a per-camera basis".
        It will implement "light culling and setup" and describe a list of "ScriptableRenderPass" to execute in a frame.
        The renderer can be extended to support more effect with additional "ScriptableRendererFeature";
        Resources for the renderer are serialized in "ScriptableRendererData";

        参考:
            "ScriptableRendererData"
            "ScriptableRendererFeature"
            "ScriptableRenderPass"
    */
    public abstract partial class ScriptableRenderer //ScriptableRenderer__RR
        : IDisposable
    {
        private static class Profiling
        {
            private const string k_Name = nameof(ScriptableRenderer);
            public static readonly ProfilingSampler setPerCameraShaderVariables = new ProfilingSampler($"{k_Name}.{nameof(SetPerCameraShaderVariables)}");
            public static readonly ProfilingSampler sortRenderPasses            = new ProfilingSampler($"Sort Render Passes");
            public static readonly ProfilingSampler setupLights                 = new ProfilingSampler($"{k_Name}.{nameof(SetupLights)}");
            public static readonly ProfilingSampler setupCamera                 = new ProfilingSampler($"Setup Camera Parameters");
            public static readonly ProfilingSampler addRenderPasses             = new ProfilingSampler($"{k_Name}.{nameof(AddRenderPasses)}");
            public static readonly ProfilingSampler clearRenderingState         = new ProfilingSampler($"{k_Name}.{nameof(ClearRenderingState)}");
            public static readonly ProfilingSampler internalStartRendering      = new ProfilingSampler($"{k_Name}.{nameof(InternalStartRendering)}");
            public static readonly ProfilingSampler internalFinishRendering     = new ProfilingSampler($"{k_Name}.{nameof(InternalFinishRendering)}");

            public static class RenderBlock//RenderBlock__RR
            {
                private const string k_Name = nameof(RenderPassBlock);
                public static readonly ProfilingSampler beforeRendering          = new ProfilingSampler($"{k_Name}.{nameof(RenderPassBlock.BeforeRendering)}");
                public static readonly ProfilingSampler mainRenderingOpaque      = new ProfilingSampler($"{k_Name}.{nameof(RenderPassBlock.MainRenderingOpaque)}");
                public static readonly ProfilingSampler mainRenderingTransparent = new ProfilingSampler($"{k_Name}.{nameof(RenderPassBlock.MainRenderingTransparent)}");
                public static readonly ProfilingSampler afterRendering           = new ProfilingSampler($"{k_Name}.{nameof(RenderPassBlock.AfterRendering)}");
            }

            public static class RenderPass//RenderPass__RR
            {
                private const string k_Name = nameof(ScriptableRenderPass);
                public static readonly ProfilingSampler configure = new ProfilingSampler($"{k_Name}.{nameof(ScriptableRenderPass.Configure)}");
            }
        }


        /// Override to provide a custom profiling name
        protected ProfilingSampler profilingExecute { get; set; }



        /*
            Configures the supported features for this renderer.
            当在创建用于 urp 的 custom renderers 时, 你可以选择 加入或退出 特定功能;

            注意:
            那个 Forward Renderer 中可添加的 Renderer Feature, 是 "ScriptableRendererFeature",
            不是本类;
        */
        public class RenderingFeatures//RenderingFeatures__
        {
            /*
                camera editor 是否应该显示 camera stack category; (猜测是 inspector)
                不支持 camera stacking 功能的 renderer, 只能渲染 Base Camera;
                参考:
                    "CameraRenderType"
                    "UniversalAdditionalCameraData.cameraStack"
                ---
                在 ForwardRenderer 初始化阶段, 自动设置此值为 true;
            */
            public bool cameraStacking { get; set; } = false;
            /*
                This setting controls if the urp asset should expose MSAA option.
                --
                "urp asset" 是否应该暴露 MSAA option (猜测是 inspector 中的)
                但在实际使用时, 此值的意思却是: "是否开启 msaa 功能";
            */
            public bool msaa { get; set; } = true;
        }


        /*
            当前正在使用的 renderer, (比如 Forward Renderer); 仅用于 底层渲染控制;
            一旦离开了 "rendering scope", 本值变为 null;

            有点类似: "Camera.current" 变量的概念
            在 "RenderSingleCamera()"-2- 中被设置;
        */
        internal static ScriptableRenderer current = null;

        
        
        /*
            Set camera matrices.
            具体设置 camera 的: "UNITY_MATRIX_V", "UNITY_MATRIX_P", "UNITY_MATRIX_VP";
            此外,还会设置: "unity_CameraProjection",

            若参数 setInverseMatrices 值为 true, 本函数还会设置:
                "UNITY_MATRIX_I_V", "UNITY_MATRIX_I_VP";

            当 rendering in stereo, 本函数不起任何作用, 此时你不能覆写 camera matrices.

            如果你想设置通用 view and projection matrices, 改用:
                SetViewAndProjectionMatrices(CommandBuffer, Matrix4x4, Matrix4x4, bool);
            
            仅在本文件内被调用
        */
        public static void SetCameraMatrices(//    读完__
                                    CommandBuffer cmd, 
                                    ref CameraData cameraData, 
                                    bool setInverseMatrices // 是否额外设置对应的几个 逆矩阵; true
        ){
/* tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            if (cameraData.xr.enabled)
            {
                cameraData.xr.UpdateGPUViewAndProjectionMatrices(cmd, ref cameraData, cameraData.xr.renderTargetIsRenderTexture);
                return;
            }
#endif
*/
            Matrix4x4 viewMatrix = cameraData.GetViewMatrix();
            Matrix4x4 projectionMatrix = cameraData.GetProjectionMatrix();

            /*
                TODO: Investigate(调查) why SetViewAndProjectionMatrices is causing y-flip / winding order issue
                    调查使用 "SetViewAndProjectionMatrices()" 函数会导致 y-flip 问题 和 缠绕顺序问题;
                for now using cmd.SetViewProjecionMatrices()
            */
            // urp 自己注释掉的
            //SetViewAndProjectionMatrices(cmd, viewMatrix, cameraData.GetDeviceProjectionMatrix(), setInverseMatrices);

            // 此函数中, 如果 view matrix 是自己实现的, 需要注意 z轴反转问题;
            cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            
            if (setInverseMatrices)// 唯一一次调用中, 开启了此 bool
            {
                // 得到最终在 shader 程序中出现的那个 投影矩阵, 比如 built-in管线中的 "UNITY_MATRIX_P" 矩阵;
                // 本函数包含处理 "y-flip" 和 "reverse z" 的平台特定更改;
                Matrix4x4 gpuProjectionMatrix = cameraData.GetGPUProjectionMatrix();

                Matrix4x4 viewAndProjectionMatrix = gpuProjectionMatrix * viewMatrix;
                Matrix4x4 inverseViewMatrix = Matrix4x4.Inverse(viewMatrix);
                Matrix4x4 inverseProjectionMatrix = Matrix4x4.Inverse(gpuProjectionMatrix);
                Matrix4x4 inverseViewProjection = inverseViewMatrix * inverseProjectionMatrix;

                /*
                    There's an inconsistency(不一致) in handedness between "unity_matrixV" and "unity_WorldToCamera"
                    Unity changes the handedness(惯用手) of unity_WorldToCamera 
                    (see Camera::CalculateMatrixShaderProps) 此函数没找到..
                    we will also change it here to avoid breaking existing shaders. (case 1257518)
                    ---
                    在 "unity_MatrixV" 的基础上, "unity_WorldToCamera" 额外翻转了自己的左右手特性;
                */
                Matrix4x4 worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f)) * viewMatrix;
                Matrix4x4 cameraToWorldMatrix = worldToCameraMatrix.inverse;

                cmd.SetGlobalMatrix(ShaderPropertyId.worldToCameraMatrix, worldToCameraMatrix);//"unity_WorldToCamera"
                cmd.SetGlobalMatrix(ShaderPropertyId.cameraToWorldMatrix, cameraToWorldMatrix);//"unity_CameraToWorld"

                // 逆矩阵
                cmd.SetGlobalMatrix(ShaderPropertyId.inverseViewMatrix, inverseViewMatrix);//"unity_MatrixInvV"
                cmd.SetGlobalMatrix(ShaderPropertyId.inverseProjectionMatrix, inverseProjectionMatrix);//"unity_MatrixInvP"
                cmd.SetGlobalMatrix(ShaderPropertyId.inverseViewAndProjectionMatrix, inverseViewProjection);//"unity_MatrixInvVP"
            }

            // TODO: missing unity_CameraWorldClipPlanes[6], currently set by "context.SetupCameraProperties()"
        }



        /*
            Set camera and screen shader variables
            https://docs.unity3d.com/Manual/SL-UnityShaderVariables.html

            配置如下几个 和 camera 相关的 shader数据:
                "_WorldSpaceCameraPos"
                "_ScreenParams"
                "_ScaledScreenParams"
                "_ZBufferParams"
                "unity_OrthoParams"
        */
        void SetPerCameraShaderVariables(CommandBuffer cmd, ref CameraData cameraData)//  读完__
        {
            using var profScope = new ProfilingScope(cmd, Profiling.setPerCameraShaderVariables);

            Camera camera = cameraData.camera;

            Rect pixelRect = cameraData.pixelRect;
            float renderScale = cameraData.isSceneViewCamera ? 1f : cameraData.renderScale;
            float scaledCameraWidth = (float)pixelRect.width * renderScale;
            float scaledCameraHeight = (float)pixelRect.height * renderScale;
            float cameraWidth = (float)pixelRect.width;
            float cameraHeight = (float)pixelRect.height;

            // Use eye texture's width and height as screen params when XR is enabled
            /*     tpr
            if (cameraData.xr.enabled)
            {
                scaledCameraWidth = (float)cameraData.cameraTargetDescriptor.width;
                scaledCameraHeight = (float)cameraData.cameraTargetDescriptor.height;
                cameraWidth = (float)cameraData.cameraTargetDescriptor.width;
                cameraHeight = (float)cameraData.cameraTargetDescriptor.height;
            }
            */

            if (camera.allowDynamicResolution)
            {
                // 动态分辨率的 缩放
                scaledCameraWidth *= ScalableBufferManager.widthScaleFactor;
                scaledCameraHeight *= ScalableBufferManager.heightScaleFactor;
            }

            float near = camera.nearClipPlane;
            float far = camera.farClipPlane;
            float invNear = Mathf.Approximately(near, 0.0f) ? 0.0f : 1.0f / near;
            float invFar = Mathf.Approximately(far, 0.0f) ? 0.0f : 1.0f / far;
            float isOrthographic = camera.orthographic ? 1.0f : 0.0f;

            /*
                From http://www.humus.name/temp/Linearize%20depth.txt
                就是笔记中图: "Linear01Depth.jpg"
            
            // But as depth component textures on OpenGL always return in 0..1 range (as in D3D), 
            // we have to use the same constants for both D3D and OpenGL here.
            // OpenGL would be this:
            // zc0 = (1.0 - far / near) / 2.0;
            // zc1 = (1.0 + far / near) / 2.0;
            */
            // D3D is this:
            float zc0 = 1.0f - far * invNear;
            float zc1 = far * invNear;

            Vector4 zBufferParams = new Vector4(zc0, zc1, zc0 * invFar, zc1 * invFar);

            if (SystemInfo.usesReversedZBuffer)
            {
                zBufferParams.y += zBufferParams.x;
                zBufferParams.x = -zBufferParams.x;
                zBufferParams.w += zBufferParams.z;
                zBufferParams.z = -zBufferParams.z;
            }

            // Projection flip sign logic is very deep in GfxDevice::SetInvertProjectionMatrix
            // For now we don't deal with _ProjectionParams.x and let "context.SetupCameraProperties()" handle it.
            // We need to enable this when we remove "context.SetupCameraProperties()"
            // ---
            // 此处的代码, 被 unity 自己注释起来了;
            //
            // float projectionFlipSign = ???
            // Vector4 projectionParams = new Vector4(projectionFlipSign, near, far, 1.0f * invFar);
            // cmd.SetGlobalVector(ShaderPropertyId.projectionParams, projectionParams);

            Vector4 orthoParams = new Vector4(
                camera.orthographicSize * cameraData.aspectRatio, 
                camera.orthographicSize, 
                0.0f, 
                isOrthographic
            );

            // Camera and Screen variables as described in https://docs.unity3d.com/Manual/SL-UnityShaderVariables.html
            cmd.SetGlobalVector(ShaderPropertyId.worldSpaceCameraPos, camera.transform.position);//"_WorldSpaceCameraPos"
            cmd.SetGlobalVector(ShaderPropertyId.screenParams, // "_ScreenParams"
                new Vector4(
                    cameraWidth, 
                    cameraHeight, 
                    1.0f + 1.0f / cameraWidth, 
                    1.0f + 1.0f / cameraHeight
                ));

            cmd.SetGlobalVector(ShaderPropertyId.scaledScreenParams, //"_ScaledScreenParams"
                new Vector4(
                    scaledCameraWidth, 
                    scaledCameraHeight, 
                    1.0f + 1.0f / scaledCameraWidth, 
                    1.0f + 1.0f / scaledCameraHeight
                ));

            cmd.SetGlobalVector(ShaderPropertyId.zBufferParams, zBufferParams);//"_ZBufferParams"
            cmd.SetGlobalVector(ShaderPropertyId.orthoParams, orthoParams);//"unity_OrthoParams"
        }





        /*
            Set shader time variables;
            https://docs.unity3d.com/Manual/SL-UnityShaderVariables.html

            配置了几个与时间 相关的 shader 变量, 并传入 shader 中;
        */
        void SetShaderTimeValues(CommandBuffer cmd, float time, float deltaTime, float smoothDeltaTime)//  读完__
        {
            float timeEights = time / 8f;
            float timeFourth = time / 4f;
            float timeHalf = time / 2f;

            // Time values
            Vector4 timeVector = time * new Vector4(
                1f / 20f, 
                1f, 
                2f, 
                3f
            );
            Vector4 sinTimeVector = new Vector4(
                Mathf.Sin(timeEights), 
                Mathf.Sin(timeFourth), 
                Mathf.Sin(timeHalf), 
                Mathf.Sin(time)
            );
            Vector4 cosTimeVector = new Vector4(
                Mathf.Cos(timeEights), 
                Mathf.Cos(timeFourth), 
                Mathf.Cos(timeHalf), 
                Mathf.Cos(time)
            );
            Vector4 deltaTimeVector = new Vector4(
                deltaTime, 
                1f / deltaTime, 
                smoothDeltaTime, 
                1f / smoothDeltaTime
            );
            Vector4 timeParametersVector = new Vector4(
                time, 
                Mathf.Sin(time), 
                Mathf.Cos(time), 
                0.0f
            );
            cmd.SetGlobalVector(ShaderPropertyId.time, timeVector);//"_Time"
            cmd.SetGlobalVector(ShaderPropertyId.sinTime, sinTimeVector);//"_SinTime"
            cmd.SetGlobalVector(ShaderPropertyId.cosTime, cosTimeVector);//"_CosTime"
            cmd.SetGlobalVector(ShaderPropertyId.deltaTime, deltaTimeVector);//"unity_DeltaTime"
            cmd.SetGlobalVector(ShaderPropertyId.timeParameters, timeParametersVector);//"_TimeParameters"
        }



        /*
            Returns the camera color target for this renderer.
            猜测是 oaque color texture;

            只有在 "ScriptableRenderPass" 作用域内访问本属性 才是合法的;
            参考: "ScriptableRenderPass";

            若此值等于 "BuiltinRenderTextureType.CameraTarget", 表示渲染进 camera 的 backbuffer; (但似乎不准确...)
        */
        public RenderTargetIdentifier cameraColorTarget//  读完__
        {
            get
            {
                if (!(m_IsPipelineExecuting || isCameraColorTargetValid))
                {
                    Debug.LogWarning(
                        @"You can only call cameraColorTarget inside the scope of a ScriptableRenderPass. 
                        Otherwise the pipeline camera target texture might have not been created 
                        or might have already been disposed.");
                    // TODO: Ideally we should return an error texture (BuiltinRenderTextureType.None?)
                    // but this might break some existing content, 
                    //so we return the pipeline texture in the hope it gives a "soft" upgrade to users.
                }

                return m_CameraColorTarget;
            }
        }
        /*
            Returns the camera depth target for this renderer.

            只有在 "ScriptableRenderPass" 作用域内访问本属性 才是合法的;
            参考: "ScriptableRenderPass"

            此属性没有被直接使用过... (仅在 注释中出现)
        */
        public RenderTargetIdentifier cameraDepthTarget//  读完__
        {
            get
            {
                if (!m_IsPipelineExecuting)
                {
                    Debug.LogWarning(
                        @"You can only call cameraDepthTarget inside the scope of a ScriptableRenderPass. 
                        Otherwise the pipeline camera target texture might have not been created 
                        or might have already been disposed.");
                    // TODO: Ideally we should return an error texture (BuiltinRenderTextureType.None?)
                    // but this might break some existing content, 
                    // so we return the pipeline texture in the hope it gives a "soft" upgrade to users.
                }

                return m_CameraDepthTarget;
            }
        }




        /*
            Returns a list of renderer features added to this renderer.

            参考:
            "ScriptableRendererFeature"
        */
        protected List<ScriptableRendererFeature> rendererFeatures
        {
            get => m_RendererFeatures;
        }



        /*
            Returns a list of "render passes" scheduled to be executed by this renderer.
            参考:
            "ScriptableRenderPass"
        */
        protected List<ScriptableRenderPass> activeRenderPassQueue
        {
            get => m_ActiveRenderPassQueue;
        }



        /*
            Supported "rendering features" by this renderer.
            包含: cameraStacking, msaa;
        */
        public RenderingFeatures supportedRenderingFeatures { get; set; } = new RenderingFeatures();



        /*
            返回本 renderer 不支持的 Graphics APIs
        */
        public GraphicsDeviceType[] unsupportedGraphicsDeviceTypes { get; set; } = new GraphicsDeviceType[0];


        /*
            猜测: 顶一个 render pass 的几个执行阶段(时间阶段); 有点 enum 的用法;
            仅在本文件内被使用;
        */
        static class RenderPassBlock//RenderPassBlock__
        {
            /*
                Executes render passes that are inputs to the main rendering but don't depend on camera state. 
                They all render in monoscopic mode. f.ex, shadow maps.
            */
            public static readonly int BeforeRendering = 0;

            /*
                Main bulk(大块) of render pass execution. 
                They required camera state to be properly set and when enabled they will render in stereo.
            */
            public static readonly int MainRenderingOpaque = 1;
            public static readonly int MainRenderingTransparent = 2;

            // Execute after Post-processing.
            public static readonly int AfterRendering = 3;
        }



        const int k_RenderPassBlockCount = 4;

        List<ScriptableRenderPass> m_ActiveRenderPassQueue = new List<ScriptableRenderPass>(32);

        
        List<ScriptableRendererFeature> m_RendererFeatures = new List<ScriptableRendererFeature>(10);

        /*
            ----------------------------------------------------------------------:
            每个 renderer 实例持有的值:

            Clear() 后会被设置为: "BuiltinRenderTextureType.CameraTarget";
            仅在本文件内出现;
            可以调用本类函数: "ConfigureCameraTarget()", 或 "ConfigureCameraColorTarget()" 设置它们;

            - color rt: 要么设置为 "_CameraColorTexture", 要么保留初始值;
            - depth rt: 要么设置为 "_CameraDepthAttachment", 要么保留初始值;
        */
        RenderTargetIdentifier m_CameraColorTarget;
        RenderTargetIdentifier m_CameraDepthTarget;


        /*
            flag used to track when "m_CameraColorTarget" should be cleared (if necessary), 
            as well as other special actions only performed the first time "m_CameraColorTarget" is bound as a render target
            ---
            Clear() 后, base camera 设为 true, overlay camera 设为 false;
        */
        bool m_FirstTimeCameraColorTargetIsBound = true; 

        /*
            flag used to track when "m_CameraDepthTarget" should be cleared (if necessary), 
            the first time "m_CameraDepthTarget" is bound as a render target
            ---
            Clear() 后, 无论 base / overlay camera, 都设为 true;
        */
        bool m_FirstTimeCameraDepthTargetIsBound = true; 


        /*
            渲染管线只保证: 只有在管线执行期间, "camera target texture" 是有效的;
            除此之外的时间中, 这些 texture/target 可能已经被 disposed;
        */
        bool m_IsPipelineExecuting = false;

        /*
            This should be removed when early camera color-target assignment is removed.
            将来将被移除
        */
        internal bool isCameraColorTargetValid = false;



        /*
            -----------------------------------------------------------------------:
            整个渲染管线中, 当前的 "active color/depth render target" !!!!!!!!!!!!!!!!!
            此组数据的唯一作用:
                在 "SetRenderPassAttachments()" 中, 检测 "当前 render pass 需要的 render target" 是否和 和本组数据相同,
                如果两者不同, 就需要 重新绑定 render target; (顺带同步本组数据)
            ---------
            Clear() 后. 
                m_ActiveColorAttachments[0]会被设置为: "BuiltinRenderTextureType.CameraTarget"; 剩余元素设置为 0;
                m_ActiveDepthAttachment 会被设置为: "BuiltinRenderTextureType.CameraTarget";

            每次调用: "SetRenderTarget()", 都会同步本变量的数据;
        */
        static RenderTargetIdentifier[] m_ActiveColorAttachments = new RenderTargetIdentifier[] {0, 0, 0, 0, 0, 0, 0, 0 };
        static RenderTargetIdentifier m_ActiveDepthAttachment;


       
        /*
            Trimmed: 修剪;

            如果 "colors" array 包含了无效的 RenderTargetIdentifier,
            "CoreUtils.SetRenderTarget()" 体内调用的 "CommandBuffer.SetRenderTarget()" 
            将会在 原生 c++ 代码端 曝出警告;
            
            为了避免每次分配一个新 array, 我们选择复用 这些 arrays;
        */
        static RenderTargetIdentifier[][] m_TrimmedColorAttachmentCopies = new RenderTargetIdentifier[][]
        {
            new RenderTargetIdentifier[0],                          // m_TrimmedColorAttachmentCopies[0] is an array of 0 RenderTargetIdentifier - 
                                                                    //      only used to make indexing code easier to read
            new RenderTargetIdentifier[] {0},                        // m_TrimmedColorAttachmentCopies[1] is an array of 1 RenderTargetIdentifier
            new RenderTargetIdentifier[] {0, 0},                     // m_TrimmedColorAttachmentCopies[2] is an array of 2 RenderTargetIdentifiers
            new RenderTargetIdentifier[] {0, 0, 0},                  // m_TrimmedColorAttachmentCopies[3] is an array of 3 RenderTargetIdentifiers
            new RenderTargetIdentifier[] {0, 0, 0, 0},               // m_TrimmedColorAttachmentCopies[4] is an array of 4 RenderTargetIdentifiers
            new RenderTargetIdentifier[] {0, 0, 0, 0, 0},            // m_TrimmedColorAttachmentCopies[5] is an array of 5 RenderTargetIdentifiers
            new RenderTargetIdentifier[] {0, 0, 0, 0, 0, 0},         // m_TrimmedColorAttachmentCopies[6] is an array of 6 RenderTargetIdentifiers
            new RenderTargetIdentifier[] {0, 0, 0, 0, 0, 0, 0},      // m_TrimmedColorAttachmentCopies[7] is an array of 7 RenderTargetIdentifiers
            new RenderTargetIdentifier[] {0, 0, 0, 0, 0, 0, 0, 0 },  // m_TrimmedColorAttachmentCopies[8] is an array of 8 RenderTargetIdentifiers
        };


        // urp 中没人用过此函数...
        internal static void ConfigureActiveTarget(  //   读完__
                                            RenderTargetIdentifier colorAttachment,
                                            RenderTargetIdentifier depthAttachment
        ){
            // [0] 绑定参数值, 后续7个id 全写0
            m_ActiveColorAttachments[0] = colorAttachment;
            for (int i = 1; i < m_ActiveColorAttachments.Length; ++i)
                m_ActiveColorAttachments[i] = 0;

            m_ActiveDepthAttachment = depthAttachment;
        }


        
        // 构造函数
        public ScriptableRenderer(ScriptableRendererData data)//   读完__
        {
            profilingExecute = new ProfilingSampler(
                $"{nameof(ScriptableRenderer)}.{nameof(ScriptableRenderer.Execute)}: {data.name}"
            );

            // 将 data 中的 renderer features 数据, 创建后存入本类实例中
            foreach (var feature in data.rendererFeatures)
            {
                if (feature == null)
                    continue;

                feature.Create();
                m_RendererFeatures.Add(feature);
            }
            Clear(CameraRenderType.Base);
            m_ActiveRenderPassQueue.Clear();
        }




        public void Dispose()
        {
            // Dispose all renderer features...
            for (int i = 0; i < m_RendererFeatures.Count; ++i)
            {
                if (rendererFeatures[i] == null)
                    continue;

                rendererFeatures[i].Dispose();
            }

            Dispose(true);
            GC.SuppressFinalize(this);
        }


        protected virtual void Dispose(bool disposing)
        {
        }


        /*
            Configures the camera target.

            下文注释中的 "CameraTarget", 仅指: "current camera 的 render target", 但它不一定是: "Currently active render target";

            在 ForwardRenderer 中:
                被调用两次:
                -1-:
                    "offsreen depth camera":
                    调用本函数时, 传入两个 "BuiltinRenderTextureType.CameraTarget";

                -2-:
                    常规的 base camera:
                    调用本函数时, 传入的要么是: "_CameraColorTexture", "_CameraDepthAttachment";
                    要么是 "BuiltinRenderTextureType.CameraTarget";
                    如果是 上面两个, 则这两个 rt 是已经被分配好了的;

            在 Renderer2D 中:
                略...
        */
        /// <param name="colorTarget">Camera color target. 
        ///                           Pass "BuiltinRenderTextureType.CameraTarget" if rendering to backbuffer.
        /// </param>
        /// <param name="depthTarget">Camera depth target. 
        ///                           Pass "BuiltinRenderTextureType.CameraTarget" if color has depth or rendering to backbuffer.
        ///                             "color has depth" 就意味着不需要单独的 depth buffer 了
        /// </param>
        public void ConfigureCameraTarget(RenderTargetIdentifier colorTarget, RenderTargetIdentifier depthTarget)//  读完__
        {
            // 这两个值在 初始状态, 都被设置为了 "BuiltinRenderTextureType.CameraTarget"
            m_CameraColorTarget = colorTarget;
            m_CameraDepthTarget = depthTarget;
        }


        /*
            This should be removed when early camera color target assignment is removed.
            仅被调用一次, 
                要么传入: "BuiltinRenderTextureType.CameraTarget"
                要么传入: "_CameraColorTexture"
        */
        internal void ConfigureCameraColorTarget(RenderTargetIdentifier colorTarget)
        {
            m_CameraColorTarget = colorTarget;
        }

        /*
            派生类必须实现之 ------------------------------------------------
            Configures the render passes that will execute for this renderer.
            This method is called per-camera every frame.
        */
        /// <param name="context">Use this render context to issue any draw commands during execution.</param>
        /// <param name="renderingData">Current render state information.</param>
        /// <seealso cref="ScriptableRenderPass"/>
        /// <seealso cref="ScriptableRendererFeature"/>
        public abstract void Setup(ScriptableRenderContext context, ref RenderingData renderingData);


        /*
            Override this method to implement "the lighting setup for the renderer". 
            You can use this to compute and upload light CBUFFER for example.
        */
        /// <param name="context">Use this render context to issue any draw commands during execution.</param>
        /// <param name="renderingData">Current render state information.</param>
        public virtual void SetupLights(ScriptableRenderContext context, ref RenderingData renderingData)
        {}


        /*
            Override this method to "configure the culling parameters for the renderer".
            You can use this to configure: "if lights should be culled per-object" or "the maximum shadow distance" for example.
        */
        /// <param name="cullingParameters">Use this to change culling parameters used by the render pipeline.</param>
        /// <param name="cameraData">Current render state information.</param>
        public virtual void SetupCullingParameters(
            ref ScriptableCullingParameters cullingParameters,
            ref CameraData cameraData)
        {}


        /*
            Called upon finishing rendering the camera stack. 
            You can release any resources created by the renderer here.
        */
        public virtual void FinishRendering(CommandBuffer cmd)// 读完__
        {}


        /*
            Execute the enqueued render passes. This automatically handles editor and stereo rendering.
            本函数只处理一个 camera

            被 "RenderSingleCamera()" 调用;
        */
        /// <param name="context">Use this render context to issue any draw commands during execution.</param>
        /// <param name="renderingData">Current render state information.</param>
        public void Execute(  //  读完__
                        ScriptableRenderContext context, 
                        ref RenderingData renderingData)
        {
            m_IsPipelineExecuting = true;
            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;

            CommandBuffer cmd = CommandBufferPool.Get();

            // TODO: move skybox code from C++ to URP in order to remove the call to context.Submit() inside DrawSkyboxPass
            // Until then, we can't use nested profiling scopes with XR multipass
            // --
            //  非 xr 程序,  始终为 cmd
            CommandBuffer cmdScope = renderingData.cameraData.xr.enabled ? null : cmd;

            using (new ProfilingScope(cmdScope, profilingExecute))
            {
                // 调用每个 active render pass 的 OnCameraSetup() 函数;
                InternalStartRendering(context, ref renderingData);

                /*
                    Cache the time for after the call to "context.SetupCameraProperties()"
                    and set the time variables in shader For now we set the time variables per camera, 
                    as we plan to remove "context.SetupCameraProperties()";
                    Setting the time per frame would take API changes to pass the variable to each camera render.
                    Once "context.SetupCameraProperties()" is gone, the variable should be set higher in the call-stack.

                    缓存调用 "context.SetupCameraProperties()" 之后的时间;
                    并在 shader 中设置时间变量 现在我们设置每个相机的时间变量，因为我们计划删除 "context.SetupCameraProperties()"。
                    设置每帧的时间 需要更改 API, 以将变量传递给每个 camera render。
                    一旦 "context.SetupCameraProperties()" 被移除, 这个变量就要被放到更高的 调用栈中去;
                */
#if UNITY_EDITOR
                float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
#else
                float time = Time.time;
#endif
                float deltaTime = Time.deltaTime;
                float smoothDeltaTime = Time.smoothDeltaTime;

                // Initialize Camera Render State
                // disable 所有 "逐相机 shader keywords";
                ClearRenderingState(cmd);

                SetPerCameraShaderVariables(cmd, ref cameraData);// 配置了几个与相机相关的数据, 传入 shader 中;
                SetShaderTimeValues(cmd, time, deltaTime, smoothDeltaTime);// 配置了几个与时间相关的数据, 并传入 shader 中;

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                using (new ProfilingScope(cmd, Profiling.sortRenderPasses))
                {
                    // Sort the render pass queue
                    // 按照 "RenderPassEvent" 来对所有 active render passes 进行排序;
                    SortStable(m_ActiveRenderPassQueue);
                }

                using var renderBlocks = new RenderBlocks(m_ActiveRenderPassQueue);

                using (new ProfilingScope(cmd, Profiling.setupLights))
                {
                    SetupLights(context, ref renderingData);
                }

                using (new ProfilingScope(cmd, Profiling.RenderBlock.beforeRendering))
                {
                    // Before Render Block. 
                    // This render blocks always execute in mono rendering.
                    // Camera is not setup. Lights are not setup.
                    // Used to render input textures like shadowmaps.
                    ExecuteBlock(
                        RenderPassBlock.BeforeRendering, // 0
                        in renderBlocks, 
                        context, 
                        ref renderingData
                    );
                }

                using (new ProfilingScope(cmd, Profiling.setupCamera))
                {
                    /*
                        This is still required because of the following reasons:
                        - Camera billboard properties.
                        - Camera frustum planes: unity_CameraWorldClipPlanes[6]
                        - _ProjectionParams.x logic is deep inside GfxDevice

                        NOTE: The only reason we have to call this here and not at the beginning (before shadows)
                        is because this need to be called for each eye in multi pass VR.
                        The side effect is that this will override some shader properties we already setup and we will have to reset them.
                        ---
                        注意, 这部分内容之所以没放到上一个模块中, 是因为和 vr 相关;
                        这部分操作 会覆写一部分已经设置好的数据;
                        ====

                        将 camera 的 specific global shader variables (如 unity_MatrixVP 等信息) 传递给 shader
                        因为 camera 内部只有一个顶点, 所以猜测省略了 OS->WS 这层转换;
                        直接使用 unity_MatrixVP 矩阵就能得到 camera 在 CS 中的状态;
                        所以, 此矩阵包含了 camera 的 坐标, 朝向, 视锥体 等信息
                    */
                    context.SetupCameraProperties(camera);
                    SetCameraMatrices(cmd, ref cameraData, true);

                    // Reset shader time variables as they were overridden in "context.SetupCameraProperties()";
                    // If we don't do it we might have a mismatch between shadows and main rendering
                    // 再次调用
                    SetShaderTimeValues(cmd, time, deltaTime, smoothDeltaTime);

// 如果 package: "com.unity.visualeffectgraph" 版本大于 0.0.1
#if VISUAL_EFFECT_GRAPH_0_0_1_OR_NEWER
                    //Triggers dispatch per camera, all global parameters should have been setup at this stage.
                    VFX.VFXManager.ProcessCameraCommand(camera, cmd);
#endif
                }


                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                BeginXRRendering(cmd, context, ref renderingData.cameraData);// xr

                // In the opaque and transparent blocks the main rendering executes.

                // Opaque blocks...
                if (renderBlocks.GetLength(RenderPassBlock.MainRenderingOpaque) > 0)
                {
                    using var profScope = new ProfilingScope(cmd, Profiling.RenderBlock.mainRenderingOpaque);
                    ExecuteBlock(
                        RenderPassBlock.MainRenderingOpaque, // 1
                        in renderBlocks, 
                        context, 
                        ref renderingData
                    );
                }

                // Transparent blocks...
                if (renderBlocks.GetLength(RenderPassBlock.MainRenderingTransparent) > 0)
                {
                    using var profScope = new ProfilingScope(cmd, Profiling.RenderBlock.mainRenderingTransparent);
                    ExecuteBlock(
                        RenderPassBlock.MainRenderingTransparent, // 2
                        in renderBlocks, 
                        context, 
                        ref renderingData
                    );
                }

                // editor 中的界面图标 在特效之前;
                DrawGizmos(context, camera, GizmoSubset.PreImageEffects);

                // In this block after rendering drawing happens, e.g, post processing, video player capture.
                if (renderBlocks.GetLength(RenderPassBlock.AfterRendering) > 0)
                {
                    using var profScope = new ProfilingScope(cmd, Profiling.RenderBlock.afterRendering);
                    ExecuteBlock(
                        RenderPassBlock.AfterRendering, // 3
                        in renderBlocks, 
                        context, 
                        ref renderingData
                    );
                }

                EndXRRendering(cmd, context, ref renderingData.cameraData);// xr

                DrawWireOverlay(context, camera); // 绘制 editor scene窗口的 线框模式;

                // editor 中的界面图标 在特效之后;
                DrawGizmos(context, camera, GizmoSubset.PostImageEffects);
                // 执行清理工作;
                InternalFinishRendering(context, cameraData.resolveFinalTarget);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }//  函数完__

        
        /*
            Enqueues a render pass for execution.
        */
        /// <param name="pass">Render pass to be enqueued.</param>
        public void EnqueuePass(ScriptableRenderPass pass)//   读完__
        {
            m_ActiveRenderPassQueue.Add(pass);
        }



        /*
            return A clear flag that tells if color and/or depth should be cleared.
            ClearFlag: None, Color, Depth, All;
            ---
            仅在本文件内被使用
        */
        protected static ClearFlag GetCameraClearFlag(ref CameraData cameraData)//   读完__
        {
            var cameraClearFlags = cameraData.camera.clearFlags;
            /*
                这段内容未来需要整理....
                
                urp doesn't support "CameraClearFlags.DepthOnly" and "CameraClearFlags.Nothing".
                -- CameraClearFlags.DepthOnly has the same effect of CameraClearFlags.SolidColor
                -- CameraClearFlags.Nothing clears Depth on PC/Desktop and in mobile it clears both depth and color.
                -- CameraClearFlags.Skybox clears depth only.

                Implementation details:
                Camera clear flags are used to initialize the attachments on the first render pass.
                ClearFlag is used together with Tile Load action to figure out how to clear the camera render target.
                In Tiled-Based GPUs ClearFlag.Depth + RenderBufferLoadAction.DontCare becomes DontCare load action.
                While ClearFlag.All + RenderBufferLoadAction.DontCare become Clear load action.
                In mobile we force ClearFlag.All as DontCare doesn't have noticeable perf. 
                difference from Clear and this avoid tile clearing issue when not rendering all pixels in some GPUs.
                In desktop/consoles there's actually performance difference between DontCare and Clear.

                RenderBufferLoadAction.DontCare in PC/Desktop behaves as not clearing screen
                RenderBufferLoadAction.DontCare in Vulkan/Metal behaves as DontCare load action
                RenderBufferLoadAction.DontCare in GLES behaves as glInvalidateBuffer
            */

            // Overlay cameras composite on top of previous ones. They don't clear color.
            // For overlay cameras we check if depth should be cleared on not.
            if (cameraData.renderType == CameraRenderType.Overlay)
                return (cameraData.clearDepth) ? ClearFlag.Depth : ClearFlag.None;

            // Always clear on first render pass in mobile as it's same perf of DontCare and avoid tile clearing issues.
            // 因为此选项的性能开销和 "DontCare" 相同, 且避免了 tile clearing 这个问题;
            if (Application.isMobilePlatform)
                return ClearFlag.All;

            if ((cameraClearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null) ||
                cameraClearFlags == CameraClearFlags.Nothing)
                return ClearFlag.Depth;

            return ClearFlag.All;
        }//  函数完__



        /*
            Calls "AddRenderPasses" for each feature added to this renderer.
            ---
            查看: "ScriptableRendererFeature.AddRenderPasses(ScriptableRenderer, ref RenderingData)"
        */
        protected void AddRenderPasses(ref RenderingData renderingData)//   读完__
        {
            using var profScope = new ProfilingScope(null, Profiling.addRenderPasses);

            // Add render passes from custom renderer features
            for (int i = 0; i < rendererFeatures.Count; ++i)
            {
                if (!rendererFeatures[i].isActive)
                {
                    continue;
                }
                // 调用 派生类自定义的 函数体, 体内通常实现了: renderer.EnqueuePass(m_ScriptablePass);
                // 即将 render pass 加入了 activeRenderPassQueue 中;
                rendererFeatures[i].AddRenderPasses(this, ref renderingData);
            }

            // 上面添加的新的 render pass 可能是 null, 在此处删除掉;
            // Remove any null render pass that might have been added by user by mistake
            int count = activeRenderPassQueue.Count;
            for (int i = count - 1; i >= 0; i--)// 倒向遍历
            {
                if (activeRenderPassQueue[i] == null)
                    activeRenderPassQueue.RemoveAt(i);
            }
        }//  函数完__



        // disable 所有 "逐相机 shader keywords";
        void ClearRenderingState(CommandBuffer cmd)//  读完__
        {
            using var profScope = new ProfilingScope(cmd, Profiling.clearRenderingState);

            // Reset per-camera shader keywords. They are enabled depending on which render passes are executed.
            cmd.DisableShaderKeyword(ShaderKeywordStrings.MainLightShadows);//"_MAIN_LIGHT_SHADOWS"
            cmd.DisableShaderKeyword(ShaderKeywordStrings.MainLightShadowCascades);//"_MAIN_LIGHT_SHADOWS_CASCADE"
            cmd.DisableShaderKeyword(ShaderKeywordStrings.MainLightShadowScreen);//"_MAIN_LIGHT_SHADOWS_SCREEN"
            cmd.DisableShaderKeyword(ShaderKeywordStrings.AdditionalLightsVertex);
            cmd.DisableShaderKeyword(ShaderKeywordStrings.AdditionalLightsPixel);//"_ADDITIONAL_LIGHTS"
            cmd.DisableShaderKeyword(ShaderKeywordStrings.AdditionalLightShadows);//"_ADDITIONAL_LIGHT_SHADOWS"
            cmd.DisableShaderKeyword(ShaderKeywordStrings.SoftShadows);//"_SHADOWS_SOFT"
            cmd.DisableShaderKeyword(ShaderKeywordStrings.MixedLightingSubtractive); //"_MIXED_LIGHTING_SUBTRACTIVE" // Backward compatibility
            cmd.DisableShaderKeyword(ShaderKeywordStrings.LightmapShadowMixing);//"LIGHTMAP_SHADOW_MIXING"
            cmd.DisableShaderKeyword(ShaderKeywordStrings.ShadowsShadowMask);//"SHADOWS_SHADOWMASK"
            cmd.DisableShaderKeyword(ShaderKeywordStrings.LinearToSRGBConversion);//"_LINEAR_TO_SRGB_CONVERSION"
        }//  函数完__



   
        /// <param name="cameraType"> enum: Base, Overlay </param>
        internal void Clear(CameraRenderType cameraType)//   读完__
        {
            // 右值: current camera 的 render target, (但不一定是 current active render target)
            m_ActiveColorAttachments[0] = BuiltinRenderTextureType.CameraTarget;
            for (int i = 1; i < m_ActiveColorAttachments.Length; ++i)
                m_ActiveColorAttachments[i] = 0;
            m_ActiveDepthAttachment = BuiltinRenderTextureType.CameraTarget;

            m_FirstTimeCameraColorTargetIsBound = cameraType==CameraRenderType.Base;
            m_FirstTimeCameraDepthTargetIsBound = true;

            m_CameraColorTarget = BuiltinRenderTextureType.CameraTarget;
            m_CameraDepthTarget = BuiltinRenderTextureType.CameraTarget;
        }//  函数完__



        /*
            一次性把一个 render block 中的所有 render passes 全部执行完毕;
        */
        void ExecuteBlock(
                    int blockIndex, // "RenderPassBlock" 中的值, 定义不同的执行阶段 {0,1,2,3}
                    in RenderBlocks renderBlocks,
                    ScriptableRenderContext context, 
                    ref RenderingData renderingData, 
                    bool submit = false
        ){
            // 遍历目标 block 内的所有 render pass idxs
            foreach (int currIndex in renderBlocks.GetRange(blockIndex))
            {
                var renderPass = m_ActiveRenderPassQueue[currIndex];
                ExecuteRenderPass(context, renderPass, ref renderingData);
            }
            if (submit)
                // 真正的 "提交" commands
                // 现有的针对本函数的 4 次调用, 都未 submit;
                context.Submit(); 
        }



        /*
            -1- renderPass.Configure()
            -2- set color / depth render target
            -3- renderPass.Execute()
        */
        void ExecuteRenderPass(//     读完__
                                ScriptableRenderContext context, 
                                ScriptableRenderPass renderPass, 
                                ref RenderingData renderingData
        ){
            using var profScope = new ProfilingScope(null, renderPass.profilingSampler);

            ref CameraData cameraData = ref renderingData.cameraData;

            CommandBuffer cmd = CommandBufferPool.Get();

            // Track CPU only as GPU markers for this scope were "too noisy".
            using (new ProfilingScope(cmd, Profiling.RenderPass.configure))
            {
                renderPass.Configure(cmd, cameraData.cameraTargetDescriptor);// 调用用户自定义函数

                // -1- 确定本次 render pass 的 color/depth target; 可能受 render pass 自定义值影响, 也可能直接使用本 renderer 的数据
                // -2- 确定 finalClearFlag, finalClearColor;
                // -3- 调用不同版本的 "SetRenderTarget()"; 设置 render target, 且配置 clear 信息;
                SetRenderPassAttachments(cmd, renderPass, ref cameraData);
            }

            // Also, we execute the commands recorded at this point to ensure "SetRenderTarget()" is called before RenderPass.Execute
            // 确保在 调用 "RenderPass.Execute()" 之前调用 "SetRenderTarget()";
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            // 用户自定义的 render pass 内容本体;
            renderPass.Execute(context, ref renderingData);
        }//  函数完__



        /*
            ----------------------------------------------------------------------------------------:
                                                本函数极其复杂
                                                     ---
                        (最好代入具体的 render pass 实例进来阅读, 以便查清特定流程中的细节)
            ----------------------------------------------------------------------------------------:
            -1- 确定本次 render pass 的 color/depth target; 可能受 render pass 自定义值影响, 也可能直接使用本 renderer 中的数据
            -2- 确定 finalClearFlag, finalClearColor;
            -3- 调用不同版本的 "SetRenderTarget()";  设置 render target, 且配置 clear 信息;
        */
        void SetRenderPassAttachments(//    读完__  没能看彻底
                                    CommandBuffer cmd, 
                                    ScriptableRenderPass renderPass, 
                                    ref CameraData cameraData
        ){

            Camera camera = cameraData.camera;
            ClearFlag cameraClearFlag = GetCameraClearFlag(ref cameraData);

            /*
                Invalid configuration - use current attachment setup
                Note: we only check color buffers. This is only technically correct 
                because for shadowmaps and depth only passes we bind depth as color and Unity handles it underneath. 
                so we never have a situation that all color buffers are null and depth is bound.
                ---
                正因为此处只检测 color buffer, 所以 shadowmaps 和 depth only passes 绑定的也都是 color buffer, 而不是 depth buffer;
                所以在这个设计中, 永远不会出现: "color buffers 全不可用, 但 depth buffer 可用" 的局面;
                (虽然这样配置才是合理的)
            */
            // 计算 render pass 的 colorAttachments 里有几个 有效的 color buffers
            uint validColorBuffersCount = RenderingUtils.GetValidColorBufferCount(renderPass.colorAttachments);
            if (validColorBuffersCount == 0)
                return;

            // =========== 两个大分支, 基于是否为 MRT ===========:

            // We use a different code path for MRT since it calls a different version of API SetRenderTarget
            if (RenderingUtils.IsMRT(renderPass.colorAttachments))//=================================================== MRT:
            {
                // In the MRT path we assume that all color attachments are REAL color attachments,
                // and that the depth attachment is a REAL depth attachment too.

                // Determine what attachments need to be cleared. ----------------
                bool needCustomCameraColorClear = false;
                bool needCustomCameraDepthClear = false;

                // 查找 本 renderer 的 color target 是否在 render pass 的 color targets 容器中; 若不存在, 返回 -1
                int cameraColorTargetIndex = RenderingUtils.IndexOf(renderPass.colorAttachments, m_CameraColorTarget);
                if (cameraColorTargetIndex != -1 && (m_FirstTimeCameraColorTargetIsBound))
                {
                    // register that we did clear the camera target the first time it was bound
                    m_FirstTimeCameraColorTargetIsBound = false; 

                    // Overlay cameras composite on top of previous ones. They don't clear.
                    // MTT: Commented due to not implemented yet (注释起来, 因为尚未实现)
                    //                    if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
                    //                        clearFlag = ClearFlag.None;

                    // We need to specifically clear the camera color target.
                    // But there is still a chance we don't need to issue individual clear() on each render-targets 
                    // if they all have the same clear parameters.
                    // 但如果每个 render target 都具有相同的 clear 参数, 我们仍有可能不需要在每个 render target 上发出单独的 clear()

                    needCustomCameraColorClear = (cameraClearFlag & ClearFlag.Color) != (renderPass.clearFlag & ClearFlag.Color)
                        || CoreUtils.ConvertSRGBToActiveColorSpace(camera.backgroundColor) != renderPass.clearColor;
                }

                // Note: if we have to give up the assumption(假设) that no depthTarget can be included in the MRT colorAttachments, 
                // we might need something like this:
                // int cameraTargetDepthIndex = IndexOf(renderPass.colorAttachments, m_CameraDepthTarget);
                // if( !renderTargetAlreadySet && cameraTargetDepthIndex != -1 && m_FirstTimeCameraDepthTargetIsBound)
                // { ...
                // }

                // 
                if( renderPass.depthAttachment==m_CameraDepthTarget && m_FirstTimeCameraDepthTargetIsBound )
                {
                    m_FirstTimeCameraDepthTargetIsBound = false;
                    needCustomCameraDepthClear = (cameraClearFlag&ClearFlag.Depth) != (renderPass.clearFlag&ClearFlag.Depth);
                }

                // Perform all clear operations needed. ----------------
                // We try to minimize calls to SetRenderTarget().

                // ===== set render target =====:

                // We get here only if cameraColorTarget needs to be handled separately from the rest of the color attachments.
                if (needCustomCameraColorClear)
                {
                    // Clear camera color render-target separately from the rest of the render-targets.

                    if ((cameraClearFlag & ClearFlag.Color) != 0)
                        SetRenderTarget(// 调用 -1-:
                            cmd, 
                            renderPass.colorAttachments[cameraColorTargetIndex], 
                            renderPass.depthAttachment, 
                            ClearFlag.Color, 
                            CoreUtils.ConvertSRGBToActiveColorSpace(camera.backgroundColor)
                        );

                    if ((renderPass.clearFlag & ClearFlag.Color) != 0)
                    {
                        // 处理 oth targets
                        // 容器中 有效且和 m_CameraColorTarget 不相同的元素 的个数;
                        uint otherTargetsCount = RenderingUtils.CountDistinct(renderPass.colorAttachments, m_CameraColorTarget);
                        // 挑选一个 指定数量的 容器; 将所有 oth targets 依次存入其中;
                        var nonCameraAttachments = m_TrimmedColorAttachmentCopies[otherTargetsCount];
                        int writeIndex = 0;
                        for (int readIndex = 0; readIndex < renderPass.colorAttachments.Length; ++readIndex)
                        {
                            if (renderPass.colorAttachments[readIndex] != m_CameraColorTarget && renderPass.colorAttachments[readIndex] != 0)
                            {
                                nonCameraAttachments[writeIndex] = renderPass.colorAttachments[readIndex];
                                ++writeIndex;
                            }
                        }

                        if (writeIndex != otherTargetsCount)
                            Debug.LogError("writeIndex and otherTargetsCount values differed. writeIndex:" 
                                + writeIndex + " otherTargetsCount:" + otherTargetsCount);
                        // 调用 -4-:
                        SetRenderTarget(
                            cmd, 
                            nonCameraAttachments, 
                            m_CameraDepthTarget, 
                            ClearFlag.Color, 
                            renderPass.clearColor
                        );
                    }
                }

                // Bind all attachments, clear color only if there was no custom behaviour for cameraColorTarget, clear depth as needed.
                ClearFlag finalClearFlag = ClearFlag.None;
                finalClearFlag |= needCustomCameraDepthClear ? (cameraClearFlag & ClearFlag.Depth) : (renderPass.clearFlag & ClearFlag.Depth);
                finalClearFlag |= needCustomCameraColorClear ? 0 : (renderPass.clearFlag & ClearFlag.Color);

                // Only setup render target if current render pass attachments are different from the active ones.
                // 只有当: 此次需要的 target 和 当前管线中 active 的 不同时, 才需要 重新绑定;
                if( !RenderingUtils.SequenceEqual(renderPass.colorAttachments, m_ActiveColorAttachments) || // 若两数组不同
                    renderPass.depthAttachment != m_ActiveDepthAttachment || 
                    finalClearFlag != ClearFlag.None
                ){
                    int lastValidRTindex = RenderingUtils.LastValid(renderPass.colorAttachments);
                    if (lastValidRTindex >= 0)// 找到了
                    {
                        int rtCount = lastValidRTindex + 1;
                        var trimmedAttachments = m_TrimmedColorAttachmentCopies[rtCount];
                        for (int i = 0; i < rtCount; ++i)
                            trimmedAttachments[i] = renderPass.colorAttachments[i];
                        // 调用 -4-:
                        SetRenderTarget(
                            cmd, 
                            trimmedAttachments, 
                            renderPass.depthAttachment, 
                            finalClearFlag, 
                            renderPass.clearColor
                        );
/*  tpr
#if ENABLE_VR && ENABLE_XR_MODULE
                        if (cameraData.xr.enabled)
                        {
                            // SetRenderTarget might alter the internal device state(winding order).
                            // Non-stereo buffer is already updated internally when switching render target. We update stereo buffers here to keep the consistency.
                            int xrTargetIndex = RenderingUtils.IndexOf(renderPass.colorAttachments, cameraData.xr.renderTarget);
                            bool isRenderToBackBufferTarget = (xrTargetIndex != -1) && !cameraData.xr.renderTargetIsRenderTexture;
                            cameraData.xr.UpdateGPUViewAndProjectionMatrices(cmd, ref cameraData, !isRenderToBackBufferTarget);
                        }
#endif
*/
                    }
                }
            }
            else//================================================================================================= no-MRT:
            {
                // Currently in non-MRT case, color attachment can actually be a depth attachment.

                // 本次 render pass 需要绑定的 render target:
                // (右侧这组值: render pass 的自定义代码中, 设定了自己的 render target;)
                RenderTargetIdentifier passColorAttachment = renderPass.colorAttachment;
                RenderTargetIdentifier passDepthAttachment = renderPass.depthAttachment;

                /*
                    When render pass doesn't call "ConfigureTarget()" 
                    we assume it's expected to render to camera target which might be backbuffer or the framebuffer render textures.
                */
                if (!renderPass.overrideCameraTarget)
                {// ----- 如果 render pass 并未改写 render target -----:
                    /*
                        "Default render pass attachment" for passes before main rendering is current active early return 
                        so we don't change current render target setup.
                        ---
                        执行时间点设置在此段区域的 render pass, 不作修改;
                    */
                    if (renderPass.renderPassEvent < RenderPassEvent.BeforeRenderingOpaques)
                        return;

                    // 改用 renderer 自己的数据
                    passColorAttachment = m_CameraColorTarget;
                    passDepthAttachment = m_CameraDepthTarget;
                }

                ClearFlag finalClearFlag = ClearFlag.None;
                Color finalClearColor;


                if( passColorAttachment == m_CameraColorTarget && // render pass 没有指定自己的 color target
                    (m_FirstTimeCameraColorTargetIsBound) // 只有 base camera 的第一次 ...
                ){
                    // register that we did clear the camera target the first time it was bound
                    m_FirstTimeCameraColorTargetIsBound = false; 

                    finalClearFlag |= (cameraClearFlag & ClearFlag.Color);
                    finalClearColor = CoreUtils.ConvertSRGBToActiveColorSpace(camera.backgroundColor);

                    if (m_FirstTimeCameraDepthTargetIsBound)
                    {
                        /*
                            m_CameraColorTarget can be an opaque pointer to a RenderTexture with depth-surface.
                            We cannot infer this information here, so we must assume both camera color and depth 
                            are first-time bound here (this is the legacy behaviour).
                            ---
                            m_CameraColorTarget 可能是一个指向具有 depth-surface 的 RenderTexture 的不透明指针。
                            我们不能在这里推断出这个信息，所以我们必须假设 camera 的 color 和 depth 在这里都是第一次绑定（这是遗留行为）。
                        */
                        m_FirstTimeCameraDepthTargetIsBound = false;
                        finalClearFlag |= (cameraClearFlag & ClearFlag.Depth);
                    }
                }
                else
                {// 要么 render pass 指定了自己的 render target, 要么 不再是两个 flag 第一次执行时:

                    finalClearFlag |= (renderPass.clearFlag & ClearFlag.Color);
                    finalClearColor = renderPass.clearColor;
                }

                /*
                    Condition (m_CameraDepthTarget!=BuiltinRenderTextureType.CameraTarget) below prevents 
                    m_FirstTimeCameraDepthTargetIsBound flag from being reset during non-camera passes 
                    (such as Color Grading LUT). This ensures that in those cases, 
                    cameraDepth will actually be cleared during the later camera pass.
                    ---

                */
                if( (m_CameraDepthTarget != BuiltinRenderTextureType.CameraTarget) && // renderer 端的 depth target 也被改写过
                    (passDepthAttachment == m_CameraDepthTarget || passColorAttachment == m_CameraDepthTarget) && 
                    m_FirstTimeCameraDepthTargetIsBound
                ){
                    m_FirstTimeCameraDepthTargetIsBound = false;

                    finalClearFlag |= (cameraClearFlag & ClearFlag.Depth);

                    // m_CameraDepthTarget is never a color-surface, so no need to add this here.
                    // finalClearFlag |= (cameraClearFlag & ClearFlag.Color);     urp 自己注释的
                    
                }
                else
                    finalClearFlag |= (renderPass.clearFlag & ClearFlag.Depth);

                // Only setup render target if current render pass attachments are different from the active ones
                // 只有当: 此次需要的 target 和 当前管线中 active 的 不同时, 才需呀 重新绑定;
                if( passColorAttachment != m_ActiveColorAttachments[0] || 
                    passDepthAttachment != m_ActiveDepthAttachment || 
                    finalClearFlag != ClearFlag.None
                ){
                    // 调用 -1-:
                    SetRenderTarget(
                        cmd, 
                        passColorAttachment, 
                        passDepthAttachment, 
                        finalClearFlag, 
                        finalClearColor
                    );

/*  tpr
#if ENABLE_VR && ENABLE_XR_MODULE
                    if (cameraData.xr.enabled)
                    {
                        // SetRenderTarget might alter the internal device state(winding order).
                        // Non-stereo buffer is already updated internally when switching render target. We update stereo buffers here to keep the consistency.
                        bool isRenderToBackBufferTarget = (passColorAttachment == cameraData.xr.renderTarget) && !cameraData.xr.renderTargetIsRenderTexture;
                        cameraData.xr.UpdateGPUViewAndProjectionMatrices(cmd, ref cameraData, !isRenderToBackBufferTarget);
                    }
#endif
*/
                }
            }
        }//  函数 SetRenderPassAttachments() 完__



        void BeginXRRendering(CommandBuffer cmd, ScriptableRenderContext context, ref CameraData cameraData)
        {
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            if (cameraData.xr.enabled)
            {
                cameraData.xr.StartSinglePass(cmd);
                cmd.EnableShaderKeyword(ShaderKeywordStrings.UseDrawProcedural);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
#endif
*/
        }//  函数完__


        void EndXRRendering(CommandBuffer cmd, ScriptableRenderContext context, ref CameraData cameraData)
        {//  函数完__
/*  tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            if (cameraData.xr.enabled)
            {
                cameraData.xr.StopSinglePass(cmd);
                cmd.DisableShaderKeyword(ShaderKeywordStrings.UseDrawProcedural);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
#endif
*/
        }//  函数完__


        // -1-:
        // 只有这个重载版本 是可以被外部调用的
        // 目前调用者: "CopyColorPass", "ScriptableRenderPass.Blit()" 
        // 此版本会强行将 color/depth target 的 RenderBufferStoreAction 设置为 Store;
        internal static void SetRenderTarget(//   读完__
                                        CommandBuffer cmd, 
                                        RenderTargetIdentifier colorAttachment, 
                                        RenderTargetIdentifier depthAttachment, 
                                        ClearFlag clearFlag,  // enum: None, Color, Depth, All;
                                        Color clearColor
        ){
            // ----- 同步数据:
            m_ActiveColorAttachments[0] = colorAttachment;
            for (int i = 1; i < m_ActiveColorAttachments.Length; ++i)
                m_ActiveColorAttachments[i] = 0;
            m_ActiveDepthAttachment = depthAttachment;
            // ------:

            RenderBufferLoadAction colorLoadAction = ((uint)clearFlag & (uint)ClearFlag.Color) != 0 ?
                RenderBufferLoadAction.DontCare :   // clear
                RenderBufferLoadAction.Load;        // 不 clear, 要加载到 rt 中

            RenderBufferLoadAction depthLoadAction = ((uint)clearFlag & (uint)ClearFlag.Depth) != 0 ?
                RenderBufferLoadAction.DontCare :   // clear
                RenderBufferLoadAction.Load;        // 不 clear, 要加载到 rt 中

            SetRenderTarget( // 调用 -3-:
                cmd, 
                colorAttachment, 
                colorLoadAction, 
                RenderBufferStoreAction.Store,// 默认居然是 Store ...
                depthAttachment, 
                depthLoadAction, 
                RenderBufferStoreAction.Store,// 默认居然是 Store ...
                clearFlag, 
                clearColor
            );
        }//  函数完__


        // -2-:
        static void SetRenderTarget(//    读完__
                            CommandBuffer cmd,
                            RenderTargetIdentifier colorAttachment,
                            RenderBufferLoadAction colorLoadAction,
                            RenderBufferStoreAction colorStoreAction,
                            ClearFlag clearFlags,
                            Color clearColor
        ){
            // 调用 -10- 号重载; 绑定单个 buffer, 比如单独的 color buffer
            CoreUtils.SetRenderTarget(cmd, colorAttachment, colorLoadAction, colorStoreAction, clearFlags, clearColor);
        }


        // -3-:
        static void SetRenderTarget(//    读完__
                                    CommandBuffer cmd,
                                    RenderTargetIdentifier colorAttachment,
                                    RenderBufferLoadAction colorLoadAction,
                                    RenderBufferStoreAction colorStoreAction,
                                    RenderTargetIdentifier depthAttachment,
                                    RenderBufferLoadAction depthLoadAction,
                                    RenderBufferStoreAction depthStoreAction,
                                    ClearFlag clearFlags,
                                    Color clearColor
        ){
            // XRTODO: Revisit the logic. Why treat CameraTarget depth specially?
            if( depthAttachment == BuiltinRenderTextureType.CameraTarget )
            {
                // 调用上面的 -2-: 仅绑定 color buffer
                SetRenderTarget(cmd, colorAttachment, colorLoadAction, colorStoreAction, clearFlags, clearColor);
            }
            else
            {
                CoreUtils.SetRenderTarget(// -12-:
                    cmd, 
                    colorAttachment, colorLoadAction, colorStoreAction,
                    depthAttachment, depthLoadAction, depthStoreAction, 
                    clearFlags, 
                    clearColor
                );
            }
        }


        // -4-:
        static void SetRenderTarget(//    读完__
                                    CommandBuffer cmd, 
                                    RenderTargetIdentifier[] colorAttachments, 
                                    RenderTargetIdentifier depthAttachment, 
                                    ClearFlag clearFlag, 
                                    Color clearColor
        ){
            // ----- 同步数据:
            m_ActiveColorAttachments = colorAttachments;
            m_ActiveDepthAttachment = depthAttachment;

            // 调用 -9-:
            CoreUtils.SetRenderTarget(cmd, colorAttachments, depthAttachment, clearFlag, clearColor);
        }




        [Conditional("UNITY_EDITOR")]
        void DrawGizmos(ScriptableRenderContext context, Camera camera, GizmoSubset gizmoSubset)// 读完__
        {
#if UNITY_EDITOR
            if (UnityEditor.Handles.ShouldRenderGizmos())
                context.DrawGizmos(camera, gizmoSubset);
#endif
        }


        [Conditional("UNITY_EDITOR")]
        void DrawWireOverlay(ScriptableRenderContext context, Camera camera)
        {
            context.DrawWireOverlay(camera);
        }



        // 调用每个 active render pass 的 OnCameraSetup() 函数;
        void InternalStartRendering(ScriptableRenderContext context, ref RenderingData renderingData)//  读完__
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, Profiling.internalStartRendering))
            {
                for (int i = 0; i < m_ActiveRenderPassQueue.Count; ++i)
                {
                    m_ActiveRenderPassQueue[i].OnCameraSetup(cmd, ref renderingData);
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }//   函数完__


        // 执行清理工作;
        void InternalFinishRendering(//   读完__
                                ScriptableRenderContext context, 
                                bool resolveFinalTarget // 如果这是 stack 中最后一个 camera
        ){
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, Profiling.internalFinishRendering))
            {
                for (int i = 0; i < m_ActiveRenderPassQueue.Count; ++i)
                    // 等同于调用 "OnCameraCleanup(cmd)"
                    // 执行用户自定义的 清理工作, 比如释放 render texture,
                    m_ActiveRenderPassQueue[i].FrameCleanup(cmd);

                // Happens when rendering the last camera in the camera stack.
                if (resolveFinalTarget)
                {
                    for (int i = 0; i < m_ActiveRenderPassQueue.Count; ++i)
                        // 执行用户自定义的 清理工作,
                        m_ActiveRenderPassQueue[i].OnFinishCameraStackRendering(cmd);

                    // renderer 实现者(如 ForwardRenderer) 自定义的 清理工作;
                    FinishRendering(cmd);

                    // We finished camera stacking and released all intermediate pipeline textures.
                    m_IsPipelineExecuting = false;
                }
                m_ActiveRenderPassQueue.Clear();
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }//  函数完__


        /*
            双指针排序, 排序规则是: "RenderPassEvent";
            如果两个 render pass 的 RenderPassEvent 值相同, 则保持原来的先后次序;
        */
        internal static void SortStable(List<ScriptableRenderPass> list)//   读完__
        {
            int j;
            for (int i = 1; i < list.Count; ++i)
            {
                ScriptableRenderPass curr = list[i];
                j = i - 1;

                for (; j >= 0 && curr < list[j]; --j)
                    list[j + 1] = list[j];

                list[j + 1] = curr;
            }
        }//  函数完__



        /*
            将所有的 actibe render pass 按照 RenderPassEvent 规则, 分成4个区间 block;
        */
        internal struct RenderBlocks//RenderBlocks__   读完__
            : IDisposable
        {
            /*
                存储 4 个 RenderPassEvent 值:
                -- BeforeRenderingPrePasses     150
                -- AfterRenderingOpaques        300
                        很奇怪, 为什么不是在 skybox 之后
                -- AfterRenderingPostProcessing 600
                -- Int32.MaxValue               +inf
            
                此容器数据仅在构造阶段被创建和使用, 然后就被立即释放了;
            */
            private NativeArray<RenderPassEvent> m_BlockEventLimits;

            // 存储 5 个 render pass idx (在queue中的idx), 充当一个 分隔栅栏的作用;
            // 分出 4 个 区间; 
            // [0] = 0,  
            // [1] 为 >= BeforeRenderingPrePasses      的第一个 pass 的 idx
            // [2] 为 >= AfterRenderingOpaques         的第一个 pass 的 idx
            // [3] 为 >= AfterRenderingPostProcessing  的第一个 pass 的 idx
            // [4] = activeRenderPassQueue.Count;      即最后一个 pass 的 idx
            private NativeArray<int> m_BlockRanges;

            // [0] block 1 的 pass 个数
            // [1] block 2 的 pass 个数
            // [2] block 3 的 pass 个数
            // [3] block 4 的 pass 个数
            // [4] 未使用
            private NativeArray<int> m_BlockRangeLengths;

            // 构造函数
            // 参数 activeRenderPassQueue 已经排序好了
            public RenderBlocks(List<ScriptableRenderPass> activeRenderPassQueue)//   读完__
            {
                // Upper limits for each block. Each block will contains render passes with events below the limit.
                m_BlockEventLimits = new NativeArray<RenderPassEvent>(k_RenderPassBlockCount, Allocator.Temp);// 4
                m_BlockRanges = new NativeArray<int>(m_BlockEventLimits.Length + 1, Allocator.Temp);// 5
                m_BlockRangeLengths = new NativeArray<int>(m_BlockRanges.Length, Allocator.Temp);// 5

                // 从 0 到 3 四个元素;
                m_BlockEventLimits[RenderPassBlock.BeforeRendering] =           RenderPassEvent.BeforeRenderingPrePasses;
                m_BlockEventLimits[RenderPassBlock.MainRenderingOpaque] =       RenderPassEvent.AfterRenderingOpaques;
                m_BlockEventLimits[RenderPassBlock.MainRenderingTransparent] =  RenderPassEvent.AfterRenderingPostProcessing;
                m_BlockEventLimits[RenderPassBlock.AfterRendering] =            (RenderPassEvent)Int32.MaxValue;

                /*
                    m_BlockRanges[0] is always 0
                    m_BlockRanges[i] is the index of the first RenderPass found in m_ActiveRenderPassQueue 
                    that has a ScriptableRenderPass.renderPassEvent higher(其实是大于等于) than blockEventLimits[i] 
                    (i.e, should be executed after blockEventLimits[i])
                    m_BlockRanges[blockEventLimits.Length] is m_ActiveRenderPassQueue.Count
                */
                // 初始化 m_BlockRanges 中的数据;
                FillBlockRanges(activeRenderPassQueue);
                m_BlockEventLimits.Dispose(); // 居然在这里释放了.....

                // 奇怪, 只写了前 4 个元素
                for (int i = 0; i < m_BlockRanges.Length - 1; i++)// {0,1,2,3}
                {
                    m_BlockRangeLengths[i] = m_BlockRanges[i + 1] - m_BlockRanges[i];
                }
            }//   函数完__


            //  RAII like Dispose pattern implementation for 'using' keyword
            public void Dispose() //   读完__
            {
                m_BlockRangeLengths.Dispose();
                m_BlockRanges.Dispose();
            }//   函数完__



            // Fill in render pass indices for each block. End index is startIndex + 1.
            // 参数 activeRenderPassQueue 已经排序好了
            void FillBlockRanges(List<ScriptableRenderPass> activeRenderPassQueue)//   读完__
            {
                int currRangeIndex = 0;
                int currRenderPass = 0;
                m_BlockRanges[currRangeIndex++] = 0; // m_BlockRanges[0] = 0;

                // For each block, it finds the first render pass index that has an event
                // higher than the block limit.
                for (int i = 0; i < m_BlockEventLimits.Length - 1; ++i)// {0,1,2}
                {
                    while( currRenderPass < activeRenderPassQueue.Count &&
                           activeRenderPassQueue[currRenderPass].renderPassEvent < m_BlockEventLimits[i]
                    ){
                        currRenderPass++;
                    }
                    // 现在, currRenderPass 的 renderPassEvent >= m_BlockEventLimits[i]
                    m_BlockRanges[currRangeIndex++] = currRenderPass;
                }

                m_BlockRanges[currRangeIndex] = activeRenderPassQueue.Count;
            }//   函数完__


            public int GetLength(int index)//   读完__
            {
                return m_BlockRangeLengths[index];
            }



            // Minimal foreach support
            // 用来遍历 pass idx 用的
            public struct BlockRange//BlockRange__
                : IDisposable
            {
                int m_Current;
                int m_End;

                // 参数: pass 在 queue 中的 idx 值;
                public BlockRange(int begin, int end)
                {
                    Assertions.Assert.IsTrue(begin <= end);// 否则抛出异常
                    m_Current = begin < end ? begin : end;
                    m_End   = end >= begin ? end : begin;
                    m_Current -= 1;
                }

                public BlockRange GetEnumerator() { return this; }
                public bool MoveNext() { return ++m_Current < m_End; }
                public int Current { get => m_Current; }
                public void Dispose() {}
            }//   函数完__


            public BlockRange GetRange(int index)
            {
                return new BlockRange(m_BlockRanges[index], m_BlockRanges[index + 1]);
            }
        }//  函数完__
    }
}
