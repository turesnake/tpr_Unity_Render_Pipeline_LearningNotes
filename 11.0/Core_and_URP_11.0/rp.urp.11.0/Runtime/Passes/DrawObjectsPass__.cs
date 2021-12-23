using System;
using System.Collections.Generic;
using UnityEngine.Profiling;

namespace UnityEngine.Rendering.Universal.Internal
{

    /*
        Draw  objects into the given "color and depth target"

        You can use this "render pass" to render objects that have a material and/or shader
        with the pass names "UniversalForward" or "SRPDefaultUnlit".
        ---
        如果一个物体的 material/shader 带有名为 "UniversalForward" or "SRPDefaultUnlit" 的pass,
        就能用这个 class 来渲染这个物体;
        ---
        目前仅在 ForwardRenderer 中被使用;

    */

    public class DrawObjectsPass //DrawObjectsPass__
        : ScriptableRenderPass
    {
        FilteringSettings m_FilteringSettings;

        // "render state": 就是 shader 中设定的: blend, cull, ZClip, 等配置指令
        // 目前的 构造函数中, 只支持自定义 stenci test 部分
        RenderStateBlock m_RenderStateBlock;

        // 存储所有 pass id;  ("LightMode" pass tag)
        List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
        string m_ProfilerTag; // 代码分析用 name
        ProfilingSampler m_ProfilingSampler;
        bool m_IsOpaque;// 本 render pass 渲染的是否为 不透明物体

        // 此变量已在 shader 被归类为 "Deprecated" 的了... 没人使用它;
        static readonly int s_DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");


        /*
            构造函数 -1-:
        */
        /// <param name="renderQueueRange">
        ///           哪个物体的 Material.renderQueue 值位于此 range 范围内(包含边界), 这个物体就会被渲染; 比如 [0, 2000]
        /// </param>
        /// <param name="layerMask"> 如果一个物体的 GameObject.layer 和这个 变量 AND 计算后不为 0, 这个物体会被渲染;
        /// </param>     
        public DrawObjectsPass( // 读完__
                                string profilerTag, // 代码分析块的 name
                                ShaderTagId[] shaderTagIds, 
                                bool opaque,
                                RenderPassEvent evt, // 设置 render pass 何时执行. "BeforeRenderingOpaques"
                                RenderQueueRange renderQueueRange, 
                                LayerMask layerMask, 
                                StencilState stencilState, // shader 中的 "render state": stencil test 部分
                                int stencilReference // stencil test 的 Ref 值
        ){
            base.profilingSampler = new ProfilingSampler(nameof(DrawObjectsPass));

            m_ProfilerTag = profilerTag;
            m_ProfilingSampler = new ProfilingSampler(profilerTag);

            foreach (ShaderTagId sid in shaderTagIds)
                m_ShaderTagIdList.Add(sid);
            renderPassEvent = evt; // 设置 base class 成员
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);

            // 参数 Nothing 表示 暂不覆写任何 render state; (下方再手动设置)
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            m_IsOpaque = opaque;

            if (stencilState.enabled)
            {
                m_RenderStateBlock.stencilReference = stencilReference;
                m_RenderStateBlock.mask = RenderStateMask.Stencil; // 指定覆写 stencil test 块
                m_RenderStateBlock.stencilState = stencilState;
            }
        }// 函数完__


        /*
            构造函数 -2-:
            -- 自动准备了一组 pass id;
            -- 调用 -1-:
        */
        public DrawObjectsPass( // 读完__
                                string profilerTag, 
                                bool opaque, 
                                RenderPassEvent evt, 
                                RenderQueueRange renderQueueRange, 
                                LayerMask layerMask, 
                                StencilState stencilState, 
                                int stencilReference
        )
            : this( profilerTag,
                    // materials 的 pass, 如果它的 "LightMode" 值为如下之一, 这个 pass 就会被执行;
                    new ShaderTagId[] { new ShaderTagId("SRPDefaultUnlit"), 
                                        new ShaderTagId("UniversalForward"), 
                                        new ShaderTagId("UniversalForwardOnly"), 
                                        new ShaderTagId("LightweightForward")
                    },
                    opaque, 
                    evt, 
                    renderQueueRange, 
                    layerMask, 
                    stencilState, 
                    stencilReference
        ){}


