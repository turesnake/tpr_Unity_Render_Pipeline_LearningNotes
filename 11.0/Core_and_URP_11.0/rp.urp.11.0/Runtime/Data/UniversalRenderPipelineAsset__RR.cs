using System;
using UnityEngine.Scripting.APIUpdating;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using System.IO;
using UnityEditorInternal;
#endif
using System.ComponentModel;
using System.Linq;


/*
namespace UnityEngine.Rendering.LWRP
{
    [Obsolete("LWRP -> Universal (UnityUpgradable) -> UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset", true)]
    public class LightweightRenderPipelineAsset
    {
    }
}
*/


namespace UnityEngine.Rendering.Universal
{
    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public enum ShadowQuality//ShadowQuality__
    {
        Disabled,
        HardShadows,
        SoftShadows,
    }

    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public enum ShadowResolution//ShadowResolution__
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096
    }

    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public enum MsaaQuality//MsaaQuality__
    {
        Disabled = 1,
        _2x = 2,
        _4x = 4,
        _8x = 8
    }

    // 下采样: 多个 texel 放在同一个像素里; 压缩 texture;
    // enum: None,_2xBilinear,_4xBox,_4xBilinear
    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public enum Downsampling//Downsampling__
    {
        None,
        _2xBilinear,
        _4xBox,
        _4xBilinear
    }


    internal enum DefaultMaterialType//DefaultMaterialType__
    {
        Standard,
        Particle,
        Terrain,
        Sprite,
        UnityBuiltinDefault
    }

    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public enum LightRenderingMode//LightRenderingMode__
    {
        Disabled = 0,
        PerVertex = 2,
        PerPixel = 1,
    }

    // Shader Variant Log Level
    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public enum ShaderVariantLogLevel//ShaderVariantLogLevel__
    {
        Disabled,
        OnlyUniversalRPShaders,
        AllShaders,
    }


    /*
    [Obsolete("PipelineDebugLevel is unused and has no effect.", false)]
    public enum PipelineDebugLevel
    {
        Disabled,
        Profiling,
    }
    */



    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public enum RendererType//RendererType__
    {
        Custom,
        ForwardRenderer,
        _2DRenderer,
    }

    // 颜色渐变模式
    public enum ColorGradingMode//ColorGradingMode__
    {
        LowDynamicRange,
        HighDynamicRange
    }
    



    /*
        =================================================================================================================:
        全 urp 唯一一个 RenderPipelineAsset 派生类;

        它的 界面由 UniversalRenderPipelineAssetEditor class 控制
    */
    [ExcludeFromPreset]
    public partial class UniversalRenderPipelineAsset //UniversalRenderPipelineAsset__RR
        : RenderPipelineAsset, ISerializationCallbackReceiver
    {
        Shader m_DefaultShader;


        // 就是 inspector 中的 "Renderer List", 通常放入一个 "ForwardRenderer" 实例;
        ScriptableRenderer[] m_Renderers = new ScriptableRenderer[1];


        // ============================================================================================:

        // Default values set when a new UniversalRenderPipeline asset is created
        // 版本管理用, 
        [SerializeField] int k_AssetVersion = 9;
        [SerializeField] int k_AssetPreviousVersion = 9;

        // Deprecated settings for upgrading sakes 已弃用的升级设置
        [SerializeField] RendererType m_RendererType = RendererType.ForwardRenderer;

        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [SerializeField] internal ScriptableRendererData m_RendererData = null;


        // ----- Renderer settings -----:
        [SerializeField] internal ScriptableRendererData[] m_RendererDataList = new ScriptableRendererData[1];
        [SerializeField] internal int m_DefaultRendererIndex = 0;


        // ----- General settings -----:
        [SerializeField] bool m_RequireDepthTexture = false; // 需要 copy depth texture
        [SerializeField] bool m_RequireOpaqueTexture = false;// 需要 copy opaque color texture

        // 若上面 启用了 "Opaque color Texture", 本变量可被使用;
        // 可用本变量来设置 Opaque Texture 的采样模式:
        // 下采样: 多个 texel 放在同一个像素里; 压缩 texture;
        [SerializeField] Downsampling m_OpaqueDownsampling = Downsampling._2xBilinear;

        //若禁用此项, urp 将移除所有 "Terrain hole Shader variants", 以节省 build 时间;
        [SerializeField] bool m_SupportsTerrainHoles = true; 


        // ----- Quality settings -----:
        [SerializeField] bool m_SupportsHDR = true;
        [SerializeField] MsaaQuality m_MSAA = MsaaQuality.Disabled;
        [SerializeField] float m_RenderScale = 1.0f;
        // TODO: Shader Quality Tiers


        // ----- Main directional light Settings -----:
        [SerializeField] LightRenderingMode m_MainLightRenderingMode = LightRenderingMode.PerPixel;
        [SerializeField] bool m_MainLightShadowsSupported = true;
        [SerializeField] ShadowResolution m_MainLightShadowmapResolution = ShadowResolution._2048;

        // ----- Additional lights settings -----:
        [SerializeField] LightRenderingMode m_AdditionalLightsRenderingMode = LightRenderingMode.PerPixel;
        [SerializeField] int m_AdditionalLightsPerObjectLimit = 4;
        [SerializeField] bool m_AdditionalLightShadowsSupported = false;
        [SerializeField] ShadowResolution m_AdditionalLightsShadowmapResolution = ShadowResolution._2048;

        // 下面这组值就是 asset inspector 中 "Shadow Resolution Low: Medium: High" 所设置的三个值;
        // 虽然它们的默认值为: 256, 512, 1024;  但在 inspector 中的默认值为: 512, 1024, 4096;
        // 还是应该以 inspector 中的为准;
        [SerializeField] int m_AdditionalLightsShadowResolutionTierLow = AdditionalLightsDefaultShadowResolutionTierLow;//256
        [SerializeField] int m_AdditionalLightsShadowResolutionTierMedium = AdditionalLightsDefaultShadowResolutionTierMedium;//512
        [SerializeField] int m_AdditionalLightsShadowResolutionTierHigh = AdditionalLightsDefaultShadowResolutionTierHigh;//1024

        // ----- Shadows Settings -----:
        [SerializeField] float m_ShadowDistance = 50.0f;
        [SerializeField] int m_ShadowCascadeCount = 1;
        [SerializeField] float m_Cascade2Split = 0.25f;
        [SerializeField] Vector2 m_Cascade3Split = new Vector2(0.1f, 0.3f);
        [SerializeField] Vector3 m_Cascade4Split = new Vector3(0.067f, 0.2f, 0.467f);
        [SerializeField] float m_ShadowDepthBias = 1.0f;
        [SerializeField] float m_ShadowNormalBias = 1.0f;
        [SerializeField] bool m_SoftShadowsSupported = false;

        // ----- Advanced settings -----:
        [SerializeField] bool m_UseSRPBatcher = true;
        [SerializeField] bool m_SupportsDynamicBatching = false;
        [SerializeField] bool m_MixedLightingSupported = true;

        /*
        [SerializeField][Obsolete] PipelineDebugLevel m_DebugLevel;
        */


        // ----- Adaptive(自适应) performance settings -----:
        [SerializeField] bool m_UseAdaptivePerformance = true;

        // ----- Post-processing settings -----:
        [SerializeField] ColorGradingMode m_ColorGradingMode = ColorGradingMode.LowDynamicRange;
        [SerializeField] int m_ColorGradingLutSize = 32;
        [SerializeField] bool m_UseFastSRGBLinearConversion = false;


        // ===== Deprecated settings =====:
        /*    tpr
        [SerializeField] ShadowQuality m_ShadowType = ShadowQuality.HardShadows;
        [SerializeField] bool m_LocalShadowsSupported = false;
        [SerializeField] ShadowResolution m_LocalShadowsAtlasResolution = ShadowResolution._256;
        [SerializeField] int m_MaxPixelLights = 0;
        [SerializeField] ShadowResolution m_ShadowAtlasResolution = ShadowResolution._256;
        */

        [SerializeField] ShaderVariantLogLevel m_ShaderVariantLogLevel = ShaderVariantLogLevel.Disabled;


        // ============================================================================================:
        

        // Note: A lut size of 16^3 is barely usable with the HDR grading mode. 32 should be the
        // minimum, the lut being encoded in log. Lower sizes would work better with an additional
        // 1D shaper lut but for now we'll keep it simple.
        public const int k_MinLutSize = 16;
        public const int k_MaxLutSize = 65;

        // shadow cascade 级数
        internal const int k_ShadowCascadeMinCount = 1;
        internal const int k_ShadowCascadeMaxCount = 4;


        // add light 的 default shadow 分辨率
        public static readonly int AdditionalLightsDefaultShadowResolutionTierLow = 256;
        public static readonly int AdditionalLightsDefaultShadowResolutionTierMedium = 512;
        public static readonly int AdditionalLightsDefaultShadowResolutionTierHigh = 1024;

#if UNITY_EDITOR
        [NonSerialized]
        internal UniversalRenderPipelineEditorResources m_EditorResourcesAsset;

        public static readonly string packagePath = "Packages/com.unity.render-pipelines.universal";
        public static readonly string editorResourcesGUID = "a3d8d823eedde654bb4c11a1cfaf1abb";

        public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null)
        {
            // Create Universal RP Asset
            var instance = CreateInstance<UniversalRenderPipelineAsset>();
            if (rendererData != null)
                instance.m_RendererDataList[0] = rendererData;
            else
                instance.m_RendererDataList[0] = CreateInstance<ForwardRendererData>();

            // Initialize default Renderer
            instance.m_EditorResourcesAsset = instance.editorResources;

            return instance;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812")]
        internal class CreateUniversalPipelineAsset //CreateUniversalPipelineAsset__RR
            : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                //Create asset
                AssetDatabase.CreateAsset(Create(CreateRendererAsset(pathName, RendererType.ForwardRenderer)), pathName);
            }
        }

        [MenuItem("Assets/Create/Rendering/Universal Render Pipeline/Pipeline Asset (Forward Renderer)", priority = CoreUtils.assetCreateMenuPriority1)]
        static void CreateUniversalPipeline()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<CreateUniversalPipelineAsset>(),
                "UniversalRenderPipelineAsset.asset", null, null);
        }

        internal static ScriptableRendererData CreateRendererAsset(string path, RendererType type, bool relativePath = true)
        {
            ScriptableRendererData data = CreateRendererData(type);
            string dataPath;
            if (relativePath)
                dataPath =
                    $"{Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path))}_Renderer{Path.GetExtension(path)}";
            else
                dataPath = path;
            AssetDatabase.CreateAsset(data, dataPath);
            return data;
        }

        static ScriptableRendererData CreateRendererData(RendererType type)
        {
            switch (type)
            {
                case RendererType.ForwardRenderer:
                    return CreateInstance<ForwardRendererData>();
                // 2D renderer is experimental
                case RendererType._2DRenderer:
                    return CreateInstance<Experimental.Rendering.Universal.Renderer2DData>();
                // Forward Renderer is the fallback renderer that works on all platforms
                default:
                    return CreateInstance<ForwardRendererData>();
            }
        }

        //[MenuItem("Assets/Create/Rendering/Universal Pipeline Editor Resources", priority = CoreUtils.assetCreateMenuPriority1)]
        static void CreateUniversalPipelineEditorResources()
        {
            var instance = CreateInstance<UniversalRenderPipelineEditorResources>();
            ResourceReloader.ReloadAllNullIn(instance, packagePath);
            AssetDatabase.CreateAsset(instance, string.Format("Assets/{0}.asset", typeof(UniversalRenderPipelineEditorResources).Name));
        }

        UniversalRenderPipelineEditorResources editorResources
        {
            get
            {
                if (m_EditorResourcesAsset != null && !m_EditorResourcesAsset.Equals(null))
                    return m_EditorResourcesAsset;

                string resourcePath = AssetDatabase.GUIDToAssetPath(editorResourcesGUID);
                var objs = InternalEditorUtility.LoadSerializedFileAndForget(resourcePath);
                m_EditorResourcesAsset = objs != null && objs.Length > 0 ? objs.First() as UniversalRenderPipelineEditorResources : null;
                return m_EditorResourcesAsset;
            }
        }
