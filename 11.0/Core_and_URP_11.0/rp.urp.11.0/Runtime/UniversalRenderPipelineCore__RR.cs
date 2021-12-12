using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.Scripting.APIUpdating;

using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Experimental.Rendering;
using Lightmapping = UnityEngine.Experimental.GlobalIllumination.Lightmapping;

namespace UnityEngine.Rendering.Universal
{

    // enum: None(Baked Indirect), ShadowMask, Subtractive;
    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public enum MixedLightingSetup//MixedLightingSetup__
    {
        None, // 有时表示 "未初始化", 有时表示: (Baked Indirect)
        ShadowMask,
        Subtractive,
    };



    /*
        "InitializeRenderingData()" 初始化一个完整的 RenderingData 实例;
    */
    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public struct RenderingData//RenderingData__
    {
        public CullingResults cullResults;
        public CameraData cameraData;
        public LightData lightData;
        public ShadowData shadowData;
        public PostProcessingData postProcessingData;
        public bool supportsDynamicBatching; // 动态批处理优化技术, 不建议使用
        public PerObjectData perObjectData;

        //  True if post-processing effect is enabled while rendering the camera stack.
        public bool postProcessingEnabled;
    }



    // RenderingData 的一个成员
    [MovedFrom("UnityEngine.Rendering.LWRP")] public struct LightData//LightData__
    {
        // 在 "GetMainLightIndex()" 中计算而得; 表示 main light 在 visibleLights 中的 idx; 
        // 若没找到合适的 main light, 此值为 -1;
        public int mainLightIndex; 

        /*
            visibleLights 中, add light 的数量; 
            根据是否有 main light, 本值为: visibleLights.Length 或 visibleLights.Length-1;
            同时, 还不能大于 "maxVisibleAdditionalLights" (16, 32, or 256)
        */
        public int additionalLightsCount;

        /*
            "逐物体 add light" 个数的上限值; 
            分别受到:
                -- "asset inspector 用户设置": [0,8] 
                -- 平台(是否为GLES2) 的影响: 4 or 8 
            取上述二值的 min, 最终值区间为: [0,8]
        */
        public int maxPerObjectAdditionalLightsCount;

        public NativeArray<VisibleLight> visibleLights;

        /*
            -- true:  add light 是 逐顶点的;
            -- false: add light 是 逐像素的;
            受 asset inspector 用户配置 的控制, 是全局统一值;
        */
        public bool shadeAdditionalLightsPerVertex;

        /*
            是否支持 Mixed 光模式;  
            受 asset inspector 用户配置 的控制, 是全局统一值;
        */
        public bool supportsMixedLighting;
    }



