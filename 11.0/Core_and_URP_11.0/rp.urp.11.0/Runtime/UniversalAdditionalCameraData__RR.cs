using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using System.ComponentModel;

/*
namespace UnityEngine.Rendering.LWRP
{
    [Obsolete("LWRP -> Universal (UnityUpgradable) -> UnityEngine.Rendering.Universal.UniversalAdditionalCameraData", true)]
    public class LWRPAdditionalCameraData
    {
    }
}
*/

namespace UnityEngine.Rendering.Universal
{

    
    /*
        Holds information about "whether to override certain camera rendering options from the render pipeline asset".
        --
        是否 覆写 当前 camera 的 rendering options;
    */
    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public enum CameraOverrideOption//CameraOverrideOption__
    {
        Off,// 不管 pipeline asset 设置了什么, 都不执行 覆写;
        On, // 不管 pipeline asset 设置了什么, 都执行 覆写;
        UsePipelineSettings,// 使用 "UniversalRenderPipelineAsset" 中的数据来决定, 是否覆写;
    }


    /*    tpr
    //  没看到此 enum 被使用 
    //[Obsolete("Renderer override is no longer used, renderers are referenced by index on the pipeline asset.")]
    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public enum RendererOverrideOption//RendererOverrideOption__
    {
        Custom,
        UsePipelineSettings,
    }
    */


    /*
        Holds information about the "post-processing anti-aliasing" mode.
        enum: None, "FXAA", "SMAA";
    */
    public enum AntialiasingMode//AntialiasingMode__
    {
        /*
            no "post-processing anti-aliasing pass" will be performed.
        */
        None,
        /*
            "FXAA"
            a "fast approximated anti-aliasing pass" will render when resolving the camera to screen.
            "快速近似"
        */
        FastApproximateAntialiasing,
        /*
            "SMAA" 
            pass will render when resolving the camera to screen. 
            通过设置 enum "AntialiasingQuality"(见本文件下方), 可以选择不同的质量;
        */
        SubpixelMorphologicalAntiAliasing,
        //TemporalAntialiasing
    }


    /*
        enum: Base, Overlay;
    */
    public enum CameraRenderType//CameraRenderType__
    {
        // allows the camera to render to either the screen or to a texture
        Base,

        //  allows the camera to render on top of a previous camera output, thus compositing camera results.
        //  运行 camera 的渲染结果 写到 上一个 "camera output" 上面去,
        //  从而将前后两次 渲染值 合成起来;
        Overlay,
    }



    /*
        Controls SMAA anti-aliasing quality.
        enum: Low, Medium, High;
    */
    public enum AntialiasingQuality
    {
        Low,
        Medium,
        High
    }



    /*
        Contains extension methods for Camera class.   扩展函数 
    */
    public static class CameraExtensions//CameraExtensions__
    {
        
        /*
            urp exposes "additional rendering data" in a separate component.
            ---
            urp 在一个单独的 组件中 暴露自己的 "additional rendering data";

            本函数返回 参数 "camera" 的 "additional data 组件";
            如果没找到目标数据, 就新建一个 类实例, 并返回之;
            
            返回值:
                参数 camera 的 "additional rendering data", 存储在类 "UniversalAdditionalCameraData" 中;
        */
        public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera)
        {
            var gameObject = camera.gameObject;
            bool componentExists = gameObject.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData);
            if (!componentExists)
                cameraData = gameObject.AddComponent<UniversalAdditionalCameraData>();

