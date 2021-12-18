using System;

namespace UnityEngine.Rendering.Universal.Internal
{

    /*
        Copy the given "depth buffer" into the given dest depth buffer.

        使用本pass 来复制 depth buffer 数据到 dest 中, 以便后续渲染使用;
        -- 如果 src texture 启用了 MSAA, the pass uses a custom MSAA resolve;
        -- 如果没启用 MSAA, the pass uses a Blit or a Copy Texture operation, (当前平台支持哪个操作, 就用哪个)
    */ 
    public class CopyDepthPass //CopyDepthPass__
        : ScriptableRenderPass
    {
        private RenderTargetHandle source { get; set; }
        private RenderTargetHandle destination { get; set; }

        internal bool AllocateRT  { get; set; }// 猜测: 是否由本类来分配 dest 的 render texture
        Material m_CopyDepthMaterial;


        // 构造函数
        public CopyDepthPass(//   读完__
                        RenderPassEvent evt,  // 设置 render pass 何时执行
                        Material copyDepthMaterial // 比如: "Shaders/Utils/CopyDepth.shader"
        ){
            base.profilingSampler = new ProfilingSampler(nameof(CopyDepthPass));
            AllocateRT = true;
            m_CopyDepthMaterial = copyDepthMaterial;
            renderPassEvent = evt; // base class 中的
        }


        /*
            Configure the pass with the source and destination to execute on.
        */
        public void Setup(//  读完__
                        RenderTargetHandle source,  
                        RenderTargetHandle destination
        ){
            this.source = source;
            this.destination = destination;
            // 当 dest 只有 nameId, 没有 rtid 时, 也许这意味着 dest 尚未分配 render texture, 需要本类来分配一个
            this.AllocateRT = AllocateRT && !destination.HasInternalRenderTargetId();
        }


        /*
            ------------------------------------------------------------------- +++
            在正式渲染一个 camera 之前, 本函数会被 renderer 调用 (比如 Forward Renderer);
            (另一说是) 在执行 render pass 之前, 本函数会被调用;

            可以在本函数中实现:
                -- configure render targets and their clear state
                -- create temporary render target textures

            如果本函数为空, 这个 render pass 会被渲染进 "active camera render target";

            永远不要调用 "CommandBuffer.SetRenderTarget()", 
            而要改用 "ScriptableRenderPass" 内的 "ConfigureTarget()", "ConfigureClear()" 函数;
            管线能保证高效地 "setup target" 和 "clear target";
        */
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)// 读完__
        {
            // 此 struct 包含用来创建 RenderTexture 所需的一切信息。
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.colorFormat = RenderTextureFormat.Depth;
            descriptor.depthBufferBits = 32; //TODO: do we really need this. double check;
            // "multisample antialiasing level", 猜测表示 "每像素采样个数"
            descriptor.msaaSamples = 1;
            if (this.AllocateRT)
                cmd.GetTemporaryRT(
                    destination.id, // 上方代码已明确得知, dest 只有 nameId 可用, 此处可安全地直接访问 nameId
                    descriptor, 
                    FilterMode.Point
                );

            // On Metal iOS, prevent camera attachments to be bound and cleared during this pass.
            //  防止在此 pass 中 "绑定和清除 camera 的 attachments";

            /*
                调用-3-: 仅设置 color Attachment
                为什么不是设置 depth target ? 查看 "SetRenderPassAttachments()" 中描述; 简而言之:
                    如果不绑定任何 color Attachment, render pass 将不被渲染;
                    如果 render pass 只需要渲染 depth 数据, 应该将它写入 color Attachment 中;
            */
            ConfigureTarget(
                new RenderTargetIdentifier(
                    destination.Identifier(),// renderTargetIdentifier
                    0,                      // miplvl;   只使用最大的那一层, 作为 render target
                    CubemapFace.Unknown,    // cubeFace  不设置
                    -1                      // depthSlice, 使用 默认层存储 depth 数据;
                )
            );
            // set render target 的 clear 设置
            ConfigureClear(ClearFlag.None, Color.black);
        }



