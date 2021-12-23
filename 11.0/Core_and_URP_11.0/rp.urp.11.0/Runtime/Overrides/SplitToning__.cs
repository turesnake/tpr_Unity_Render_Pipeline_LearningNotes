using System;

namespace UnityEngine.Rendering.Universal
{
    /*
        分别控制 阴影/亮区 的颜色;
    */
    [Serializable, VolumeComponentMenu("Post-processing/Split Toning")]
    public sealed class SplitToning //SplitToning__
        : VolumeComponent, IPostProcessComponent
    {

        [Tooltip("The color to use for shadows.")]
        public ColorParameter shadows = new ColorParameter(
            Color.grey, 
            false,  // is hdr ?
            false,  // 不能在 inspector 中 edit alpha channel
            true    // the eye dropper is visible in the editor (猜测 xr 相关)
        );


        [Tooltip("The color to use for highlights.")]
        public ColorParameter highlights = new ColorParameter(
            Color.grey, 
            false,  // is hdr ?
            false,  // 不能在 inspector 中 edit alpha channel
            true    // the eye dropper is visible in the editor (猜测 xr 相关)
        );


        // [-100,100]
        // 值越大, 画面中更多区域被判定为 亮区, 进而被上面的 highlights 所影响;
        [Tooltip("Balance between the colors in the highlights and shadows.")]
        public ClampedFloatParameter balance = new ClampedFloatParameter(0f, -100f, 100f);//(val,min,max)


        public bool IsActive() => shadows != Color.grey || highlights != Color.grey;

        public bool IsTileCompatible() => true;
    }
}
