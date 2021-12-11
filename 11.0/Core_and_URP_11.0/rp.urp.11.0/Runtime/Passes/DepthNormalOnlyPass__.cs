using System;

namespace UnityEngine.Rendering.Universal.Internal
{
    public class DepthNormalOnlyPass //DepthNormalOnlyPass__
        : ScriptableRenderPass
    {

        //    沿用 cameraTargetDescriptor, 并改写了部分配置, 
        internal RenderTextureDescriptor normalDescriptor { get; private set; }
        //    沿用 cameraTargetDescriptor, 并改写了部分配置, 
        internal RenderTextureDescriptor depthDescriptor { get; private set; }


        // 仅存储一个 render texture 的 id 信息, 比如 "_CameraDepthTexture"
        private RenderTargetHandle depthHandle { get; set; }

        // 仅存储一个 render texture 的 id 信息, 比如 "_CameraNormalsTexture"
        private RenderTargetHandle normalHandle { get; set; }

        private ShaderTagId m_ShaderTagId = new ShaderTagId("DepthNormals");
        private FilteringSettings m_FilteringSettings;

        // Constants
        private const int k_DepthBufferBits = 32;


        // 构造函数
        public DepthNormalOnlyPass( //  读完__
                                RenderPassEvent evt, 
                                RenderQueueRange renderQueueRange, 
                                LayerMask layerMask
        ){
            base.profilingSampler = new ProfilingSampler(nameof(DepthNormalOnlyPass));
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            renderPassEvent = evt; // base class 中的;
        }

        

        public void Setup( // 读完__
                        RenderTextureDescriptor baseDescriptor, // 比如: cameraTargetDescriptor
                        RenderTargetHandle depthHandle,  // 比如: "_CameraDepthTexture"
                        RenderTargetHandle normalHandle // 比如: "_CameraNormalsTexture"
        ){
            this.depthHandle = depthHandle;
            baseDescriptor.colorFormat = RenderTextureFormat.Depth;
            baseDescriptor.depthBufferBits = k_DepthBufferBits; // 32-bits
            baseDescriptor.msaaSamples = 1;// Depth-Only pass don't use MSAA
            depthDescriptor = baseDescriptor;

            this.normalHandle = normalHandle;
            baseDescriptor.colorFormat = RenderTextureFormat.RGHalf; // {R:16bits, G:16bits}
            baseDescriptor.depthBufferBits = 0; // 不存储 depth 信息
            baseDescriptor.msaaSamples = 1; 
            normalDescriptor = baseDescriptor;
        }



        /// <inheritdoc/>
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)//  读完__
        {
            // 这两个 render texture 也是 camera 要写入的 render target;
            cmd.GetTemporaryRT(normalHandle.id, normalDescriptor, FilterMode.Point);
            cmd.GetTemporaryRT(depthHandle.id, depthDescriptor, FilterMode.Point);

            // 类似于: "cmd.SetRenderTarget()"
            ConfigureTarget(
                new RenderTargetIdentifier(
                    normalHandle.Identifier(),  // get a RenderTargetIdentifier: "_CameraNormalsTexture"
                    0,                          // miplvl
                    CubemapFace.Unknown,        // 不设置
                    -1                          // depthSlice; -1 将执行 "RenderTargetIdentifier.AllDepthSlices" 所表达的意思
                                                //              即: 系统将使用 "default slice" 去写入 depth 数据; 
                ),
                new RenderTargetIdentifier(
                    depthHandle.Identifier(),   // get a RenderTargetIdentifier: "_CameraDepthTexture"
                    0, 
                    CubemapFace.Unknown, 
                    -1
                )
            );
            ConfigureClear(ClearFlag.All, Color.black);
        }


        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)//  读完__
        {
            // NOTE: Do NOT mix ProfilingScope with named CommandBuffers i.e. CommandBufferPool.Get("name").
            // Currently there's an issue which results in mismatched markers.
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.DepthNormalPrepass)))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // 处理 不透明物体的 排序技术 flags;
                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;

                var drawSettings = CreateDrawingSettings(
                    m_ShaderTagId,       // "DepthNormals"
                    ref renderingData, 
                    sortFlags
                );

                // 渲染时要 setup 哪些 "逐物体" 数据;
                drawSettings.perObjectData = PerObjectData.None;

                ref CameraData cameraData = ref renderingData.cameraData;
                Camera camera = cameraData.camera; // 感觉没用到

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
        public override void OnCameraCleanup(CommandBuffer cmd)//  读完__
        {
            if (cmd == null)
            {
                throw new ArgumentNullException("cmd");
            }

            if (depthHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(normalHandle.id);
                cmd.ReleaseTemporaryRT(depthHandle.id);
                normalHandle = RenderTargetHandle.CameraTarget;
                depthHandle = RenderTargetHandle.CameraTarget;
            }
        }
    }
}
