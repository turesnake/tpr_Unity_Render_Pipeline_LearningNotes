using System;

namespace UnityEngine.Rendering.Universal
{
    /*
        调整画面的整体颜色
    */
    [Serializable, VolumeComponentMenu("Post-processing/Color Adjustments")]
    public sealed class ColorAdjustments //ColorAdjustments__
        : VolumeComponent, IPostProcessComponent
    {

        // 在 EV100 中调整场景的整体曝光; 这个修改在执行在 "HDR" 之后,在 "tonemapping" 之前，所以它不会影响 chain 中之前的效果;
        // 无取值界限
        [Tooltip("Adjusts the overall exposure of the scene in EV100. This is applied after HDR effect and right before tonemapping so it won't affect previous effects in the chain.")]
        public FloatParameter postExposure = new FloatParameter(0f);

        // 扩大或缩小 tone 的 整体范围; [-100, 100];
        // 值越大, 画面对比度越高,
        [Tooltip("Expands or shrinks the overall range of tonal values.")]
        public ClampedFloatParameter contrast = new ClampedFloatParameter(0f, -100f, 100f);//(val,min,max)

        // 这个值将被 乘到 画面中;
        [Tooltip("Tint the render by multiplying a color.")]
        public ColorParameter colorFilter = new ColorParameter(
            Color.white, 
            true,   // is hdr ?
            false,  // 不能在 inspector 中 edit alpha channel
            true    // the eye dropper is visible in the editor (猜测 xr 相关)
        );

        // 偏移所有颜色的 色相; [-180,180]
        [Tooltip("Shift the hue of all colors.")]
        public ClampedFloatParameter hueShift = new ClampedFloatParameter(0f, -180f, 180f);//(val,min,max)

        // 修改所有颜色的 饱和度 [-100, 100]
        [Tooltip("Pushes the intensity of all colors.")]
        public ClampedFloatParameter saturation = new ClampedFloatParameter(0f, -100f, 100f);//(val,min,max)


        // 任何值脱离了 原始默认值, 就认为此功能被启用了
        public bool IsActive()
        {
            return postExposure.value != 0f
                || contrast.value != 0f
                || colorFilter != Color.white
                || hueShift != 0f
                || saturation != 0f;
        }

        public bool IsTileCompatible() => true;
    }
}