            return cameraData;
        }
    }


    // 没有被任何 代码使用过... 
    static class CameraTypeUtility//CameraTypeUtility__
    {
        static string[] s_CameraTypeNames = Enum.GetNames(typeof(CameraRenderType)).ToArray();

        public static string GetName(this CameraRenderType type)
        {
            int typeInt = (int)type;
            if (typeInt < 0 || typeInt >= s_CameraTypeNames.Length)
                typeInt = (int)CameraRenderType.Base;
            return s_CameraTypeNames[typeInt];
        }
    }



    /*
        =======================================================================================================================
        这是一个 go 组件, manual 处的内容翻译:

        本类用来存储 camera 的内部数据;
        本类允许 urp 来拓展和覆写 "unity 标准 camera 组件" 的 功能和界面;

        在 urp 中, 如果一个 go 绑定了 camera 组件, 那么它也必须绑定 本类的组件;
        如果你在一个 urp 项目中新建一个 camera go 时, unity 会自动绑定上 本类组件; 而且无法移除这个 本类组件;

        如果你并不使用 脚本 来控制和自定义 urp, 那么你不需要对 本类组件 做任何事;
        如果你想使用 脚本 来自定义 urp, 你可以这样写:
            var cameraData = camera.GetUniversalAdditionalCameraData();

        来得到 camera 的本类组件实例;
        如果你要经常访问 本类数据, 应该做个缓存, 以减少 cpu 开支;
    */
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [ImageEffectAllowedInSceneView]
    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public class UniversalAdditionalCameraData//UniversalAdditionalCameraData__RR
        : MonoBehaviour, ISerializationCallbackReceiver
    {

        [FormerlySerializedAs("renderShadows"), SerializeField]bool m_RenderShadows = true;

        [SerializeField]CameraOverrideOption m_RequiresDepthTextureOption = CameraOverrideOption.UsePipelineSettings;

        [SerializeField]CameraOverrideOption m_RequiresOpaqueTextureOption = CameraOverrideOption.UsePipelineSettings;

        [SerializeField] CameraRenderType m_CameraType = CameraRenderType.Base;

        // inspector 中 stack 中的所有 cameras, 只有绑入 overlay camera 才是合理的;
        [SerializeField] List<Camera> m_Cameras = new List<Camera>();

        // 值为 -1,  表示使用 asset 的默认值;
        [SerializeField] int m_RendererIndex = -1;

        [SerializeField] LayerMask m_VolumeLayerMask = 1; // "Default"
        [SerializeField] Transform m_VolumeTrigger = null;

        [SerializeField] bool m_RenderPostProcessing = false;
        [SerializeField] AntialiasingMode m_Antialiasing = AntialiasingMode.None;
        [SerializeField] AntialiasingQuality m_AntialiasingQuality = AntialiasingQuality.High;
        [SerializeField] bool m_StopNaN = false;
        [SerializeField] bool m_Dithering = false;
        [SerializeField] bool m_ClearDepth = true;
        [SerializeField] bool m_AllowXRRendering = true;


        [NonSerialized] Camera m_Camera;
        // Deprecated:
        [FormerlySerializedAs("requiresDepthTexture"), SerializeField]
        bool m_RequiresDepthTexture = false;

        [FormerlySerializedAs("requiresColorTexture"), SerializeField]
        bool m_RequiresColorTexture = false;

        [HideInInspector][SerializeField] float m_Version = 2;

        public float version => m_Version;

        static UniversalAdditionalCameraData s_DefaultAdditionalCameraData = null;
        internal static UniversalAdditionalCameraData defaultAdditionalCameraData
        {
            get
            {
                if (s_DefaultAdditionalCameraData == null)
                    s_DefaultAdditionalCameraData = new UniversalAdditionalCameraData();

                return s_DefaultAdditionalCameraData;
            }
        }

#if UNITY_EDITOR
        internal new Camera camera
#else
        internal Camera camera
#endif
        {
            get
            {
                if (!m_Camera)
                {
                    gameObject.TryGetComponent<Camera>(out m_Camera);
                }
                return m_Camera;
            }
        }


        
        /*
            Controls if this camera should render shadows.

            对应 camera inspector: "Render Shadows"
        */
        public bool renderShadows
        {
            get => m_RenderShadows;
            set => m_RenderShadows = value;
        }


        
        /*
            对应 camera inspector: "Depth Texture"

            Controls if a camera should render depth.
            depth 值可在 shader 中被绑定为 "_CameraDepthTexture";
        */
        public CameraOverrideOption requiresDepthOption
        {
            get => m_RequiresDepthTextureOption;
            set => m_RequiresDepthTextureOption = value;
        }



        /*
            对应 camera inspector: "Opaque Texture";

            Controls if a camera should copy the "color contents of a camera" after rendering opaques.

            设置: camera 在渲染完 不透明物体后, 是否应该把 渲染内容 复制一份, 以便在 流程的后续阶段 可以访问这个数据;
            这个 color texture 数据 对应 shader 中的  "_CameraOpaqueTexture";
        */
        public CameraOverrideOption requiresColorOption
        {
            get => m_RequiresOpaqueTextureOption;
            set => m_RequiresOpaqueTextureOption = value;
        }



        /*
            对应 camera inspector: "Render Type" (最顶部位置)
            enum: base, Overlay;
        */
        public CameraRenderType renderType
        {
            get => m_CameraType;
            set => m_CameraType = value;
        }
       

        /*  
            Returns the camera stack. 
            Only valid for "Base cameras". "Overlay cameras" have no stack and will return null.
            ---
            只有当本函数的调用者自己为 base camera 时, 调用本函数才是有效的, 否则返回 null;
            返回的 stack 中, 存储的都是 overlay camera, (猜测就是 inspector 中手动绑定的那堆)
        */
        public List<Camera> cameraStack//    读完__
        {
            get
            {
                // 若发现本 camera 为 Overlay Camera, 直接警报
                if (renderType != CameraRenderType.Base){
                    var camera = gameObject.GetComponent<Camera>();
                    Debug.LogWarning(string.Format("{0}: This camera is of {1} type. Only Base cameras can have a camera stack.", 
                        camera.name, renderType));
                    return null;
                }

                // 若本 camera 有个不支持 camera stacking 的 scriptableRenderer
                if (scriptableRenderer.supportedRenderingFeatures.cameraStacking == false)
                {
                    var camera = gameObject.GetComponent<Camera>();
                    Debug.LogWarning(string.Format("{0}: This camera has a ScriptableRenderer that doesn't support camera stacking. Camera stack is null.", camera.name));
                    return null;
                }

                // 真正的返回
                return m_Cameras;
            }
        }



        // 把 camera stack 中所有值为 null 的元素剔除掉
        internal void UpdateCameraStack()//   读完__
        {
#if UNITY_EDITOR
            Undo.RecordObject(this, "Update camera stack");
#endif
            int prev = m_Cameras.Count;
            m_Cameras.RemoveAll(cam => cam == null);//剔除掉 stack 中所有的 null 元素;
            int curr = m_Cameras.Count;
            int removedCamsCount = prev - curr;
            if (removedCamsCount != 0)
            {
                Debug.LogWarning(name + ": " + removedCamsCount + " camera overlay" + (removedCamsCount > 1 ? "s" : "") + " no longer exists and will be removed from the camera stack.");
            }
        }

        

        /*
            If true, 前面的 cameras 写入的 depth 将被清除掉, 然后再执行 本 overlay camera 的渲染;
            对应 camera inspector: "Clear Depth", (只在 Overlay Camera 中存在)
        */
        public bool clearDepth
        {
            get => m_ClearDepth;
        }


        /*
            Returns true if this camera needs to render depth information in a texture. 
            If enabled, depth texture is available to be bound and read from shaders as "_CameraDepthTexture" after rendering skybox.
            ---
            
            如果本 camera 会将 depth 数据写入一个 texture 中, 本变量返回 true;

            此时, 可在 渲染完 skybox 之后, 在 shader 中绑定和访问 "_CameraDepthTexture" 来获得这个 depth 数据;
            猜测:
                skybox 往往是 实心物渲染的最后一步, 之后就要渲染 半透明物体了, 而它们不会改写 depth buffer值;
                所以在这个时间节点后, 此 camera 的 depth texture 就不变了, 可以被访问了;

            这个数据到底是在 depth prepass 阶段, 还是 copy depth 阶段写入的, 不是很清晰...
        */
        public bool requiresDepthTexture
        {
            get
            {
                if (m_RequiresDepthTextureOption == CameraOverrideOption.UsePipelineSettings)
                {
                    return UniversalRenderPipeline.asset.supportsCameraDepthTexture;
                }
                else
                {
                    return m_RequiresDepthTextureOption == CameraOverrideOption.On;
                }
            }
            set { m_RequiresDepthTextureOption = (value) ? CameraOverrideOption.On : CameraOverrideOption.Off; }
        }


        /*
            如果这个 camera 将 "opaque color contents" 渲染进一张 texture 中, 本值返回 true;

            此时, 在 渲染完毕 skybox 之后,
            可在 shader 中绑定和访问一张 "_CameraOpaqueTexture", 来获得这个 opaque color 数据;
            猜测:
                skybox 往往是 实心物渲染的最后一步, 之后就要渲染 半透明物体了;
        */
        public bool requiresColorTexture
        {
            get
            {
                if (m_RequiresOpaqueTextureOption == CameraOverrideOption.UsePipelineSettings)
                {
                    return UniversalRenderPipeline.asset.supportsCameraOpaqueTexture;
                }
                else
                {
                    return m_RequiresOpaqueTextureOption == CameraOverrideOption.On;
                }
            }
            set { m_RequiresOpaqueTextureOption = (value) ? CameraOverrideOption.On : CameraOverrideOption.Off; }
        }


        /*
            返回 用来渲染本 camera的 ScriptableRenderer, 如 "Forward Renderer"
        */
        public ScriptableRenderer scriptableRenderer
        {
            get
            {
                if (UniversalRenderPipeline.asset is null)
                    return null;
                if (!UniversalRenderPipeline.asset.ValidateRendererData(m_RendererIndex))
                {   // 如果 m_RendererIndex 不能指向 asset 的容器中 一个有效的 renderer
                    // 就要直接改用 asset 的默认 renderer 
                    int defaultIndex = UniversalRenderPipeline.asset.m_DefaultRendererIndex;
                    Debug.LogWarning(
                        $"Renderer at <b>index {m_RendererIndex.ToString()}</b> is missing for camera <b>{camera.name}</b>, falling back to Default Renderer. <b>{UniversalRenderPipeline.asset.m_RendererDataList[defaultIndex].name}</b>",
                        UniversalRenderPipeline.asset);
                    return UniversalRenderPipeline.asset.GetRenderer(defaultIndex);
                }
                return UniversalRenderPipeline.asset.GetRenderer(m_RendererIndex);
            }
        }

        

        /// <summary>
        /// Use this to set this Camera's current <see cref="ScriptableRenderer"/> to one listed on the Render Pipeline Asset. Takes an index that maps to the list on the Render Pipeline Asset.
        /// </summary>
        /// <param name="index">The index that maps to the RendererData list on the currently assigned Render Pipeline Asset</param>
        public void SetRenderer(int index)
        {
            m_RendererIndex = index;
        }


        // defines which Volumes affect this Camera. 对应 inspector 中 Volumes Mask 一项;
        public LayerMask volumeLayerMask
        {
            get => m_VolumeLayerMask;
            set => m_VolumeLayerMask = value;
        }

        /*
            Assign a Transform that the Volume system uses to handle the position of this Camera. 
            
            For example, if your application uses 第三人称角色, set this property to the character's Transform. 
            The Camera then uses the post-processing and Scene settings for Volumes that the character enters. 
            If you do not assign a Transform, the Camera uses its own Transform instead.

            对应 inspector 中 Volumes Trigger 一栏; 
            一般为空, 此时本函数返回 null, 表示沿用 camera transform
        */
        public Transform volumeTrigger
        {
            get => m_VolumeTrigger;
            set => m_VolumeTrigger = value;
        }


        /// Returns true if this camera should render post-processing.
        // 猜测: 此值由 camera inspector 设置;
        // base / overlay camera 皆可独立配置;
        public bool renderPostProcessing
        {
            get => m_RenderPostProcessing;
            set => m_RenderPostProcessing = value;
        }

       
        // Returns the current anti-aliasing mode used by this camera.
        // enum: None, "FXAA", "SMAA";
        public AntialiasingMode antialiasing
        {
            get => m_Antialiasing;
            set => m_Antialiasing = value;
        }

        /*
            Returns the current anti-aliasing quality used by this camera.
            enum: Low, Medium, High;
            沿用 inspector 中配置
        */
        public AntialiasingQuality antialiasingQuality
        {
            get => m_AntialiasingQuality;
            set => m_AntialiasingQuality = value;
        }

        /*
            暂时将 shaders 中的所有 NaN/Inf 值都替换成一个 黑色pix, 以避免 "中断某个效果";
            开启此功能会影响性能, 只推荐在 修复 NaN bug 时使用, GLES2 平台不支持本功能;
        */
        public bool stopNaN
        {
            get => m_StopNaN;
            set => m_StopNaN = value;
        }

        /*
            Enable the checkbox to apply 8-bit dithering to the final render. 
            This can help reduce banding on wide gradients and low light areas.
        */
        public bool dithering
        {
            get => m_Dithering;
            set => m_Dithering = value;
        }

        /// <summary>
        /// Returns true if this camera allows render in XR.
        /// </summary>
        public bool allowXRRendering
        {
            get => m_AllowXRRendering;
            set => m_AllowXRRendering = value;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (version <= 1)
            {
                m_RequiresDepthTextureOption = (m_RequiresDepthTexture) ? CameraOverrideOption.On : CameraOverrideOption.Off;
                m_RequiresOpaqueTextureOption = (m_RequiresColorTexture) ? CameraOverrideOption.On : CameraOverrideOption.Off;
            }
        }

        public void OnDrawGizmos()
        {
            string path = "Packages/com.unity.render-pipelines.universal/Editor/Gizmos/";
            string gizmoName = "";
            Color tint = Color.white;

            if (m_CameraType == CameraRenderType.Base)
            {
                gizmoName = $"{path}Camera_Base.png";
            }
            else if (m_CameraType == CameraRenderType.Overlay)
            {
                gizmoName = $"{path}Camera_Overlay.png";
            }

#if UNITY_2019_2_OR_NEWER
#if UNITY_EDITOR
            if (Selection.activeObject == gameObject)
            {
                // Get the preferences selection color
                tint = SceneView.selectedOutlineColor;
            }
#endif
            if (!string.IsNullOrEmpty(gizmoName))
            {
                Gizmos.DrawIcon(transform.position, gizmoName, true, tint);
            }

            if (renderPostProcessing)
            {
                Gizmos.DrawIcon(transform.position, $"{path}Camera_PostProcessing.png", true, tint);
            }
#else
            if (renderPostProcessing)
            {
                Gizmos.DrawIcon(transform.position, $"{path}Camera_PostProcessing.png");
            }
            Gizmos.DrawIcon(transform.position, gizmoName);
#endif
        }
    }// UniversalAdditionalCameraData end
}
