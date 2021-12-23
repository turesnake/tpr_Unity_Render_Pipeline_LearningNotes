using System;

namespace UnityEngine.Rendering.Universal
{
    /*
        Lift, Gamma, Gain; 分别控制 暗/中/亮 三个区域的颜色
    */
    [Serializable, VolumeComponentMenu("Post-processing/Lift, Gamma, Gain")]
    public sealed class LiftGammaGain //LiftGammaGain__
        : VolumeComponent, IPostProcessComponent
    {

        // 提升: 控制暗部颜色
        [Tooltip("Controls the darkest portions of the render.")]
        public Vector4Parameter lift = new Vector4Parameter(new Vector4(1f, 1f, 1f, 0f));


        // 伽玛: 控制中间调的 乘幂函数
        [Tooltip("Power function that controls mid-range tones.")]
        public Vector4Parameter gamma = new Vector4Parameter(new Vector4(1f, 1f, 1f, 0f));

        // 增益: 控制亮部颜色
        [Tooltip("Controls the lightest portions of the render.")]
        public Vector4Parameter gain = new Vector4Parameter(new Vector4(1f, 1f, 1f, 0f));


        // 只要被改动, 就认为本模块功能被启动了
        public bool IsActive()
        {
            var defaultState = new Vector4(1f, 1f, 1f, 0f);
            return lift != defaultState
                || gamma != defaultState
                || gain != defaultState;
        }

        public bool IsTileCompatible() => true;
    }
}
