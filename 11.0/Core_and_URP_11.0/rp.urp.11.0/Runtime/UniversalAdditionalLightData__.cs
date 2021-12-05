using System;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.Rendering.LWRP
{
    /*    tpr
    [Obsolete("LWRP -> Universal (UnityUpgradable) -> UnityEngine.Rendering.Universal.UniversalAdditionalLightData", true)]
    public class LWRPAdditionalLightData
    {
    }
    */
}


namespace UnityEngine.Rendering.Universal
{
    /*
        light go 可以添加这个组件;
        然后用户 可以在脚本中 设置部分数据;

        目前有的数据是: 
            -- "usePipelineSettings" 
            -- "additional Lights Shadow Resolution Tier";

    */
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public class UniversalAdditionalLightData //UniversalAdditionalLightData__
        : MonoBehaviour
    {

        [Tooltip("Controls if light Shadow Bias parameters use pipeline settings.")]
        [SerializeField] bool m_UsePipelineSettings = true;

        /*
            在 urp 中没看到写入此值; 也许是希望用户在 脚本中设置此值;
            -- 若为 true:  此时应该使用 asset 中存储的 shadow bias 数据
            -- 若为 false: 此时应该使用 Light 中存储的 shadow bias 数据
        */
        public bool usePipelineSettings
        {
            get { return m_UsePipelineSettings; }
            set { m_UsePipelineSettings = value; }
        }


        // tier 值, 此处只是一个 标识号, "GetAdditionalLightsShadowResolution()" 会用到这数据;
        public static readonly int AdditionalLightsShadowResolutionTierCustom    = -1;// 选用 用户自己设置的值
        public static readonly int AdditionalLightsShadowResolutionTierLow       =  0;
        public static readonly int AdditionalLightsShadowResolutionTierMedium    =  1;
        public static readonly int AdditionalLightsShadowResolutionTierHigh      =  2;
        public static readonly int AdditionalLightsShadowDefaultResolutionTier   = AdditionalLightsShadowResolutionTierHigh;

        // 具体的 分辨率值,
        public static readonly int AdditionalLightsShadowDefaultCustomResolution = 128;
        public static readonly int AdditionalLightsShadowMinimumResolution       = 128;

        [Tooltip("Controls if light shadow resolution uses pipeline settings.")]
        [SerializeField] int m_AdditionalLightsShadowResolutionTier   = AdditionalLightsShadowDefaultResolutionTier;


        /*
            此值可能等于 上面那串 "AdditionalLightsShadow..." 中的一种;

        

        */
        public int additionalLightsShadowResolutionTier
        {
            get { return m_AdditionalLightsShadowResolutionTier; }
        }
    }
}