    // ========================================< CameraData >====================================:
    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public struct CameraData//CameraData__RR
    {
        // Internal camera data as we are not yet sure how to expose View in stereo context.
        // We might change this API soon.
        // 12.1 中也没移除... 
        Matrix4x4 m_ViewMatrix;
        Matrix4x4 m_ProjectionMatrix;

        internal void SetViewAndProjectionMatrix(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            m_ViewMatrix = viewMatrix;
            m_ProjectionMatrix = projectionMatrix;
        }


        /*
            Returns the camera view-matrix.
        */
        public Matrix4x4 GetViewMatrix(int viewIndex = 0)
        {
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            if (xr.enabled)
                return xr.GetViewMatrix(viewIndex);
#endif
*/
            return m_ViewMatrix;
        }


        /*
            Returns the camera projection-matrix.
        */
        public Matrix4x4 GetProjectionMatrix(int viewIndex = 0)
        {
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            if (xr.enabled)
                return xr.GetProjMatrix(viewIndex);
#endif
*/
            return m_ProjectionMatrix;
        }

       
        /*
            Returns the camera "GPU projection-matrix";
            tpr:
                也就是最终在 shader 程序中出现的那个 投影矩阵, 比如 built-in管线中的 "UNITY_MATRIX_P" 矩阵;

            本函数包含处理 "y-flip" 和 "reverse z" 的平台特定更改;

            和 "GL.GetGPUProjectionMatrix()" 函数相似, 但是本函数会查询 urp内部状态, 
            来得知当前管线 是否正在把渲染结果写入一个 render texture;
        */ 
        public Matrix4x4 GetGPUProjectionMatrix(int viewIndex = 0)
        {
            return GL.GetGPUProjectionMatrix(
                GetProjectionMatrix(viewIndex), 
                IsCameraProjectionMatrixFlipped() // 在下方查看此函数
            );
        }


        public Camera camera; // camera 本体, 

        // enum: Base, Overlay
        public CameraRenderType renderType; // Base, Overlay

        public RenderTexture targetTexture; // = Camera's; [- stack 中所有 camera 的值都相同 -]


        // 此 struct 包含用来创建 RenderTexture 所需的一切信息。
        // 关于这个 变量:
        // -- 要么根据 context 当场新建一个
        // -- 要么沿用 camera.targetTexture 中的数据, 并做适当调整
        public RenderTextureDescriptor cameraTargetDescriptor;
        

        // ---------------------------------------------------:
        // 下面这组 viewport 相关值, 都直接源自 base camera 的数据, 
        // overlay camera 的 CameraData 中的数据也等于 base camera 的;
        // 但这不意味着, overlay camera 的 viewport 一定要和 base camera 的一样
        // 那些独立的不一样的值, 被记录在 overlay camera 的 "Camera" class 实例内 
        internal Rect pixelRect; // [- stack 中所有 camera 的值都相同 -]
        internal int pixelWidth; // [- stack 中所有 camera 的值都相同 -]
        internal int pixelHeight; // [- stack 中所有 camera 的值都相同 -]
        internal float aspectRatio; // 横纵比; [- stack 中所有 camera 的值都相同 -]


        /*
            渲染区域缩放因子;
            如果 asset.renderScale 十分接近 1.0, 那就设为 1.0;  否则, 沿用 asset.renderScale 的值;
            [- stack 中所有 camera 的值都相同 -]
        */
        public float renderScale;

        public bool clearDepth; // 是否清理 depth buffer

        
        //    enum: Game, SceneView, Preview, VR, Reflection
        public CameraType cameraType; // = Camera's; [- stack 中所有 camera 的值都相同 -]

        /*
            从外部被设置;
            猜测: 只有当 viewport 为 全屏时, 才算是 default 的, 此值才为 true; 
            [- stack 中所有 camera 的值都相同 -]
        */
        public bool isDefaultViewport;

        public bool isHdrEnabled;//[- stack 中所有 camera 的值都相同 -]

        // 是否将 camera 的 depth buffer 复制一份到 "_CameraDepth"
        // 从实现中看到: overlay camera 不支持
        public bool requiresDepthTexture; 

        //  是否将 camera 的 不透明物的 color buffer 复制一份到 "_CameraOpaque"
        //  从实现中看到: overlay camera 不支持
        public bool requiresOpaqueTexture;

/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
        public bool xrRendering;
#endif
*/


        /*
            如果你选择了 linear 工作流, 同时 backbuffer 并不支持 "自动将 线性颜色转换为 sRGB值" 这个功能,
            而且你正要把 一组 linear 数据, 从某个 render texture 上 blit 到 backbuffer 上去;
            那么本变量 就要设置为 true;

            此时,  urp 会启用 keyword: _LINEAR_TO_SRGB_CONVERSION
                以便在 shader 中手动实现 "linear->sRGB" 转换;
        */
        internal bool requireSrgbConversion
        {
            get{
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
                if (xr.enabled)
                    return !xr.renderTargetDesc.sRGB && (QualitySettings.activeColorSpace == ColorSpace.Linear);
#endif
*/
                return Display.main.requiresSrgbBlitToBackbuffer;
            }
        }


        
        // True if the camera rendering is for the scene window in the editor
        // 仅在 editor 中存在
        public bool isSceneViewCamera => cameraType==CameraType.SceneView;

        
        // True if the camera rendering is for the preview window in the editor
        public bool isPreviewCamera => cameraType==CameraType.Preview;

        
        
        /*
            True if the camera device projection-matrix is flipped;

            当在一个 No-OpenGL 平台, 管线正在渲染进 render texture 时, 会产生 flipped, 进而本函数返回 true;

            如果你正在执行一个 "自定义 Blit pass": 复制 camera textures; 
            (比如 _CameraColorTexture, _CameraDepthAttachment )
            
            you need to check this flag to know if you should flip the matrix 
            when rendering with for cmd.Draw* (如: "cmd.DrawMesh()") and reading from camera textures.

            tpr:
                简而言之, 如果 平台是 d3d 类的, 同时当前渲染又正在写入 render texture,
                那么本函数就会返回 true;
        */
        public bool IsCameraProjectionMatrixFlipped()
        {
            // 用户只能在 urp渲染范围内 访问 CameraData, "current renderer" 应该永不为 null;
            var renderer = ScriptableRenderer.current;
            Debug.Assert(renderer != null, "IsCameraProjectionMatrixFlipped is being called outside camera rendering scope.");

            if (renderer != null)
            {
                bool renderingToBackBufferTarget = renderer.cameraColorTarget==BuiltinRenderTextureType.CameraTarget;
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
                if (xr.enabled)
                    renderingToBackBufferTarget |= renderer.cameraColorTarget == xr.renderTarget && !xr.renderTargetIsRenderTexture;
#endif
*/
                bool renderingToTexture = !renderingToBackBufferTarget || targetTexture!=null;
                return SystemInfo.graphicsUVStartsAtTop && renderingToTexture;
            }
            return true;
        }


        /*
            处理 不透明物体的 排序技术 flags ( flags 可组合)

            通常沿用 "SortingCriteria.CommonOpaque", 
            
            有的 支持 "hidden surface removal". (隐藏面去除) 的gpu 不需要对 不透明物体执行 "front-to-back" 排序工作,
            还有的 camera 出于性能考虑手动关闭了 排序工作
            对于这种情况, 可在 "SortingCriteria.CommonOpaque" 的基础上, 减去 "QuantizedFrontToBack" 排序;
            [- stack 中所有 camera 的值都相同 -]
        */
        public SortingCriteria defaultOpaqueSortFlags;

        /*
            只要不加载 xr,vr package, xr.enable 就为 false
            [- stack 中所有 camera 的值都相同 -]
        */
        internal XRPass xr;

        /*
        [Obsolete("Please use xr.enabled instead.")]
        public bool isStereoEnabled;
        */


        
        public float maxShadowDistance; // 见具体设置,

        // 是否支持 后处理,
        public bool postProcessEnabled;

        // 元素们 原本存储在 "CameraCaptureBridge" 中
        // [- stack 中所有 camera 的值都相同 -]  
        public IEnumerator<Action<RenderTargetIdentifier, CommandBuffer>> captureActions;

        // the Layer Mask that defines which Volumes affect this Camera. 
        // [- stack 中所有 camera 的值都相同 -]
        public LayerMask volumeLayerMask;

        /*
            Assign a Transform that the Volume system uses to handle the position of this Camera. 
            
            For example, if your application uses 第三人称角色, set this property to the character's Transform. 
            The Camera then uses the post-processing and Scene settings for Volumes that the character enters. 
            If you do not assign a Transform, the Camera uses its own Transform instead.

            [- stack 中所有 camera 的值都相同 -]
        */
        public Transform volumeTrigger;

        /*
            暂时将 shaders 中的所有 NaN/Inf 值都替换成一个 黑色pix, 以避免 "中断某个效果";
            开启此功能会影响性能, 只推荐在 修复 NaN bug 时使用, GLES2 平台不支持本功能;
            [- stack 中所有 camera 的值都相同 -]
        */
        public bool isStopNaNEnabled;

        /*
            Enable the checkbox to apply 8-bit dithering to the final render. 
            This can help reduce banding on wide gradients and low light areas.
            [- stack 中所有 camera 的值都相同 -]
        */
        public bool isDitheringEnabled;

        public AntialiasingMode antialiasing;// [- stack 中所有 camera 的值都相同 -]

        public AntialiasingQuality antialiasingQuality;// [- stack 中所有 camera 的值都相同 -]

        
        // Returns the current renderer used by this camera.  比如 "Forward Renderer"
        public ScriptableRenderer renderer;


        /*
            True if this camera is resolving rendering to the final camera render target.
            When rendering a stack of cameras only the last camera in the stack will resolve to camera target.

            如果这是 stack 中最后一个 camera, 则为 true;
        */
        public bool resolveFinalTarget;


    }// CameraData end