#endif //UNITY_EDITOR 

        public ScriptableRendererData LoadBuiltinRendererData(RendererType type = RendererType.ForwardRenderer)
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            return m_RendererDataList[0] =
                CreateRendererAsset("Assets/ForwardRenderer.asset", type, false);
#else
            m_RendererDataList[0] = null;
            return m_RendererDataList[0];
#endif
        }


        /*
            -------------------------------------------------------------------------------------------------------:
            由我们来实现此 abstract 函数
            在程序渲染第一帧之前,unity 会主动调用此函数
            如果运行中途, RenderPipelineAsset 的一个设置发生了改变, unity 会销毁当前的 Render Pipe Instance
            然后在下一帧之前,重新调用本函数
        */
        protected override RenderPipeline CreatePipeline()//   读完__
        {
            // -----------------------------------:
            // 一些资源 和 version 检测;
            if (m_RendererDataList == null)
                m_RendererDataList = new ScriptableRendererData[1];

            // If no default data we can't create pipeline instance
            if (m_RendererDataList[m_DefaultRendererIndex] == null)
            {
                // If previous version and current version are miss-matched, then we are waiting for the upgrader to kick in
                // 如果 上一个版本 和 当前版本不匹配, 那我们就等待 upgrader 的介入;
                if (k_AssetPreviousVersion != k_AssetVersion)
                    return null;

                Debug.LogError(
                    $"Default Renderer is missing, make sure there is a Renderer assigned as the default on the current Universal RP asset:{UniversalRenderPipeline.asset.name}",
                    this);
                return null;
            }

            // ------------------------------------:
            CreateRenderers();// 创建 renderer 实例

            return new UniversalRenderPipeline(this);
        }





        void DestroyRenderers()
        {
            if (m_Renderers == null)
                return;
            for (int i = 0; i < m_Renderers.Length; i++)
                DestroyRenderer(ref m_Renderers[i]);
        }

        void DestroyRenderer(ref ScriptableRenderer renderer)
        {
            if (renderer != null)
            {
                renderer.Dispose();
                renderer = null;
            }
        }

        protected override void OnValidate()
        {
            DestroyRenderers();

            // This will call RenderPipelineManager.CleanupRenderPipeline that in turn disposes the render pipeline instance and
            // assign pipeline asset reference to null
            base.OnValidate();
        }

        protected override void OnDisable()
        {
            DestroyRenderers();

            // This will call RenderPipelineManager.CleanupRenderPipeline that in turn disposes the render pipeline instance and
            // assign pipeline asset reference to null
            base.OnDisable();
        }



        void CreateRenderers()//   读完__
        {
            DestroyRenderers();

            if (m_Renderers == null || m_Renderers.Length != m_RendererDataList.Length)
                m_Renderers = new ScriptableRenderer[m_RendererDataList.Length];

            for (int i = 0; i < m_RendererDataList.Length; ++i)
            {
                if (m_RendererDataList[i] != null)
                    // 创建出 如 ForwardRenderer 的实例
                    m_Renderers[i] = m_RendererDataList[i].InternalCreateRenderer();
            }
        }



        Material GetMaterial(DefaultMaterialType materialType)
        {
#if UNITY_EDITOR
            if (scriptableRendererData == null || editorResources == null)
                return null;

            var material = scriptableRendererData.GetDefaultMaterial(materialType);
            if (material != null)
                return material;

            switch (materialType)
            {
                case DefaultMaterialType.Standard:
                    return editorResources.materials.lit;

                case DefaultMaterialType.Particle:
                    return editorResources.materials.particleLit;

                case DefaultMaterialType.Terrain:
                    return editorResources.materials.terrainLit;

                // Unity Builtin Default
                default:
                    return null;
            }
#else
            return null;
#endif
        }

        
        // Returns the default renderer being used by this pipeline. 比如 "Forward Renderer" 
        public ScriptableRenderer scriptableRenderer
        {
            get
            {
                if (m_RendererDataList?.Length > m_DefaultRendererIndex && m_RendererDataList[m_DefaultRendererIndex] == null)
                {
                    Debug.LogError("Default renderer is missing from the current Pipeline Asset.", this);
                    return null;
                }

                if (scriptableRendererData.isInvalidated || m_Renderers[m_DefaultRendererIndex] == null)
                {
                    DestroyRenderer(ref m_Renderers[m_DefaultRendererIndex]);
                    m_Renderers[m_DefaultRendererIndex] = scriptableRendererData.InternalCreateRenderer();
                }

                return m_Renderers[m_DefaultRendererIndex];
            }
        }

    
        /// Returns a renderer from the current pipeline asset
        /// <param name="index">Index to the renderer. If invalid index is passed, the default renderer is returned instead.</param>
        public ScriptableRenderer GetRenderer(int index)
        {
            if (index == -1)
                index = m_DefaultRendererIndex;

            if (index >= m_RendererDataList.Length || index < 0 || m_RendererDataList[index] == null)
            {
                Debug.LogWarning(
                    $"Renderer at index {index.ToString()} is missing, falling back to Default Renderer {m_RendererDataList[m_DefaultRendererIndex].name}",
                    this);
                index = m_DefaultRendererIndex;
            }

            // RendererData list differs from RendererList. Create RendererList.
            if (m_Renderers == null || m_Renderers.Length < m_RendererDataList.Length)
                CreateRenderers();

            // This renderer data is outdated or invalid, we recreate the renderer
            // so we construct all render passes with the updated data
            if (m_RendererDataList[index].isInvalidated || m_Renderers[index] == null)
            {
                DestroyRenderer(ref m_Renderers[index]);
                m_Renderers[index] = m_RendererDataList[index].InternalCreateRenderer();
            }

            return m_Renderers[index];
        }



        internal ScriptableRendererData scriptableRendererData
        {
            get
            {
                if (m_RendererDataList[m_DefaultRendererIndex] == null)
                    CreatePipeline();

                return m_RendererDataList[m_DefaultRendererIndex];
            }
        }