        /*
            ------------------------------------------------------------------- +++
            可在本函数体内编写: 渲染逻辑本身, 也就是 用户希望本 render pass 要做的那些工作;

            使用参数 context 来发送 绘制指令, 执行 commandbuffers;

            不需要在本函数实现体内 调用 "ScriptableRenderContext.submit()", 渲染管线会在何时的时间点自动调用它;
        */
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_CopyDepthMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", m_CopyDepthMaterial, GetType().Name);
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();// 这个 cmd 不创建 profiling markers
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.CopyDepth)))
            {
                // 此 struct 包含用来创建 RenderTexture 所需的一切信息。
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                int cameraSamples = descriptor.msaaSamples;

                CameraData cameraData = renderingData.cameraData;

                switch (cameraSamples)
                {
                    case 8:
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                        cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);//8
                        break;

                    case 4:
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                        cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);//4
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                        break;

                    case 2:
                        cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);//2
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                        break;

                    // MSAA disabled
                    default:
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                        break;
                }

                cmd.SetGlobalTexture("_CameraDepthAttachment", source.Identifier());

/* tpr
#if ENABLE_VR && ENABLE_XR_MODULE
                // XR uses procedural draw instead of cmd.blit or cmd.DrawFullScreenMesh
                if (renderingData.cameraData.xr.enabled)
                {
                    // XR flip logic is not the same as non-XR case because XR uses draw procedure
                    // and draw procedure does not need to take projection matrix yflip into account
                    // We y-flip if
                    // 1) we are bliting from render texture to back buffer and
                    // 2) renderTexture starts UV at top
                    // XRTODO: handle scalebias and scalebiasRt for src and dst separately
                    bool isRenderToBackBufferTarget = destination.Identifier() == cameraData.xr.renderTarget && !cameraData.xr.renderTargetIsRenderTexture;
                    bool yflip = isRenderToBackBufferTarget && SystemInfo.graphicsUVStartsAtTop;
                    float flipSign = (yflip) ? -1.0f : 1.0f;
                    Vector4 scaleBiasRt = (flipSign < 0.0f)
                        ? new Vector4(flipSign, 1.0f, -1.0f, 1.0f)
                        : new Vector4(flipSign, 0.0f, 1.0f, 1.0f);
                    cmd.SetGlobalVector(ShaderPropertyId.scaleBiasRt, scaleBiasRt);

                    cmd.DrawProcedural(Matrix4x4.identity, m_CopyDepthMaterial, 0, MeshTopology.Quads, 4);
                }
                else
#endif
*/
                {
                    /*
                    // Blit has logic to flip projection-matrix when rendering to render texture.
                    // Currently the y-flip is handled in CopyDepthPass.hlsl by checking _ProjectionParams.x
                    // If you replace this Blit with a Draw* (如下方的 "cmd.DrawMesh") 
                    // that sets projection matrix double check to also update shader.
                    // ---
                    // scaleBias.x = flipSign
                    // scaleBias.y = scale
                    // scaleBias.z = bias
                    // scaleBias.w = unused
                        ----
                        总之,因为某种原因, 没有直接使用 "_ProjectionParams.x", 而是手动配置了一个新的变量 去表达相同的值;
                    
                        其实在 pass 中也只用到 x 分量;
                    */
                    float flipSign = (cameraData.IsCameraProjectionMatrixFlipped()) ? -1.0f : 1.0f;

                    Vector4 scaleBiasRt = (flipSign < 0.0f)
                        ? new Vector4(flipSign, 1.0f, -1.0f, 1.0f)
                        : new Vector4(flipSign, 0.0f, 1.0f, 1.0f);
                    cmd.SetGlobalVector(ShaderPropertyId.scaleBiasRt, scaleBiasRt);//"_ScaleBiasRt"

                    // 执行 Blit 工作, (也许还带有 MSAA 滤波)
                    cmd.DrawMesh(
                        RenderingUtils.fullscreenMesh,  // 一个 全屏 quad mesh
                        Matrix4x4.identity,             // 变换矩阵, 不做任何变换
                        m_CopyDepthMaterial             // material
                    );
                }
            }//using end


            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }





        /// <inheritdoc/>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (this.AllocateRT)
                cmd.ReleaseTemporaryRT(destination.id);
            destination = RenderTargetHandle.CameraTarget;// 即:"BuiltinRenderTextureType.CameraTarget"
        }
    }
}
