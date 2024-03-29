#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
#endif
using System;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.Rendering.Universal
{

    /*


    */
    [Serializable, ReloadGroup, ExcludeFromPreset]
    [MovedFrom("UnityEngine.Rendering.LWRP")]
    public class ForwardRendererData //ForwardRendererData__RR
        : ScriptableRendererData
    {
#if UNITY_EDITOR
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812")]
        internal class CreateForwardRendererAsset//CreateForwardRendererAsset__RR
            : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                var instance = CreateInstance<ForwardRendererData>();
                instance.postProcessData = PostProcessData.GetDefaultPostProcessData();
                AssetDatabase.CreateAsset(instance, pathName);
                ResourceReloader.ReloadAllNullIn(instance, UniversalRenderPipelineAsset.packagePath);
                Selection.activeObject = instance;
            }
        }

        [MenuItem("Assets/Create/Rendering/Universal Render Pipeline/Forward Renderer", priority = CoreUtils.assetCreateMenuPriority2)]
        static void CreateForwardRendererData()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<CreateForwardRendererAsset>(), "CustomForwardRendererData.asset", null, null);
        }

#endif

        [Serializable, ReloadGroup]
        public sealed class ShaderResources//ShaderResources__
        {
            [Reload("Shaders/Utils/Blit.shader")]
            public Shader blitPS;

            [Reload("Shaders/Utils/CopyDepth.shader")]
            public Shader copyDepthPS;

            /*    tpr
            [Obsolete("Obsolete, this feature will be supported by new 'ScreenSpaceShadows' renderer feature")]
            public Shader screenSpaceShadowPS;
            */

            [Reload("Shaders/Utils/Sampling.shader")]
            public Shader samplingPS;

            [Reload("Shaders/Utils/StencilDeferred.shader")]
            public Shader stencilDeferredPS;

            [Reload("Shaders/Utils/FallbackError.shader")]
            public Shader fallbackErrorPS;

            [Reload("Shaders/Utils/MaterialError.shader")]
            public Shader materialErrorPS;
        }


        public PostProcessData postProcessData = null;

/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
        [Reload("Runtime/Data/XRSystemData.asset")]
        public XRSystemData xrSystemData = null;
#endif
*/

        public ShaderResources shaders = null; // 见上方

        [SerializeField] LayerMask m_OpaqueLayerMask = -1;
        [SerializeField] LayerMask m_TransparentLayerMask = -1;

        // This default state is compatible with deferred renderer.
        [SerializeField] StencilStateData m_DefaultStencilState = new StencilStateData() { 
            passOperation = StencilOp.Replace 
        }; 

        [SerializeField] bool m_ShadowTransparentReceive = true;
        [SerializeField] RenderingMode m_RenderingMode = RenderingMode.Forward;
        [SerializeField] bool m_AccurateGbufferNormals = false;
        //[SerializeField] bool m_TiledDeferredShading = false;



        protected override ScriptableRenderer Create()//  读完__
        {
            if (!Application.isPlaying)
            {
                ReloadAllNullProperties();
            }
            return new ForwardRenderer(this);
        }

        /*
            Use this to configure how to filter opaque objects.
            ---
            指定了哪些 Layer 属于 不透明物部分
            可在 Forward Renderer inspector 中设置
        */
        public LayerMask opaqueLayerMask
        {
            get => m_OpaqueLayerMask;
            set
            {
                SetDirty();
                m_OpaqueLayerMask = value;
            }
        }

        /*
            Use this to configure how to filter transparent objects.
            ---
            指定了哪些 Layer 属于 半透明物部分
            可在 Forward Renderer inspector 中设置
        */
        public LayerMask transparentLayerMask
        {
            get => m_TransparentLayerMask;
            set
            {
                SetDirty();
                m_TransparentLayerMask = value;
            }
        }

        /*
            render state 中的 stencil 部分;
            ---
            可在 Forward Renderer inspector 中设置
        */
        public StencilStateData defaultStencilState
        {
            get => m_DefaultStencilState;
            set
            {
                SetDirty();
                m_DefaultStencilState = value;
            }
        }

        /// <summary>
        /// True if transparent objects receive shadows.
        /// </summary>
        public bool shadowTransparentReceive
        {
            get => m_ShadowTransparentReceive;
            set
            {
                SetDirty();
                m_ShadowTransparentReceive = value;
            }
        }

        /*
            Rendering mode.
            enum: Forward, Deferred;
        */
        public RenderingMode renderingMode
        {
            get => m_RenderingMode; // 默认值 Forward;
            set
            {
                SetDirty();
                m_RenderingMode = value;
            }
        }

        /// <summary>
        /// Use Octaedron Octahedron normal vector encoding for gbuffer normals.
        /// The overhead is negligible from desktop GPUs, while it should be avoided for mobile GPUs.
        /// </summary>
        public bool accurateGbufferNormals
        {
            get => m_AccurateGbufferNormals;
            set
            {
                SetDirty();
                m_AccurateGbufferNormals = value;
            }
        }

        /*
        public bool tiledDeferredShading
        {
            get => m_TiledDeferredShading;
            set
            {
                SetDirty();
                m_TiledDeferredShading = value;
            }
        }
        */

        protected override void OnEnable()
        {
            base.OnEnable();

            // Upon asset creation, OnEnable is called and `shaders` reference is not yet initialized
            // We need to call the OnEnable for data migration when updating from old versions of UniversalRP that
            // serialized resources in a different format. Early returning here when OnEnable is called
            // upon asset creation is fine because we guarantee new assets get created with all resources initialized.
            if (shaders == null)
                return;

            ReloadAllNullProperties();
        }

        private void ReloadAllNullProperties()
        {
#if UNITY_EDITOR
            ResourceReloader.TryReloadAllNullIn(this, UniversalRenderPipelineAsset.packagePath);
    /*   tpr
    #if ENABLE_VR && ENABLE_XR_MODULE
            ResourceReloader.TryReloadAllNullIn(xrSystemData, UniversalRenderPipelineAsset.packagePath);
    #endif
    */

#endif
        }

    }
}
