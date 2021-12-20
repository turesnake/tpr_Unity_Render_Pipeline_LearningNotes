using System;

namespace UnityEngine.Rendering.Universal.Internal
{
    /*
        Copy the given color buffer to the given dest color buffer.
        You can use this pass to copy a color buffer to the dest, so you can use it later in rendering.
        For example, you can copy the opaque texture to use it for distortion effects(扭曲效果);
        ---
        在 "AfterRenderingSkybox" 时刻, 将 opaque color 数据, 
        从 "_CameraColorTexture" 或 "BuiltinRenderTextureType.CameraTarget", 复制到 "_CameraOpaqueTexture";
    */
    public class CopyColorPass //CopyColorPass__RR
        : ScriptableRenderPass
    {
        int m_SampleOffsetShaderHandle;
        Material m_SamplingMaterial;
        Downsampling m_DownsamplingMethod;
        Material m_CopyColorMaterial; // // "Shaders/Utils/Blit.shader"

        private RenderTargetIdentifier source { get; set; } // "_CameraColorTexture" 或 "BuiltinRenderTextureType.CameraTarget"
        private RenderTargetHandle destination { get; set; } // "_CameraOpaqueTexture"


        // 构造函数
        public CopyColorPass(
                        RenderPassEvent evt,  // 设置 render pass 何时执行
                        Material samplingMaterial, 
                        Material copyColorMaterial = null // "Shaders/Utils/Blit.shader"
        ){
            base.profilingSampler = new ProfilingSampler(nameof(CopyColorPass));

            m_SamplingMaterial = samplingMaterial;
            m_CopyColorMaterial = copyColorMaterial;
            m_SampleOffsetShaderHandle = Shader.PropertyToID("_SampleOffset");
            renderPassEvent = evt;
            m_DownsamplingMethod = Downsampling.None;
        }


        /// <summary>
        /// Configure the pass with the source and destination to execute on.
        /// </summary>
        /// <param name="source">Source Render Target</param>
        /// <param name="destination">Destination Render Target</param>
        public void Setup(
                        RenderTargetIdentifier source,  // "_CameraColorTexture" 或 "BuiltinRenderTextureType.CameraTarget"
                        RenderTargetHandle destination, // "_CameraOpaqueTexture"
                        Downsampling downsampling
        ){
            this.source = source;
            this.destination = destination;
            m_DownsamplingMethod = downsampling;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            descriptor.depthBufferBits = 0;
            if (m_DownsamplingMethod == Downsampling._2xBilinear)
            {
                descriptor.width /= 2;
                descriptor.height /= 2;
            }
            else if (m_DownsamplingMethod == Downsampling._4xBox || m_DownsamplingMethod == Downsampling._4xBilinear)
            {
                descriptor.width /= 4;
                descriptor.height /= 4;
            }

            cmd.GetTemporaryRT(destination.id, descriptor, m_DownsamplingMethod == Downsampling.None ? FilterMode.Point : FilterMode.Bilinear);
        }



        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_SamplingMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", m_SamplingMaterial, GetType().Name);
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.CopyColor)))
            {
                RenderTargetIdentifier opaqueColorRT = destination.Identifier();// "_CameraOpaqueTexture"

                // 调用-1-:
                // 此版本会强行将 color/depth target 的 RenderBufferStoreAction 设置为 Store;
                ScriptableRenderer.SetRenderTarget(
                    cmd, 
                    opaqueColorRT,                          // colorAttachment, "_CameraOpaqueTexture"
                    BuiltinRenderTextureType.CameraTarget,  // depthAttachment, 
                    clearFlag,  // 沿用 base class 的数据
                    clearColor  // 沿用 base class 的数据
                );

                bool useDrawProceduleBlit = renderingData.cameraData.xr.enabled;// xr
                switch (m_DownsamplingMethod)
                {
                    case Downsampling.None:
                        RenderingUtils.Blit(
                            cmd, 
                            source,             // src
                            opaqueColorRT,      // dst
                            m_CopyColorMaterial, // "Shaders/Utils/Blit.shader"
                            0,                   // pass idx
                            useDrawProceduleBlit // false
                            // -- colorLoadAction = RenderBufferLoadAction.Load
                            // -- colorStoreAction = RenderBufferStoreAction.Store
                            // -- depthLoadAction = RenderBufferLoadAction.Load
                            // -- depthStoreAction = RenderBufferStoreAction.Store
                        );
                        break;
                    case Downsampling._2xBilinear:
                        RenderingUtils.Blit(cmd, source, opaqueColorRT, m_CopyColorMaterial, 0, useDrawProceduleBlit);
                        break;
                    case Downsampling._4xBox:
                        m_SamplingMaterial.SetFloat(m_SampleOffsetShaderHandle, 2);
                        RenderingUtils.Blit(cmd, source, opaqueColorRT, m_SamplingMaterial, 0, useDrawProceduleBlit);
                        break;
                    case Downsampling._4xBilinear:
                        RenderingUtils.Blit(cmd, source, opaqueColorRT, m_CopyColorMaterial, 0, useDrawProceduleBlit);
                        break;
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }



        /// <inheritdoc/>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (destination != RenderTargetHandle.CameraTarget)// 即:"BuiltinRenderTextureType.CameraTarget"
            {
                cmd.ReleaseTemporaryRT(destination.id);
                destination = RenderTargetHandle.CameraTarget;// 即:"BuiltinRenderTextureType.CameraTarget"
            }
        }
    }
}
