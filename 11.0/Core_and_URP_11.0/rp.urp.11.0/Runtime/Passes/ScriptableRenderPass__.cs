using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.Rendering.Universal
{
    
    /* 
       "ScriptableRenderPass" 的 input 需求;
       tpr:
            感觉 falgs 之间可以组合

       建议查看 "ConfigureInput()" 就在本文件中 
    */
    [Flags]
    public enum ScriptableRenderPassInput //ScriptableRenderPassInput__
    {
        None = 0,
        Depth = 1 << 0, // 1
        Normal = 1 << 1,// 2
        Color = 1 << 2, // 4
    }


    /*
        Controls when the "render pass" executes.
        控制 render pass 何时执行;

        Note: Spaced built-in events so we can add events in between them;
        We need to leave room "as we sort render passes based on event".
        Users can also inject "render pass events" in a specific point by doing RenderPassEvent + offset
        --
        注意:
            在 built-in events 之间设置间隔, 以便我们可以在它们之间添加 事件;
            在我们基于 event 对 render passes 排序期间, 留出空间;
            用户也能通过 "RenderPassEvent + offset" 的格式, 在特定位置点上 注入 "render pass events"
    */
    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public enum RenderPassEvent//RenderPassEvent__
    {
        /*
            Executes a "ScriptableRenderPass" before rendering any other passes in the pipeline.
            --
            在管线的 任何其它 passes 之前, 渲染本 "ScriptableRenderPass";
            此时, camera矩阵 和 "stereo rendering" 并未被设置; 

            你可以在这个时间点上, 绘制一些 "自定义 input textures", 以便它们在后续流程中被使用;
            比如 LUT textures
        */
        BeforeRendering = 0,
        
        /*
            Executes a "ScriptableRenderPass" before rendering shadowmaps.
            --
            此时, camera矩阵 和 "stereo rendering" 并未被设置; 
            (毕竟渲染 shadow 信息只需要 light 矩阵...)
        */
        BeforeRenderingShadows = 50,

        /* 
            Executes a "ScriptableRenderPass" after rendering shadowmaps.
            --
            此时, camera矩阵 和 "stereo rendering" 并未被设置; 
            (毕竟渲染 shadow 信息只需要 light 矩阵...)
        */
        AfterRenderingShadows = 100,

        /*
            Executes a "ScriptableRenderPass" before rendering prepasses,
            --
            在 渲染 prepasses 之前, 比如, 在渲染 depth prepass 之前 
            此时, camera矩阵 和 "stereo rendering" 已经被设置 !!! 
            (毕竟, 计算 depth, normal 数据都是基于 camera 视角)
            =================

            prepass:
                DepthOnlyPass, DepthNormalOnlyPass 似乎都属于;
        */
        BeforeRenderingPrePasses = 150,

        /*
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Obsolete, to match the capital from 'Prepass' to 'PrePass' (UnityUpgradable) -> BeforeRenderingPrePasses")]
        BeforeRenderingPrepasses = 151,
        */

        /*
            Executes a "ScriptableRenderPass" after rendering prepasses,
            --
            在 渲染 prepasses 之后, 比如, 在渲染 depth prepass 之后 
            此时, camera矩阵 和 "stereo rendering" 已经被设置 !!! 
            (毕竟, 计算 depth, normal 数据都是基于 camera 视角)
            =================

            prepass:
                DepthOnlyPass, DepthNormalOnlyPass 似乎都属于;
        */
        AfterRenderingPrePasses = 200,
        
        /*
            Executes a "ScriptableRenderPass" before rendering opaque objects.

            在 渲染实心物体之前;
        */
        BeforeRenderingOpaques = 250,
        //    Executes a "ScriptableRenderPass" after rendering opaque objects.
        AfterRenderingOpaques = 300,

        /*
            Executes a "ScriptableRenderPass" before rendering the sky.
        */
        BeforeRenderingSkybox = 350,
        //    Executes a "ScriptableRenderPass" after rendering the sky.
        AfterRenderingSkybox = 400,
    
        /*
            Executes a "ScriptableRenderPass" before rendering transparent objects.
        */
        BeforeRenderingTransparents = 450,
        //    Executes a "ScriptableRenderPass" after rendering transparent objects.
        AfterRenderingTransparents = 500,
        
        /*
            Executes a "ScriptableRenderPass" before rendering post-processing effects.
        */
        BeforeRenderingPostProcessing = 550,

        /*
            Executes a "ScriptableRenderPass" after rendering post-processing effects.
            --
            但是又在: "final blit", "post-processing AA effects" and "color grading" 之前
        */
        AfterRenderingPostProcessing = 600,

        /*
            Executes a "ScriptableRenderPass" after rendering all effects.
        */
        AfterRendering = 1000,
    }



    /*
        =====================================================================================================================
        本类能实现一个 "logical rendering pass", 能用它来拓展 urp;
        ---
        用户可以自定义一个 class 来继承本类, 
        这个 "派生 render pass" 可以被指定在某个特定的时间点 被管线调用;
        用户可以实现这个 render pass 的工作内容, 

        具体示范可参考 主笔记中的 "ColorContrast.cs" 文件;

            本 "派生 render pass" 通常要和 "ScriptableRendererFeature 的派生类" 一起使用才行;

    */
    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public abstract partial class ScriptableRenderPass//ScriptableRenderPass__
    {
        public RenderPassEvent renderPassEvent { get; set; } // 设置 render pass 何时执行;

        public RenderTargetIdentifier[] colorAttachments
        {
            get => m_ColorAttachments;
        }

        public RenderTargetIdentifier colorAttachment
        {
            get => m_ColorAttachments[0];
        }

        public RenderTargetIdentifier depthAttachment
        {
            get => m_DepthAttachment;
        }

        
        /*
            本 render pass 需要的 input 数据; 
            可通过 "ScriptableRenderPass.ConfigureInput()" 函数来设置此值; (见本文下方)
            enum: None, Depth, Normal, Color; flags 可组合;
        */
        public ScriptableRenderPassInput input
        {
            get => m_Input;
        }


        public ClearFlag clearFlag
        {
            get => m_ClearFlag;
        }

        public Color clearColor
        {
            get => m_ClearColor;
        }
        

        /*
            A ProfilingSampler for the entire pass. Used by higher level objects such as "ScriptableRenderer" etc.
        */
        protected internal ProfilingSampler profilingSampler { get; set; }


        /*
            "m_ColorAttachments[0]", "m_DepthAttachment" 最初都被绑定为 "BuiltinRenderTextureType.CameraTarget";
            (即: current camera 的 render target, 注意,这不意味着它一定就是 current active render target);
            此时本值为 false;

            如果用户调用 "ConfigureTarget()" 重新设置了 color 和 depth target, 
            此时本值为 true; 
        */
        internal bool overrideCameraTarget { get; set; }
        internal bool isBlitRenderPass { get; set; }

        // 可以用 "BuiltinRenderTextureType" 去构造一个 RenderTargetIdentifier 实例;
        RenderTargetIdentifier[] m_ColorAttachments = new RenderTargetIdentifier[] {BuiltinRenderTextureType.CameraTarget};
        RenderTargetIdentifier m_DepthAttachment = BuiltinRenderTextureType.CameraTarget;
        ScriptableRenderPassInput m_Input = ScriptableRenderPassInput.None;
        ClearFlag m_ClearFlag = ClearFlag.None;
        Color m_ClearColor = Color.black;

        /*
            构造函数
        */
        public ScriptableRenderPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            m_ColorAttachments = new RenderTargetIdentifier[] {BuiltinRenderTextureType.CameraTarget, 0, 0, 0, 0, 0, 0, 0};
            m_DepthAttachment = BuiltinRenderTextureType.CameraTarget;
            m_ClearFlag = ClearFlag.None;
            m_ClearColor = Color.black;
            overrideCameraTarget = false;
            isBlitRenderPass = false;
            profilingSampler = new ProfilingSampler(nameof(ScriptableRenderPass));
        }



        /*
            配置此 render pass 的 "input Requirements": None,Depth, Normal, Color;
            本函数必须在 "ScriptableRendererFeature.AddRenderPasses()" 函数实现体内 调用;
        */
        public void ConfigureInput(ScriptableRenderPassInput passInput)
        {
            m_Input = passInput;
        }


        /*
            Configures render targets for this render pass.

            用本函数来取代: "CommandBuffer.SetRenderTarget()"
            本函数应该在 "ScriptableRenderPass.Configure()" 体内调用; (见本文件下方)
            tpr:
                源码中也看到在 "ScriptableRenderPass.OnCameraSetup()" 体内被调用;
        */
        public void ConfigureTarget(RenderTargetIdentifier colorAttachment, RenderTargetIdentifier depthAttachment)
        {
            m_DepthAttachment = depthAttachment;
            ConfigureTarget(colorAttachment);
        }

        // 重构
        public void ConfigureTarget(RenderTargetIdentifier[] colorAttachments, RenderTargetIdentifier depthAttachment)
        {
            overrideCameraTarget = true;

            // 计算出 参数 colorAttachments 中 "有效的 color buffer" 的个数; (如果 id值不为0, 就是有效的)
            uint nonNullColorBuffers = RenderingUtils.GetValidColorBufferCount(colorAttachments);
            // 系统只支持 "同时向有限个 rt 输出数据", 不能超过 系统支持的最大值;
            if (nonNullColorBuffers > SystemInfo.supportedRenderTargetCount)
                Debug.LogError("Trying to set " + nonNullColorBuffers + " renderTargets, which is more than the maximum supported:" + SystemInfo.supportedRenderTargetCount);

            m_ColorAttachments = colorAttachments;
            m_DepthAttachment = depthAttachment;
        }

        // 重构
        public void ConfigureTarget(RenderTargetIdentifier colorAttachment)
        {
            overrideCameraTarget = true;

            m_ColorAttachments[0] = colorAttachment;
            for (int i = 1; i < m_ColorAttachments.Length; ++i)
                m_ColorAttachments[i] = 0;
        }

        // 重构
        public void ConfigureTarget(RenderTargetIdentifier[] colorAttachments)
        {
            ConfigureTarget(colorAttachments, BuiltinRenderTextureType.CameraTarget);
        }



        /*
            Configures clearing for the render targets for this render pass.
            --
            配置本 render targets 的 clear 设置;
            本函数应该在 "ScriptableRenderPass.Configure()" 体内调用; (见本文件下方)
            tpr:
                源码中也看到在 "ScriptableRenderPass.OnCameraSetup()" 体内被调用;
        */
        public void ConfigureClear(ClearFlag clearFlag, Color clearColor)
        {
            m_ClearFlag = clearFlag;
            m_ClearColor = clearColor;
        }



        

        /*
            ------------------------------------------------------------------- +++
            通常是一个 class 继承了 "ScriptableRenderPass", 然后它会覆写本函数;
            ===
            在正式渲染一个 camera 之前, 本函数会被 renderer 调用 (比如 Forward Renderer);
            (另一说是) 在执行 render pass 之前, 本函数会被调用;

            可以在本函数中实现:
                -- configure render targets and their clear state
                -- create temporary render target textures

            如果本函数为空, 这个 render pass 会被渲染进 "active camera render target";

            永远不要调用 "CommandBuffer.SetRenderTarget()", 
            而要改用 本文件内的 "ConfigureTarget()", "ConfigureClear()" 函数;
            管线能保证高效地 "setup target" 和 "clear target";
        */
        /// <param name="cmd">CommandBuffer to enqueue rendering commands. This will be executed by the pipeline;
        ///                     将需要的 渲染指令 安排进 render queue; 
        /// </param>
        /// <param name="renderingData">Current rendering state information</param>
        public virtual void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {}


        /*
            ------------------------------------------------------------------- +++
            通常是一个 class 继承了 "ScriptableRenderPass", 然后它会覆写本函数;
            ===
            在执行 render pass 之前, 本函数会被 renderer 调用 (比如 Forward Renderer);
            在 "ScriptableRenderer.ExecuteRenderPass()" 中被调用;

            可在本函数体内实现:
            -- configure render targets and their clear state
            -- create temporary render target textures
            
            如果不覆写此函数, 这个 render pass 会渲染进 active Camera's render target;

            永远不要调用 "CommandBuffer.SetRenderTarget()", 
            而要改用 本文件内的 "ConfigureTarget()", "ConfigureClear()" 函数;
            管线能保证高效地 "setup target" 和 "clear target";
        */
        /// <param name="cmd">CommandBuffer to enqueue rendering commands. This will be executed by the pipeline;
        ///                     将需要的 渲染指令 安排进 render queue; 
        /// </param>
        /// <param name="cameraTextureDescriptor">描述了 camera render target 的细节;</param>
        public virtual void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {}

        /*
            ------------------------------------------------------------------- +++
            通常是一个 class 继承了 "ScriptableRenderPass", 然后它会覆写本函数;
            ===
            在完成渲染 camera 时, 本函数会被调用, 

            可在此函数体内释放本 render pass 新建的所有资源;

            本函数会清理 camera stack 中的所有 cameras;
        */
        /// <param name="cmd">Use this CommandBuffer to cleanup any generated data</param>
        public virtual void OnCameraCleanup(CommandBuffer cmd)
        {}


        /*
            ------------------------------------------------------------------- +++
            通常是一个 class 继承了 "ScriptableRenderPass", 然后它会覆写本函数;
            ===
            在完成整个 camera stack 的渲染后, 本函数会被调用;
            可在本函数实现体内释放由本 render pass 新建的任何资源;

            如果一个 camera 没有显式的 camera stack, 它也被认为是一个 camera stack,
            不过这个 stack 内只有它一个 camera;
        */
        /// <param name="cmd">Use this CommandBuffer to cleanup any generated data</param>
        public virtual void OnFinishCameraStackRendering(CommandBuffer cmd)
        {}


        /*
            ------------------------------------------------------------------- +++
            本函数必须要被 ScriptableRenderPass 的继承则完成
            ===
            可在本函数体内编写: 渲染逻辑本身, 也就是 用户希望本 render pass 要做的那些工作;

            使用参数 context 来发送 绘制指令, 执行 commandbuffers;

            不需要在本函数实现体内 调用 "ScriptableRenderContext.submit()", 渲染管线会在何时的时间点自动调用它;
        */
        /// <param name="context">Use this render context to issue(发射) any draw commands during execution</param>
        /// <param name="renderingData">Current rendering state information</param>
        public abstract void Execute(ScriptableRenderContext context, ref RenderingData renderingData);


        /*
            Add a blit command to the context for execution. 
            This changes the active render target in the ScriptableRenderer to destination.
            --
            本函数会将 "ScriptableRenderer" 的 active render target 改写为 参数 destination 设置的值;
        */
        /// <param name="cmd">Command buffer to record command for execution.</param>
        /// <param name="source">Source texture or target identifier to blit from.</param>
        /// <param name="destination">Destination texture or target identifier to blit into. 
        ///                           This becomes the renderer active render target.</param>
        /// <param name="material">Material to use.</param>
        /// <param name="passIndex">Shader pass to use. Default is 0.</param>
        /// <seealso cref="ScriptableRenderer"/>
        /// 
        public void Blit(CommandBuffer cmd, RenderTargetIdentifier source, 
                        RenderTargetIdentifier destination, Material material = null, int passIndex = 0
        ){
            // 其实调用了 "CoreUtils.SetRenderTarget()";
            ScriptableRenderer.SetRenderTarget(cmd, destination, BuiltinRenderTextureType.CameraTarget, clearFlag, clearColor);
            cmd.Blit(source, destination, material, passIndex);
        }



        /*
            重载-1-:
            Creates "DrawingSettings" based on current the rendering state.
        */
        public DrawingSettings CreateDrawingSettings(
                                                ShaderTagId shaderTagId,  // 要渲染的 shader pass
                                                ref RenderingData renderingData, // Current rendering state
                                                SortingCriteria sortingCriteria //Criteria to sort objects being rendered
        ){
            Camera camera = renderingData.cameraData.camera;
            SortingSettings sortingSettings = new SortingSettings(camera) { criteria = sortingCriteria };
            DrawingSettings settings = new DrawingSettings(shaderTagId, sortingSettings)
            {
                perObjectData = renderingData.perObjectData,
                mainLightIndex = renderingData.lightData.mainLightIndex,
                enableDynamicBatching = renderingData.supportsDynamicBatching,
                /*
                    Disable instancing for preview cameras. This is consistent with the built-in forward renderer. 
                    Also fixes case 1127324.
                    ---
                    如果 camera 类型为 Preview, 就关闭 GPU Instancing, 可能和某个系统错误有关;
                */
                enableInstancing = camera.cameraType==CameraType.Preview ? false : true,
            };
            return settings;
        }


        /*
            重载-2-;
        */
        public DrawingSettings CreateDrawingSettings(
                                    List<ShaderTagId> shaderTagIdList, // 要渲染的 好几个 shader passes
                                    ref RenderingData renderingData, // Current rendering state
                                    SortingCriteria sortingCriteria // Criteria to sort objects being rendered
        ){
            if (shaderTagIdList==null || shaderTagIdList.Count==0)
            {
                Debug.LogWarning("ShaderTagId list is invalid. DrawingSettings is created with default pipeline ShaderTagId");
                return CreateDrawingSettings(new ShaderTagId("UniversalPipeline"), ref renderingData, sortingCriteria);
            }

            DrawingSettings settings = CreateDrawingSettings(shaderTagIdList[0], ref renderingData, sortingCriteria);
            for (int i = 1; i < shaderTagIdList.Count; ++i)
                settings.SetShaderPassName(i, shaderTagIdList[i]);
            return settings;
        }





        public static bool operator<(ScriptableRenderPass lhs, ScriptableRenderPass rhs)
        {
            return lhs.renderPassEvent < rhs.renderPassEvent;
        }

        public static bool operator>(ScriptableRenderPass lhs, ScriptableRenderPass rhs)
        {
            return lhs.renderPassEvent > rhs.renderPassEvent;
        }
    }
}
