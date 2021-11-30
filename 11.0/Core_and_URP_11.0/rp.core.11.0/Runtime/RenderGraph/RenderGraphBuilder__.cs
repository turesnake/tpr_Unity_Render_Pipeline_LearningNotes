using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.RenderGraphModule
{
    /*
        Use this struct to set up a new Render Pass.
        由 "RenderGraph.AddRenderPass<T>()" 返回;

        这是构建与 render pass 相关的信息的入口点。
        有几个核心部分:
        -- Declaring resource usage:
            这是 Render Graph API 中最重要的一部分; 
            在这里,你明确声明 render pass 是否需要对资源进行 读和/或写 访问。
            这允许 render graph 获得整个 渲染帧 的整体视图，
            从而确定 GPU 内存的最佳使用, 和各种 render pass 之间的同步点。

        -- Declaring the rendering function:
            这是调用 图形命令 的函数。
            它接收 "您为 render pass 定义的 pass data", 
            把它作为: 参数 和 render graph contexts;
            通过 "SetRenderFunc()" 来设置一个 render pass 的 渲染函数;
            在 graph 编译完毕后, 这个函数就会被运行;

        -- Creating transient(瞬态) resources:
            瞬态或内部资源 是您仅在此 render pass 期间创建的资源。
            你在 本builder 实例内部创建它们, (而不是在 rendr graph 层面)
            以便控制它们的生命周期; 
            创建 瞬态资源 使用与 RenderGraph API 中的等效函数相同的参数。

            如果一个 临时buffer 不应该在 pass 外部被访问, 就应该用此办法来创建;

            一旦超出这个 pass, 这个资源的 handle 就会变成 invalid, 
            此时如果你想访问它, unity将报错;

    */
    public struct RenderGraphBuilder //RenderGraphBuilder__
        : IDisposable
    {
        RenderGraphPass             m_RenderPass;
        RenderGraphResourceRegistry m_Resources;
        RenderGraph                 m_RenderGraph;
        bool                        m_Disposed;

        // 只是用来折叠代码的 工具
        #region Public Interface


        /*
            Specify that the pass will use a Texture resource as a color render target.
            This has the same effect as WriteTexture and also automatically sets the Texture to use as a render target.
            --
            指定一个 texture 为 "color render target";
            这与下方的 "WriteTexture()" 具有相同的效果，
            
            但本函数还会在 pass 开始时, 将目标 texture 绑定为一个 render texture (render target),
            参数 index 指定了 render texture 序号(下标), 
            (当存在 multiple render target 时, 需要用到这个 idx)
        */
        /// <param name="input">The Texture resource to use as a "color render target".</param>
        /// <param name="index">Index for multiple render target usage.</param>
        /// <returns>An updated resource handle to the input resource.</returns>
        public TextureHandle UseColorBuffer(in TextureHandle input, int index)
        {
            CheckResource(input.handle);
            m_Resources.IncrementWriteCount(input.handle);
            m_RenderPass.SetColorBuffer(input, index);
            return input;
        }


        /*
            Specify that the pass will use a Texture resource as a depth buffer.
            --
            本函数与下方的 "WriteTexture()" 具有相同的效果，

            但本函数还会自动将目标 texture 绑定为一个 depth texture;
            (这回只有一个, 所以无需 idx)

            参数 flags (enum: Read, Write, ReadWrite) 指定了对这个 depth texture 的访问性质;
        */
        /// <param name="input">The Texture resource to use as a depth buffer during the pass.</param>
        /// <param name="flags">Specify the access level for the depth buffer. This allows you to say 
        ///                     whether you will read from or write to the depth buffer, or do both.
        /// </param>
        /// <returns>An updated resource handle to the input resource.</returns>
        public TextureHandle UseDepthBuffer(in TextureHandle input, DepthAccess flags)
        {
            CheckResource(input.handle);
            m_Resources.IncrementWriteCount(input.handle);
            m_RenderPass.SetDepthBuffer(input, flags);
            return input;
        }


        /*
            Specify "a Texture resource to read from" during the pass.
            本 render pass 将从这个 texture 中读取数据;
        */
        /// <param name="input">The Texture resource to read from during the pass.</param>
        /// <returns>An updated resource handle to the input resource.</returns>
        public TextureHandle ReadTexture(in TextureHandle input)
        {
            CheckResource(input.handle);

            if (!m_Resources.IsRenderGraphResourceImported(input.handle) && m_Resources.TextureNeedsFallback(input))
            {
                var texDimension = m_Resources.GetTextureResourceDesc(input.handle).dimension;
                if (texDimension == TextureXR.dimension)
                {
                    return m_RenderGraph.defaultResources.blackTextureXR;
                }
                else if (texDimension == TextureDimension.Tex3D)
                {
                    return m_RenderGraph.defaultResources.blackTexture3DXR;
                }
                else
                {
                    return m_RenderGraph.defaultResources.blackTexture;
                }
            }

            m_RenderPass.AddResourceRead(input.handle);
            return input;
        }


        /*
            Specify "a Texture resource to write to" during the pass.
            本 render pass 的渲染/计算数据 将写入这个 texture 中;
        */
        /// <param name="input">The Texture resource to write to during the pass.</param>
        /// <returns>An updated resource handle to the input resource.</returns>
        public TextureHandle WriteTexture(in TextureHandle input)
        {
            CheckResource(input.handle);
            m_Resources.IncrementWriteCount(input.handle);
            // TODO RENDERGRAPH: Manage resource "version" for debugging purpose
            m_RenderPass.AddResourceWrite(input.handle);
            return input;
        }


        /*
            Specify a Texture resource to read and write to during the pass.
        */
        /// <param name="input">The Texture resource to read and write to during the pass.</param>
        /// <returns>An updated resource handle to the input resource.</returns>
        public TextureHandle ReadWriteTexture(in TextureHandle input)
        {
            CheckResource(input.handle);
            m_Resources.IncrementWriteCount(input.handle);
            m_RenderPass.AddResourceWrite(input.handle);
            m_RenderPass.AddResourceRead(input.handle);
            return input;
        }


        /*
            Create a new Render Graph Texture resource.
            This texture will only be available for the current pass and will be assumed to be both written and read 
            so users don't need to add explicit read/write declarations.
            --
            此瞬态 texture 仅在本 render pass 期间有效, 它同时支持读写;
        */
        /// <param name="desc">Texture descriptor.</param>
        /// <returns>A new transient TextureHandle.</returns>
        public TextureHandle CreateTransientTexture(in TextureDesc desc)
        {
            var result = m_Resources.CreateTexture(desc, m_RenderPass.index);
            m_RenderPass.AddTransientResource(result.handle);
            return result;
        }

        /*
            重载, using the descriptor from another texture 来创建 瞬态texture;
            此瞬态 texture 仅在本 render pass 期间有效, 它同时支持读写;
        */
        /// <param name="texture">Texture from which the descriptor should be used.</param>
        /// <returns>A new transient TextureHandle.</returns>
        public TextureHandle CreateTransientTexture(in TextureHandle texture)
        {
            var desc = m_Resources.GetTextureResourceDesc(texture.handle);
            var result = m_Resources.CreateTexture(desc, m_RenderPass.index);
            m_RenderPass.AddTransientResource(result.handle);
            return result;
        }


        /*
            Specify a Renderer List resource to use during the pass.
            --
            The render pass uses the "RendererList.Draw" command to render the list.
        */
        /// <param name="input">The Renderer List resource to use during the pass.</param>
        /// <returns>An updated resource handle to the input resource.</returns>
        public RendererListHandle UseRendererList(in RendererListHandle input)
        {
            m_RenderPass.UseRendererList(input);
            return input;
        }


        /*
            Specify a Compute Buffer resource to read from during the pass.
        */
        /// <param name="input">The Compute Buffer resource to read from during the pass.</param>
        /// <returns>An updated resource handle to the input resource.</returns>
        public ComputeBufferHandle ReadComputeBuffer(in ComputeBufferHandle input)
        {
            CheckResource(input.handle);
            m_RenderPass.AddResourceRead(input.handle);
            return input;
        }

        /*
            Specify a Compute Buffer resource to write to during the pass.
        */
        /// <param name="input">The Compute Buffer resource to write to during the pass.</param>
        /// <returns>An updated resource handle to the input resource.</returns>
        public ComputeBufferHandle WriteComputeBuffer(in ComputeBufferHandle input)
        {
            CheckResource(input.handle);
            m_RenderPass.AddResourceWrite(input.handle);
            m_Resources.IncrementWriteCount(input.handle);
            return input;
        }

        /*
            Create a new Render Graph Compute Buffer resource.
            此资源仅在本 render pass 运行期间可用,  同时支持读写;
        */
        /// <param name="desc">Compute Buffer descriptor.</param>
        /// <returns>A new transient ComputeBufferHandle.</returns>
        public ComputeBufferHandle CreateTransientComputeBuffer(in ComputeBufferDesc desc)
        {
            var result = m_Resources.CreateComputeBuffer(desc, m_RenderPass.index);
            m_RenderPass.AddTransientResource(result.handle);
            return result;
        }

        /*
            重载
            Create a new Render Graph Compute Buffer resource 
            using the descriptor from another Compute Buffer.
            --
            此资源仅在本 render pass 运行期间可用,  同时支持读写;
        */
        /// <param name="computebuffer">Compute Buffer from which the descriptor should be used.</param>
        /// <returns>A new transient ComputeBufferHandle.</returns>
        public ComputeBufferHandle CreateTransientComputeBuffer(in ComputeBufferHandle computebuffer)
        {
            var desc = m_Resources.GetComputeBufferResourceDesc(computebuffer.handle);
            var result = m_Resources.CreateComputeBuffer(desc, m_RenderPass.index);
            m_RenderPass.AddTransientResource(result.handle);
            return result;
        }


        /*
            Specify the render function to use for this pass.
            A call to this is mandatory for the pass to be valid.
            --
            本函数必须被调用, render pass 才算被设置完成;
        */
        /// <typeparam name="PassData">The Type of the class that provides data to the Render Pass.</typeparam>
        /// <param name="renderFunc">Render function for the pass.</param>
        public void SetRenderFunc<PassData>(RenderFunc<PassData> renderFunc) where PassData : class, new()
        {
            ((RenderGraphPass<PassData>)m_RenderPass).renderFunc = renderFunc;
        }


        /*
            Enable asynchronous compute for this pass.
        */
        /// <param name="value">Set to true to enable asynchronous compute.</param>
        public void EnableAsyncCompute(bool value)
        {
            m_RenderPass.EnableAsyncCompute(value);
        }



        /*
            Allow or not pass culling
            ---
            By default all passes can be culled out if the render graph detects it's not actually used.
            In some cases, a pass may not write or read any texture but rather do something with side effects 
            (like setting a global texture parameter for example).
            This function can be used to tell the system that it should not cull this pass.
       
            通常, 所有 render pass 都要开启 cull 功能;
            这样, 当一个 render pass 不被使用时, 系统可以把这个 pass 直接剔除掉;

            但是有些 pass, 它不执行任何读写操作, 但它存在一些 side effects;
            ( 比如, 此pass 会设置 a global texture parameter )
            对于这样的 render pass, 就要向本函数传递参数 false, 以便告诉系统, 无论如何, 本 render pass 都不能被剔除掉;
        */
        /// <param name="value">True to allow pass culling.</param>
        public void AllowPassCulling(bool value)
        {
            m_RenderPass.AllowPassCulling(value);
        }


        // Dispose the RenderGraphBuilder instance.
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion



        #region Internal Interface
        internal RenderGraphBuilder(RenderGraphPass renderPass, RenderGraphResourceRegistry resources, RenderGraph renderGraph)
        {
            m_RenderPass = renderPass;
            m_Resources = resources;
            m_RenderGraph = renderGraph;
            m_Disposed = false;
        }

        void Dispose(bool disposing)
        {
            if (m_Disposed)
                return;

            m_RenderGraph.OnPassAdded(m_RenderPass);
            m_Disposed = true;
        }

        void CheckResource(in ResourceHandle res)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (res.IsValid())
            {
                int transientIndex = m_Resources.GetRenderGraphResourceTransientIndex(res);
                if (transientIndex == m_RenderPass.index)
                {
                    Debug.LogError($"Trying to read or write a transient resource at pass {m_RenderPass.name}.Transient resource are always assumed to be both read and written.");
                }

                if (transientIndex != -1 && transientIndex != m_RenderPass.index)
                {
                    throw new ArgumentException($"Trying to use a transient texture (pass index {transientIndex}) in a different pass (pass index {m_RenderPass.index}).");
                }
            }
            else
            {
                throw new ArgumentException($"Trying to use an invalid resource (pass {m_RenderPass.name}).");
            }
#endif
        }

        internal void GenerateDebugData(bool value)
        {
            m_RenderPass.GenerateDebugData(value);
        }

        #endregion
    }
}
