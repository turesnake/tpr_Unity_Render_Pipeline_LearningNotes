using System;

namespace UnityEngine.Rendering.Universal
{
    /*
        使用一组曲线来控制 整个画面的颜色
    */
    [Serializable, VolumeComponentMenu("Post-processing/Color Curves")]
    public sealed class ColorCurves //ColorCurves__
        : VolumeComponent, IPostProcessComponent
    {
        // 调整全局颜色
        public TextureCurveParameter master = new TextureCurveParameter(
            // 默认值为一条 从 (0,0)左下 到 (1,1)右上 的直线; 
            new TextureCurve(
                new[] { 
                    new Keyframe(0f, 0f, 1f, 1f), // (time, value, inTangent, outTangent)
                    new Keyframe(1f, 1f, 1f, 1f)  
                }, 
                0f,                 // The default value to use when the curve doesn't have any key
                false,              // 曲线是否在 两端边界处 自动循环
                new Vector2(0f, 1f) // 两侧端点的值 (x轴值)
            )
        );

        public TextureCurveParameter red = new TextureCurveParameter(
            // 默认值为一条 从 (0,0)左下 到 (1,1)右上 的直线; 
            new TextureCurve(
                new[] { 
                    new Keyframe(0f, 0f, 1f, 1f), 
                    new Keyframe(1f, 1f, 1f, 1f) 
                }, 
                0f,                 // The default value to use when the curve doesn't have any key
                false,              // 曲线是否在 两端边界处 自动循环
                new Vector2(0f, 1f) // 两侧端点的值 (x轴值)
            )
        );

        public TextureCurveParameter green = new TextureCurveParameter(
            // 默认值为一条 从 (0,0)左下 到 (1,1)右上 的直线; 
            new TextureCurve(
                new[] { 
                    new Keyframe(0f, 0f, 1f, 1f), 
                    new Keyframe(1f, 1f, 1f, 1f) 
                }, 
                0f,
                false, 
                new Vector2(0f, 1f)
            )
        );

        public TextureCurveParameter blue = new TextureCurveParameter(
            // 默认值为一条 从 (0,0)左下 到 (1,1)右上 的直线; 
            new TextureCurve(
                new[] { 
                    new Keyframe(0f, 0f, 1f, 1f), 
                    new Keyframe(1f, 1f, 1f, 1f) 
                }, 
                0f, 
                false, 
                new Vector2(0f, 1f))
        );

        // 
        public TextureCurveParameter hueVsHue = new TextureCurveParameter(
            // 默认值为一条水平线, 没有节点, 全程值都为 0.5
            new TextureCurve(
                new Keyframe[] {}, 
                0.5f, 
                true,               // 曲线在 两端边界处 自动循环
                new Vector2(0f, 1f)
            )
        );

        // 
        public TextureCurveParameter hueVsSat = new TextureCurveParameter(
            // 默认值为一条水平线, 没有节点, 全程值都为 0.5
            new TextureCurve(
                new Keyframe[] {}, 
                0.5f, 
                true,               // 曲线在 两端边界处 自动循环
                new Vector2(0f, 1f)
            )
        );

        //
        public TextureCurveParameter satVsSat = new TextureCurveParameter(
            // 默认值为一条水平线, 没有节点, 全程值都为 0.5
            new TextureCurve(
                new Keyframe[] {}, 
                0.5f, 
                false,              // 曲线在 两端边界处 不 自动循环
                new Vector2(0f, 1f)
            )
        );

        //
        public TextureCurveParameter lumVsSat = new TextureCurveParameter(
            // 默认值为一条水平线, 没有节点, 全程值都为 0.5
            new TextureCurve(
                new Keyframe[] {}, 
                0.5f, 
                false, 
                new Vector2(0f, 1f)
            )
        );

        // 开启了本模块, 就会被算入 渲染流程中
        public bool IsActive() => true;

        public bool IsTileCompatible() => true;
    }
}
