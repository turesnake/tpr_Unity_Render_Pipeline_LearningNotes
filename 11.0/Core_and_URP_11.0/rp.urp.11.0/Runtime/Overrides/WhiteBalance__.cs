using System;

namespace UnityEngine.Rendering.Universal
{
    /*
        白平衡
    */
    [Serializable, VolumeComponentMenu("Post-processing/White Balance")]
    public sealed class WhiteBalance //WhiteBalance__
        : VolumeComponent, IPostProcessComponent
    {

        // 设置 白平衡点的色温; [-100,100]
        [Tooltip("Sets the white balance to a custom color temperature.")]
        public ClampedFloatParameter temperature = new ClampedFloatParameter(0f, -100, 100f);//(val,min.max)

        // "绿<->品红" 的白平衡色温;  [-100,100]
        [Tooltip("Sets the white balance to compensate for a green or magenta tint.")]
        public ClampedFloatParameter tint = new ClampedFloatParameter(0f, -100, 100f);//(val,min.max)

        public bool IsActive() => temperature.value != 0f || tint.value != 0f;

        public bool IsTileCompatible() => true;
    }
}