    // RenderingData 的一个成员
    [MovedFrom("UnityEngine.Rendering.LWRP")] public struct ShadowData//ShadowData__
    {
        public bool supportsMainLightShadows;

        /*
        [Obsolete("Obsolete, this feature was replaced by new 'ScreenSpaceShadows' renderer feature")]
        public bool requiresScreenSpaceShadowResolve;
        */

        public int mainLightShadowmapWidth; // 其实就是 shadow resolution
        public int mainLightShadowmapHeight; // 其实就是 shadow resolution

        public int mainLightShadowCascadesCount;// cascade 有几层, 区间[1,4]; (比如: 4个重叠的球体) 

        
        //    就是 cascade ratio, 分别定义了 第1,2,3层 cascade 占据的区域的 比例;
        public Vector3 mainLightShadowCascadesSplit;

        public bool supportsAdditionalLightShadows;
        public int additionalLightsShadowmapWidth; // 其实就是 shadow resolution
        public int additionalLightsShadowmapHeight; // 其实就是 shadow resolution

        // 同时满足:
        // -1- asset inspector 用户勾选了: supportsSoftShadows
        // -2- main/add light 任意一种支持渲染 shadow 时
        //  本变量就为 true;
        public bool supportsSoftShadows;

        
        public int shadowmapDepthBufferBits; // 比如 16: 一个texl 存储消耗 16-bits

        /*
            visibleLights 中, 每个光的 ShadowBias 数据
                x: shadowBias
                y: shadowNormalBias
                z: 0.0
                w: 0.0
            要么使用 light 的设置, 要么使用 asset 的设置;
        */
        public List<Vector4> bias;


        /*
            (本数据仅被 add light 使用)

            visibleLights 中, 每个 add light 的 shadow tile(slice) 的最大分辨率(pix)
            
            分三个档次: Low/Medium/High; 每个档次对应的值 记录在 asset inspector 中;
            值通常为: 512 / 1024 / 4096;

            至于具体选哪一档, 全看 point光 / spot光 light inspector 中的选择;
        */
        public List<int> resolution;
    }



    // Precomputed tile data.
    public struct PreTile
    {
        // Tile left, right, bottom and top plane equations in view space.
        // Normals are pointing out.
        public Unity.Mathematics.float4 planeLeft;
        public Unity.Mathematics.float4 planeRight;
        public Unity.Mathematics.float4 planeBottom;
        public Unity.Mathematics.float4 planeTop;
    }

    // Actual tile data passed to the deferred shaders.
    public struct TileData
    {
        public uint tileID;         // 2x 16 bits
        public uint listBitMask;    // 32 bits
        public uint relLightOffset; // 16 bits is enough
        public uint unused;
    }

    // Actual point/spot light data passed to the deferred shaders.
    public struct PunctualLightData
    {
        public Vector3 wsPos;
        public float radius; // TODO remove? included in attenuation
        public Vector4 color;
        public Vector4 attenuation; // .xy are used by DistanceAttenuation - .zw are used by AngleAttenuation (for SpotLights)
        public Vector3 spotDirection;   // for spotLights
        public int lightIndex;
        public Vector4 occlusionProbeInfo;
    }

    internal static class ShaderPropertyId//ShaderPropertyId__RR
    {
        public static readonly int glossyEnvironmentColor = Shader.PropertyToID("_GlossyEnvironmentColor");
        public static readonly int subtractiveShadowColor = Shader.PropertyToID("_SubtractiveShadowColor");

        // 环境光: 顶光, 赤道光, 底光
        public static readonly int ambientSkyColor      = Shader.PropertyToID("unity_AmbientSky");
        public static readonly int ambientEquatorColor  = Shader.PropertyToID("unity_AmbientEquator");//赤道
        public static readonly int ambientGroundColor   = Shader.PropertyToID("unity_AmbientGround");

        public static readonly int time = Shader.PropertyToID("_Time");
        public static readonly int sinTime = Shader.PropertyToID("_SinTime");
        public static readonly int cosTime = Shader.PropertyToID("_CosTime");
        public static readonly int deltaTime = Shader.PropertyToID("unity_DeltaTime");
        public static readonly int timeParameters = Shader.PropertyToID("_TimeParameters");

        public static readonly int scaledScreenParams = Shader.PropertyToID("_ScaledScreenParams");
        public static readonly int worldSpaceCameraPos = Shader.PropertyToID("_WorldSpaceCameraPos");
        public static readonly int screenParams = Shader.PropertyToID("_ScreenParams");

        // 按照 "ScriptableRenderer" 中的说法, 这个变量现在依靠 "context.SetupCameraProperties" 来配置,
        // urp 暂不自行配置
        public static readonly int projectionParams = Shader.PropertyToID("_ProjectionParams");
        public static readonly int zBufferParams = Shader.PropertyToID("_ZBufferParams");
        public static readonly int orthoParams = Shader.PropertyToID("unity_OrthoParams");

        public static readonly int viewMatrix = Shader.PropertyToID("unity_MatrixV");
        public static readonly int projectionMatrix = Shader.PropertyToID("glstate_matrix_projection");
        public static readonly int viewAndProjectionMatrix = Shader.PropertyToID("unity_MatrixVP");

        public static readonly int inverseViewMatrix = Shader.PropertyToID("unity_MatrixInvV");
        public static readonly int inverseProjectionMatrix = Shader.PropertyToID("unity_MatrixInvP");
        public static readonly int inverseViewAndProjectionMatrix = Shader.PropertyToID("unity_MatrixInvVP");