#if UNITY_EDITOR
        internal GUIContent[] rendererDisplayList
        {
            get
            {
                GUIContent[] list = new GUIContent[m_RendererDataList.Length + 1];
                list[0] = new GUIContent($"Default Renderer ({RendererDataDisplayName(m_RendererDataList[m_DefaultRendererIndex])})");

                for (var i = 1; i < list.Length; i++)
                {
                    list[i] = new GUIContent($"{(i - 1).ToString()}: {RendererDataDisplayName(m_RendererDataList[i-1])}");
                }
                return list;
            }
        }

        string RendererDataDisplayName(ScriptableRendererData data)
        {
            if (data != null)
                return data.name;

            return "NULL (Missing RendererData)";
        }

#endif

        internal int[] rendererIndexList
        {
            get
            {
                int[] list = new int[m_RendererDataList.Length + 1];
                for (int i = 0; i < list.Length; i++)
                {
                    list[i] = i - 1;
                }
                return list;
            }
        }

        
        //  是否将 camera 的 depth buffer 复制一份到 "_CameraDepthTexture"
        public bool supportsCameraDepthTexture
        {
            get { return m_RequireDepthTexture; }
            set { m_RequireDepthTexture = value; }
        }

        // 是否将 camera 的 不透明物的 color buffer 复制一份到 "_CameraOpaque"
        public bool supportsCameraOpaqueTexture
        {
            get { return m_RequireOpaqueTexture; }
            set { m_RequireOpaqueTexture = value; }
        }

        // 下采样: 多个 texel 放在同一个像素里; 压缩 texture;
        public Downsampling opaqueDownsampling
        {
            get { return m_OpaqueDownsampling; }
        }

        public bool supportsTerrainHoles
        {
            get { return m_SupportsTerrainHoles; }
        }

        public bool supportsHDR
        {
            get { return m_SupportsHDR; }
            set { m_SupportsHDR = value; }
        }

        public int msaaSampleCount
        {
            get { return (int)m_MSAA; }
            set { m_MSAA = (MsaaQuality)value; }
        }

        public float renderScale
        {
            get { return m_RenderScale; }
            set { m_RenderScale = ValidateRenderScale(value); }
        }

        // main light 的模式: Disabled, PerVertex, PerPixel
        public LightRenderingMode mainLightRenderingMode
        {
            get { return m_MainLightRenderingMode; }
        }

        public bool supportsMainLightShadows
        {
            get { return m_MainLightShadowsSupported; }
        }

        public int mainLightShadowmapResolution
        {
            get { return (int)m_MainLightShadowmapResolution; }
        }

        // add 光的模式: Disabled, PerVertex, PerPixel
        public LightRenderingMode additionalLightsRenderingMode
        {
            get { return m_AdditionalLightsRenderingMode; }
        }

        // 支持的 add 光 的最大数量
        // 对应 asset inspector 中: Lighting - Additional lights - "Per Object Limit"
        // 可设置的区间 [0,8], 默认值为 4;
        public int maxAdditionalLightsCount
        {
            get { return m_AdditionalLightsPerObjectLimit; }
            set { m_AdditionalLightsPerObjectLimit = ValidatePerObjectLights(value); }
        }

        public bool supportsAdditionalLightShadows
        {
            get { return m_AdditionalLightShadowsSupported; }
        }

        public int additionalLightsShadowmapResolution
        {
            get { return (int)m_AdditionalLightsShadowmapResolution; }
        }

       

        /*
            Returns the additional light shadow resolution defined for tier "Low/Medium/High" 
            in the UniversalRenderPipeline asset.
            ---
            专用于 add light 的, Low/Medium/High 档配置中 shadow resolution 值;
            由 asset inspector 配置, inspector 中默认值为: 512/1024/4096
        */
        public int additionalLightsShadowResolutionTierLow{
            get { return (int)m_AdditionalLightsShadowResolutionTierLow; }
        }
        public int additionalLightsShadowResolutionTierMedium{
            get { return (int)m_AdditionalLightsShadowResolutionTierMedium; }
        }
        public int additionalLightsShadowResolutionTierHigh{
            get { return (int)m_AdditionalLightsShadowResolutionTierHigh; }
        }

        /*
            获得 单张 add light 的 shadow tile(slice) 分辨率上限 (pix)
            分三个档次: Low/Medium/High;
            至于具体选哪一档, 全看 point光 / spot光 light 界面配置;
        */
        internal int GetAdditionalLightsShadowResolution( int additionalLightsShadowResolutionTier )// 读完__
        {
            if (additionalLightsShadowResolutionTier <= UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow /* 0 */)
                return additionalLightsShadowResolutionTierLow;

            if (additionalLightsShadowResolutionTier == UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium /* 1 */)
                return additionalLightsShadowResolutionTierMedium;

            if (additionalLightsShadowResolutionTier >= UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh /* 2 */)
                return additionalLightsShadowResolutionTierHigh;

            return additionalLightsShadowResolutionTierMedium;
        }// 函数完__



        /// <summary>
        /// Controls the maximum distance at which shadows are visible.
        /// </summary>
        public float shadowDistance
        {
            get { return m_ShadowDistance; }
            set { m_ShadowDistance = Mathf.Max(0.0f, value); }
        }

        /*
            Returns the number of shadow cascades.
            cascade 有几层, 区间[1,4]; (比如: 4个重叠的球体) 
        */
        public int shadowCascadeCount
        {
            get { return m_ShadowCascadeCount; }
            set
            {
                if (value < k_ShadowCascadeMinCount || value > k_ShadowCascadeMaxCount)
                {
                    throw new ArgumentException($"Value ({value}) needs to be between {k_ShadowCascadeMinCount} and {k_ShadowCascadeMaxCount}.");
                }
                m_ShadowCascadeCount = value;
            }
        }


        /// <summary>
        /// Returns the split value.
        /// </summary>
        /// <returns>Returns a Float with the split value.</returns>
        public float cascade2Split
        {
            get { return m_Cascade2Split; }
        }

        /// <summary>
        /// Returns the split values.
        /// </summary>
        /// <returns>Returns a Vector2 with the split values.</returns>
        public Vector2 cascade3Split
        {
            get { return m_Cascade3Split; }
        }


        /// <summary>
        /// Returns the split values.
        /// </summary>
        /// <returns>Returns a Vector3 with the split values.</returns>
        public Vector3 cascade4Split
        {
            get { return m_Cascade4Split; }
        }

        /// <summary>
        /// The Shadow Depth Bias, controls the offset of the lit pixels.
        /// </summary>
        public float shadowDepthBias
        {
            get { return m_ShadowDepthBias; }
            set { m_ShadowDepthBias = ValidateShadowBias(value); }
        }

        /// <summary>
        /// Controls the distance at which the shadow casting surfaces are shrunk along the surface normal.
        /// </summary>
        public float shadowNormalBias
        {
            get { return m_ShadowNormalBias; }
            set { m_ShadowNormalBias = ValidateShadowBias(value); }
        }

        /// <summary>
        /// Returns true Soft Shadows are supported, false otherwise.
        /// </summary>
        public bool supportsSoftShadows
        {
            get { return m_SoftShadowsSupported; }
        }

        // 动态批处理优化技术, 不建议使用
        public bool supportsDynamicBatching
        {
            get { return m_SupportsDynamicBatching; }
            set { m_SupportsDynamicBatching = value; }
        }

        // 是否支持 Mixed mode 的光;
        // asset inspector 中可设置
        public bool supportsMixedLighting
        {
            get { return m_MixedLightingSupported; }
        }

        public ShaderVariantLogLevel shaderVariantLogLevel
        {
            get { return m_ShaderVariantLogLevel; }
            set { m_ShaderVariantLogLevel = value; }
        }

        /*
        [Obsolete("PipelineDebugLevel is deprecated. Calling debugLevel is not necessary.", false)]
        public PipelineDebugLevel debugLevel
        {
            get => PipelineDebugLevel.Disabled;
        }
        */



        public bool useSRPBatcher
        {
            get { return m_UseSRPBatcher; }
            set { m_UseSRPBatcher = value; }
        }

        public ColorGradingMode colorGradingMode
        {
            get { return m_ColorGradingMode; }
            set { m_ColorGradingMode = value; }
        }

        /*
            可设置的区间 [16, 65], 通常为 32;
        */
        public int colorGradingLutSize
        {
            get { return m_ColorGradingLutSize; }
            set { m_ColorGradingLutSize = Mathf.Clamp(value, k_MinLutSize, k_MaxLutSize); }
        }

       
        /*
            Returns true if "fast approximation functions" are used when converting between the sRGB and Linear color spaces, 
            false otherwise.
            ---
            快速但不够精确的 sRGB<->Linear 转换函数
        */
        public bool useFastSRGBLinearConversion
        {
            get { return m_UseFastSRGBLinearConversion; }
        }

        /// <summary>
        /// Set to true to allow Adaptive performance to modify graphics quality settings during runtime.
        /// Only applicable when Adaptive performance package is available.
        /// </summary>
        public bool useAdaptivePerformance
        {
            get { return m_UseAdaptivePerformance; }
            set { m_UseAdaptivePerformance = value; }
        }

        public override Material defaultMaterial
        {
            get { return GetMaterial(DefaultMaterialType.Standard); }
        }

        public override Material defaultParticleMaterial
        {
            get { return GetMaterial(DefaultMaterialType.Particle); }
        }

        public override Material defaultLineMaterial
        {
            get { return GetMaterial(DefaultMaterialType.Particle); }
        }

        public override Material defaultTerrainMaterial
        {
            get { return GetMaterial(DefaultMaterialType.Terrain); }
        }

        public override Material defaultUIMaterial
        {
            get { return GetMaterial(DefaultMaterialType.UnityBuiltinDefault); }
        }

        public override Material defaultUIOverdrawMaterial
        {
            get { return GetMaterial(DefaultMaterialType.UnityBuiltinDefault); }
        }

        public override Material defaultUIETC1SupportedMaterial
        {
            get { return GetMaterial(DefaultMaterialType.UnityBuiltinDefault); }
        }

        public override Material default2DMaterial
        {
            get { return GetMaterial(DefaultMaterialType.Sprite); }
        }

        public override Shader defaultShader
        {
            get
            {
#if UNITY_EDITOR
                // TODO: When importing project, AssetPreviewUpdater:CreatePreviewForAsset will be called multiple time
                // which in turns calls this property to get the default shader.
                // The property should never return null as, when null, it loads the data using AssetDatabase.LoadAssetAtPath.
                // However it seems there's an issue that LoadAssetAtPath will not load the asset in some cases. so adding the null check
                // here to fix template tests.
                if (scriptableRendererData != null)
                {
                    Shader defaultShader = scriptableRendererData.GetDefaultShader();
                    if (defaultShader != null)
                        return defaultShader;
                }

                if (m_DefaultShader == null)
                {
                    string path = AssetDatabase.GUIDToAssetPath(ShaderUtils.GetShaderGUID(ShaderPathID.Lit));
                    m_DefaultShader  = AssetDatabase.LoadAssetAtPath<Shader>(path);
                }
#endif

                if (m_DefaultShader == null)
                    m_DefaultShader = Shader.Find(ShaderUtils.GetShaderPath(ShaderPathID.Lit));

                return m_DefaultShader;
            }
        }

