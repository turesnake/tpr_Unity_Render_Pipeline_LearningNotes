using System;
using System.Diagnostics;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.RenderGraphModule
{
    /// <summary>
    /// Texture resource handle.
    /// </summary>
    [DebuggerDisplay("Texture ({handle.index})")]
    public struct TextureHandle
    {
        private static TextureHandle s_NullHandle = new TextureHandle();

        /// <summary>
        /// Returns a null texture handle
        /// </summary>
        /// <returns>A null texture handle.</returns>
        public static TextureHandle nullHandle { get { return s_NullHandle; } }

        internal ResourceHandle handle;

        internal TextureHandle(int handle, bool shared = false) { this.handle = new ResourceHandle(handle, RenderGraphResourceType.Texture, shared); }

        /// <summary>
        /// Cast to RenderTargetIdentifier
        /// </summary>
        /// <param name="texture">Input TextureHandle.</param>
        /// <returns>Resource as a RenderTargetIdentifier.</returns>
        public static implicit operator RenderTargetIdentifier(TextureHandle texture) => texture.IsValid() ? RenderGraphResourceRegistry.current.GetTexture(texture) : default(RenderTargetIdentifier);

        /// <summary>
        /// Cast to Texture
        /// </summary>
        /// <param name="texture">Input TextureHandle.</param>
        /// <returns>Resource as a Texture.</returns>
        public static implicit operator Texture(TextureHandle texture) => texture.IsValid() ? RenderGraphResourceRegistry.current.GetTexture(texture) : null;

        /// <summary>
        /// Cast to RenderTexture
        /// </summary>
        /// <param name="texture">Input TextureHandle.</param>
        /// <returns>Resource as a RenderTexture.</returns>
        public static implicit operator RenderTexture(TextureHandle texture) => texture.IsValid() ? RenderGraphResourceRegistry.current.GetTexture(texture) : null;

        /// <summary>
        /// Cast to RTHandle
        /// </summary>
        /// <param name="texture">Input TextureHandle.</param>
        /// <returns>Resource as a RTHandle.</returns>
        public static implicit operator RTHandle(TextureHandle texture) => texture.IsValid() ? RenderGraphResourceRegistry.current.GetTexture(texture) : null;

        /// <summary>
        /// Return true if the handle is valid.
        /// </summary>
        /// <returns>True if the handle is valid.</returns>
        public bool IsValid() => handle.IsValid();
    }


    // The mode that determines the size of a Texture.
    public enum TextureSizeMode//TextureSizeMode__
    {
        Explicit, // Explicit size 显式
        Scale, // Size automatically scaled by a Vector
        Functor // Size automatically scaled by a Functor
    }



#if UNITY_2020_2_OR_NEWER
    /// <summary>
    /// Subset of the texture desc containing information for fast memory allocation (when platform supports it)
    /// </summary>
    public struct FastMemoryDesc
    {
        ///<summary>Whether the texture will be in fast memory.</summary>
        public bool inFastMemory;
        ///<summary>Flag to determine what parts of the render target is spilled if not fully resident in fast memory.</summary>
        public FastMemoryFlags flags;
        ///<summary>How much of the render target is to be switched into fast memory (between 0 and 1).</summary>
        public float residencyFraction;
    }
#endif


    /*
        Descriptor used to create texture resources
    */
    public struct TextureDesc//TextureDesc__
    {
        
        public TextureSizeMode sizeMode;
        // texture width/height
        public int width;
        public int height;

        // texture slices 的个数 (猜测是用于 texture array)
        // 常规渲染, 此值为1,  用于 XR 时, 此值大于1;
        public int slices;
        public Vector2 scale; // Texture scale, 配合上面的 "sizeMode" 使用
        public ScaleFunc func; // Texture scale function
        public DepthBits depthBufferBits; // Depth buffer bit depth
        public GraphicsFormat colorFormat; // Color format
        public FilterMode filterMode; // Filtering mode
        public TextureWrapMode wrapMode; // Addressing mode
        public TextureDimension dimension; // Texture dimension
        public bool enableRandomWrite; // Enable random UAV read/write on the texture
        public bool useMipMap;
        public bool autoGenerateMips;
        public bool isShadowMap; // Texture is a shadow map
        public int anisoLevel; // Anisotropic filtering level   各向异性滤波lvl
        public float mipMapBias;
        public bool enableMSAA; // Only supported for Scale and Functor size mode
        public MSAASamples msaaSamples; // Number of MSAA samples. Only supported for Explicit size mode
        public bool bindTextureMS; // Bind texture multi sampled
        public bool useDynamicScale; // Texture uses dynamic scaling
        public RenderTextureMemoryless memoryless; // Memory less flag
        public string name; // Texture name

#if UNITY_2020_2_OR_NEWER
        ///<summary>Descriptor to determine how the texture will be in fast memory on platform that supports it.</summary>
        public FastMemoryDesc fastMemoryDesc;
#endif
        
        /*
            Determines whether the texture will fallback to a black texture
            if it is read without ever writing to it;
            --
            如果一个 texture 有被读的需求, 但没检查到有谁向它写入数据, 
            那么这个 texture 是否要被退化为一个 black texture;
        */
        public bool fallBackToBlackTexture;


        // Initial state. Those should not be used in the hash
        // 下面这两个数据, 不参与 hash 值得计算;
        /*
            当创建 texture 时, 是否 clear buffer; 因为 render graph 用池子管理资源, 
            这些资源被回收利用, 里面包含的数据是未定义的;
        */
        public bool clearBuffer; // Texture needs to be cleared on first use
        public Color clearColor; // 存储一个颜色值, 如果需要 clear buffer, 就用这个颜色去覆写;

        /*
            参数 
            dynamicResolution:
                当程序使用 动态分辨率 功能时, 是否也动态 resize 本texture 的尺寸;
            
            xrReady:
                指示本 texture 是否被用于 XR 渲染; 如果是, render graph 系统会将这个资源
                新建为一个 texture array, 以供每只眼睛使用;
        */
        void InitDefaultValues(bool dynamicResolution, bool xrReady)
        {
            useDynamicScale = dynamicResolution;
            // XR Ready
            if (xrReady)
            {
                slices = TextureXR.slices;
                dimension = TextureXR.dimension;
            }
            else
            {
                slices = 1;
                dimension = TextureDimension.Tex2D;
            }
        }


        /// <summary> =============================================
        ///     构造函数
        ///     TextureDesc constructor for a texture using explicit size
        /// </summary>
        /// <param name="width">Texture width</param>
        /// <param name="height">Texture height</param>
        /// <param name="dynamicResolution">Use dynamic resolution</param>
        /// <param name="xrReady">Set this to true if the Texture is a render texture in an XR setting.</param>
        public TextureDesc(int width, int height, bool dynamicResolution = false, bool xrReady = false)
            : this()
        {
            // Size related init
            sizeMode = TextureSizeMode.Explicit;
            this.width = width;
            this.height = height;
            // Important default values not handled by zero construction in this()
            msaaSamples = MSAASamples.None;
            InitDefaultValues(dynamicResolution, xrReady);
        }

        /// <summary> ========================================
        /// TextureDesc constructor for a texture using a fixed scaling
        /// </summary>
        /// <param name="scale">RTHandle scale used for this texture</param>
        public TextureDesc(Vector2 scale, bool dynamicResolution = false, bool xrReady = false)
            : this()
        {
            // Size related init
            sizeMode = TextureSizeMode.Scale;
            this.scale = scale;
            // Important default values not handled by zero construction in this()
            msaaSamples = MSAASamples.None;
            dimension = TextureDimension.Tex2D;
            InitDefaultValues(dynamicResolution, xrReady);
        }

        /// <summary> =============================================
        /// TextureDesc constructor for a texture using a functor for scaling
        /// </summary>
        /// <param name="func">Function used to determnine the texture size</param>
        public TextureDesc(ScaleFunc func, bool dynamicResolution = false, bool xrReady = false)
            : this()
        {
            // Size related init
            sizeMode = TextureSizeMode.Functor;
            this.func = func;
            // Important default values not handled by zero construction in this()
            msaaSamples = MSAASamples.None;
            dimension = TextureDimension.Tex2D;
            InitDefaultValues(dynamicResolution, xrReady);
        }

        /// <summary> ==============
        ///     复制构造函数
        ///     Copy constructor
        /// </summary>
        /// <param name="input"></param>
        public TextureDesc(TextureDesc input)
        {
            this = input;
        }



        /// <summary> ==============================
        /// Hash function
        /// </summary>
        /// <returns>The texture descriptor hash.</returns>
        public override int GetHashCode()
        {
            int hashCode = 17;

            unchecked
            {
                switch (sizeMode)
                {
                    case TextureSizeMode.Explicit:
                        hashCode = hashCode * 23 + width;
                        hashCode = hashCode * 23 + height;
                        hashCode = hashCode * 23 + (int)msaaSamples;
                        break;
                    case TextureSizeMode.Functor:
                        if (func != null)
                            hashCode = hashCode * 23 + func.GetHashCode();
                        hashCode = hashCode * 23 + (enableMSAA ? 1 : 0);
                        break;
                    case TextureSizeMode.Scale:
                        hashCode = hashCode * 23 + scale.x.GetHashCode();
                        hashCode = hashCode * 23 + scale.y.GetHashCode();
                        hashCode = hashCode * 23 + (enableMSAA ? 1 : 0);
                        break;
                }

                hashCode = hashCode * 23 + mipMapBias.GetHashCode();
                hashCode = hashCode * 23 + slices;
                hashCode = hashCode * 23 + (int)depthBufferBits;
                hashCode = hashCode * 23 + (int)colorFormat;
                hashCode = hashCode * 23 + (int)filterMode;
                hashCode = hashCode * 23 + (int)wrapMode;
                hashCode = hashCode * 23 + (int)dimension;
                hashCode = hashCode * 23 + (int)memoryless;
                hashCode = hashCode * 23 + anisoLevel;
                hashCode = hashCode * 23 + (enableRandomWrite ? 1 : 0);
                hashCode = hashCode * 23 + (useMipMap ? 1 : 0);
                hashCode = hashCode * 23 + (autoGenerateMips ? 1 : 0);
                hashCode = hashCode * 23 + (isShadowMap ? 1 : 0);
                hashCode = hashCode * 23 + (bindTextureMS ? 1 : 0);
                hashCode = hashCode * 23 + (useDynamicScale ? 1 : 0);
#if UNITY_2020_2_OR_NEWER
                hashCode = hashCode * 23 + (fastMemoryDesc.inFastMemory ? 1 : 0);
#endif
            }
            return hashCode;
        }
    }// TextureDesc end





    [DebuggerDisplay("TextureResource ({desc.name})")]
    class TextureResource 
        : RenderGraphResource<TextureDesc, RTHandle>
    {
        static int m_TextureCreationIndex;

        public override string GetName()
        {
            if (imported && !shared)
                return graphicsResource != null ? graphicsResource.name : "null resource";
            else
                return desc.name;
        }

        // NOTE:
        // Next two functions should have been implemented in RenderGraphResource<DescType, ResType> but for some reason,
        // when doing so, it's impossible to break in the Texture version of the virtual function (with VS2017 at least), making this completely un-debuggable.
        // To work around this, we just copy/pasted the implementation in each final class...

        public override void CreatePooledGraphicsResource()
        {
            Debug.Assert(m_Pool != null, "CreatePooledGraphicsResource should only be called for regular pooled resources");

            int hashCode = desc.GetHashCode();

            if (graphicsResource != null)
                throw new InvalidOperationException(string.Format("Trying to create an already created resource ({0}). Resource was probably declared for writing more than once in the same pass.", GetName()));

            var pool = m_Pool as TexturePool;
            if (!pool.TryGetResource(hashCode, out graphicsResource))
            {
                CreateGraphicsResource();
            }

            cachedHash = hashCode;
            pool.RegisterFrameAllocation(cachedHash, graphicsResource);
        }

        public override void ReleasePooledGraphicsResource(int frameIndex)
        {
            if (graphicsResource == null)
                throw new InvalidOperationException($"Tried to release a resource ({GetName()}) that was never created. Check that there is at least one pass writing to it first.");

            // Shared resources don't use the pool
            var pool = m_Pool as TexturePool;
            if (pool != null)
            {
                pool.ReleaseResource(cachedHash, graphicsResource, frameIndex);
                pool.UnregisterFrameAllocation(cachedHash, graphicsResource);
            }

            Reset(null);
        }

        public override void CreateGraphicsResource(string name = "")
        {
            // Textures are going to be reused under different aliases along the frame so we can't provide a specific name upon creation.
            // The name in the desc is going to be used for debugging purpose and render graph visualization.
            if (name == "")
                name = $"RenderGraphTexture_{m_TextureCreationIndex++}";

            switch (desc.sizeMode)
            {
                case TextureSizeMode.Explicit:
                    graphicsResource = RTHandles.Alloc(desc.width, desc.height, desc.slices, desc.depthBufferBits, desc.colorFormat, desc.filterMode, desc.wrapMode, desc.dimension, desc.enableRandomWrite,
                        desc.useMipMap, desc.autoGenerateMips, desc.isShadowMap, desc.anisoLevel, desc.mipMapBias, desc.msaaSamples, desc.bindTextureMS, desc.useDynamicScale, desc.memoryless, name);
                    break;
                case TextureSizeMode.Scale:
                    graphicsResource = RTHandles.Alloc(desc.scale, desc.slices, desc.depthBufferBits, desc.colorFormat, desc.filterMode, desc.wrapMode, desc.dimension, desc.enableRandomWrite,
                        desc.useMipMap, desc.autoGenerateMips, desc.isShadowMap, desc.anisoLevel, desc.mipMapBias, desc.enableMSAA, desc.bindTextureMS, desc.useDynamicScale, desc.memoryless, name);
                    break;
                case TextureSizeMode.Functor:
                    graphicsResource = RTHandles.Alloc(desc.func, desc.slices, desc.depthBufferBits, desc.colorFormat, desc.filterMode, desc.wrapMode, desc.dimension, desc.enableRandomWrite,
                        desc.useMipMap, desc.autoGenerateMips, desc.isShadowMap, desc.anisoLevel, desc.mipMapBias, desc.enableMSAA, desc.bindTextureMS, desc.useDynamicScale, desc.memoryless, name);
                    break;
            }
        }

        public override void ReleaseGraphicsResource()
        {
            if (graphicsResource != null)
                graphicsResource.Release();
            base.ReleaseGraphicsResource();
        }

        public override void LogCreation(RenderGraphLogger logger)
        {
            logger.LogLine($"Created Texture: {desc.name} (Cleared: {desc.clearBuffer})");
        }

        public override void LogRelease(RenderGraphLogger logger)
        {
            logger.LogLine($"Released Texture: {desc.name}");
        }
    }




    class TexturePool 
        : RenderGraphResourcePool<RTHandle>
    {
        protected override void ReleaseInternalResource(RTHandle res)
        {
            res.Release();
        }

        protected override string GetResourceName(RTHandle res)
        {
            return res.rt.name;
        }

        protected override long GetResourceSize(RTHandle res)
        {
            return Profiling.Profiler.GetRuntimeMemorySizeLong(res.rt);
        }

        override protected string GetResourceTypeName()
        {
            return "Texture";
        }

        // Another C# nicety.
        // We need to re-implement the whole thing every time because:
        // - obj.resource.Release is Type specific so it cannot be called on a generic (and there's no shared interface for resources like RTHandle, ComputeBuffers etc)
        // - We can't use a virtual release function because it will capture 'this' in the lambda for RemoveAll generating GCAlloc in the process.
        override public void PurgeUnusedResources(int currentFrameIndex)
        {
            // Update the frame index for the lambda. Static because we don't want to capture.
            s_CurrentFrameIndex = currentFrameIndex;

            foreach (var kvp in m_ResourcePool)
            {
                var list = kvp.Value;
                list.RemoveAll(obj =>
                {
                    if (ShouldReleaseResource(obj.frameIndex, s_CurrentFrameIndex))
                    {
                        obj.resource.Release();
                        return true;
                    }
                    return false;
                });
            }
        }
    }
}
