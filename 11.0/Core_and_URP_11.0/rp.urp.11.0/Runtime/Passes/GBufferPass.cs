using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using Unity.Collections;

namespace UnityEngine.Rendering.Universal.Internal
{
    /*
        Render all tiled-based deferred lights.
    */
    internal class GBufferPass //GBufferPass__RR
        : ScriptableRenderPass
    {
        static ShaderTagId s_ShaderTagLit = new ShaderTagId("Lit");
        static ShaderTagId s_ShaderTagSimpleLit = new ShaderTagId("SimpleLit");
        static ShaderTagId s_ShaderTagUnlit = new ShaderTagId("Unlit");
        static ShaderTagId s_ShaderTagUniversalGBuffer = new ShaderTagId("UniversalGBuffer");
        static ShaderTagId s_ShaderTagUniversalMaterialType = new ShaderTagId("UniversalMaterialType");

        ProfilingSampler m_ProfilingSampler = new ProfilingSampler("Render GBuffer");

        DeferredLights m_DeferredLights;

        ShaderTagId[] m_ShaderTagValues;
        RenderStateBlock[] m_RenderStateBlocks;

        FilteringSettings m_FilteringSettings;
        RenderStateBlock m_RenderStateBlock;

        // 构造函数
        public GBufferPass(
                        RenderPassEvent evt,  // render pass 在哪个时间点执行
                        RenderQueueRange renderQueueRange, 
                        LayerMask layerMask, 
                        StencilState stencilState, 
                        int stencilReference, 
                        DeferredLights deferredLights
        ){
            base.profilingSampler = new ProfilingSampler(nameof(GBufferPass));
            base.renderPassEvent = evt;// base class 中的;

            m_DeferredLights = deferredLights;
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

            m_RenderStateBlock.stencilState = stencilState;
            m_RenderStateBlock.stencilReference = stencilReference;
            m_RenderStateBlock.mask = RenderStateMask.Stencil;

            m_ShaderTagValues = new ShaderTagId[4];
            m_ShaderTagValues[0] = s_ShaderTagLit;      //"Lit"
            m_ShaderTagValues[1] = s_ShaderTagSimpleLit;//"SimpleLit"
            m_ShaderTagValues[2] = s_ShaderTagUnlit;    //"Unlit"
            
            // Special catch all case for materials where "UniversalMaterialType" is not defined or the tag value doesn't match anything we know.
            // 如果一个 material 的 subshader 的 tag: "UniversalMaterialType" 没有被设置, 或者设置的值 不匹配已知的选项时, 就使用这个 [3] 值;
            // "UniversalMaterialType" 长被设置为: "Lit", "SimpleLit", "Unlit", "ComplexLit" 这几个值;
            m_ShaderTagValues[3] = new ShaderTagId(); 
            


            m_RenderStateBlocks = new RenderStateBlock[4];
            m_RenderStateBlocks[0] = DeferredLights.OverwriteStencil(m_RenderStateBlock, (int)StencilUsage.MaterialMask, (int)StencilUsage.MaterialLit);
            m_RenderStateBlocks[1] = DeferredLights.OverwriteStencil(m_RenderStateBlock, (int)StencilUsage.MaterialMask, (int)StencilUsage.MaterialSimpleLit);
            m_RenderStateBlocks[2] = DeferredLights.OverwriteStencil(m_RenderStateBlock, (int)StencilUsage.MaterialMask, (int)StencilUsage.MaterialUnlit);
            m_RenderStateBlocks[3] = m_RenderStateBlocks[0];
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTargetHandle[] gbufferAttachments = m_DeferredLights.GbufferAttachments;

            // Create and declare the render targets used in the pass
            for (int i = 0; i < gbufferAttachments.Length; ++i)
            {
                // Lighting buffer has already been declared with line ConfigureCameraTarget(m_ActiveCameraColorAttachment.Identifier(), ...) in DeferredRenderer.Setup
                if (i != m_DeferredLights.GBufferLightingIndex)
                {
                    RenderTextureDescriptor gbufferSlice = cameraTextureDescriptor;
                    gbufferSlice.depthBufferBits = 0; // make sure no depth surface is actually created
                    gbufferSlice.stencilFormat = GraphicsFormat.None;
                    gbufferSlice.graphicsFormat = m_DeferredLights.GetGBufferFormat(i);
                    cmd.GetTemporaryRT(m_DeferredLights.GbufferAttachments[i].id, gbufferSlice);
                }
            }

            ConfigureTarget(m_DeferredLights.GbufferAttachmentIdentifiers, m_DeferredLights.DepthAttachmentIdentifier);
            // We must explicitely specify we don't want any clear to avoid unwanted side-effects.
            // ScriptableRenderer may still implicitely force a clear the first time the camera color/depth targets are bound.
            ConfigureClear(ClearFlag.None, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer gbufferCommands = CommandBufferPool.Get();
            using (new ProfilingScope(gbufferCommands, m_ProfilingSampler))
            {
                // User can stack several scriptable renderers during rendering but deferred renderer should only lit pixels added by this gbuffer pass.
                // If we detect we are in such case (camera isin  overlay mode), we clear the highest bits of stencil we have control of and use them to
                // mark what pixel to shade during deferred pass. Gbuffer will always mark pixels using their material types.
                if (m_DeferredLights.IsOverlay)
                    m_DeferredLights.ClearStencilPartial(gbufferCommands);

                context.ExecuteCommandBuffer(gbufferCommands);
                gbufferCommands.Clear();

                ref CameraData cameraData = ref renderingData.cameraData;
                Camera camera = cameraData.camera;
                ShaderTagId lightModeTag = s_ShaderTagUniversalGBuffer;//"UniversalGBuffer"
                DrawingSettings drawingSettings = CreateDrawingSettings(lightModeTag, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
                ShaderTagId universalMaterialTypeTag = s_ShaderTagUniversalMaterialType;//"UniversalMaterialType"

                NativeArray<ShaderTagId> tagValues = new NativeArray<ShaderTagId>(m_ShaderTagValues, Allocator.Temp);
                NativeArray<RenderStateBlock> stateBlocks = new NativeArray<RenderStateBlock>(m_RenderStateBlocks, Allocator.Temp);
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings, universalMaterialTypeTag, false, tagValues, stateBlocks);
                tagValues.Dispose();
                stateBlocks.Dispose();

                // Render objects that did not match any shader pass with error shader
                RenderingUtils.RenderObjectsWithError(context, ref renderingData.cullResults, camera, m_FilteringSettings, SortingCriteria.None);
            }
            context.ExecuteCommandBuffer(gbufferCommands);
            CommandBufferPool.Release(gbufferCommands);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            RenderTargetHandle[] gbufferAttachments = m_DeferredLights.GbufferAttachments;

            for (int i = 0; i < gbufferAttachments.Length; ++i)
                if (i != m_DeferredLights.GBufferLightingIndex)
                    cmd.ReleaseTemporaryRT(gbufferAttachments[i].id);
        }
    }
}