        public static readonly int cameraProjectionMatrix = Shader.PropertyToID("unity_CameraProjection");
        public static readonly int inverseCameraProjectionMatrix = Shader.PropertyToID("unity_CameraInvProjection");
        public static readonly int worldToCameraMatrix = Shader.PropertyToID("unity_WorldToCamera");
        public static readonly int cameraToWorldMatrix = Shader.PropertyToID("unity_CameraToWorld");

        public static readonly int sourceTex = Shader.PropertyToID("_SourceTex");
        public static readonly int scaleBias = Shader.PropertyToID("_ScaleBias");
        public static readonly int scaleBiasRt = Shader.PropertyToID("_ScaleBiasRt");

        // Required for 2D Unlit Shadergraph master node as it doesn't currently support hidden properties.
        public static readonly int rendererColor = Shader.PropertyToID("_RendererColor");
    }



    // RenderingData 的一个成员
    public struct PostProcessingData//PostProcessingData__
    {
        public ColorGradingMode gradingMode; // 颜色渐变模式; enum: LowDynamicRange, HighDynamicRange
        public int lutSize; // 通常为 32,
 
        /*
            True if fast approximation functions are used when converting between the sRGB and Linear color spaces, false otherwise.
            快速但不够精确的 sRGB<->Linear 转换函数;
            默认为 false;
        */
        public bool useFastSRGBLinearConversion;
    }



    public static class ShaderKeywordStrings//ShaderKeywordStrings__RR
    {
        public static readonly string MainLightShadows = "_MAIN_LIGHT_SHADOWS";
        public static readonly string MainLightShadowCascades = "_MAIN_LIGHT_SHADOWS_CASCADE";
        public static readonly string MainLightShadowScreen = "_MAIN_LIGHT_SHADOWS_SCREEN";

        /*
            This is used during shadow map generation to differentiate between directional and punctual light shadows, 
            as they use different formulas to apply Normal Bias
            ---
            在 shadowmap caster shader 中, 使用此 keyword 来区分 平行光 和 精确光,
            因为它们使用 Normal Bias 的方式不一样
        */
        public static readonly string CastingPunctualLightShadow = "_CASTING_PUNCTUAL_LIGHT_SHADOW"; 
        public static readonly string AdditionalLightsVertex = "_ADDITIONAL_LIGHTS_VERTEX";
        public static readonly string AdditionalLightsPixel = "_ADDITIONAL_LIGHTS";
        public static readonly string AdditionalLightShadows = "_ADDITIONAL_LIGHT_SHADOWS";
        public static readonly string SoftShadows = "_SHADOWS_SOFT";
        public static readonly string MixedLightingSubtractive = "_MIXED_LIGHTING_SUBTRACTIVE"; // Backward compatibility
        public static readonly string LightmapShadowMixing = "LIGHTMAP_SHADOW_MIXING";
        public static readonly string ShadowsShadowMask = "SHADOWS_SHADOWMASK";

        // 1,2,4,8 4种中只能启用一种
        public static readonly string DepthNoMsaa = "_DEPTH_NO_MSAA";
        public static readonly string DepthMsaa2 = "_DEPTH_MSAA_2";
        public static readonly string DepthMsaa4 = "_DEPTH_MSAA_4";
        public static readonly string DepthMsaa8 = "_DEPTH_MSAA_8";


        public static readonly string LinearToSRGBConversion = "_LINEAR_TO_SRGB_CONVERSION";
        internal static readonly string UseFastSRGBLinearConversion = "_USE_FAST_SRGB_LINEAR_CONVERSION";

        public static readonly string SmaaLow = "_SMAA_PRESET_LOW";
        public static readonly string SmaaMedium = "_SMAA_PRESET_MEDIUM";
        public static readonly string SmaaHigh = "_SMAA_PRESET_HIGH";
        public static readonly string PaniniGeneric = "_GENERIC";
        public static readonly string PaniniUnitDistance = "_UNIT_DISTANCE";
        public static readonly string BloomLQ = "_BLOOM_LQ";
        public static readonly string BloomHQ = "_BLOOM_HQ";
        public static readonly string BloomLQDirt = "_BLOOM_LQ_DIRT";
        public static readonly string BloomHQDirt = "_BLOOM_HQ_DIRT";
        public static readonly string UseRGBM = "_USE_RGBM";
        public static readonly string Distortion = "_DISTORTION";
        public static readonly string ChromaticAberration = "_CHROMATIC_ABERRATION";
        public static readonly string HDRGrading = "_HDR_GRADING";
        public static readonly string TonemapACES = "_TONEMAP_ACES";
        public static readonly string TonemapNeutral = "_TONEMAP_NEUTRAL";
        public static readonly string FilmGrain = "_FILM_GRAIN";
        public static readonly string Fxaa = "_FXAA";
        public static readonly string Dithering = "_DITHERING";
        public static readonly string ScreenSpaceOcclusion = "_SCREEN_SPACE_OCCLUSION";

        public static readonly string HighQualitySampling = "_HIGH_QUALITY_SAMPLING";

        public static readonly string DOWNSAMPLING_SIZE_2 = "DOWNSAMPLING_SIZE_2";
        public static readonly string DOWNSAMPLING_SIZE_4 = "DOWNSAMPLING_SIZE_4";
        public static readonly string DOWNSAMPLING_SIZE_8 = "DOWNSAMPLING_SIZE_8";
        public static readonly string DOWNSAMPLING_SIZE_16 = "DOWNSAMPLING_SIZE_16";
        public static readonly string _SPOT = "_SPOT";
        public static readonly string _DIRECTIONAL = "_DIRECTIONAL";
        public static readonly string _POINT = "_POINT";
        public static readonly string _DEFERRED_ADDITIONAL_LIGHT_SHADOWS = "_DEFERRED_ADDITIONAL_LIGHT_SHADOWS";
        public static readonly string _GBUFFER_NORMALS_OCT = "_GBUFFER_NORMALS_OCT";
        public static readonly string _DEFERRED_MIXED_LIGHTING = "_DEFERRED_MIXED_LIGHTING";
        public static readonly string LIGHTMAP_ON = "LIGHTMAP_ON";
        public static readonly string _ALPHATEST_ON = "_ALPHATEST_ON";
        public static readonly string DIRLIGHTMAP_COMBINED = "DIRLIGHTMAP_COMBINED";
        public static readonly string _DETAIL_MULX2 = "_DETAIL_MULX2";
        public static readonly string _DETAIL_SCALED = "_DETAIL_SCALED";
        public static readonly string _CLEARCOAT = "_CLEARCOAT";
        public static readonly string _CLEARCOATMAP = "_CLEARCOATMAP";