#if UNITY_EDITOR
        public override Shader autodeskInteractiveShader
        {
            get { return editorResources?.shaders.autodeskInteractivePS; }
        }

        public override Shader autodeskInteractiveTransparentShader
        {
            get { return editorResources?.shaders.autodeskInteractiveTransparentPS; }
        }

        public override Shader autodeskInteractiveMaskedShader
        {
            get { return editorResources?.shaders.autodeskInteractiveMaskedPS; }
        }

        public override Shader terrainDetailLitShader
        {
            get { return editorResources?.shaders.terrainDetailLitPS; }
        }

        public override Shader terrainDetailGrassShader
        {
            get { return editorResources?.shaders.terrainDetailGrassPS; }
        }

        public override Shader terrainDetailGrassBillboardShader
        {
            get { return editorResources?.shaders.terrainDetailGrassBillboardPS; }
        }

        public override Shader defaultSpeedTree7Shader
        {
            get { return editorResources?.shaders.defaultSpeedTree7PS; }
        }

        public override Shader defaultSpeedTree8Shader
        {
            get { return editorResources?.shaders.defaultSpeedTree8PS; }
        }
#endif

        public void OnBeforeSerialize()
        {}

        //   版本管理 
        public void OnAfterDeserialize()
        {
            if (k_AssetVersion < 3)
            {
                m_SoftShadowsSupported = (m_ShadowType == ShadowQuality.SoftShadows);
                k_AssetPreviousVersion = k_AssetVersion;
                k_AssetVersion = 3;
            }

            if (k_AssetVersion < 4)
            {
                m_AdditionalLightShadowsSupported = m_LocalShadowsSupported;
                m_AdditionalLightsShadowmapResolution = m_LocalShadowsAtlasResolution;
                m_AdditionalLightsPerObjectLimit = m_MaxPixelLights;
                m_MainLightShadowmapResolution = m_ShadowAtlasResolution;
                k_AssetPreviousVersion = k_AssetVersion;
                k_AssetVersion = 4;
            }

            if (k_AssetVersion < 5)
            {
                if (m_RendererType == RendererType.Custom)
                {
                    m_RendererDataList[0] = m_RendererData;
                }
                k_AssetPreviousVersion = k_AssetVersion;
                k_AssetVersion = 5;
            }

            if (k_AssetVersion < 6)
            {
#pragma warning disable 618 // Obsolete warning
                // Adding an upgrade here so that if it was previously set to 2 it meant 4 cascades.
                // So adding a 3rd cascade shifted this value up 1.
                int value = (int)m_ShadowCascades;
                if (value == 2)
                {
                    m_ShadowCascadeCount = 4;
                }
                else
                {
                    m_ShadowCascadeCount = value + 1;
                }
                k_AssetVersion = 6;
#pragma warning restore 618 // Obsolete warning
            }

            if (k_AssetVersion < 7)
            {
                k_AssetPreviousVersion = k_AssetVersion;
                k_AssetVersion = 7;
            }

            if (k_AssetVersion < 9)
            {
                bool assetContainsCustomAdditionalLightShadowResolutions =
                    m_AdditionalLightsShadowResolutionTierHigh != AdditionalLightsDefaultShadowResolutionTierHigh ||
                    m_AdditionalLightsShadowResolutionTierMedium != AdditionalLightsDefaultShadowResolutionTierMedium ||
                    m_AdditionalLightsShadowResolutionTierLow != AdditionalLightsDefaultShadowResolutionTierLow;

                if (!assetContainsCustomAdditionalLightShadowResolutions)
                {
                    // if all resolutions are still the default values, we assume that they have never been customized and that it is safe to upgrade them to fit better the Additional Lights Shadow Atlas size
                    m_AdditionalLightsShadowResolutionTierHigh = (int)m_AdditionalLightsShadowmapResolution;
                    m_AdditionalLightsShadowResolutionTierMedium = Mathf.Max(m_AdditionalLightsShadowResolutionTierHigh / 2, UniversalAdditionalLightData.AdditionalLightsShadowMinimumResolution);
                    m_AdditionalLightsShadowResolutionTierLow = Mathf.Max(m_AdditionalLightsShadowResolutionTierMedium / 2, UniversalAdditionalLightData.AdditionalLightsShadowMinimumResolution);
                }

                k_AssetPreviousVersion = k_AssetVersion;
                k_AssetVersion = 9;
            }

#if UNITY_EDITOR
            if (k_AssetPreviousVersion != k_AssetVersion)
            {
                EditorApplication.delayCall += () => UpgradeAsset(this);
            }
#endif
        }

