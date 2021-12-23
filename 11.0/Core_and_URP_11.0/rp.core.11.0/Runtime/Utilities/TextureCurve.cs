using System;
using System.Runtime.CompilerServices;

namespace UnityEngine.Rendering
{
    // Due to limitations in the builtin "AnimationCurve" we need this custom wrapper.
    // 相对 "AnimationCurve" 的改进:
    //   - Dirty state handling so we know when a curve has changed or not
    //   - Looping support (infinite curve)
    //   - Zero-value curve
    //   - Cheaper length property

    /*
        A wrapper around "AnimationCurve" to automatically bake it into a texture.

        本 class 支持将一个 curve 转换为一个 texture2d (w=128,h=1), 传入 shader 中;
        非常厉害 !!!
    */
    [Serializable]
    public class TextureCurve//TextureCurv__RR
        : IDisposable
    {
        const int k_Precision = 128; // Edit LutBuilder3D if you change this value
        const float k_Step = 1f / k_Precision;

        /// The number of keys in the curve.
        [field: SerializeField]public int length { get; private set; } // Calling AnimationCurve.length is very slow, let's cache it

        [SerializeField]bool m_Loop;//曲线是否在 两端边界处 自动循环

        [SerializeField]float m_ZeroValue;//The default value to use when the curve doesn't have any key.

        [SerializeField]float m_Range;// 曲线的两个边界点 之间的距离值;

        [SerializeField]AnimationCurve m_Curve; // 曲线本体

        AnimationCurve m_LoopingCurve;// 内部计算时才被使用;

        // 一个 texture (w=128,h=1), 将曲线图的 x轴[0,1] 区间均匀分为 128份,
        // 每个节点计算自己的 y轴值, 存入对用的 texel.r 通道;
        Texture2D m_Texture;

        bool m_IsCurveDirty;// m_Curve 是否被改写
        bool m_IsTextureDirty;// m_Texture 是否被改写


     
        /// Retrieves the key at index.
        /// <param name="index">The index to look for.</param>
        /// <returns>A key.</returns>
        public Keyframe this[int index] => m_Curve[index];


        /*
            构造函数 -1-:
            Creates a new "TextureCurve" from an existing AnimationCurve.
            直接调用 -2-
        */
        /// <param name="baseCurve">The source "AnimationCurve".</param>
        /// <param name="zeroValue">The default value to use when the curve doesn't have any key.</param>
        /// <param name="loop">Should the curve automatically loop in the given "bounds" ?</param>
        /// <param name="bounds">The boundaries of the curve.</param>
        public TextureCurve(AnimationCurve baseCurve, float zeroValue, bool loop, in Vector2 bounds)//   读完__
            : this(baseCurve.keys, zeroValue, loop, bounds) {}

        /*
            构造函数 -2-:
            Creates a new "TextureCurve" from an arbitrary number of keyframes.
        */
        /// <param name="keys">An array of Keyframes used to define the curve.</param>
        /// <param name="zeroValue">The default value to use when the curve doesn't have any key.</param>
        /// <param name="loop">Should the curve automatically loop in the given "bounds" ?
        ///                     曲线是否在 两端边界处 自动循环
        /// </param>
        /// <param name="bounds">The boundaries of the curve.</param>
        public TextureCurve(Keyframe[] keys, float zeroValue, bool loop, in Vector2 bounds)//   读完__
        {
            m_Curve = new AnimationCurve(keys);
            m_ZeroValue = zeroValue;
            m_Loop = loop;
            m_Range = bounds.magnitude;// 向量模长
            length = keys.Length;
            SetDirty();
        }



        // Finalizer.
        ~TextureCurve() {}



        /*    tpr
        /// Cleans up the internal texture resource.
        [Obsolete("Please use Release() instead.")]
        public void Dispose() {}
        */


        /// Releases the internal texture resource.
        public void Release()
        {
            CoreUtils.Destroy(m_Texture);
            m_Texture = null;
        }


        // Marks the curve as dirty to trigger a redraw of the texture the next time "GetTexture()" is called.
        // 设为 dirty 后, 下次调用 "GetTexture()" 时, texture 会被重新绘制一遍;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDirty()
        {
            m_IsCurveDirty = true;
            m_IsTextureDirty = true;
        }


        // RHalf, R8, or ARGB32;
        static TextureFormat GetTextureFormat()
        {
            if (SystemInfo.SupportsTextureFormat(TextureFormat.RHalf))
                return TextureFormat.RHalf;// Scalar (R) texture format, 16 bit floating point.
            if (SystemInfo.SupportsTextureFormat(TextureFormat.R8))
                return TextureFormat.R8;// Single channel (R) texture format, 8 bit integer.
            return TextureFormat.ARGB32;// Color with alpha texture format, 8-bits per channel.
        }


        /*
            Gets the texture representation of this curve.

            生成一个 texture (w=128,h=1), 将曲线的 x轴[0,1] 区间均匀分为 128份,
            每个节点计算自己的 y轴值, 存入对用的 texel.r 通道;
        */
        /// <returns>A 128x1 texture.</returns>
        public Texture2D GetTexture()//   读完__
        {
            if (m_Texture == null)
            {
                m_Texture = new Texture2D(
                    k_Precision,        // width: 默认 128 个;
                    1,                  // height: 只有一个 texel 
                    GetTextureFormat(), //  RHalf, R8, or ARGB32;  其实只需存储一个 r通道的值;
                    false,              // mipChain: 是否携带 mipmap
                    true                // linear: 是否在 linear 颜色空间
                );
                m_Texture.name = "CurveTexture";

                /*
                    "HideAndDontSave":
                    The GameObject is not shown in the Hierarchy, not saved to to Scenes, and not unloaded by "Resources.UnloadUnusedAssets()".
                    This is most commonly used for GameObjects which are created by a script and are purely under the script's control.
                */
                m_Texture.hideFlags = HideFlags.HideAndDontSave;

                m_Texture.filterMode = FilterMode.Bilinear;
                m_Texture.wrapMode = TextureWrapMode.Clamp;
                m_IsTextureDirty = true;
            }

            if (m_IsTextureDirty)
            {
                var pixels = new Color[k_Precision];// 128 个;

                for (int i = 0; i < pixels.Length; i++)
                    pixels[i].r = Evaluate(i * k_Step);// 只存储 r 通道;

                m_Texture.SetPixels(pixels);
                m_Texture.Apply(
                    false,  // updateMipmaps
                    false   // makeNoLongerReadable
                );
                m_IsTextureDirty = false;
            }

            return m_Texture;
        }//   函数完__




        /// Evaluate a time value on the curve.
        /// <param name="time">The time within the curve you want to evaluate.</param>
        /// <returns>The value of the curve, at the point in time specified.</returns>
        public float Evaluate(float time)//   读完__
        {
            if (m_IsCurveDirty)
                length = m_Curve.length;// 节点数

            if (length == 0)
                return m_ZeroValue;

            if (!m_Loop || length == 1)
                return m_Curve.Evaluate(time);

            // 因为 curve 是 loop 的, 参数 time 有可能在 "曲线的 尾后 和 头前" 这个区间中
            // 解决方案就是复制出一个新曲线 m_LoopingCurve, 在它的尾后添加一个新节点 (等于 原始曲线 [0] 节点值)
            // 然后在这个 新曲线里 做 Evaluate();
            if (m_IsCurveDirty)
            {
                if (m_LoopingCurve == null)
                    m_LoopingCurve = new AnimationCurve();

                var prev = m_Curve[length - 1];
                prev.time -= m_Range;
                var next = m_Curve[0];
                next.time += m_Range;
                m_LoopingCurve.keys = m_Curve.keys; // GC pressure 压力
                m_LoopingCurve.AddKey(prev);
                m_LoopingCurve.AddKey(next);
                m_IsCurveDirty = false;
            }

            return m_LoopingCurve.Evaluate(time);
        }//   函数完__


        
        // Adds a new key to the curve.
        // 如果参数 time 指向的位置已经存在节点了, add 操作将失败; 此时本函数什么都不做, 返回 -1;
        /// <param name="time">The time at which to add the key.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>The index of the added key, or -1 if the key could not be added.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AddKey(float time, float value)//   读完__
        {
            // 如果参数 time 指向的位置已经存在节点了, add 操作将失败, 此时函数返回 -1;
            int r = m_Curve.AddKey(time, value);

            if (r > -1)
                SetDirty();

            return r;
        }//   函数完__




        /// Removes the keyframe at "index" and inserts "key".
        /// <param name="index"></param>
        /// <param name="key"></param>
        /// <returns>The index of the keyframe after moving it.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int MoveKey(int index, in Keyframe key)//  读完__
        {
            // If a keyframe already exists at "key.time", 
            //then the time of the old keyframe's position: "key[index].time" will be used instead. 
            // 没看懂...
            int r = m_Curve.MoveKey(index, key);
            SetDirty();
            return r;
        }//   函数完__



        /// Removes a key.
        /// <param name="index">The index of the key to remove.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveKey(int index)//   读完__
        {
            m_Curve.RemoveKey(index);
            SetDirty();
        }//   函数完__


        /*
            Smoothes the in and out tangents of the keyframe at "index". A "weight" of 0 evens out tangents.
            --
            平滑节点 index 处的曲线, 
        */
        /// <param name="index">The index of the keyframe to be smoothed.</param>
        /// <param name="weight">The smoothing weight to apply to the keyframe's tangents.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SmoothTangents(int index, float weight)//   读完__
        {
            m_Curve.SmoothTangents(index, weight);
            SetDirty();
        }//   函数完__
    }




    /*
        A "VolumeParameter" that holds a "TextureCurve" value.
    */
    [Serializable]
    public class TextureCurveParameter 
        : VolumeParameter<TextureCurve>
    {
        /// <summary>
        /// Creates a new "TextureCurveParameter" instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public TextureCurveParameter(TextureCurve value, bool overrideState = false)
            : base(value, overrideState) {}


        /// Release implementation.
        public override void Release() => m_Value.Release();

        // TODO: TextureCurve interpolation
    }
}
