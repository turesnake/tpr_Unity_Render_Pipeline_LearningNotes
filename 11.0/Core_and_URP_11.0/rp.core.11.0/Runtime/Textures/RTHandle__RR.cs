using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering
{
    /*
        A RTHandle is a RenderTexture that scales automatically with the camera size.
        This allows proper reutilization(再利用) of RenderTexture memory when different cameras with various sizes are used during rendering.
        --
        在渲染期间使用 不同分辨率的各种 camera, 在这种情况下, 本系统也能保证 render texture 内存的 重复利用;
        (直接使用 render texture 是做不到这点的)
    */
    public class RTHandle//RTHandle__RR
    {
        internal RTHandleSystem             m_Owner;
        internal RenderTexture              m_RT;

        // 如果本 RTHandle 绑定的是一个 texture, 那么就存储在此;
        internal Texture                    m_ExternalTexture;
        internal RenderTargetIdentifier     m_NameID;
        internal bool                       m_EnableMSAA = false;
        internal bool                       m_EnableRandomWrite = false;
        internal bool                       m_EnableHWDynamicScale = false;
        internal string                     m_Name;


        
        // Scale factor applied to the RTHandle reference size. 
        // 就是 "scale" 值
        public Vector2 scaleFactor { get; internal set; }

        
        //    由用户提供的函数, 输入 max rederence size, 返回目标 rt 的 像素分辨率;
        internal ScaleFunc scaleFunc;


        //    如果启用了 "自动缩放" 功能, 返回 true;
        public bool                         useScaling { get; internal set; }
  
        // "Reference size" of the RTHandle System associated with the RTHandle
        public Vector2Int                   referenceSize {get; internal set; }

        // Current properties of the RTHandle System
        public RTHandleProperties           rtHandleProperties { get { return m_Owner.rtHandleProperties; } }


        // 与本 RTHandle 绑定的 render texture, 一个真实的数据 
        public RenderTexture rt { get { return m_RT; } }

        // 与本 RTHandle 绑定的 RenderTargetIdentifier
        public RenderTargetIdentifier nameID { get { return m_NameID; } }

        public string name { get { return m_Name; } }//Name of the RTHandle


        public bool isMSAAEnabled { get { return m_EnableMSAA; } }//Returns true is MSAA is enabled

        // Keep constructor private
        internal RTHandle(RTHandleSystem owner)
        {
            m_Owner = owner;
        }

        /*
            -----------------------------------------------------------------------------
            隐式转换: RTHandle -> RenderTargetIdentifier
            隐式转换: RTHandle -> Texture
            隐式转换: RTHandle -> RenderTexture
        */
        public static implicit operator RenderTargetIdentifier(RTHandle handle)
        {
            return handle!=null ? handle.nameID : default(RenderTargetIdentifier);
        }
        public static implicit operator Texture(RTHandle handle)
        {
            // If RTHandle is null then conversion should give a null Texture
            if (handle == null)
                return null;
            Debug.Assert(handle.m_ExternalTexture != null || handle.rt != null);
            return (handle.rt != null) ? handle.rt : handle.m_ExternalTexture;
        }
        public static implicit operator RenderTexture(RTHandle handle)
        {
            // If RTHandle is null then conversion should give a null RenderTexture
            if (handle == null)
                return null;
            Debug.Assert(handle.rt != null, "RTHandle was created using a regular Texture and is used as a RenderTexture");
            return handle.rt;
        }
        // ------------------------------------------------------------------------------


        internal void SetRenderTexture(RenderTexture rt)
        {
            m_RT =  rt;
            m_ExternalTexture = null;
            m_NameID = new RenderTargetIdentifier(rt);
        }

        internal void SetTexture(Texture tex)
        {
            m_RT = null;
            m_ExternalTexture = tex;
            m_NameID = new RenderTargetIdentifier(tex);
        }

        internal void SetTexture(RenderTargetIdentifier tex)
        {
            m_RT = null;
            m_ExternalTexture = null;
            m_NameID = tex;
        }


        /*
            Release the RTHandle
        */
        public void Release()
        {
            m_Owner.Remove(this);
            CoreUtils.Destroy(m_RT);
            m_NameID = BuiltinRenderTextureType.None;
            m_RT = null;
            m_ExternalTexture = null;
        }


        /*
            Return the input size, scaled by the RTHandle scale factor.

            如果本 RTHandle 不支持 "auto-scale", 直接返回参数 refSize;
            如果本 RTHandle 支持 "auto-scale":
                -- 如果本 RTHandle 携带 scaleFunc: 用这个函数去计算 rt 的像素分辨率;
                -- 否则, refSize * 本 RTHandle 的 "scale" 值, 返回的也是 rt 的像素分辨率;

            这个运算的特征就是, "reference size" 这个数据是直接由 参数 refSize 提供的;
        */
        /// <param name="refSize">Input size</param>
        /// <returns>Input size scaled by the RTHandle scale factor.</returns>
        public Vector2Int GetScaledSize(Vector2Int refSize)
        {
            if (!useScaling)
                return refSize;

            if (scaleFunc != null){
                return scaleFunc(refSize);
            }
            else{
                return new Vector2Int(
                    x: Mathf.RoundToInt(scaleFactor.x * refSize.x),
                    y: Mathf.RoundToInt(scaleFactor.y * refSize.y)
                );
            }
        }



#if UNITY_2020_2_OR_NEWER
        /// <summary>
        /// Switch the render target to fast memory on platform that have it.
        /// </summary>
        /// <param name="cmd">Command buffer used for rendering.</param>
        /// <param name="residencyFraction">How much of the render target is to be switched into fast memory (between 0 and 1).</param>
        /// <param name="flags">Flag to determine what parts of the render target is spilled if not fully resident in fast memory.</param>
        /// <param name="copyContents">Whether the content of render target are copied or not when switching to fast memory.</param>

        public void SwitchToFastMemory(CommandBuffer cmd,
            float residencyFraction = 1.0f,
            FastMemoryFlags flags = FastMemoryFlags.SpillTop,
            bool copyContents = false
        )
        {
            residencyFraction = Mathf.Clamp01(residencyFraction);
            cmd.SwitchIntoFastMemory(m_RT, flags, residencyFraction, copyContents);
        }

        /// <summary>
        /// Switch the render target to fast memory on platform that have it and copies the content.
        /// </summary>
        /// <param name="cmd">Command buffer used for rendering.</param>
        /// <param name="residencyFraction">How much of the render target is to be switched into fast memory (between 0 and 1).</param>
        /// <param name="flags">Flag to determine what parts of the render target is spilled if not fully resident in fast memory.</param>
        public void CopyToFastMemory(CommandBuffer cmd,
            float residencyFraction = 1.0f,
            FastMemoryFlags flags = FastMemoryFlags.SpillTop
        )
        {
            SwitchToFastMemory(cmd, residencyFraction, flags, copyContents: true);
        }

        /// <summary>
        /// Switch out the render target from fast memory back to main memory on platforms that have fast memory.
        /// </summary>
        /// <param name="cmd">Command buffer used for rendering.</param>
        /// <param name="copyContents">Whether the content of render target are copied or not when switching out fast memory.</param>
        public void SwitchOutFastMemory(CommandBuffer cmd, bool copyContents = true)
        {
            cmd.SwitchOutOfFastMemory(m_RT, copyContents);
        }

#endif
    }
}