#if UNITY_EDITOR
        static void UpgradeAsset(UniversalRenderPipelineAsset asset)
        {
            if (asset.k_AssetPreviousVersion < 5)
            {
                if (asset.m_RendererType == RendererType.ForwardRenderer)
                {
                    var data = AssetDatabase.LoadAssetAtPath<ForwardRendererData>("Assets/ForwardRenderer.asset");
                    if (data)
                    {
                        asset.m_RendererDataList[0] = data;
                    }
                    else
                    {
                        asset.LoadBuiltinRendererData();
                    }
                    asset.m_RendererData = null; // Clears the old renderer
                }

                asset.k_AssetPreviousVersion = 5;
            }

            if (asset.k_AssetPreviousVersion < 9)
            {
                // The added feature was reverted, we keep this version to avoid breakage in case somebody already has version 7
                asset.k_AssetPreviousVersion = 9;
            }

            EditorUtility.SetDirty(asset);
        }

#endif

        float ValidateShadowBias(float value)
        {
            return Mathf.Max(0.0f, Mathf.Min(value, UniversalRenderPipeline.maxShadowBias));
        }

        // 将 参数 value 约束在 [0, UniversalRenderPipeline.maxPerObjectLights] 之间;
        // gles2 最多支持 4 个, 别的平台则是 8 个;
        int ValidatePerObjectLights(int value)
        {
            return System.Math.Max(0, System.Math.Min(value, UniversalRenderPipeline.maxPerObjectLights));
        }

        float ValidateRenderScale(float value)
        {
            return Mathf.Max(UniversalRenderPipeline.minRenderScale, Mathf.Min(value, UniversalRenderPipeline.maxRenderScale));
        }


        /// <summary>
        /// Check to see if the RendererData list contains valid RendererData references.
        /// </summary>
        /// <param name="partial">This bool controls whether to test against all or any, 
        ///                     if false then there has to be no invalid RendererData
        /// </param>
        internal bool ValidateRendererDataList(bool partial = false)
        {
            var emptyEntries = 0;
            for (int i = 0; i < m_RendererDataList.Length; i++) emptyEntries += ValidateRendererData(i) ? 0 : 1;
            if (partial)
                return emptyEntries == 0;
            return emptyEntries != m_RendererDataList.Length;
        }


        // 检查参数 index 是否指向一个有效的的 renderer;
        internal bool ValidateRendererData(int index)//  读完__
        {
            // Check to see if you are asking for the default renderer
            if (index == -1) index = m_DefaultRendererIndex;
            return index < m_RendererDataList.Length ? m_RendererDataList[index] != null : false;
        }

        
    }
}