        // XR;  目前看来确实只有 加载了 xr/vr package 的程序, 才能启用此 keyword
        public static readonly string UseDrawProcedural = "_USE_DRAW_PROCEDURAL";
    }



    //==========================================================================================================:
    public sealed partial class UniversalRenderPipeline//UniversalRenderPipeline__RR_2
    {
        /*
            Holds light direction for "directional lights" or position for "punctual lights".
            When w is set to 1.0, it means it's a punctual light.
            ---
            若 w = 0:  xyz分量 存储的是 "punctual lights" 的 pos;
            若 w = 1:  xyz分量 存储的是 平行光 的 方向;
        */
        static Vector4 k_DefaultLightPosition = new Vector4(0.0f, 0.0f, 1.0f, 0.0f);

        static Vector4 k_DefaultLightColor = Color.black;

        /*
            Default light attenuation is setup in a particular way 
            that it causes "directional lights" to return 1.0 for both distance and angle attenuation
            ---
            若为平行光, distance 和 angle attenuation 都为 1;
        */
        static Vector4 k_DefaultLightAttenuation = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
        static Vector4 k_DefaultLightSpotDirection = new Vector4(0.0f, 0.0f, 1.0f, 0.0f);
        static Vector4 k_DefaultLightsProbeChannel = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

        /*
            visibleLights 中, 每个光的 ShadowBias 数据
                x: shadowBias
                y: shadowNormalBias
                z: 0.0
                w: 0.0
            要么使用 light 的设置, 要么使用 asset 的设置;
        */
        static List<Vector4> m_ShadowBiasData = new List<Vector4>();

        /*
            (本数据仅被 add light 使用)

            visibleLights 中, 每个 add light 的 shadow tile(slice) 的最大分辨率(pix)
            
            分三个档次: Low/Medium/High; 每个档次对应的值 记录在 asset inspector 中;
            值通常为: 512 / 1024 / 4096;

            至于具体选哪一档, 全看 point光 / spot光 light inspector 中的选择;
        */
        static List<int> m_ShadowResolutionData = new List<int>(); 

        /*
            Checks if a camera is a game camera.
        */
        /// <param name="camera">Camera to check state from.</param>
        /// <returns>true if given camera is a game camera, false otherwise.</returns>
        public static bool IsGameCamera(Camera camera)
        {
            if (camera == null)
                throw new ArgumentNullException("camera");

            return camera.cameraType == CameraType.Game || camera.cameraType == CameraType.VR;
        }


        /*
        /// <summary>
        /// Checks if a camera is rendering in stereo mode.
        /// </summary>
        /// <param name="camera">Camera to check state from.</param>
        /// <returns>Returns true if the given camera is rendering in stereo mode, false otherwise.</returns>
        [Obsolete("Please use CameraData.xr.enabled instead.")]
        public static bool IsStereoEnabled(Camera camera)
        {
            if (camera == null)
                throw new ArgumentNullException("camera");

            return IsGameCamera(camera) && (camera.stereoTargetEye == StereoTargetEyeMask.Both);
        }
        */



        /*
            ===========================================================================
                                        asset  真正出处!
            ---------------------------------------------------------------------------
            Returns the current render pipeline asset for the current quality setting.
            If no render pipeline asset is assigned in QualitySettings, then returns the one assigned in GraphicsSettings.
            --
            返回当前的 QualitySettings 中的 "current rp asset" 实例;
            如果在 "QualitySettings" 没有绑定任何 rp asset, 那就返回 绑定在 "GraphicsSettings" 中的 rp asset;
        */
        public static UniversalRenderPipelineAsset asset
        {
            // 和上文注释不同,  好像是直接访问 GraphicsSettings 中的值
            get => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        }



        /*
        /// <summary>
        /// Checks if a camera is rendering in MultiPass stereo mode.
        /// </summary>
        /// <param name="camera">Camera to check state from.</param>
        /// <returns>Returns true if the given camera is rendering in multi pass stereo mode, false otherwise.</returns>
        [Obsolete("Please use CameraData.xr.singlePassEnabled instead.")]
        static bool IsMultiPassStereoEnabled(Camera camera)
        {
            if (camera == null)
                throw new ArgumentNullException("camera");

            return false;
        }
        */

/*   tpr
#if ENABLE_VR && ENABLE_VR_MODULE
        static List<XR.XRDisplaySubsystem> displaySubsystemList = new List<XR.XRDisplaySubsystem>();
        static XR.XRDisplaySubsystem GetFirstXRDisplaySubsystem()
        {
            XR.XRDisplaySubsystem display = null;
            SubsystemManager.GetInstances(displaySubsystemList);

            if (displaySubsystemList.Count > 0)
                display = displaySubsystemList[0];

            return display;
        }

        // NB: This method is required for a hotfix in Hololens to prevent creating a render texture when using a renderer
        // with custom render pass.
        // TODO: Remove this method and usages when we have proper dependency tracking in the pipeline to know
        // when a render pass requires camera color as input.
        internal static bool IsRunningHololens(CameraData cameraData)
        {
    #if PLATFORM_WINRT
            if (cameraData.xr.enabled)
            {
                var platform = Application.platform;
                if (platform == RuntimePlatform.WSAPlayerX86 || platform == RuntimePlatform.WSAPlayerARM || platform == RuntimePlatform.WSAPlayerX64)
                {
                    var displaySubsystem = GetFirstXRDisplaySubsystem();

                    if (displaySubsystem != null && !displaySubsystem.displayOpaque)
                        return true;
                }
            }
    #endif
            return false;
        }

#endif
*/