        // 重载-3-:
        //  -- 用 参数 profileId 代替 参数 profilerTag
        //  -- 调用 重载2
        internal DrawObjectsPass( // 读完__
                                URPProfileId profileId,  // 新参数
                                bool opaque, 
                                RenderPassEvent evt, 
                                RenderQueueRange renderQueueRange, 
                                LayerMask layerMask, 
                                StencilState stencilState, 
                                int stencilReference
        )
            : this(
                    profileId.GetType().Name, 
                    opaque, 
                    evt, 
                    renderQueueRange, 
                    layerMask, 
                    stencilState, 
                    stencilReference
        ){
            m_ProfilingSampler = ProfilingSampler.Get(profileId);
        }


        /*
            根据 构造函数, 以及参数 renderingData 中提供的数据, 
            配置出:
                -- sortingSettings
                -- drawSettings
                -- filterSettings
            进而调用 context.DrawRenderers() 绘制一部分 可见物体;
        */
        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)// 读完__
        {
            // NOTE: Do NOT mix ProfilingScope with "named CommandBuffers" i.e. CommandBufferPool.Get("name").
            // Currently there's an issue which results in mismatched markers.
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {

                /*
                    Global render pass data containing various settings.
                    x,y,z are currently unused
                    w is used for knowing whether the object is opaque(1) or alpha blended(0)
                    ---
                    // 此变量已在 shader 被归类为 "Deprecated" 的了... 没人使用它;
                */
                Vector4 drawObjectPassData = new Vector4(0.0f, 0.0f, 0.0f, (m_IsOpaque) ? 1.0f : 0.0f);
                cmd.SetGlobalVector(s_DrawObjectPassDataPropID, drawObjectPassData);//"_DrawObjectPassData"


                // scaleBias.x = flipSign
                // scaleBias.y = scale
                // scaleBias.z = bias
                // scaleBias.w = unused
                float flipSign = (renderingData.cameraData.IsCameraProjectionMatrixFlipped()) ? -1.0f : 1.0f;
                Vector4 scaleBias = (flipSign < 0.0f)
                    ? new Vector4(flipSign, 1.0f, -1.0f, 1.0f)
                    : new Vector4(flipSign, 0.0f, 1.0f, 1.0f);
                cmd.SetGlobalVector(ShaderPropertyId.scaleBiasRt, scaleBias);//"_ScaleBiasRt"


                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();


                Camera camera = renderingData.cameraData.camera;
                var sortFlags = (m_IsOpaque) ? renderingData.cameraData.defaultOpaqueSortFlags : SortingCriteria.CommonTransparent;
                var drawSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortFlags);
                var filterSettings = m_FilteringSettings; // 如何过滤 待渲染物体;

                #if UNITY_EDITOR
                    // When rendering the preview camera, we want the layer mask to be forced to Everything
                    // editor 中的 "预览窗口" 使用的 camera
                    if (renderingData.cameraData.isPreviewCamera)
                    {
                        filterSettings.layerMask = -1; // 渲染所有物体
                    }
                #endif

                // 绘制一部分 可见物体, 覆写一部分 render state;
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings, ref m_RenderStateBlock);

                // Render objects that did not match any shader pass with error shader
                // 使用 紫红色 shader 来绘制那些 srp 不支持的 error/built-in pass;
                // 仅在 editor, development_build 模式下才被执行;
                RenderingUtils.RenderObjectsWithError(context, ref renderingData.cullResults, camera, filterSettings, SortingCriteria.None);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }// 函数完__
    }
}
