using System;

namespace UnityEngine.Rendering.Universal
{
    public enum TonemappingMode
    {
        None,
        Neutral, // Neutral tonemapper
        ACES,    // ACES Filmic reference tonemapper (custom approximation)
    }

    /*
        控制 hdr 颜色是符合转换到 sRGB 颜色的;
    */
    [Serializable, VolumeComponentMenu("Post-processing/Tonemapping")]
    public sealed class Tonemapping //Tonemapping__
        : VolumeComponent, IPostProcessComponent
    {

        [Tooltip("Select a tonemapping algorithm to use for the color grading process.")]
        public TonemappingModeParameter mode = new TonemappingModeParameter(TonemappingMode.None);

        public bool IsActive() => mode.value != TonemappingMode.None;

        public bool IsTileCompatible() => true;
    }


    [Serializable]
    public sealed class TonemappingModeParameter //TonemappingModeParameter__RR
        : VolumeParameter<TonemappingMode> 
    { 
        public TonemappingModeParameter(TonemappingMode value, bool overrideState = false) 
            : base(value, overrideState) 
        {} 
    }
}