        // 排序方式, 依靠 camera1.depth, 值小的排前面
        Comparison<Camera> cameraComparison = (camera1, camera2) => { return (int)camera1.depth - (int)camera2.depth; };


#if UNITY_2021_1_OR_NEWER

        void SortCameras(List<Camera> cameras)
        {
            if (cameras.Count > 1)
                cameras.Sort(cameraComparison);
        }
#else
        /*   tpr
        void SortCameras(Camera[] cameras)
        {
            if (cameras.Length > 1)
                Array.Sort(cameras, cameraComparison);
        }
        */
#endif


        // 拿着现有数据, 新建并初始化一个 RenderTextureDescriptor 实例;
        static RenderTextureDescriptor CreateRenderTextureDescriptor(  // 读完__
                                        Camera camera,      // must be base camera
                                        float renderScale,
                                        bool isHdrEnabled, 
                                        int msaaSamples, 
                                        bool needsAlpha, 
                                        bool requiresOpaqueTexture
        ){
            RenderTextureDescriptor desc;

            // 当前平台为 LHR 预定的 具体数据format, 比如 B8G8R8A8_SRGB 这种的;
            GraphicsFormat renderTextureFormatDefault = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);

            if (camera.targetTexture == null)
            {// 需要从零配置一个新的
                desc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
                desc.width = (int)((float)desc.width * renderScale);
                desc.height = (int)((float)desc.height * renderScale);


                GraphicsFormat hdrFormat;
                if (!needsAlpha && RenderingUtils.SupportsGraphicsFormat(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.Linear | FormatUsage.Render))
                    // 不需要 alpha 通道
                    hdrFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                else if (RenderingUtils.SupportsGraphicsFormat(GraphicsFormat.R16G16B16A16_SFloat, FormatUsage.Linear | FormatUsage.Render))
                    hdrFormat = GraphicsFormat.R16G16B16A16_SFloat;
                else
                    hdrFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR); // This might actually be a LDR format on old devices.

