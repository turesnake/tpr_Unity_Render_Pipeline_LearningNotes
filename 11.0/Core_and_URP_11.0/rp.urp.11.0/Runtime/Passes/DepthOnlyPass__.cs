using System;

namespace UnityEngine.Rendering.Universal.Internal
{
    /*
        Render all "objects that have a 'DepthOnly' pass" into the given depth buffer.
        You can use this pass to prime(准备) a depth buffer for subsequent rendering.
        Use it as a z-prepass, or use it to generate a depth buffer.
        ---
        目前仅被 ForwardRenderer 使用过;

        在 "BeforeRenderingPrePasses" 时刻, 将 camera 视野中不透明物的 depth 信息, 写入 rt: "_CameraDepthTexture";
    
    */
    public class DepthOnlyPass //DepthOnlyPass__
        : ScriptableRenderPass
    {
        int kDepthBufferBits = 32;

        // 仅存储一个 render texture 的 id 信息, 比如 "_CameraDepthTexture"
        private RenderTargetHandle depthAttachmentHandle { get; set; }

        
        // 沿用 cameraTargetDescriptor, 并改写了部分配置, 
        internal RenderTextureDescriptor descriptor { get; private set; }

        FilteringSettings m_FilteringSettings;
        ShaderTagId m_ShaderTagId = new ShaderTagId("DepthOnly");


        // 构造函数
        public DepthOnlyPass( //  读完__
                            RenderPassEvent evt,  // 设置 render pass 何时执行; "BeforeRenderingPrePasses"
                            RenderQueueRange renderQueueRange, // renderQueue 区间值, 比如 "opaque"
                            LayerMask layerMask // 
        ){
            base.profilingSampler = new ProfilingSampler(nameof(DepthOnlyPass));
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            renderPassEvent = evt; // base class 中的;
        }



        public void Setup( //   读完__  第二遍
                    RenderTextureDescriptor baseDescriptor, // 比如: cameraData.cameraTargetDescriptor
                    RenderTargetHandle depthAttachmentHandle // 比如: "_CameraDepthTexture"
        ){
            this.depthAttachmentHandle = depthAttachmentHandle;

            // 改造 descriptor
            baseDescriptor.colorFormat = RenderTextureFormat.Depth;
            baseDescriptor.depthBufferBits = kDepthBufferBits; // 32-bits
            baseDescriptor.msaaSamples = 1;// Depth-Only pass don't use MSAA

            descriptor = baseDescriptor;
        }


        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)//  读完__  第二遍
        {
            // 这个 render texture 也是 camera 要写入的 render target;
            cmd.GetTemporaryRT(
                depthAttachmentHandle.id, // 比如: "_CameraDepthTexture"
                descriptor, 
                FilterMode.Point
            );

            /*
                调用-3-: 仅设置 color Attachment
                类似于: "cmd.SetRenderTarget()", 写到 ScriptableRenderPass 的 m_ColorAttachments[0] 上去;
                ---
                为什么不是设置 depth target ? 查看 "SetRenderPassAttachments()" 中描述; 简而言之:
                    如果不绑定任何 color Attachment, render pass 将不被渲染;
                    如果 render pass 只需要渲染 depth 数据, 应该将它写入 color Attachment 中;
            */

            // 确立了本 render pass 的 render target, ( 即 shader pass 的写入目标 )
            ConfigureTarget(new RenderTargetIdentifier(
                depthAttachmentHandle.Identifier(), // get a RenderTargetIdentifier
                0,                                  // miplvl
                CubemapFace.Unknown,                // 不设置
                -1                                  // depthSlice; -1 将执行 "RenderTargetIdentifier.AllDepthSlices" 所表达的意思
                                                    //              即: 系统将使用 "default slice" 去写入 depth 数据; 
            ));
            ConfigureClear(ClearFlag.All, Color.black);
        }



        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)//  读完__
        {
            // NOTE: Do NOT mix ProfilingScope with named CommandBuffers i.e. CommandBufferPool.Get("name").
            // Currently there's an issue which results in mismatched markers.
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.DepthPrepass)))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // 处理 不透明物体的 排序技术 flags;
                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;

                var drawSettings = CreateDrawingSettings(
                    m_ShaderTagId,     // shader pass: Tags{"LightMode" = "DepthOnly"}
                    ref renderingData, 
                    sortFlags
                );

                // 渲染时要 setup 哪些 "逐物体" 数据;
                drawSettings.perObjectData = PerObjectData.None;

                context.DrawRenderers(
                    renderingData.cullResults, 
                    ref drawSettings, 
                    ref m_FilteringSettings
                );
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }



        /// <inheritdoc/>
        public override void OnCameraCleanup(CommandBuffer cmd)// 读完__
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            // 即不为:"BuiltinRenderTextureType.CameraTarget", 说明是一个具体的 rt, 需要被释放
            if (depthAttachmentHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(depthAttachmentHandle.id);
                depthAttachmentHandle = RenderTargetHandle.CameraTarget;// 即:"BuiltinRenderTextureType.CameraTarget"
            }
        }
    }
}
