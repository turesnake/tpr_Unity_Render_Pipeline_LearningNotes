using System;

namespace UnityEngine.Rendering.Universal
{
    public enum FilmGrainLookup
    {
        Thin1,
        Thin2,
        Medium1,
        Medium2,
        Medium3,
        Medium4,
        Medium5,
        Medium6,
        Large01,
        Large02,
        Custom
    }

    /*
        对应: 后处理 inspector 面板:
        胶片颗粒
    */
    [Serializable, VolumeComponentMenu("Post-processing/FilmGrain")]
    public sealed class FilmGrain //FilmGrain__
        : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("The type of grain to use. You can select a preset or provide your own texture by selecting Custom.")]
        public FilmGrainLookupParameter type = new FilmGrainLookupParameter(FilmGrainLookup.Thin1);

        // 屏幕上 "渐晕, 周边暗角" 的量; [0,1]
        // 但视觉上看, 感觉像是 颗粒的尺寸大小 
        [Tooltip("Amount of vignetting on screen.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

        // 根据场景亮度 控制 噪声响应曲线。 本值越高, 意味着浅色区域的噪点越少, [0,1]
        [Tooltip("Controls the noisiness response curve based on scene luminance. Higher values mean less noise in light areas.")]
        public ClampedFloatParameter response = new ClampedFloatParameter(0.8f, 0f, 1f);


        [Tooltip("A tileable texture to use for the grain. The neutral value is 0.5 where no grain is applied.")]
        public NoInterpTextureParameter texture = new NoInterpTextureParameter(null);


        public bool IsActive() => intensity.value > 0f 
                                && (type.value!=FilmGrainLookup.Custom || texture.value != null);

        public bool IsTileCompatible() => true;
    }


    [Serializable]
    public sealed class FilmGrainLookupParameter //FilmGrainLookupParameter__RR
        : VolumeParameter<FilmGrainLookup> 
    { public FilmGrainLookupParameter(FilmGrainLookup value, bool overrideState = false) : base(value, overrideState) {} }
}
