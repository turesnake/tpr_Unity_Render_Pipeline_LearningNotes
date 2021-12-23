using System;

namespace UnityEngine.Rendering.Universal
{

    /*
        调整 rgb 三通道的 混合;
    */
    [Serializable, VolumeComponentMenu("Post-processing/Channel Mixer")]
    public sealed class ChannelMixer //ChannelMixer__
        : VolumeComponent, IPostProcessComponent
    {
        /*
            red out:
                red   in: 100  [-200, 200]
                green in: 0    [-200, 200]
                blue  in: 0    [-200, 200]
        */
        [Tooltip("Modify influence of the red channel in the overall mix.")]
        public ClampedFloatParameter redOutRedIn = new ClampedFloatParameter(100f, -200f, 200f);//(val,min,max)
        [Tooltip("Modify influence of the green channel in the overall mix.")]
        public ClampedFloatParameter redOutGreenIn = new ClampedFloatParameter(0f, -200f, 200f);//(val,min,max)
        [Tooltip("Modify influence of the blue channel in the overall mix.")]
        public ClampedFloatParameter redOutBlueIn = new ClampedFloatParameter(0f, -200f, 200f);//(val,min,max)


        /*
            green out:
                red   in: 0    [-200, 200]
                green in: 100  [-200, 200]
                blue  in: 0    [-200, 200]
        */
        [Tooltip("Modify influence of the red channel in the overall mix.")]
        public ClampedFloatParameter greenOutRedIn = new ClampedFloatParameter(0f, -200f, 200f);//(val,min,max)
        [Tooltip("Modify influence of the green channel in the overall mix.")]
        public ClampedFloatParameter greenOutGreenIn = new ClampedFloatParameter(100f, -200f, 200f);//(val,min,max)
        [Tooltip("Modify influence of the blue channel in the overall mix.")]
        public ClampedFloatParameter greenOutBlueIn = new ClampedFloatParameter(0f, -200f, 200f);//(val,min,max)


        /*
            blue out:
                red   in: 0    [-200, 200]
                green in: 0    [-200, 200]
                blue  in: 100  [-200, 200]
        */
        [Tooltip("Modify influence of the red channel in the overall mix.")]
        public ClampedFloatParameter blueOutRedIn = new ClampedFloatParameter(0f, -200f, 200f);//(val,min,max)
        [Tooltip("Modify influence of the green channel in the overall mix.")]
        public ClampedFloatParameter blueOutGreenIn = new ClampedFloatParameter(0f, -200f, 200f);//(val,min,max)
        [Tooltip("Modify influence of the blue channel in the overall mix.")]
        public ClampedFloatParameter blueOutBlueIn = new ClampedFloatParameter(100f, -200f, 200f);//(val,min,max)



        // 只要 和默认值不同, 本功能就算是 active 了
        public bool IsActive()
        {
            return redOutRedIn.value != 100f
                || redOutGreenIn.value != 0f
                || redOutBlueIn.value != 0f

                || greenOutRedIn.value != 0f
                || greenOutGreenIn.value != 100f
                || greenOutBlueIn.value != 0f

                || blueOutRedIn.value != 0f
                || blueOutGreenIn.value != 0f
                || blueOutBlueIn.value != 100f;
        }

        public bool IsTileCompatible() => true;
    }
}
