namespace UnityEngine.Rendering.Universal.Internal
{
    /*
        Copy the given color target to the current camera target

        You can use this pass to copy the result of rendering to the camera target.
        The pass takes the screen viewport into consideration.
    */
    public class FinalBlitPass //FinalBlitPass__RR
        : ScriptableRenderPass
    {
        RenderTargetHandle m_Source;
        Material m_BlitMaterial; // 如: "Shaders/Utils/Blit.shader"

        

        // 构造函数
        public FinalBlitPass(//   读完__
                            RenderPassEvent evt,  // 设置 render pass 何时执行; 如: AfterRendering + 1
                            Material blitMaterial // 如: "Shaders/Utils/Blit.shader"
        ){
            base.profilingSampler = new ProfilingSampler(nameof(FinalBlitPass));

            m_BlitMaterial = blitMaterial;
            renderPassEvent = evt; // base class 中的;
        }


  
        public void Setup(//   读完__
                    RenderTextureDescriptor baseDescriptor, 
                    RenderTargetHandle colorHandle
        ){
            m_Source = colorHandle;
        }



        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_BlitMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", m_BlitMaterial, GetType().Name);
                return;
            }


            /*
                Note: We need to get the cameraData.targetTexture as this will get the targetTexture of the camera stack.
                Overlay cameras need to output to the target described in the base camera while doing camera stack.
                ---
                base camera 的 "targetTexture"; 也是整个 stack 的 render target;
            */
            ref CameraData cameraData = ref renderingData.cameraData;
            RenderTargetIdentifier cameraTarget = (cameraData.targetTexture != null) ? 
                        new RenderTargetIdentifier(cameraData.targetTexture) : 
                        BuiltinRenderTextureType.CameraTarget;//仅指: "current camera 的 render target", 但它不一定是: "Currently active render target";


            bool isSceneViewCamera = cameraData.isSceneViewCamera;// 是否为 editor 中 scene窗口 使用的 camera;
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.FinalBlit)))
            {
                // 如果启用了 linear 工作流, 且 backbuffer 不支持 "自动执行 linear->sRGB 转换",
                // 那么当把 backbuffer 定位一次 Blit() 的目的地时, 
                // 需要启用此 keyword, 并在 shader 中手动执行 "linear->sRGB" 转换;
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.LinearToSRGBConversion,//"_LINEAR_TO_SRGB_CONVERSION"
                    cameraData.requireSrgbConversion);

                cmd.SetGlobalTexture(ShaderPropertyId.sourceTex, m_Source.Identifier());//"_SourceTex"

/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
                if (cameraData.xr.enabled)
                {
                    int depthSlice = cameraData.xr.singlePassEnabled ? -1 : cameraData.xr.GetTextureArraySlice();
                    cameraTarget =
                        new RenderTargetIdentifier(cameraData.xr.renderTarget, 0, CubemapFace.Unknown, depthSlice);

                    CoreUtils.SetRenderTarget(
                        cmd,
                        cameraTarget,
                        RenderBufferLoadAction.Load,
                        RenderBufferStoreAction.Store,
                        ClearFlag.None,
                        Color.black);

                    cmd.SetViewport(cameraData.pixelRect);

                    // We y-flip if
                    // 1) we are bliting from render texture to back buffer(UV starts at bottom) and
                    // 2) renderTexture starts UV at top
                    bool yflip = !cameraData.xr.renderTargetIsRenderTexture && SystemInfo.graphicsUVStartsAtTop;
                    Vector4 scaleBias = yflip ? new Vector4(1, -1, 0, 1) : new Vector4(1, 1, 0, 0);
                    cmd.SetGlobalVector(ShaderPropertyId.scaleBias, scaleBias);

                    cmd.DrawProcedural(Matrix4x4.identity, m_BlitMaterial, 0, MeshTopology.Quads, 4);
                }
                else
#endif
*/

                if (isSceneViewCamera || cameraData.isDefaultViewport)
                {
                    // This set render target is necessary so we change the LOAD state to DontCare.
                    cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,
                        RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, // color
                        RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare); // depth
                    cmd.Blit(m_Source.Identifier(), cameraTarget, m_BlitMaterial);
                }
                else
                {
                    // TODO: Final blit pass should always blit to backbuffer. The first time we do we don't need to Load contents to tile.
                    // We need to keep in the pipeline of first render pass to each render target to properly set load/store actions.
                    // meanwhile we set to load so split screen case works.
                    CoreUtils.SetRenderTarget(
                        cmd,
                        cameraTarget,
                        RenderBufferLoadAction.Load,
                        RenderBufferStoreAction.Store,
                        ClearFlag.None,
                        Color.black);

                    Camera camera = cameraData.camera;
                    cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                    cmd.SetViewport(cameraData.pixelRect);
                    cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, m_BlitMaterial);
                    cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