                desc.graphicsFormat = isHdrEnabled ? hdrFormat : renderTextureFormatDefault;
                desc.depthBufferBits = 32;
                desc.msaaSamples = msaaSamples;
                // 是否启用 sRGB read/write conversions, 
                // 若为 true, 那么不管 render texture 内部数据format 是 ldr 还是 hdr, 它的对外读写口都一定是 linear 格式的;
                desc.sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear);
            }
            else
            {// 沿用 camera.targetTexture 中已经分配好的
                desc = camera.targetTexture.descriptor;
                desc.width = camera.pixelWidth;
                desc.height = camera.pixelHeight;
                if (camera.cameraType==CameraType.SceneView  && !isHdrEnabled)
                {
                    desc.graphicsFormat = renderTextureFormatDefault;
                }
                /*
                    SystemInfo.SupportsRenderTextureFormat(camera.targetTexture.descriptor.colorFormat)
                    will assert on R8_SINT since it isn't a valid value of RenderTextureFormat.
                    --
                    If this is fixed then we can implement debug statement to the user explaining why some
                    RenderTextureFormats available resolves in a black render texture when no warning or error
                    is given.
                    ---
                    如果这个问题得到了修复, 那么我们就能向用户实现一个 debug statement, 以解释为什么在没有 warning 和 error
                    的情况下, 有些可用的 render texture format 会解析出一张纯黑的画面;
                */
            }

            desc.enableRandomWrite = false;
            desc.bindMS = false;
            desc.useDynamicScale = camera.allowDynamicResolution;

            /*
                check that the requested MSAA samples count is supported by the current platform. If it's not supported,
                replace the requested desc.msaaSamples value with the actual value the engine falls back to
                --
                参数 desc 中记录了需要的 msaa 采样次数值, 本函数检测当前平台是否支持这个 采样次数;
                -- 如果支持这个次数, 那就返回这个次数值;
                -- 如果不支持, 那就返回一个 fallback 值; (当前平台支持的最大 msaa 采样次数值)
            */ 
            desc.msaaSamples = SystemInfo.GetRenderTextureSupportedMSAASampleCount(desc);

            /*
                if the target platform doesn't support storing multisampled RTs, and we are doing a separate opaque pass, 
                using a Load load action on the subsequent passes will result in loading Resolved data, 
                which on some platforms is discarded, resulting in losing the results of the previous passes.
                ---
                As a workaround we disable MSAA to make sure that the results of previous passes are stored. (fix for Case 1247423).
                ---

                如果目标平台 并不支持 "multisampled RTs", 而且我们正在执行一个单独的 opaque pass, 
                在后续 passes 中执行 "Load" 操作时, 可能因为某些平台的不支持, 而导致之前存储的 数据(可能时 msaa 处理过的数据) 被丢弃,
                进而加载失败;

                此处的弥补方案就是彻底关闭 msaa, 以保证 之前pass 渲染的结果, 一定能在后续pass 中加载得到; 
            */
            if (!SystemInfo.supportsStoreAndResolveAction && requiresOpaqueTexture)
                desc.msaaSamples = 1;

            return desc;
        }// 函数完__


        

        static Lightmapping.RequestLightsDelegate lightsDelegate = (Light[] requests, NativeArray<LightDataGI> lightsOutput) =>
        {
            // Editor only.
#if UNITY_EDITOR
            LightDataGI lightData = new LightDataGI();

            for (int i = 0; i < requests.Length; i++)
            {
                Light light = requests[i];
                switch (light.type)
                {
                    case LightType.Directional:
                        DirectionalLight directionalLight = new DirectionalLight();
                        LightmapperUtils.Extract(light, ref directionalLight);
                        lightData.Init(ref directionalLight);
                        break;
                    case LightType.Point:
                        PointLight pointLight = new PointLight();
                        LightmapperUtils.Extract(light, ref pointLight);
                        lightData.Init(ref pointLight);
                        break;
                    case LightType.Spot:
                        SpotLight spotLight = new SpotLight();
                        LightmapperUtils.Extract(light, ref spotLight);
                        spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
                        spotLight.angularFalloff = AngularFalloffType.AnalyticAndInnerAngle;
                        lightData.Init(ref spotLight);
                        break;
                    case LightType.Area:
                        RectangleLight rectangleLight = new RectangleLight();
                        LightmapperUtils.Extract(light, ref rectangleLight);
                        rectangleLight.mode = LightMode.Baked;
                        lightData.Init(ref rectangleLight);
                        break;
                    case LightType.Disc:
                        DiscLight discLight = new DiscLight();
                        LightmapperUtils.Extract(light, ref discLight);
                        discLight.mode = LightMode.Baked;
                        lightData.Init(ref discLight);
                        break;
                    default:
                        lightData.InitNoBake(light.GetInstanceID());
                        break;
                }

                lightData.falloff = FalloffType.InverseSquared;
                lightsOutput[i] = lightData;
            }
#else
            LightDataGI lightData = new LightDataGI();

            for (int i = 0; i < requests.Length; i++)
            {
                Light light = requests[i];
                lightData.InitNoBake(light.GetInstanceID());
                lightsOutput[i] = lightData;
            }
#endif
        };// 函数完__



        // called from DeferredLights.cs too
        public static void GetLightAttenuationAndSpotDirection(// 读完__
                                            LightType lightType, 
                                            float lightRange, 
                                            Matrix4x4 lightLocalToWorldMatrix,
                                            float spotAngle, 
                                            float? innerSpotAngle,
                                            out Vector4 lightAttenuation, // 核心
                                            out Vector4 lightSpotDir
        ){
            lightAttenuation = k_DefaultLightAttenuation;
            lightSpotDir = k_DefaultLightSpotDirection;

            // Directional Light attenuation is initialize so distance attenuation always be 1.0
            // 平行光的衰减值 永远为 1; (永远不衰减)

            if (lightType != LightType.Directional)
            {//  ------ Point光 or Spot光 -----:

                /*
                    Light attenuation in universal matches the unity vanilla one.
                    attenuation = 1.0 / distanceToLightSqr   
                    (tpr: 1/d^2 )

                    We offer two different smoothing factors.
                    The smoothing factors make sure that the light intensity is zero at the light range limit.

                --1--:
                    The first smoothing factor is a linear fade starting at 80 % of the light range:

                                         lightRangeSqr - distanceToLightSqr                   r^2 - d^2
                        smoothFactor = ------------------------------------------ = (tpr) -------------------
                                         lightRangeSqr - fadeStartDistanceSqr                r^2 - (0.8r)^2
                    
                        (其中, "d" 就是 "fragment 到 light 的距离";)

                    --------
                    We rewrite smoothFactor to be able to pre compute the constant terms below 
                    and apply the smooth factor with one MAD instruction;
                    将上面的 "smoothFactor" 做以下优化,变成如下公式: 

                    (a)    smoothFactor =  distanceSqr * (1.0 / (fadeDistanceSqr - lightRangeSqr)) + (-lightRangeSqr / (fadeDistanceSqr - lightRangeSqr)
                    (b)                    distanceSqr *           oneOverFadeRangeSqr             +              lightRangeSqrOverFadeRangeSqr

                    其中, (a)行就是优化后的公式, (b)行则是对 (a)行的模块化; (b)中各变量的计算已在下方;

                --2--:
                    此段计算, 参考笔记图片: "点光源衰减的计算2.jpg"
                    The other smoothing factor matches the one used in the Unity lightmapper but is slower than the linear one.
 
                        smoothFactor = (1.0 - saturate((distanceSqr * 1.0 / lightrangeSqr)^2))^2


                */
                float lightRangeSqr = lightRange * lightRange; // r^2
                float fadeStartDistanceSqr = 0.8f * 0.8f * lightRangeSqr; // (0.8r)^2
                float fadeRangeSqr = (fadeStartDistanceSqr - lightRangeSqr); // ((0.8r)^2 - r^2) 负值

                // 两个组件, 以便在 shader 代码中组装出 "smoothFactor"
                float oneOverFadeRangeSqr = 1.0f / fadeRangeSqr;
                float lightRangeSqrOverFadeRangeSqr = -lightRangeSqr / fadeRangeSqr; // r^2 / (r^2 - (0.8r)^2)

                float oneOverLightRangeSqr = 1.0f / Mathf.Max(0.0001f, lightRange * lightRange);

                // On mobile and Nintendo Switch: Use the faster linear smoothing factor (SHADER_HINT_NICE_QUALITY).
                // On other devices: Use the smoothing factor that matches the GI.
                lightAttenuation.x = Application.isMobilePlatform || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Switch 
                                        ? oneOverFadeRangeSqr : oneOverLightRangeSqr;

                lightAttenuation.y = lightRangeSqrOverFadeRangeSqr;
            }

            // --------------------------------------------------------------------"
            if (lightType == LightType.Spot)
            {
                
                Vector4 dir = lightLocalToWorldMatrix.GetColumn(2);      // 方向: light->fragment
                lightSpotDir = new Vector4(-dir.x, -dir.y, -dir.z, 0.0f);// 方向: fragment->light

                /*
                    此段计算, 参考笔记图片: "spot光源衰减的计算.jpg"
                    ----------------------------------------------------------
                    Spot Attenuation with a linear falloff can be defined as:
                                   SdotL - cosOuterAngle
                      atten =  ---------------------------------
                                 cosInnerAngle - cosOuterAngle

                    If we precompute the terms in a MAD instruction,
                    This can be rewritten as:

                        invAngleRange = 1.0 / (cosInnerAngle - cosOuterAngle);  // 一个中间件
                        atten = SdotL * invAngleRange + (-cosOuterAngle * invAngleRange);

                    spotAngle 是全角,取一半来计算;
                */
                float cosOuterAngle = Mathf.Cos(Mathf.Deg2Rad * spotAngle * 0.5f);

                // We neeed to do a null check for particle lights (粒子光)
                // This should be changed in the future
                // Particle lights will use an inline function
                float cosInnerAngle;
                if (innerSpotAngle.HasValue)
                    cosInnerAngle = Mathf.Cos(innerSpotAngle.Value * Mathf.Deg2Rad * 0.5f);
                else
                    // 此处实现猜测是针对 粒子光 的, 没有细读;
                    cosInnerAngle = Mathf.Cos(
                        (2.0f * Mathf.Atan(Mathf.Tan(spotAngle * 0.5f * Mathf.Deg2Rad) * (64.0f - 18.0f) / 64.0f)) * 0.5f
                    );
                
                float smoothAngleRange = Mathf.Max(0.001f, cosInnerAngle - cosOuterAngle);

                // 两个零件,
                float invAngleRange = 1.0f / smoothAngleRange;
                float add = -cosOuterAngle * invAngleRange;

                lightAttenuation.z = invAngleRange;
                lightAttenuation.w = add;
            }
        }// 函数完__



        /*
            一次处理一个光源的: lights[lightIndex], 初始化它的一部分 "通用" 数据;
        */
        /// <param name="lights"></param>
        /// <param name="lightIndex"></param>
        /// <param name="lightPos"></param>
        /// <param name="lightColor"></param>
        /// <param name="lightAttenuation"></param>
        /// <param name="lightSpotDir"></param>
        /// <param name="lightOcclusionProbeChannel">
        ///         一个 "通道筛选器", 类似 (0,0,1,0), 可以从一个 float4 中提取出对应分量的数值; 
        ///         算是 array idx 的另类版本;  真正的 shadowmask 数据 并不存储于此;
        /// </param>
        public static void InitializeLightConstants_Common( //    读完__
                                                    NativeArray<VisibleLight> lights, 
                                                    int lightIndex, 
                                                    out Vector4 lightPos, 
                                                    out Vector4 lightColor, 
                                                    out Vector4 lightAttenuation, 
                                                    out Vector4 lightSpotDir, 
                                                    out Vector4 lightOcclusionProbeChannel
        ){
            lightPos = k_DefaultLightPosition; // (0,0,1,0)
            lightColor = k_DefaultLightColor; //黑色
            lightOcclusionProbeChannel = k_DefaultLightsProbeChannel;//(0,0,0,0)
            lightAttenuation = k_DefaultLightAttenuation;
            lightSpotDir = k_DefaultLightSpotDirection;

            /*
                When no lights are visible, main light will be set to -1.
                In this case we initialize it to default values and return
                ---
                感觉描述不够严谨, idx == -1 好像只能说明: 没有在场景中找到 main light, 还是可能存在 oth光源的
                当然如果 lightIndex = -1, 说明当前正在处理的就是一个 不存在的 main light; 就不用处理了直接返回吧
                   ---
                按照 add light 部分的代码可知, idx == -1 还可能表示: 
                    这个 light 是 超量部分的 add light, 它也是不需要被处理的; 
            */
            if (lightIndex < 0)
                return;

            // ---- 说明是个有效的光源 -----:
            VisibleLight lightData = lights[lightIndex];
            if (lightData.lightType == LightType.Directional)
            {// 平行光中, lightPos 存储 光的方向: ( frag->light )
                Vector4 dir = -lightData.localToWorldMatrix.GetColumn(2);// 第三列, 
                lightPos = new Vector4(dir.x, dir.y, dir.z, 0.0f);
            }
            else
            {// 精确光中, lightPos 存储 光的 posWS
                Vector4 pos = lightData.localToWorldMatrix.GetColumn(3);// 第四列
                lightPos = new Vector4(pos.x, pos.y, pos.z, 1.0f);
            }

            // VisibleLight.finalColor already returns color in active color space
            lightColor = lightData.finalColor;

            GetLightAttenuationAndSpotDirection(
                lightData.lightType, 
                lightData.range, 
                lightData.localToWorldMatrix,
                lightData.spotAngle, 
                lightData.light?.innerSpotAngle,
                out lightAttenuation, 
                out lightSpotDir
            );

            Light light = lightData.light;


            if (light != null && 
                light.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed &&
                // 说明 "occlusionMaskChannel" 不为 -1, 本光源参与到了 shadowmask 的计算, 此贡献被记录在 shadowmask float4 的某个分量里
                // 使用这个 "occlusionMaskChannel" 可以充当 idx 访问到具体的分量;
                0<=light.bakingOutput.occlusionMaskChannel && light.bakingOutput.occlusionMaskChannel<4
            ){
                // 这会导致 "lightOcclusionProbeChannel" 变成类似 (0,0,1,0) 的样子, 他是 "通道赛选器"
                // 在 shader: BakedShadow() 中, 使用这个 Channel 信息去取出真正的 shadowmask 值;
                lightOcclusionProbeChannel[light.bakingOutput.occlusionMaskChannel] = 1.0f;
            }
        }// 函数完__
    }




    internal enum URPProfileId//URPProfileId__
    {
        // CPU
        UniversalRenderTotal,
        UpdateVolumeFramework,
        RenderCameraStack,

        // GPU
        AdditionalLightsShadow,
        ColorGradingLUT,
        CopyColor,
        CopyDepth,
        DepthNormalPrepass,
        DepthPrepass,

        // DrawObjectsPass
        DrawOpaqueObjects,
        DrawTransparentObjects,

        // RenderObjectsPass
        //RenderObjects,

        MainLightShadow,
        ResolveShadows,
        SSAO,

        // PostProcessPass
        StopNaNs,
        SMAA,
        GaussianDepthOfField,
        BokehDepthOfField,
        MotionBlur,
        PaniniProjection,
        UberPostProcess,
        Bloom,

        FinalBlit
    }
}
