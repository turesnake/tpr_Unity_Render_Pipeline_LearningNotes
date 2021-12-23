using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering
{
    using UnityObject = UnityEngine.Object;

    

    /*
        使用函数集, for the Core srp Library
    */
    public static class CoreUtils//CoreUtils__RR
    {
        /// <summary>
        /// List of look at matrices for cubemap faces.
        /// Ref: https://msdn.microsoft.com/en-us/library/windows/desktop/bb204881(v=vs.85).aspx
        /// </summary>
        static public readonly Vector3[] lookAtList =
        {
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(-1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(0.0f, -1.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 1.0f),
            new Vector3(0.0f, 0.0f, -1.0f),
        };

        /// <summary>
        /// List of up vectors for cubemap faces.
        /// Ref: https://msdn.microsoft.com/en-us/library/windows/desktop/bb204881(v=vs.85).aspx
        /// </summary>
        static public readonly Vector3[] upVectorList =
        {
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(0.0f, 0.0f, -1.0f),
            new Vector3(0.0f, 0.0f, 1.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
        };

        /// <summary>Edit Menu priority 1</summary>
        public const int editMenuPriority1 = 320;
        /// <summary>Edit Menu priority 2</summary>
        public const int editMenuPriority2 = 331;
        /// <summary>Edit Menu priority 3</summary>
        public const int editMenuPriority3 = 342;
        /// <summary>Edit Menu priority 4</summary>
        public const int editMenuPriority4 = 353;
        /// <summary>Asset Create Menu priority 1</summary>
        public const int assetCreateMenuPriority1 = 230;
        /// <summary>Asset Create Menu priority 2</summary>
        public const int assetCreateMenuPriority2 = 241;
        /// <summary>Asset Create Menu priority 3</summary>
        public const int assetCreateMenuPriority3 = 300;
        /// <summary>Game Object Menu priority</summary>
        public const int gameObjectMenuPriority = 10;

        static Cubemap m_BlackCubeTexture;
        /// <summary>
        /// Black cubemap texture.
        /// </summary>
        public static Cubemap blackCubeTexture
        {
            get
            {
                if (m_BlackCubeTexture == null)
                {
                    m_BlackCubeTexture = new Cubemap(1, TextureFormat.ARGB32, false);
                    for (int i = 0; i < 6; ++i)
                        m_BlackCubeTexture.SetPixel((CubemapFace)i, 0, 0, Color.black);
                    m_BlackCubeTexture.Apply();
                }

                return m_BlackCubeTexture;
            }
        }

        static Cubemap m_MagentaCubeTexture;
        /// <summary>
        /// Magenta cubemap texture.
        /// </summary>
        public static Cubemap magentaCubeTexture
        {
            get
            {
                if (m_MagentaCubeTexture == null)
                {
                    m_MagentaCubeTexture = new Cubemap(1, TextureFormat.ARGB32, false);
                    for (int i = 0; i < 6; ++i)
                        m_MagentaCubeTexture.SetPixel((CubemapFace)i, 0, 0, Color.magenta);
                    m_MagentaCubeTexture.Apply();
                }

                return m_MagentaCubeTexture;
            }
        }

        static CubemapArray m_MagentaCubeTextureArray;
        /// <summary>
        /// Black cubemap array texture.
        /// </summary>
        public static CubemapArray magentaCubeTextureArray
        {
            get
            {
                if (m_MagentaCubeTextureArray == null)
                {
                    m_MagentaCubeTextureArray = new CubemapArray(1, 1, TextureFormat.RGBAFloat, false);
                    for (int i = 0; i < 6; ++i)
                    {
                        Color[] colors = { Color.magenta };
                        m_MagentaCubeTextureArray.SetPixels(colors, (CubemapFace)i, 0);
                    }
                    m_MagentaCubeTextureArray.Apply();
                }

                return m_MagentaCubeTextureArray;
            }
        }

        static Cubemap m_WhiteCubeTexture;
        /// <summary>
        /// White cubemap texture.
        /// </summary>
        public static Cubemap whiteCubeTexture
        {
            get
            {
                if (m_WhiteCubeTexture == null)
                {
                    m_WhiteCubeTexture = new Cubemap(1, TextureFormat.ARGB32, false);
                    for (int i = 0; i < 6; ++i)
                        m_WhiteCubeTexture.SetPixel((CubemapFace)i, 0, 0, Color.white);
                    m_WhiteCubeTexture.Apply();
                }

                return m_WhiteCubeTexture;
            }
        }

        static RenderTexture m_EmptyUAV;
        /// <summary>
        /// Empty 1x1 texture usable as a dummy UAV.
        /// </summary>
        public static RenderTexture emptyUAV
        {
            get
            {
                if (m_EmptyUAV == null)
                {
                    m_EmptyUAV = new RenderTexture(1, 1, 0);
                    m_EmptyUAV.enableRandomWrite = true;
                    m_EmptyUAV.Create();
                }

                return m_EmptyUAV;
            }
        }

        static Texture3D m_BlackVolumeTexture;
        /// <summary>
        /// Black 3D texture.
        /// </summary>
        public static Texture3D blackVolumeTexture
        {
            get
            {
                if (m_BlackVolumeTexture == null)
                {
                    Color[] colors = { Color.black };
                    m_BlackVolumeTexture = new Texture3D(1, 1, 1, TextureFormat.ARGB32, false);
                    m_BlackVolumeTexture.SetPixels(colors, 0);
                    m_BlackVolumeTexture.Apply();
                }

                return m_BlackVolumeTexture;
            }
        }


        /*
            Clear the currently bound render texture.
            --
            清空当前绑定的 render target (本质也是一个 render texture);
        */
        /// <param name="cmd">CommandBuffer used for rendering commands.</param>
        /// <param name="clearFlag">Specify how the render texture should be cleared.
        ///                         enum: None, Color, Depth, All
        /// </param>
        /// <param name="clearColor">Specify with which color the render texture should be cleared.</param>
        public static void ClearRenderTarget(CommandBuffer cmd, ClearFlag clearFlag, Color clearColor)
        {
            if (clearFlag != ClearFlag.None)
                cmd.ClearRenderTarget((clearFlag & ClearFlag.Depth) != 0, (clearFlag & ClearFlag.Color) != 0, clearColor);
        }// 函数完__

        
        /*
            We use -1 as a default value because when doing SPI for XR,
            it will bind the full texture array by default (and has no effect on 2D textures)
            Unfortunately, for cubemaps, passing -1 does not work for faces other than the first one, 
            so we fall back to 0 in this case.
        */
        private static int FixupDepthSlice(int depthSlice, RTHandle buffer)
        {
            if (depthSlice == -1 && buffer.rt.dimension == TextureDimension.Cube)
                depthSlice = 0;

            return depthSlice;
        }// 函数完__

        private static int FixupDepthSlice(int depthSlice, CubemapFace cubemapFace)
        {
            if (depthSlice == -1 && cubemapFace != CubemapFace.Unknown)
                depthSlice = 0;

            return depthSlice;
        }


        /*
           // -------------------------------------------------------------: -1-:
           Set the current render texture.

            -1- 设置 render target
            -2- 设置 viewport
            -3- clear render target  


            参数:
            cmd:
                CommandBuffer used for rendering commands.

            buffer:
                RenderTargetIdentifier of the render texture.

            clearFlag:
                If not set to ClearFlag.None, specifies how to clear the render target after setup.

            clearColor:
                If applicable, color with which to clear the render texture after setup.

            miplevel:
                Mip level that should be bound as a render texture if applicable.

            cubemapFace:
                Cubemap face that should be bound as a render texture if applicable.

            depthSlice:
                Depth slice that should be bound as a render texture if applicable.
        */ 
        // -------------------------------------------------------------: -2-:
        public static void SetRenderTarget(
                                        CommandBuffer cmd, 
                                        RenderTargetIdentifier buffer, 
                                        ClearFlag clearFlag, 
                                        Color clearColor, 
                                        int miplevel = 0, 
                                        CubemapFace cubemapFace = CubemapFace.Unknown, 
                                        int depthSlice = -1
        ){
            depthSlice = FixupDepthSlice(depthSlice, cubemapFace);
            cmd.SetRenderTarget(buffer, miplevel, cubemapFace, depthSlice);
            ClearRenderTarget(cmd, clearFlag, clearColor);
        }

        // -------------------------------------------------------------: -3-:
        public static void SetRenderTarget(
                                        CommandBuffer cmd, 
                                        RenderTargetIdentifier colorBuffer, 
                                        RenderTargetIdentifier depthBuffer, 
                                        ClearFlag clearFlag, 
                                        Color clearColor, 
                                        int miplevel = 0, 
                                        CubemapFace cubemapFace = CubemapFace.Unknown, 
                                        int depthSlice = -1
        ){
            depthSlice = FixupDepthSlice(depthSlice, cubemapFace);
            cmd.SetRenderTarget(colorBuffer, depthBuffer, miplevel, cubemapFace, depthSlice);
            ClearRenderTarget(cmd, clearFlag, clearColor);
        }

        
        // -------------------------------------------------------------: -4-:
        // 重载, 默认参数: Color.clear
        public static void SetRenderTarget(
                                        CommandBuffer cmd, 
                                        RenderTargetIdentifier buffer, 
                                        ClearFlag clearFlag = ClearFlag.None, 
                                        int miplevel = 0, 
                                        CubemapFace cubemapFace = CubemapFace.Unknown, 
                                        int depthSlice = -1
        ){
            SetRenderTarget(cmd, buffer, clearFlag, Color.clear, miplevel, cubemapFace, depthSlice);
        }

        // -------------------------------------------------------------: -5-:
        // 重载, 
        // 分开设置 colorBuffer, depthBuffer
        // 默认参数: ClearFlag.None, Color.clear
        public static void SetRenderTarget(
                                        CommandBuffer cmd, 
                                        RenderTargetIdentifier colorBuffer, 
                                        RenderTargetIdentifier depthBuffer, 
                                        int miplevel = 0, 
                                        CubemapFace cubemapFace = CubemapFace.Unknown, 
                                        int depthSlice = -1
        ){
            SetRenderTarget(cmd, colorBuffer, depthBuffer, ClearFlag.None, Color.clear, miplevel, cubemapFace, depthSlice);
        }

        // -------------------------------------------------------------: -6-:
        // 重载, 
        // 分开设置 colorBuffer, depthBuffer
        // 默认参数: Color.clear
        public static void SetRenderTarget(
                                        CommandBuffer cmd, 
                                        RenderTargetIdentifier colorBuffer, 
                                        RenderTargetIdentifier depthBuffer, 
                                        ClearFlag clearFlag, 
                                        int miplevel = 0, 
                                        CubemapFace cubemapFace = CubemapFace.Unknown, 
                                        int depthSlice = -1
        ){
            SetRenderTarget(cmd, colorBuffer, depthBuffer, clearFlag, Color.clear, miplevel, cubemapFace, depthSlice);
        }
        

        // -------------------------------------------------------------: -7-:
        public static void SetRenderTarget(
                                        CommandBuffer cmd, 
                                        RenderTargetIdentifier[] colorBuffers, 
                                        RenderTargetIdentifier depthBuffer
        ){
            SetRenderTarget(cmd, colorBuffers, depthBuffer, ClearFlag.None, Color.clear);
        }

        // -------------------------------------------------------------: -8-:
        public static void SetRenderTarget(
                                        CommandBuffer cmd, 
                                        RenderTargetIdentifier[] colorBuffers, 
                                        RenderTargetIdentifier depthBuffer, 
                                        ClearFlag clearFlag = ClearFlag.None
        ){
            SetRenderTarget(cmd, colorBuffers, depthBuffer, clearFlag, Color.clear);
        }


        // -------------------------------------------------------------: -9-:
        public static void SetRenderTarget(
                                        CommandBuffer cmd, 
                                        RenderTargetIdentifier[] colorBuffers, 
                                        RenderTargetIdentifier depthBuffer, 
                                        ClearFlag clearFlag, 
                                        Color clearColor
        ){
            cmd.SetRenderTarget(colorBuffers, depthBuffer, 0, CubemapFace.Unknown, -1);
            ClearRenderTarget(cmd, clearFlag, clearColor);
        }

        // -------------------------------------------------------------: -10-:
        // Explicit load and store actions
        // 绑定单个 buffer, 比如单独的 color buffer
        public static void SetRenderTarget(
                                        CommandBuffer cmd, 
                                        RenderTargetIdentifier buffer, 
                                        RenderBufferLoadAction loadAction, 
                                        RenderBufferStoreAction storeAction, 
                                        ClearFlag clearFlag, 
                                        Color clearColor
        ){
            cmd.SetRenderTarget(buffer, loadAction, storeAction);
            ClearRenderTarget(cmd, clearFlag, clearColor);
        }

        // -------------------------------------------------------------: -11-:
        public static void SetRenderTarget(
                                        CommandBuffer cmd, 
                                        RenderTargetIdentifier buffer, 
                                        RenderBufferLoadAction loadAction, 
                                        RenderBufferStoreAction storeAction, 
                                        ClearFlag clearFlag
        ){
            SetRenderTarget(cmd, buffer, loadAction, storeAction, clearFlag, Color.clear);
        }

        // -------------------------------------------------------------: -12-:
        public static void SetRenderTarget(
                                        CommandBuffer cmd, 
                                        RenderTargetIdentifier colorBuffer, 
                                        RenderBufferLoadAction colorLoadAction, 
                                        RenderBufferStoreAction colorStoreAction,
                                        RenderTargetIdentifier depthBuffer, 
                                        RenderBufferLoadAction depthLoadAction, 
                                        RenderBufferStoreAction depthStoreAction,
                                        ClearFlag clearFlag, 
                                        Color clearColor
        ){
            // 同时设置了 color buffer 和 depth buffer
            cmd.SetRenderTarget(colorBuffer, colorLoadAction, colorStoreAction, depthBuffer, depthLoadAction, depthStoreAction);
            ClearRenderTarget(cmd, clearFlag, clearColor);
        }

        // -------------------------------------------------------------: -13-:
        public static void SetRenderTarget(
                                        CommandBuffer cmd, 
                                        RenderTargetIdentifier colorBuffer, 
                                        RenderBufferLoadAction colorLoadAction, 
                                        RenderBufferStoreAction colorStoreAction,
                                        RenderTargetIdentifier depthBuffer, 
                                        RenderBufferLoadAction depthLoadAction, 
                                        RenderBufferStoreAction depthStoreAction,
                                        ClearFlag clearFlag
        ){
            SetRenderTarget(cmd, colorBuffer, colorLoadAction, colorStoreAction, depthBuffer, depthLoadAction, depthStoreAction, clearFlag, Color.clear);
        }



        /*
            -1- 设置 viewport
            -2- clear render target
        */
        private static void SetViewportAndClear(CommandBuffer cmd, RTHandle buffer, ClearFlag clearFlag, Color clearColor)
        {
            /*
                Clearing a partial viewport currently does not go through the hardware clear.
                Instead it goes through a quad rendered with a specific shader.
                When enabling wireframe mode in the scene view, unfortunately it overrides this shader thus breaking every clears.
                That's why in the editor we don't set the viewport before clearing (it's set to full screen by the previous SetRenderTarget) 
                but AFTER so that we benefit from un-bugged hardware clear.
                We consider that the small loss in performance is acceptable in the editor.
                A refactor of wireframe is needed before we can fix this properly (with not doing anything!)
                ===
                清除 "部分-viewport" 不会使用到 硬件清除, 相反, 它使用一个特殊 shader, 这个shader 渲染一个 quad, 来实现 clear 功能;
                不幸的是, 如果在 editor: scene窗口中, 开启了 wireframe mode, 它会覆盖掉上面这个 shader, 从而中断这些 "部分-viewport的清理";

                因此, 在 editor 模式, 我们不在 ClearRenderTarget() 之前调用 SetViewport();
                这样, 我们就能借用 没有bug 的 硬件 clear; (没完全看懂)
                这会导致 editor 模式存在微小性能折损, 但这能接收;
                也许未来会重新实现 wireframe mode, 以此来彻底搞定此问题;
                ===
                若在 editor 中: 先 ClearRenderTarget(),   后 SetViewport();
                若在 build  中: 先 SetViewport(),         后 ClearRenderTarget();
                ---

                建议无视 editor 模式, 直接看 build 模式的内容;
            */
#if !UNITY_EDITOR
            SetViewport(cmd, buffer);
#endif
            CoreUtils.ClearRenderTarget(cmd, clearFlag, clearColor);
#if UNITY_EDITOR
            SetViewport(cmd, buffer);
#endif
        }// 函数完__



        /*
            -1- 设置 render target
            -2- 设置 viewport
            -3- clear render target  


            "This set of RenderTarget management methods" is supposed to be used when "rendering RTHandle render texture".
            This will automatically set the viewport based on the "RTHandle System reference size" and the RTHandle scaling info.
            ---
            当 RTHandle texture 被设置为 render target 时, 使用下面这组 SetRenderTarget() 函数;

            它们会自动设置 viewport, 具体解释参见下方的: "CoreUtils.SetViewport()"
        */

        /*
            Setup the current render texture using an RTHandle;
        */
        /// <param name="cmd">CommandBuffer used for rendering commands</param>
        /// <param name="buffer">Color buffer RTHandle</param>
        /// <param name="clearFlag">If not set to ClearFlag.None, specifies how to clear the render target after setup.</param>
        /// <param name="clearColor">If applicable, color with which to clear the render texture after setup.</param>
        /// <param name="miplevel">Mip level that should be bound as a render texture if applicable.</param>
        /// <param name="cubemapFace">Cubemap face that should be bound as a render texture if applicable.</param>
        /// <param name="depthSlice">Depth slice that should be bound as a render texture if applicable.</param>
        public static void SetRenderTarget(CommandBuffer cmd, RTHandle buffer, ClearFlag clearFlag, Color clearColor, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown, int depthSlice = -1)
        {
            depthSlice = FixupDepthSlice(depthSlice, buffer);
            cmd.SetRenderTarget(buffer, miplevel, cubemapFace, depthSlice);
            SetViewportAndClear(cmd, buffer, clearFlag, clearColor);
        }

        /// <summary>
        /// Setup the current render texture using an RTHandle
        /// </summary>
        /// <param name="cmd">CommandBuffer used for rendering commands</param>
        /// <param name="buffer">Color buffer RTHandle</param>
        /// <param name="clearFlag">If not set to ClearFlag.None, specifies how to clear the render target after setup.</param>
        /// <param name="miplevel">Mip level that should be bound as a render texture if applicable.</param>
        /// <param name="cubemapFace">Cubemap face that should be bound as a render texture if applicable.</param>
        /// <param name="depthSlice">Depth slice that should be bound as a render texture if applicable.</param>
        public static void SetRenderTarget(CommandBuffer cmd, RTHandle buffer, ClearFlag clearFlag = ClearFlag.None, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown, int depthSlice = -1)
            => SetRenderTarget(cmd, buffer, clearFlag, Color.clear, miplevel, cubemapFace, depthSlice);

        /// <summary>
        /// Setup the current render texture using an RTHandle
        /// </summary>
        /// <param name="cmd">CommandBuffer used for rendering commands</param>
        /// <param name="colorBuffer">Color buffer RTHandle</param>
        /// <param name="depthBuffer">Depth buffer RTHandle</param>
        /// <param name="miplevel">Mip level that should be bound as a render texture if applicable.</param>
        /// <param name="cubemapFace">Cubemap face that should be bound as a render texture if applicable.</param>
        /// <param name="depthSlice">Depth slice that should be bound as a render texture if applicable.</param>
        public static void SetRenderTarget(CommandBuffer cmd, RTHandle colorBuffer, RTHandle depthBuffer, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown, int depthSlice = -1)
        {
            int cw = colorBuffer.rt.width;
            int ch = colorBuffer.rt.height;
            int dw = depthBuffer.rt.width;
            int dh = depthBuffer.rt.height;

            Debug.Assert(cw == dw && ch == dh);

            SetRenderTarget(cmd, colorBuffer, depthBuffer, ClearFlag.None, Color.clear, miplevel, cubemapFace, depthSlice);
        }

        /// <summary>
        /// Setup the current render texture using an RTHandle
        /// </summary>
        /// <param name="cmd">CommandBuffer used for rendering commands</param>
        /// <param name="colorBuffer">Color buffer RTHandle</param>
        /// <param name="depthBuffer">Depth buffer RTHandle</param>
        /// <param name="clearFlag">If not set to ClearFlag.None, specifies how to clear the render target after setup.</param>
        /// <param name="miplevel">Mip level that should be bound as a render texture if applicable.</param>
        /// <param name="cubemapFace">Cubemap face that should be bound as a render texture if applicable.</param>
        /// <param name="depthSlice">Depth slice that should be bound as a render texture if applicable.</param>
        public static void SetRenderTarget(CommandBuffer cmd, RTHandle colorBuffer, RTHandle depthBuffer, ClearFlag clearFlag, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown, int depthSlice = -1)
        {
            int cw = colorBuffer.rt.width;
            int ch = colorBuffer.rt.height;
            int dw = depthBuffer.rt.width;
            int dh = depthBuffer.rt.height;

            Debug.Assert(cw == dw && ch == dh);

            SetRenderTarget(cmd, colorBuffer, depthBuffer, clearFlag, Color.clear, miplevel, cubemapFace, depthSlice);
        }

        /// <summary>
        /// Setup the current render texture using an RTHandle
        /// </summary>
        /// <param name="cmd">CommandBuffer used for rendering commands</param>
        /// <param name="colorBuffer">Color buffer RTHandle</param>
        /// <param name="depthBuffer">Depth buffer RTHandle</param>
        /// <param name="clearFlag">If not set to ClearFlag.None, specifies how to clear the render target after setup.</param>
        /// <param name="clearColor">If applicable, color with which to clear the render texture after setup.</param>
        /// <param name="miplevel">Mip level that should be bound as a render texture if applicable.</param>
        /// <param name="cubemapFace">Cubemap face that should be bound as a render texture if applicable.</param>
        /// <param name="depthSlice">Depth slice that should be bound as a render texture if applicable.</param>
        public static void SetRenderTarget(CommandBuffer cmd, RTHandle colorBuffer, RTHandle depthBuffer, ClearFlag clearFlag, Color clearColor, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown, int depthSlice = -1)
        {
            int cw = colorBuffer.rt.width;
            int ch = colorBuffer.rt.height;
            int dw = depthBuffer.rt.width;
            int dh = depthBuffer.rt.height;

            Debug.Assert(cw == dw && ch == dh);

            CoreUtils.SetRenderTarget(cmd, colorBuffer.rt, depthBuffer.rt, miplevel, cubemapFace, depthSlice);
            SetViewportAndClear(cmd, colorBuffer, clearFlag, clearColor);
        }

        /// <summary>
        /// Set the current multiple render texture.
        /// </summary>
        /// <param name="cmd">CommandBuffer used for rendering commands.</param>
        /// <param name="colorBuffers">RenderTargetIdentifier array of the color render textures.</param>
        /// <param name="depthBuffer">Depth Buffer RTHandle.</param>
        public static void SetRenderTarget(CommandBuffer cmd, RenderTargetIdentifier[] colorBuffers, RTHandle depthBuffer)
        {
            CoreUtils.SetRenderTarget(cmd, colorBuffers, depthBuffer.rt, ClearFlag.None, Color.clear);
            SetViewport(cmd, depthBuffer);
        }

        /// <summary>
        /// Set the current multiple render texture.
        /// </summary>
        /// <param name="cmd">CommandBuffer used for rendering commands.</param>
        /// <param name="colorBuffers">RenderTargetIdentifier array of the color render textures.</param>
        /// <param name="depthBuffer">Depth Buffer RTHandle.</param>
        /// <param name="clearFlag">If not set to ClearFlag.None, specifies how to clear the render target after setup.</param>
        public static void SetRenderTarget(CommandBuffer cmd, RenderTargetIdentifier[] colorBuffers, RTHandle depthBuffer, ClearFlag clearFlag = ClearFlag.None)
        {
            CoreUtils.SetRenderTarget(cmd, colorBuffers, depthBuffer.rt); // Don't clear here, viewport needs to be set before we do.
            SetViewportAndClear(cmd, depthBuffer, clearFlag, Color.clear);
        }

        /// <summary>
        /// Set the current multiple render texture.
        /// </summary>
        /// <param name="cmd">CommandBuffer used for rendering commands.</param>
        /// <param name="colorBuffers">RenderTargetIdentifier array of the color render textures.</param>
        /// <param name="depthBuffer">Depth Buffer RTHandle.</param>
        /// <param name="clearFlag">If not set to ClearFlag.None, specifies how to clear the render target after setup.</param>
        /// <param name="clearColor">If applicable, color with which to clear the render texture after setup.</param>
        public static void SetRenderTarget(CommandBuffer cmd, RenderTargetIdentifier[] colorBuffers, RTHandle depthBuffer, ClearFlag clearFlag, Color clearColor)
        {
            cmd.SetRenderTarget(colorBuffers, depthBuffer, 0, CubemapFace.Unknown, -1);
            SetViewportAndClear(cmd, depthBuffer, clearFlag, clearColor);
        }



        /*
            Scaling viewport is done for auto-scaling render targets.
            In the context of SRP, every auto-scaled RT is scaled against the "maximum RTHandles reference size" (that can only grow).
            When we render using a camera whose viewport is smaller than the "RTHandles reference size" 
            (and thus smaller than the RT actual size), we need to set it explicitly 
            (otherwise, native code will set the viewport at the size of the RT)
            For auto-scaled RTs (like for example a half-resolution RT), we need to scale this viewport accordingly(相应的).
            For non scaled RTs we just do nothing, the native code will set the viewport at the size of the RT anyway.
            ---
            Setup the viewport to the size of the provided RTHandle.
            ---

            在 srp 中, 所有开启了 "auto-scaling" 功能的 RTHandle texture, 都将根据 "max reference size" 来调整自己的 实际尺寸;
            (而且只会不停地变大, 不会变小)
            
            但是万一 camera 想要渲染的尺寸, 比 上述 RTHandle 的尺寸小的时候, 该怎么办呢?
            我们就需要显式的设置 camera 的 viewport 信息;
            ( 要不然, 让一个 RTHandle 被设置为 camera 的 render target 时, camera 会把 render target 的尺寸, 
            当成为自己的 渲染尺寸 )

            第二点: 如果这个 "auto-scaling" RTH 的 "scale" 值不为 1, (比如 0.5), 我们还要把这个 "scale" 考虑进来;

            所以, 最终 viewport 的计算变成:

                = "当前帧像素分辨率" * "scale"

            此函数的本质就是调用 commandbuffer 的 SetViewport() 函数;
        */
        /// <param name="cmd">CommandBuffer used for rendering commands.</param>
        /// <param name="target">RTHandle from which to compute the proper viewport.</param>
        public static void SetViewport(CommandBuffer cmd, RTHandle target)
        {
            // 只有启用 "auto-scale" 的 RTHandle 会被设置; 
            if (target.useScaling)
            {
                Vector2Int scaledViewportSize = target.GetScaledSize(target.rtHandleProperties.currentViewportSize);
                cmd.SetViewport(new Rect(0.0f, 0.0f, scaledViewportSize.x, scaledViewportSize.y));
            }
        }



        /// <summary>
        /// Generate a name based on render texture parameters.
        /// </summary>
        /// <param name="width">With of the texture.</param>
        /// <param name="height">Height of the texture.</param>
        /// <param name="depth">Depth of the texture.</param>
        /// <param name="format">Format of the render texture.</param>
        /// <param name="name">Base name of the texture.</param>
        /// <param name="mips">True if the texture has mip maps.</param>
        /// <param name="enableMSAA">True if the texture is multisampled.</param>
        /// <param name="msaaSamples">Number of MSAA samples.</param>
        /// <returns>Generated names bassed on the provided parameters.</returns>
        public static string GetRenderTargetAutoName(int width, int height, int depth, RenderTextureFormat format, string name, bool mips = false, bool enableMSAA = false, MSAASamples msaaSamples = MSAASamples.None)
            => GetRenderTargetAutoName(width, height, depth, format.ToString(), TextureDimension.None, name, mips, enableMSAA, msaaSamples, dynamicRes: false);

        /// <summary>
        /// Generate a name based on render texture parameters.
        /// </summary>
        /// <param name="width">With of the texture.</param>
        /// <param name="height">Height of the texture.</param>
        /// <param name="depth">Depth of the texture.</param>
        /// <param name="format">Graphics format of the render texture.</param>
        /// <param name="name">Base name of the texture.</param>
        /// <param name="mips">True if the texture has mip maps.</param>
        /// <param name="enableMSAA">True if the texture is multisampled.</param>
        /// <param name="msaaSamples">Number of MSAA samples.</param>
        /// <returns>Generated names bassed on the provided parameters.</returns>
        public static string GetRenderTargetAutoName(int width, int height, int depth, GraphicsFormat format, string name, bool mips = false, bool enableMSAA = false, MSAASamples msaaSamples = MSAASamples.None)
            => GetRenderTargetAutoName(width, height, depth, format.ToString(), TextureDimension.None, name, mips, enableMSAA, msaaSamples, dynamicRes: false);

        /// <summary>
        /// Generate a name based on render texture parameters.
        /// </summary>
        /// <param name="width">With of the texture.</param>
        /// <param name="height">Height of the texture.</param>
        /// <param name="depth">Depth of the texture.</param>
        /// <param name="format">Graphics format of the render texture.</param>
        /// <param name="dim">Dimension of the texture.</param>
        /// <param name="name">Base name of the texture.</param>
        /// <param name="mips">True if the texture has mip maps.</param>
        /// <param name="enableMSAA">True if the texture is multisampled.</param>
        /// <param name="msaaSamples">Number of MSAA samples.</param>
        /// <param name="dynamicRes">True if the texture uses dynamic resolution.</param>
        /// <returns>Generated names bassed on the provided parameters.</returns>
        public static string GetRenderTargetAutoName(int width, int height, int depth, GraphicsFormat format, TextureDimension dim, string name, bool mips = false, bool enableMSAA = false, MSAASamples msaaSamples = MSAASamples.None, bool dynamicRes = false)
            => GetRenderTargetAutoName(width, height, depth, format.ToString(), dim, name, mips, enableMSAA, msaaSamples, dynamicRes);

        static string GetRenderTargetAutoName(int width, int height, int depth, string format, TextureDimension dim, string name, bool mips, bool enableMSAA, MSAASamples msaaSamples, bool dynamicRes)
        {
            string result = string.Format("{0}_{1}x{2}", name, width, height);

            if (depth > 1)
                result = string.Format("{0}x{1}", result, depth);

            if (mips)
                result = string.Format("{0}_{1}", result, "Mips");

            result = string.Format("{0}_{1}", result, format);

            if (dim != TextureDimension.None)
                result = string.Format("{0}_{1}", result, dim);

            if (enableMSAA)
                result = string.Format("{0}_{1}", result, msaaSamples.ToString());

            if (dynamicRes)
                result = string.Format("{0}_{1}", result, "dynamic");

            return result;
        }// 函数完__

        /// <summary>
        /// Generate a name based on texture parameters.
        /// </summary>
        /// <param name="width">With of the texture.</param>
        /// <param name="height">Height of the texture.</param>
        /// <param name="format">Format of the texture.</param>
        /// <param name="dim">Dimension of the texture.</param>
        /// <param name="name">Base name of the texture.</param>
        /// <param name="mips">True if the texture has mip maps.</param>
        /// <param name="depth">Depth of the texture.</param>
        /// <returns>Generated names based on the provided parameters.</returns>
        public static string GetTextureAutoName(int width, int height, TextureFormat format, TextureDimension dim = TextureDimension.None, string name = "", bool mips = false, int depth = 0)
            => GetTextureAutoName(width, height, format.ToString(), dim, name, mips, depth);

        /// <summary>
        /// Generate a name based on texture parameters.
        /// </summary>
        /// <param name="width">With of the texture.</param>
        /// <param name="height">Height of the texture.</param>
        /// <param name="format">Graphics format of the texture.</param>
        /// <param name="dim">Dimension of the texture.</param>
        /// <param name="name">Base name of the texture.</param>
        /// <param name="mips">True if the texture has mip maps.</param>
        /// <param name="depth">Depth of the texture.</param>
        /// <returns>Generated names based on the provided parameters.</returns>
        public static string GetTextureAutoName(int width, int height, GraphicsFormat format, TextureDimension dim = TextureDimension.None, string name = "", bool mips = false, int depth = 0)
            => GetTextureAutoName(width, height, format.ToString(), dim, name, mips, depth);

        static string GetTextureAutoName(int width, int height, string format, TextureDimension dim = TextureDimension.None, string name = "", bool mips = false, int depth = 0)
        {
            string temp;
            if (depth == 0)
                temp = string.Format("{0}x{1}{2}_{3}", width, height, mips ? "_Mips" : "", format);
            else
                temp = string.Format("{0}x{1}x{2}{3}_{4}", width, height, depth, mips ? "_Mips" : "", format);
            temp = String.Format("{0}_{1}_{2}", name == "" ? "Texture" : name, (dim == TextureDimension.None) ? "" : dim.ToString(), temp);

            return temp;
        }

        /// <summary>
        /// Clear a cubemap render texture.
        /// </summary>
        /// <param name="cmd">CommandBuffer used for rendering commands.</param>
        /// <param name="renderTexture">Cubemap render texture that needs to be cleared.</param>
        /// <param name="clearColor">Color used for clearing.</param>
        /// <param name="clearMips">Set to true to clear the mip maps of the render texture.</param>
        public static void ClearCubemap(CommandBuffer cmd, RenderTexture renderTexture, Color clearColor, bool clearMips = false)
        {
            int mipCount = 1;
            if (renderTexture.useMipMap && clearMips)
            {
                mipCount = (int)Mathf.Log((float)renderTexture.width, 2.0f) + 1;
            }

            for (int i = 0; i < 6; ++i)
            {
                for (int mip = 0; mip < mipCount; ++mip)
                {
                    SetRenderTarget(cmd, new RenderTargetIdentifier(renderTexture), ClearFlag.Color, clearColor, mip, (CubemapFace)i);
                }
            }
        }

        /// <summary>
        /// Draws a full screen triangle.
        /// </summary>
        /// <param name="commandBuffer">CommandBuffer used for rendering commands.</param>
        /// <param name="material">Material used on the full screen triangle.</param>
        /// <param name="properties">Optional material property block for the provided material.</param>
        /// <param name="shaderPassId">Index of the material pass.</param>
        public static void DrawFullScreen(CommandBuffer commandBuffer, Material material,
            MaterialPropertyBlock properties = null, int shaderPassId = 0)
        {
            commandBuffer.DrawProcedural(Matrix4x4.identity, material, shaderPassId, MeshTopology.Triangles, 3, 1, properties);
        }

        /// <summary>
        /// Draws a full screen triangle.
        /// </summary>
        /// <param name="commandBuffer">CommandBuffer used for rendering commands.</param>
        /// <param name="material">Material used on the full screen triangle.</param>
        /// <param name="colorBuffer">RenderTargetIdentifier of the color buffer that needs to be set before drawing the full screen triangle.</param>
        /// <param name="properties">Optional material property block for the provided material.</param>
        /// <param name="shaderPassId">Index of the material pass.</param>
        public static void DrawFullScreen(CommandBuffer commandBuffer, Material material,
            RenderTargetIdentifier colorBuffer,
            MaterialPropertyBlock properties = null, int shaderPassId = 0)
        {
            commandBuffer.SetRenderTarget(colorBuffer);
            commandBuffer.DrawProcedural(Matrix4x4.identity, material, shaderPassId, MeshTopology.Triangles, 3, 1, properties);
        }

        /// <summary>
        /// Draws a full screen triangle.
        /// </summary>
        /// <param name="commandBuffer">CommandBuffer used for rendering commands.</param>
        /// <param name="material">Material used on the full screen triangle.</param>
        /// <param name="colorBuffer">RenderTargetIdentifier of the color buffer that needs to be set before drawing the full screen triangle.</param>
        /// <param name="depthStencilBuffer">RenderTargetIdentifier of the depth buffer that needs to be set before drawing the full screen triangle.</param>
        /// <param name="properties">Optional material property block for the provided material.</param>
        /// <param name="shaderPassId">Index of the material pass.</param>
        public static void DrawFullScreen(CommandBuffer commandBuffer, Material material,
            RenderTargetIdentifier colorBuffer, RenderTargetIdentifier depthStencilBuffer,
            MaterialPropertyBlock properties = null, int shaderPassId = 0)
        {
            commandBuffer.SetRenderTarget(colorBuffer, depthStencilBuffer, 0, CubemapFace.Unknown, -1);
            commandBuffer.DrawProcedural(Matrix4x4.identity, material, shaderPassId, MeshTopology.Triangles, 3, 1, properties);
        }

        /// <summary>
        /// Draws a full screen triangle.
        /// </summary>
        /// <param name="commandBuffer">CommandBuffer used for rendering commands.</param>
        /// <param name="material">Material used on the full screen triangle.</param>
        /// <param name="colorBuffers">RenderTargetIdentifier array of the color buffers that needs to be set before drawing the full screen triangle.</param>
        /// <param name="depthStencilBuffer">RenderTargetIdentifier of the depth buffer that needs to be set before drawing the full screen triangle.</param>
        /// <param name="properties">Optional material property block for the provided material.</param>
        /// <param name="shaderPassId">Index of the material pass.</param>
        public static void DrawFullScreen(CommandBuffer commandBuffer, Material material,
            RenderTargetIdentifier[] colorBuffers, RenderTargetIdentifier depthStencilBuffer,
            MaterialPropertyBlock properties = null, int shaderPassId = 0)
        {
            commandBuffer.SetRenderTarget(colorBuffers, depthStencilBuffer, 0, CubemapFace.Unknown, -1);
            commandBuffer.DrawProcedural(Matrix4x4.identity, material, shaderPassId, MeshTopology.Triangles, 3, 1, properties);
        }

        // Important: the first RenderTarget must be created with 0 depth bits!

        /// <summary>
        /// Draws a full screen triangle.
        /// </summary>
        /// <param name="commandBuffer">CommandBuffer used for rendering commands.</param>
        /// <param name="material">Material used on the full screen triangle.</param>
        /// <param name="colorBuffers">RenderTargetIdentifier array of the color buffers that needs to be set before drawing the full screen triangle.</param>
        /// <param name="properties">Optional material property block for the provided material.</param>
        /// <param name="shaderPassId">Index of the material pass.</param>
        public static void DrawFullScreen(CommandBuffer commandBuffer, Material material,
            RenderTargetIdentifier[] colorBuffers,
            MaterialPropertyBlock properties = null, int shaderPassId = 0)
        {
            // It is currently not possible to have MRT without also setting a depth target.
            // To work around this deficiency of the CommandBuffer.SetRenderTarget() API,
            // we pass the first color target as the depth target. If it has 0 depth bits,
            // no depth target ends up being bound.
            DrawFullScreen(commandBuffer, material, colorBuffers, colorBuffers[0], properties, shaderPassId);
        }

        
        // ================= Color space utilities =================:

        /*
            Converts the provided sRGB color to the current active color space.
        */
        /// <returns>Linear color if the active color space is ColorSpace.Linear, the original input otherwise.</returns>
        public static Color ConvertSRGBToActiveColorSpace(Color color)//   读完__
        {
            return (QualitySettings.activeColorSpace == ColorSpace.Linear) ? color.linear : color;
        }



        /*
            Converts the provided linear color to the current active color space.
        */
        /// <param name="color">Input color.</param>
        /// <returns>sRGB color if the active color space is ColorSpace.Gamma, the original input otherwise.</returns>
        public static Color ConvertLinearToActiveColorSpace(Color color)
        {
            return (QualitySettings.activeColorSpace == ColorSpace.Linear) ? color : color.gamma;
        }


        /*
            Creates a Material with the provided "shader path" / "shader";
            hideFlags will be set to HideFlags.HideAndDontSave.
        */
        /// <param name="shaderPath">Path of the shader used for the material.</param>
        /// <returns>A new Material instance using "the shader found at the provided path."</returns>
        public static Material CreateEngineMaterial(string shaderPath)// 读完__
        {
            Shader shader = Shader.Find(shaderPath);
            if (shader == null)
            {
                Debug.LogError("Cannot create required material because shader " + shaderPath + " could not be found");
                return null;
            }

            var mat = new Material(shader)
            {
                // "HideAndDontSave":
                //  The GameObject is not shown in the Hierarchy, not saved to to Scenes, and not unloaded by "Resources.UnloadUnusedAssets()".
                //  This is most commonly used for GameObjects which are created by a script and are purely under the script's control.
                hideFlags = HideFlags.HideAndDontSave
            };
            return mat;
        }

        

        /// <param name="shader">Shader used for the material.</param>
        /// <returns>A new Material instance using "the provided shader."</returns>
        public static Material CreateEngineMaterial(Shader shader)// 读完__
        {
            if (shader == null)
            {
                Debug.LogError("Cannot create required material because shader is null");
                return null;
            }

            var mat = new Material(shader)
            {
                // "HideAndDontSave":
                //  The GameObject is not shown in the Hierarchy, not saved to to Scenes, and not unloaded by "Resources.UnloadUnusedAssets()".
                //  This is most commonly used for GameObjects which are created by a script and are purely under the script's control.
                hideFlags = HideFlags.HideAndDontSave
            };
            return mat;
        }




        /// <summary>
        /// Bitfield flag test.
        /// </summary>
        /// <typeparam name="T">Type of the enum flag.</typeparam>
        /// <param name="mask">Bitfield to test the flag against.</param>
        /// <param name="flag">Flag to be tested against the provided mask.</param>
        /// <returns>True if the flag is present in the mask.</returns>
        public static bool HasFlag<T>(T mask, T flag) where T : IConvertible
        {
            return (mask.ToUInt32(null) & flag.ToUInt32(null)) != 0;
        }




        /// Swaps two values.
        /// <typeparam name="T">Type of the values</typeparam>
        /// <param name="a">First value.</param>
        /// <param name="b">Second value.</param>
        public static void Swap<T>(ref T a, ref T b)// 读完__
        {
            var tmp = a;
            a = b;
            b = tmp;
        }



        /// <summary>
        /// Set a global keyword using a CommandBuffer
        /// </summary>
        /// <param name="cmd">CommandBuffer on which to set the global keyword.</param>
        /// <param name="keyword">Keyword to be set.</param>
        /// <param name="state">Value of the keyword to be set.</param>
        public static void SetKeyword(CommandBuffer cmd, string keyword, bool state)
        {
            if (state)
                cmd.EnableShaderKeyword(keyword);
            else
                cmd.DisableShaderKeyword(keyword);
        }


        // Caution: such a call should not be use interlaced with command buffer command, as it is immediate
        /// <summary>
        /// Set a keyword immediatly on a Material.
        /// </summary>
        /// <param name="material">Material on which to set the keyword.</param>
        /// <param name="keyword">Keyword to set on the material.</param>
        /// <param name="state">Value of the keyword to set on the material.</param>
        public static void SetKeyword(Material material, string keyword, bool state)//   读完__
        {
            if (state)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        /// <summary>
        /// Set a keyword to a compute shader
        /// </summary>
        /// <param name="cs">Compute Shader on which to set the keyword.</param>
        /// <param name="keyword">Keyword to be set.</param>
        /// <param name="state">Value of the keyword to be set.</param>
        public static void SetKeyword(ComputeShader cs, string keyword, bool state)//   读完__
        {
            if (state)
                cs.EnableKeyword(keyword);
            else
                cs.DisableKeyword(keyword);
        }

        

        /// Destroys a UnityObject safely.
        /// <param name="obj">Object to be destroyed.</param>
        public static void Destroy(UnityObject obj)
        {
            if (obj != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                    UnityObject.Destroy(obj);
                else
                    UnityObject.DestroyImmediate(obj);
#else
                UnityObject.Destroy(obj);
#endif
            }
        }


        static IEnumerable<Type> m_AssemblyTypes;

        /// <summary>
        /// Returns all assembly types.
        /// </summary>
        /// <returns>The list of all assembly types of the current domain.</returns>
        public static IEnumerable<Type> GetAllAssemblyTypes()
        {
            if (m_AssemblyTypes == null)
            {
                m_AssemblyTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(
                    t =>
                    {
                        // Ugly hack to handle mis-versioned dlls
                        var innerTypes = new Type[0];
                        try
                        {
                            innerTypes = t.GetTypes();
                        }
                        catch {}
                        return innerTypes;
                    }
                );
            }

            return m_AssemblyTypes;
        }



        /*
            Returns a list of types that inherit from the provided type.
            ---
            能一口气把当前系统中, 所有继承自 参数 Type 的 类型信息, 都收集起来并返回;
        */
        /// <typeparam name="T">Parent Type</typeparam>
        /// <returns>A list of types that inherit from the provided type.</returns>
        public static IEnumerable<Type> GetAllTypesDerivedFrom<T>()//  读完__
        {
#if UNITY_EDITOR && UNITY_2019_2_OR_NEWER
            return UnityEditor.TypeCache.GetTypesDerivedFrom<T>();
#else
            return GetAllAssemblyTypes().Where(
                t => t.IsSubclassOf(typeof(T))
            );
#endif
        }



        /// <summary>
        /// Safely release a Compute Buffer.
        /// </summary>
        /// <param name="buffer">Compute Buffer that needs to be released.</param>
        public static void SafeRelease(ComputeBuffer buffer)
        {
            if (buffer != null)
                buffer.Release();
        }

        /// <summary>
        /// Creates a cube mesh.
        /// </summary>
        /// <param name="min">Minimum corner coordinates in local space.</param>
        /// <param name="max">Maximum corner coordinates in local space.</param>
        /// <returns>A new instance of a cube Mesh.</returns>
        public static Mesh CreateCubeMesh(Vector3 min, Vector3 max)
        {
            Mesh mesh = new Mesh();

            Vector3[] vertices = new Vector3[8];

            vertices[0] = new Vector3(min.x, min.y, min.z);
            vertices[1] = new Vector3(max.x, min.y, min.z);
            vertices[2] = new Vector3(max.x, max.y, min.z);
            vertices[3] = new Vector3(min.x, max.y, min.z);
            vertices[4] = new Vector3(min.x, min.y, max.z);
            vertices[5] = new Vector3(max.x, min.y, max.z);
            vertices[6] = new Vector3(max.x, max.y, max.z);
            vertices[7] = new Vector3(min.x, max.y, max.z);

            mesh.vertices = vertices;

            int[] triangles = new int[36];

            triangles[0] = 0; triangles[1] = 2; triangles[2] = 1;
            triangles[3] = 0; triangles[4] = 3; triangles[5] = 2;
            triangles[6] = 1; triangles[7] = 6; triangles[8] = 5;
            triangles[9] = 1; triangles[10] = 2; triangles[11] = 6;
            triangles[12] = 5; triangles[13] = 7; triangles[14] = 4;
            triangles[15] = 5; triangles[16] = 6; triangles[17] = 7;
            triangles[18] = 4; triangles[19] = 3; triangles[20] = 0;
            triangles[21] = 4; triangles[22] = 7; triangles[23] = 3;
            triangles[24] = 3; triangles[25] = 6; triangles[26] = 2;
            triangles[27] = 3; triangles[28] = 7; triangles[29] = 6;
            triangles[30] = 4; triangles[31] = 1; triangles[32] = 5;
            triangles[33] = 4; triangles[34] = 0; triangles[35] = 1;

            mesh.triangles = triangles;
            return mesh;
        }

        /*
            Returns true if "Post Processes" are enabled for the view associated with the given camera.

            非 editor 模式, 一定返回 true;

        */
        /// <param name="camera">Input camera.</param>
        /// <returns>True if "Post Processes" are enabled for the view associated with the given camera.</returns>
        public static bool ArePostProcessesEnabled(Camera camera)
        {
            bool enabled = true;

        #if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView)
            {// editor scene 窗口使用的 camera
                enabled = false;

                // Determine whether the "Post Processes" checkbox is checked for the current view.
                for (int i = 0; i < UnityEditor.SceneView.sceneViews.Count; i++)
                {
                    var sv = UnityEditor.SceneView.sceneViews[i] as UnityEditor.SceneView;

                    // Post-processing is disabled in scene view if either showImageEffects is disabled or we are
                    // rendering in wireframe mode.
                    if (sv.camera == camera &&
                        (sv.sceneViewState.imageEffectsEnabled && sv.cameraMode.drawMode != UnityEditor.DrawCameraMode.Wireframe))
                    {
                        enabled = true;
                        break;
                    }
                }
            }
        #endif

            return enabled;
        }



        /// <summary>
        /// Returns true if "Animated Materials" are enabled for the view associated with the given camera.
        /// </summary>
        /// <param name="camera">Input camera.</param>
        /// <returns>True if "Animated Materials" are enabled for the view associated with the given camera.</returns>
        public static bool AreAnimatedMaterialsEnabled(Camera camera)
        {
            bool animateMaterials = true;

        #if UNITY_EDITOR
            animateMaterials = Application.isPlaying; // For Game and VR views; Reflection views pass the parent camera

            if (camera.cameraType == CameraType.SceneView)
            {
                animateMaterials = false;

                // Determine whether the "Animated Materials" checkbox is checked for the current view.
                for (int i = 0; i < UnityEditor.SceneView.sceneViews.Count; i++) // Using a foreach on an ArrayList generates garbage ...
                {
                    var sv = UnityEditor.SceneView.sceneViews[i] as UnityEditor.SceneView;
            #if UNITY_2020_2_OR_NEWER
                    if (sv.camera == camera && sv.sceneViewState.alwaysRefreshEnabled)
            #else
                    if (sv.camera == camera && sv.sceneViewState.materialUpdateEnabled)
            #endif
                    {
                        animateMaterials = true;
                        break;
                    }
                }
            }
            else if (camera.cameraType == CameraType.Preview)
            {
                // Enable for previews so the shader graph main preview works with time parameters.
                animateMaterials = true;
            }
            else if (camera.cameraType == CameraType.Reflection)
            {
                // Reflection cameras should be handled outside this function.
                // Debug.Assert(false, "Unexpected View type.");
            }

            // IMHO, a better solution would be:
            // A window invokes a camera render. The camera knows which window called it, so it can query its properies
            // (such as animated materials). This camera provides the space-time position. It should also be able
            // to access the rendering settings somehow. Using this information, it is then able to construct the
            // primary view with information about camera-relative rendering, LOD, time, rendering passes/features
            // enabled, etc. We then render this view. It can have multiple sub-views (shadows, reflections).
            // They inherit all the properties of the primary view, but also have the ability to override them
            // (e.g. primary cam pos and time are retained, matrices are modified, SSS and tessellation are disabled).
            // These views can then have multiple sub-views (probably not practical for games),
            // which simply amounts to a recursive call, and then the story repeats itself.
            //
            // TLDR: we need to know the caller and its status/properties to make decisions.
        #endif

            return animateMaterials;
        }

        /// <summary>
        /// Returns true if "Scene Lighting" is enabled for the view associated with the given camera.
        /// </summary>
        /// <param name="camera">Input camera.</param>
        /// <returns>True if "Scene Lighting" is enabled for the view associated with the given camera.</returns>
        public static bool IsSceneLightingDisabled(Camera camera)
        {
            bool disabled = false;
#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView)
            {
                // Determine whether the "No Scene Lighting" checkbox is checked for the current view.
                for (int i = 0; i < UnityEditor.SceneView.sceneViews.Count; i++)
                {
                    var sv = UnityEditor.SceneView.sceneViews[i] as UnityEditor.SceneView;
                    if (sv.camera == camera && !sv.sceneLighting)
                    {
                        disabled = true;
                        break;
                    }
                }
            }
#endif
            return disabled;
        }

        /// <summary>
        /// Returns true if the "Light Overlap" scene view draw mode is enabled.
        /// </summary>
        /// <param name="camera">Input camera.</param>
        /// <returns>True if "Light Overlap" is enabled in the scene view associated with the input camera.</returns>
        public static bool IsLightOverlapDebugEnabled(Camera camera)
        {
            bool enabled = false;
#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView)
            {
                // Determine whether the "LightOverlap" mode is enabled for the current view.
                for (int i = 0; i < UnityEditor.SceneView.sceneViews.Count; i++)
                {
                    var sv = UnityEditor.SceneView.sceneViews[i] as UnityEditor.SceneView;
                    if (sv.camera == camera && sv.cameraMode.drawMode == UnityEditor.DrawCameraMode.LightOverlap)
                    {
                        enabled = true;
                        break;
                    }
                }
            }
#endif
            return enabled;
        }

#if UNITY_EDITOR
        static Func<List<UnityEditor.MaterialEditor>> materialEditors;

        static CoreUtils()
        {
            //quicker than standard reflection as it is compiled
            System.Reflection.FieldInfo field = typeof(UnityEditor.MaterialEditor).GetField("s_MaterialEditors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var fieldExpression = System.Linq.Expressions.Expression.Field(null, field);
            var lambda = System.Linq.Expressions.Expression.Lambda<Func<List<UnityEditor.MaterialEditor>>>(fieldExpression);
            materialEditors = lambda.Compile();
        }

#endif

        /// <summary>
        /// Returns true if "Fog" is enabled for the view associated with the given camera.
        /// </summary>
        /// <param name="camera">Input camera.</param>
        /// <returns>True if "Fog" is enabled for the view associated with the given camera.</returns>
        public static bool IsSceneViewFogEnabled(Camera camera)
        {
            bool fogEnable = true;

#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView)
            {
                fogEnable = false;

                // Determine whether the "Animated Materials" checkbox is checked for the current view.
                for (int i = 0; i < UnityEditor.SceneView.sceneViews.Count; i++)
                {
                    var sv = UnityEditor.SceneView.sceneViews[i] as UnityEditor.SceneView;
                    if (sv.camera == camera && sv.sceneViewState.fogEnabled)
                    {
                        fogEnable = true;
                        break;
                    }
                }
            }
#endif

            return fogEnable;
        }

        /// <summary>
        /// Draw a renderer list.
        /// </summary>
        /// <param name="renderContext">Current Scriptable Render Context.</param>
        /// <param name="cmd">Command Buffer used for rendering.</param>
        /// <param name="rendererList">Renderer List to render.</param>
        public static void DrawRendererList(ScriptableRenderContext renderContext, CommandBuffer cmd, RendererList rendererList)
        {
            if (!rendererList.isValid)
                throw new ArgumentException("Invalid renderer list provided to DrawRendererList");

            // This is done here because DrawRenderers API lives outside command buffers so we need to make call this before doing any DrawRenders or things will be executed out of order
            renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            if (rendererList.stateBlock == null)
                renderContext.DrawRenderers(rendererList.cullingResult, ref rendererList.drawSettings, ref rendererList.filteringSettings);
            else
            {
                var renderStateBlock = rendererList.stateBlock.Value;
                renderContext.DrawRenderers(rendererList.cullingResult, ref rendererList.drawSettings, ref rendererList.filteringSettings, ref renderStateBlock);
            }
        }
    }
}
