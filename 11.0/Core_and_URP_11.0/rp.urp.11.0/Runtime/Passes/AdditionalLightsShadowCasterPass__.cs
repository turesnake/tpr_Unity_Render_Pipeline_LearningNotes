using System;
using System.Collections.Generic;
using Unity.Collections;

namespace UnityEngine.Rendering.Universal.Internal
{
    
    
    /*
        Renders a shadow map atlas for additional shadow-casting Lights.
    */ 
    public partial class AdditionalLightsShadowCasterPass //AdditionalLightsShadowCasterPass__
        : ScriptableRenderPass
    {
        private static class AdditionalShadowsConstantBuffer
        {
            public static int _AdditionalLightsWorldToShadow;//"_AdditionalLightsWorldToShadow"
            public static int _AdditionalShadowParams;//"_AdditionalShadowParams"
            public static int _AdditionalShadowOffset0;//"_AdditionalShadowOffset0"
            public static int _AdditionalShadowOffset1;//"_AdditionalShadowOffset1"
            public static int _AdditionalShadowOffset2;//"_AdditionalShadowOffset2"
            public static int _AdditionalShadowOffset3;//"_AdditionalShadowOffset3"
            public static int _AdditionalShadowmapSize;//"_AdditionalShadowmapSize"
        }


        /*
            每一块 shadow tile(slice) 的 "要求的" 创建信息; (但不是最终分配到的)
        */
        internal struct ShadowResolutionRequest//ShadowResolutionRequest__
        {
            public int visibleLightIndex; // light 在 visibleLights 中的 idx
            public int perLightShadowSliceIndex; // 在每个 add light 中, 每个 tile(slice) 的 idx

            // shadow tile 分辨率上限值 (pix), 记录在 asset inspcetor 中,分三档, 由 light inspcetor 选择;
            // 如果本 shadow tile 被丢弃(不执行渲染), 将此值设置为 0;
            public int requestedResolution; 
            public bool softShadow;         // 是否为 soft shadow
            public bool pointLightShadow;   // 是否为 point light 的


            // x/y coordinate of the square area allocated in the atlas for this shadow map
            // 在 shadowmap atlas 中的 起始坐标 (左下角) (pix)
            public int offsetX;
            public int offsetY; 


            // width of the square area allocated in the atlas for this shadow map
            // 实际在 shadowmap atlas 中分配的 区域的 分辨率 (width 值, pix)
            public int allocatedResolution;

            public ShadowResolutionRequest(
                                            int _visibleLightIndex, 
                                            int _perLightShadowSliceIndex, 
                                            int _requestedResolution, // shadow tile 分辨率上限值 (pix)
                                            bool _softShadow , 
                                            bool _pointLightShadow
            ){
                visibleLightIndex = _visibleLightIndex;
                perLightShadowSliceIndex = _perLightShadowSliceIndex;
                requestedResolution = _requestedResolution;
                softShadow = _softShadow;
                pointLightShadow = _pointLightShadow;

                offsetX = 0;
                offsetY = 0;
                allocatedResolution = 0;
            }
        }


        static int m_AdditionalLightsWorldToShadow_SSBO;//"_AdditionalLightsWorldToShadow_SSBO"
        static int m_AdditionalShadowParams_SSBO;//"_AdditionalShadowParams_SSBO"
        bool m_UseStructuredBuffer;// 暂为 false

        const int k_ShadowmapBufferBits = 16;// texel 存储精度 16-bits

        private RenderTargetHandle m_AdditionalLightsShadowmap;// "_AdditionalLightsShadowmapTexture"; 只是个 handle
        RenderTexture m_AdditionalLightsShadowmapTexture;// rt本体

        int m_ShadowmapWidth;
        int m_ShadowmapHeight;

        ShadowSliceData[] m_AdditionalLightsShadowSlices = null;

        /*                            
            maps a "global" visible light index (index to visibleLights) to an "additional light index" 
            (index to arrays _AdditionalLightsPosition, _AdditionalShadowParams, ...), 
            or -1 if it is not an additional light  (i.e if it is the main light)
            ---
            下标:  visibleLight 中的 light 的idx;
            元素:  对应的 light 在 "additional light array" 中的 idx 值;
                    -1 表示这个 light 不处理, 比如它是 main light;
        */
        int[] m_VisibleLightIndexToAdditionalLightIndex = null; 

        /*
            maps additional light index (index to arrays _AdditionalLightsPosition, _AdditionalShadowParams, ...) 
            to its "global" visible light index (index to renderingData.lightData.visibleLights)
            ---
            
        */
        int[] m_AdditionalLightIndexToVisibleLightIndex = null;                         
        
        /* 
            For each shadow slice, store the "additional light indices" of the punctual light that casts it
            下标: shadow tile(slice) idx
            元素: "additional light indices"
        */
        List<int> m_ShadowSliceToAdditionalLightIndex = new List<int>();                
        

        /* 
            For each shadow slice, store its "per-light shadow slice index" in the punctual light that casts it 
            (can be up to 5 for point lights)
            ---
            下标: Global Shadow Slice Index
            元素: Per Light Shadow Slice Index, 比如对于 point光来说, [0,5]
        */
        List<int> m_GlobalShadowSliceIndexToPerLightShadowSliceIndex = new List<int>(); 
        
        /*
            per-additional-light shadow info passed to the lighting shader
            ---
            下标: "additional light indices"
            元素:
                x: shadowStrength; shadow强度, 1表示光线全通过, 
                y: soft 为 1, hard 为 0;
                z: spot光 为 0, point光 为 1;
                w: 每个 light 的第一个 shadow tile 的 idx; 
                    若为 -1, 表示这个元素是空的, 不参与渲染; (仅在 c# 脚本端有效)
        */
        Vector4[] m_AdditionalLightIndexToShadowParams = null;                          
        
        /*
            per-shadow-slice info passed to the lighting shader
            下标: global Shadow Slice Index
            元素: shadowTransform 矩阵; (posWS -> posSTS 的那个)
                在 "Setup()" 函数的尾部, 此矩阵还叠加了一层 scale+ofset 功能
                此后, 直接用这个矩阵去乘一个 posWS, 可得到 它在 shadow atlas 中对应的的 uv值 [0,1]
                而且能指在 当前 light 对应的 tile(slice) 区域内;
        */
        Matrix4x4[] m_AdditionalLightShadowSliceIndexTo_WorldShadowMatrix = null;       
        
        /*
            intermediate array used to compute the final resolution of each shadow slice rendered in the frame;
            ---
            一个容器, 存储了: "每个 shadow tile(slice) 要求的 创建信息", 这个信息并不是最终会被分配到的信息;
            使用这个信息, 来计算最终的 分辨率;
        */
        List<ShadowResolutionRequest> m_ShadowResolutionRequests = new List<ShadowResolutionRequest>();  
        

        /* 
            stores for each "shadowed additional light" its (squared) distance to camera ; 
            used to sub-sort shadow requests according to how close their casting light is;
            ---
            存储: "每个投射阴影的 add light 的: (cameraPos - lightPos) 的长度的平方;
            以便后续根据 这个距离来对 "shadow requests" 数据进行排序;
            ---
            元素完全对应于 visibleLights 中的次序, 包含无效的元素
        */
        float[] m_VisibleLightIndexToCameraSquareDistance = null;                                        
        

        // 将 "m_ShadowResolutionRequests" 中的元素进行排序后得到的 array
        // 一部分 尺寸太小的 tiles 会被废弃, 废弃的元素没有被删除, 都放在尾部, 它们的成员 "requestedResolution" 都被设置为 0;
        ShadowResolutionRequest[] m_SortedShadowResolutionRequests = null;


        // for each visible light, store the index of its first shadow slice in m_SortedShadowResolutionRequests (for quicker access)
        // ---
        // 下标: light 在 visibleLights 中的 idx;
        // 元素: 对应的第一个 shadow tile idx 值;  无效的元素值为 -1;
        int[] m_VisibleLightIndexToSortedShadowResolutionRequestsFirstSliceIndex = null;                 
        

        // this list tracks space available in the atlas
        List<RectInt> m_UnusedAtlasSquareAreas = new List<RectInt>();                                    
        

        bool m_SupportsBoxFilterForShadows;// "移动平台 和 switch" 设为 true,
        ProfilingSampler m_ProfilingSetupSampler = new ProfilingSampler("Setup Additional Shadows");


        // keep in sync with "MAX_PUNCTUAL_LIGHT_SHADOW_SLICES_IN_UBO" in Shadows.hlsl
        // 16, 32, or 545
        int MAX_PUNCTUAL_LIGHT_SHADOW_SLICES_IN_UBO  
        {
            get{
                // != 256
                if (UniversalRenderPipeline.maxVisibleAdditionalLights != UniversalRenderPipeline.k_MaxVisibleAdditionalLightsNonMobile)
                    // Reduce uniform block size on Mobile/GL to avoid shader performance or compilation issues - 
                    // keep in sync with "MAX_PUNCTUAL_LIGHT_SHADOW_SLICES_IN_UBO" in Shadows.hlsl
                    return UniversalRenderPipeline.maxVisibleAdditionalLights; // 16, or 32
                else
                    return 545;  // keep in sync with "MAX_PUNCTUAL_LIGHT_SHADOW_SLICES_IN_UBO" in Shadows.hlsl
            }
        }


        
        //  构造函数
        public AdditionalLightsShadowCasterPass(RenderPassEvent evt) //   读完__
        {
            base.profilingSampler = new ProfilingSampler(nameof(AdditionalLightsShadowCasterPass));
            renderPassEvent = evt; // base class 中的

            AdditionalShadowsConstantBuffer._AdditionalLightsWorldToShadow = Shader.PropertyToID("_AdditionalLightsWorldToShadow");
            AdditionalShadowsConstantBuffer._AdditionalShadowParams = Shader.PropertyToID("_AdditionalShadowParams");
            AdditionalShadowsConstantBuffer._AdditionalShadowOffset0 = Shader.PropertyToID("_AdditionalShadowOffset0");
            AdditionalShadowsConstantBuffer._AdditionalShadowOffset1 = Shader.PropertyToID("_AdditionalShadowOffset1");
            AdditionalShadowsConstantBuffer._AdditionalShadowOffset2 = Shader.PropertyToID("_AdditionalShadowOffset2");
            AdditionalShadowsConstantBuffer._AdditionalShadowOffset3 = Shader.PropertyToID("_AdditionalShadowOffset3");
            AdditionalShadowsConstantBuffer._AdditionalShadowmapSize = Shader.PropertyToID("_AdditionalShadowmapSize");
            m_AdditionalLightsShadowmap.Init("_AdditionalLightsShadowmapTexture");


            m_AdditionalLightsWorldToShadow_SSBO = Shader.PropertyToID("_AdditionalLightsWorldToShadow_SSBO");
            m_AdditionalShadowParams_SSBO = Shader.PropertyToID("_AdditionalShadowParams_SSBO");

            m_UseStructuredBuffer = RenderingUtils.useStructuredBuffer; // 暂为 false
            m_SupportsBoxFilterForShadows = Application.isMobilePlatform || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Switch;

            // Preallocated a fixed size. CommandBuffer.SetGlobal* does allow this data to grow.
            int maxVisibleAdditionalLights = UniversalRenderPipeline.maxVisibleAdditionalLights;// 16, 32, or 256
            const int maxMainLights = 1;
            int maxVisibleLights = UniversalRenderPipeline.maxVisibleAdditionalLights + maxMainLights;

            /*
                These array sizes should be as big as "ScriptableCullingParameters.maximumVisibleLights"
                We initialize these array sizes with the number of visible lights allowed by the ForwardRenderer.
                The number of visible lights can become much higher when using the Deferred rendering path, 
                we resize the arrays during Setup() if required.
            */
            m_AdditionalLightIndexToVisibleLightIndex = new int[maxVisibleLights];
            m_VisibleLightIndexToAdditionalLightIndex = new int[maxVisibleLights];
            m_VisibleLightIndexToSortedShadowResolutionRequestsFirstSliceIndex = new int[maxVisibleLights];
            m_AdditionalLightIndexToShadowParams = new Vector4[maxVisibleLights];
            m_VisibleLightIndexToCameraSquareDistance = new float[maxVisibleLights];


            if (!m_UseStructuredBuffer)// 成立
            {
                // Uniform buffers are faster on some platforms, but they have stricter size limitations
                m_AdditionalLightShadowSliceIndexTo_WorldShadowMatrix = new Matrix4x4[MAX_PUNCTUAL_LIGHT_SHADOW_SLICES_IN_UBO];// 16, 32, or 545
                m_UnusedAtlasSquareAreas.Capacity = MAX_PUNCTUAL_LIGHT_SHADOW_SLICES_IN_UBO;// 16, 32, or 545
                m_ShadowResolutionRequests.Capacity = MAX_PUNCTUAL_LIGHT_SHADOW_SLICES_IN_UBO;// 16, 32, or 545
            }
        }//  函数完__



        private int GetPunctualLightShadowSlicesCount(in LightType lightType)// 读完__
        {
            switch (lightType)
            {
                case LightType.Spot:
                    return 1;
                case LightType.Point:
                    return 6;
                default:
                    return 0;
            }
        }


        // Magic numbers used to identify light type when rendering shadow receiver.
        // Keep in sync with AdditionalLightRealtimeShadow code in com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl
        private const float LightTypeIdentifierInShadowParams_Spot = 0;
        private const float LightTypeIdentifierInShadowParams_Point = 1;



        /*
            Returns the guard angle that must be added to a frustum angle covering a projection map of resolution sliceResolutionInTexels,
            in order to also cover a guard band of size guardBandSizeInTexels around the projection map.
            Formula illustrated in https://i.ibb.co/wpW5Mnf/Calc-Guard-Angle.png
            ---
            见笔记参考图: "urp point light shadow fovBias guard angle-1.jpg"
            ---
            返回 guard angle, 角度制; 将这个角度 加上 90度, 得到新得 cubemap shadow tile(slice) 的 frustum 的 fov值; 
        */
        internal static float CalcGuardAngle(   //    读完__
                                        float frustumAngleInDegrees, // fov值, 90
                                        float guardBandSizeInTexels, // soft: 5; hard: 1; 分辨率(pix), 
                                        float sliceResolutionInTexels // shadow tile(slice) 分辨率(pix)
        ){
            float frustumAngle = frustumAngleInDegrees * Mathf.Deg2Rad;// radian
            float halfFrustumAngle = frustumAngle / 2; // 45度的 弧度
            float tanHalfFrustumAngle = Mathf.Tan(halfFrustumAngle);

            float halfSliceResolution = sliceResolutionInTexels / 2;
            float halfGuardBand = guardBandSizeInTexels / 2;
            float factorBetweenAngleTangents = 1 + halfGuardBand / halfSliceResolution;

            float tanHalfGuardAnglePlusHalfFrustumAngle = tanHalfFrustumAngle * factorBetweenAngleTangents;

            float halfGuardAnglePlusHalfFrustumAngle = Mathf.Atan(tanHalfGuardAnglePlusHalfFrustumAngle);
            float halfGuardAngleInRadian = halfGuardAnglePlusHalfFrustumAngle - halfFrustumAngle;

            float guardAngleInRadian = 2 * halfGuardAngleInRadian;
            float guardAngleInDegree = guardAngleInRadian * Mathf.Rad2Deg;

            return guardAngleInDegree;
        }//  函数完__



        private const int kMinimumPunctualLightHardShadowResolution =  8;
        private const int kMinimumPunctualLightSoftShadowResolution = 16;

        //  Minimal shadow map resolution required to have meaningful shadows visible during lighting
        //  shadow tile(slice) 最小分辨率, 再小就看不清了
        int MinimalPunctualLightShadowResolution(bool softShadow)//  读完__
        {
            return softShadow ? 
                    kMinimumPunctualLightSoftShadowResolution : // 16
                    kMinimumPunctualLightHardShadowResolution;  // 8
        }



        static bool m_IssuedMessageAboutPointLightHardShadowResolutionTooSmall = false;// debug
        static bool m_IssuedMessageAboutPointLightSoftShadowResolutionTooSmall = false;// debug

        /*
            Returns the guard angle that must be added to a point light shadow face frustum angle
            in order to avoid shadows missing at the boundaries between cube faces.
            ---
            返回值: guard angle, 需要累加到 shadow tile(slice) resolution 上, 以消除 cubemap 接缝处的视觉错误;
        */
        internal static float GetPointLightShadowFrustumFovBiasInDegrees(//   读完__
                                                                int shadowSliceResolution, 
                                                                bool shadowFiltering // soft shadow 时为 true
        ){
            /*
                Commented-out code below(下方注释掉的代码) uses the theoretical formula to compute 
                the required guard angle based on the number of additional texels that the projection should cover. 
                它和 HDRP's "HDShadowUtils.CalcGuardAnglePerspective()" 很相似;
               
                However, due to precision issues or other filterings performed at lighting for example, 
                this formula also still requires a fudge factor.
                Since we only handle a fixed number of resolutions, we use empirical values instead.
                -------
                然而，由于精度问题 或 在照明时执行的其他过滤，这个公式仍然需要一个模糊因素。
                我们选择改用 经验值;
            */
#if false
            float fudgeFactor = 1.5f;
            return fudgeFactor * CalcGuardAngle( 90, shadowFiltering ? 5 : 1, shadowSliceResolution );
#endif
            // ---------------------------------------------------------:
            // 下方方法选择 直接设置 经验值, 
            // 参见笔记图: "urp point light shadow fovBias guard angle-2.jpg"

            float fovBias = 4.00f;

            /*
                Empirical value(经验值) found to remove gaps between point light shadow faces in test scenes.
                We can see that the guard angle is roughly proportional to the inverse of resolution 
                https://docs.google.com/spreadsheets/d/1QrIZJn18LxVKq2-K1XS4EFRZcZdZOJTTKKhDN8Z1b_s
            */
            if (shadowSliceResolution <= kMinimumPunctualLightHardShadowResolution)
            {
                if (!m_IssuedMessageAboutPointLightHardShadowResolutionTooSmall)
                {
                    Debug.LogWarning("Too many additional punctual lights shadows, increase shadow atlas size or remove some shadowed lights");
                    // Only output this once per shadow requests configuration
                    m_IssuedMessageAboutPointLightHardShadowResolutionTooSmall = true; 
                }
            }
            else if (shadowSliceResolution <= 16)
                fovBias = 43.0f;
            else if (shadowSliceResolution <= 32)
                fovBias = 18.55f;
            else if (shadowSliceResolution <= 64)
                fovBias = 8.63f;
            else if (shadowSliceResolution <= 128)
                fovBias = 4.13f;
            else if (shadowSliceResolution <= 256)
                fovBias = 2.03f;
            else if (shadowSliceResolution <= 512)
                fovBias = 1.00f;
            else if (shadowSliceResolution <= 1024)
                fovBias = 0.50f;

            if (shadowFiltering)
            {
                if (shadowSliceResolution <= kMinimumPunctualLightSoftShadowResolution)
                {
                    if (!m_IssuedMessageAboutPointLightSoftShadowResolutionTooSmall)
                    {
                        Debug.LogWarning("Too many additional punctual lights shadows to use Soft Shadows. Increase shadow atlas size, remove some shadowed lights or use Hard Shadows.");
                        // With such small resolutions no fovBias can give good visual results
                        // Only output this once per shadow requests configuration
                        m_IssuedMessageAboutPointLightSoftShadowResolutionTooSmall = true; 
                    }
                }
                else if (shadowSliceResolution <= 32)
                    fovBias += 9.35f;// 27.9
                else if (shadowSliceResolution <= 64)
                    fovBias += 4.07f;// 12.7
                else if (shadowSliceResolution <= 128)
                    fovBias += 1.77f;// 5.9
                else if (shadowSliceResolution <= 256)
                    fovBias += 0.85f;// 2.88
                else if (shadowSliceResolution <= 512)
                    fovBias += 0.39f;// 1.39
                else if (shadowSliceResolution <= 1024)
                    fovBias += 0.17f;// 0.67

                // These values were verified to work on platforms for which "m_SupportsBoxFilterForShadows" is true (Mobile, Switch).
                // TODO: Investigate finer-tuned values for those platforms. Soft shadows are implemented differently for them.
            }

            return fovBias;
        }//  函数完__





        bool m_IssuedMessageAboutShadowSlicesTooMany = false;// debug

        // Shadow Fade parameters _MainLightShadowParams.zw are actually also used by AdditionalLights
        Vector4 m_MainLightShadowParams; 


        /*
            ------------------------------------------------
            Adapted from(改编自) InsertionSort() in: 
            "com.unity.render-pipelines.high-definition/Runtime/Lighting/Shadow/HDDynamicShadowAtlas.cs"
            -1- 
                Sort array in decreasing requestedResolution order, (降序)
            -2-
                sub-sorting in "HardShadow > SoftShadow" and then "Spot > Point", 
                    i.e place last requests that will be removed in priority to make room for the others, 
                    because their resolution is too small to produce good-looking shadows ; 
                    or because they take relatively more space in the atlas )
            -3-
                sub-sub-sorting in light distance to camera
            -4- 
                grouping in increasing visibleIndex 
            -5-
                 sub-sorting each group in ShadowSliceIndex order)
            -----------------------------------
            双指针算法;
            依次执行如下排序,(只有在前一种 "比较"相同时, 才会执行下一种比较):
                -1- requestedResolution 值大的, 在前;
                -2- Hard shadow 在前, soft 在后;
                -3- spot光 在前, point光 在后;
                -4- cameraPos<->lightPos 距离较近的, 在前;
                -5- visible Light Index 值小的, 在前;
                -6- 一个 light 内, tile(slice) idx 值小的, 在前;
        */
        internal void InsertionSort(   //   读完__
                                    ShadowResolutionRequest[] array, // 要排序的 array;  m_SortedShadowResolutionRequests
                                    int startIndex, // 起始 idx;
                                    int lastIndex   // 尾后 idx;  totalShadowResolutionRequestsCount
        ){
            int i = startIndex + 1;

            while (i < lastIndex)
            {
                var curr = array[i];
                int j = i - 1;
                // j:前一个元素; i:当前元素

                /*  
                    -----------------------:
                    Sort in priority order
                    从 [i-1] 开始, 向 [0] 进行 逐元素比较; 这个过程中: i 始终指向 "被比较对象" 不动, j 作为遍历用 idx;
                    一路遍历直到找到第一个 "不符合条件" 的元素(j指向它), 将 i指向的元素 放到 j位置上,
                    [j+1] 直到 [i-1], 这些元素 都后退一位;
                */
                while ((j >= 0) && 
                        (
                            // 下面这组条件, 就是  "不符合排序顺序 的条件": 
                            (curr.requestedResolution > array[j].requestedResolution) || 
                            (curr.requestedResolution == array[j].requestedResolution && !curr.softShadow && array[j].softShadow) || 
                            (curr.requestedResolution == array[j].requestedResolution &&  curr.softShadow == array[j].softShadow && !curr.pointLightShadow && array[j].pointLightShadow) || 
                            (curr.requestedResolution == array[j].requestedResolution &&  curr.softShadow == array[j].softShadow &&  curr.pointLightShadow == array[j].pointLightShadow && m_VisibleLightIndexToCameraSquareDistance[curr.visibleLightIndex]  < m_VisibleLightIndexToCameraSquareDistance[array[j].visibleLightIndex]) || 
                            (curr.requestedResolution == array[j].requestedResolution &&  curr.softShadow == array[j].softShadow &&  curr.pointLightShadow == array[j].pointLightShadow && m_VisibleLightIndexToCameraSquareDistance[curr.visibleLightIndex] == m_VisibleLightIndexToCameraSquareDistance[array[j].visibleLightIndex] && curr.visibleLightIndex  < array[j].visibleLightIndex) || 
                            (curr.requestedResolution == array[j].requestedResolution &&  curr.softShadow == array[j].softShadow &&  curr.pointLightShadow == array[j].pointLightShadow && m_VisibleLightIndexToCameraSquareDistance[curr.visibleLightIndex] == m_VisibleLightIndexToCameraSquareDistance[array[j].visibleLightIndex] && curr.visibleLightIndex == array[j].visibleLightIndex && curr.perLightShadowSliceIndex < array[j].perLightShadowSliceIndex)
                        )
                ){
                    array[j + 1] = array[j];
                    j--;
                }
                array[j + 1] = curr; // 结尾步;

                // ------------------------:
                i++; // 用于下一回合
            }
        }


        /*
            "EstimateScaleFactor"(预测缩放因子) Needed To Fit All Shadows In Atlas;
            此缩放因子要作用于 每一个 shadow tile(slice) 之上;
            此值是 2 的倍数. 大于1;
        */
        int EstimateScaleFactorNeededToFitAllShadowsInAtlas( //   读完__
                                        in ShadowResolutionRequest[] shadowResolutionRequests,
                                        int endIndex,  // shadow tiles(slice) 个数, 可能比上面的 array 的个数少, 丢弃尾部元素
                                        int atlasWidth // shadow atlas width 分辨率(pix)
        ){
            long totalTexelsInShadowAtlas = atlasWidth * atlasWidth; // 能用的 atlas 的面积

            long totalTexelsInShadowRequests = 0;// 理论上想要的面积
            for (int shadowRequestIndex = 0; shadowRequestIndex < endIndex; ++shadowRequestIndex)
                totalTexelsInShadowRequests +=  shadowResolutionRequests[shadowRequestIndex].requestedResolution * 
                                                shadowResolutionRequests[shadowRequestIndex].requestedResolution;

            int estimatedScaleFactor = 1;
            while (totalTexelsInShadowRequests > totalTexelsInShadowAtlas * estimatedScaleFactor * estimatedScaleFactor)
                estimatedScaleFactor *= 2;

            return estimatedScaleFactor;
        }//  函数完__



        /*
            Assigns to each of the first totalShadowSlicesCount items in "m_SortedShadowResolutionRequests" 
            a location in the shadow atlas based on requested resolutions.
            If necessary, scales down shadow maps active in the frame, to make all of them fit in the atlas.
            ----------
            将每个 shadow tile(slice) 真的放置到 shadowmap stlas 中去;
            计算好每个 tile 的 offset 和 实际分辨率, 写入 "m_SortedShadowResolutionRequests" 对应元素中:
                -- offsetX
                -- offsetY
                -- allocatedResolution

        */
        void AtlasLayout(  //   读完__
                        int atlasSize,  // shadow atlas 分辨率(pix)
                        int totalShadowSlicesCount, // 实际要执行渲染的  shadow tile(slice) 的个数
                        int estimatedScaleFactor // 预测缩放因子, 2的倍数, 大于 1;
        ){
            bool allShadowSlicesFitInAtlas = false; // 每个 shadow tile(slice) 都能放入 atlas 了吗
            bool tooManyShadows = false;
            int shadowSlicesScaleFactor = estimatedScaleFactor; // 缩放因子

            while (!allShadowSlicesFitInAtlas && !tooManyShadows)
            {
                m_UnusedAtlasSquareAreas.Clear(); // List<RectInt>
                m_UnusedAtlasSquareAreas.Add(new RectInt(0, 0, atlasSize, atlasSize));

                allShadowSlicesFitInAtlas = true;

                for (int shadowRequestIndex = 0; shadowRequestIndex < totalShadowSlicesCount; ++shadowRequestIndex)
                {
                    // 缩放后的 tile 的分辨率
                    var resolution = m_SortedShadowResolutionRequests[shadowRequestIndex].requestedResolution / shadowSlicesScaleFactor;

                    if (resolution < MinimalPunctualLightShadowResolution(m_SortedShadowResolutionRequests[shadowRequestIndex].softShadow))
                    {
                        tooManyShadows = true;
                        break;
                    }

                    bool foundSpaceInAtlas = false;

                    // Try to find free space in the atlas
                    for (int unusedAtlasSquareAreaIndex = 0; unusedAtlasSquareAreaIndex < m_UnusedAtlasSquareAreas.Count; ++unusedAtlasSquareAreaIndex)
                    {
                        var atlasArea = m_UnusedAtlasSquareAreas[unusedAtlasSquareAreaIndex];// RectInt
                        var atlasAreaWidth = atlasArea.width;
                        var atlasAreaHeight = atlasArea.height;
                        var atlasAreaX = atlasArea.x;
                        var atlasAreaY = atlasArea.y;
                        if (atlasAreaWidth >= resolution)
                        {
                            /*
                                we can use this atlas area for the shadow request
                                ---
                                最核心的一步    !!!!!!
                                真的为当前 shadow tile 分配 offset 和 实际分辨率
                            */ 
                            m_SortedShadowResolutionRequests[shadowRequestIndex].offsetX = atlasAreaX;
                            m_SortedShadowResolutionRequests[shadowRequestIndex].offsetY = atlasAreaY;
                            m_SortedShadowResolutionRequests[shadowRequestIndex].allocatedResolution = resolution;

                            // this atlas space is not available anymore, so remove it from the list
                            m_UnusedAtlasSquareAreas.RemoveAt(unusedAtlasSquareAreaIndex);

                            // make sure to split space so that the rest of this square area can be used

                            // 剩下还需要分配的 shadow tile 数量;
                            int remainingShadowRequestsCount = totalShadowSlicesCount - shadowRequestIndex - 1; // (no need to add more than that)
                            int newSquareAreasCount = 0;
                            int newSquareAreaWidth = resolution; // we split the area in squares of same size
                            int newSquareAreaHeight = resolution;
                            var newSquareAreaX = atlasAreaX;
                            var newSquareAreaY = atlasAreaY;

                            // 本次使用的 "空白空间" 还只使用了一部分, 将他切割成更多块更小的 "空白空间", 
                            // 存入 "m_UnusedAtlasSquareAreas" 中, 以便后续 shadow tiles 使用;
                            while (newSquareAreasCount < remainingShadowRequestsCount)
                            {
                                // 先尝试在 右侧分配 空间空间, 不行就改从上方分配
                                newSquareAreaX += newSquareAreaWidth;
                                if (newSquareAreaX + newSquareAreaWidth > (atlasAreaX + atlasAreaWidth))
                                {
                                    newSquareAreaX = atlasAreaX;
                                    newSquareAreaY += newSquareAreaHeight;
                                    if (newSquareAreaY + newSquareAreaHeight > (atlasAreaY + atlasAreaHeight))
                                        // 当前这张 atlasArea 完全放不下 newSquareArea 了
                                        break;
                                }

                                // replace the space we removed previously by new smaller squares 
                                // (inserting them in this order ensures shadow maps will be packed at the side of the atlas, without gaps)
                                // ---
                                // 插入一个 新的"空白空间"
                                // 之所以把 新的"空白空间" 插入到这个 idx 中, 是因为在上面的循环中, "unusedAtlasSquareAreaIndex" 是只能递增的不能归零;
                                m_UnusedAtlasSquareAreas.Insert(
                                    unusedAtlasSquareAreaIndex + newSquareAreasCount, 
                                    new RectInt(newSquareAreaX, newSquareAreaY, newSquareAreaWidth, newSquareAreaHeight)
                                );
                                ++newSquareAreasCount;
                            }

                            foundSpaceInAtlas = true;
                            break;
                        }
                    }

                    if (!foundSpaceInAtlas)
                    {
                        allShadowSlicesFitInAtlas = false;
                        break;
                    }
                }

                if (!allShadowSlicesFitInAtlas && !tooManyShadows)
                    shadowSlicesScaleFactor *= 2;
            }

            if (!m_IssuedMessageAboutShadowMapsTooBig && tooManyShadows)
            {
                Debug.LogWarning($"Too many additional punctual lights shadows. URP tried reducing shadow resolutions by {shadowSlicesScaleFactor} but it was still too much. Increase shadow atlas size, decrease big shadow resolutions, or reduce the number of shadow maps active in the same frame (currently was {totalShadowSlicesCount}).");
                m_IssuedMessageAboutShadowMapsTooBig = true; // Only output this once per shadow requests configuration
            }

            if (!m_IssuedMessageAboutShadowMapsRescale && shadowSlicesScaleFactor > 1)
            {
                Debug.Log($"Reduced additional punctual light shadows resolution by {shadowSlicesScaleFactor} to make {totalShadowSlicesCount} shadow maps fit in the {atlasSize}x{atlasSize} shadow atlas. To avoid this, increase shadow atlas size, decrease big shadow resolutions, or reduce the number of shadow maps active in the same frame");
                m_IssuedMessageAboutShadowMapsRescale = true; // Only output this once per shadow requests configuration
            }
        }//  函数完__




        bool m_IssuedMessageAboutShadowMapsRescale = false;// debug
        bool m_IssuedMessageAboutShadowMapsTooBig = false;// debug
        bool m_IssuedMessageAboutRemovedShadowSlices = false;// debug

          
        /* 
            used to keep track of changes in the shadow requests and shadow atlas configuration (per camera)
            < cameraHash, ShadowRequestHash >
        */
        Dictionary<int, ulong> m_ShadowRequestsHashes = new Dictionary<int, ulong>();


        ulong ResolutionLog2ForHash(int resolution)//   读完__
        {
            switch (resolution)
            {
                case 4096: return 12;
                case 2048: return 11;
                case 1024: return 10;
                case 0512: return 09;
            }
            return 08;
        }


        // 根据 visibleLights 信息, 计算一个 hash 值;
        // 当 visibleLights 中关键信息发生改变时, 计算出的 hash 值就会改变;
        ulong ComputeShadowRequestHash(ref RenderingData renderingData) //   读完__
        {
            ulong numberOfShadowedPointLights = 0;
            ulong numberOfSoftShadowedLights = 0;
            ulong numberOfShadowsWithResolution0128 = 0;
            ulong numberOfShadowsWithResolution0256 = 0;
            ulong numberOfShadowsWithResolution0512 = 0;
            ulong numberOfShadowsWithResolution1024 = 0;
            ulong numberOfShadowsWithResolution2048 = 0;
            ulong numberOfShadowsWithResolution4096 = 0;

            var visibleLights = renderingData.lightData.visibleLights;
            for (int visibleLightIndex = 0; visibleLightIndex < visibleLights.Length; ++visibleLightIndex)
            {
                if (!IsValidShadowCastingLight(ref renderingData.lightData, visibleLightIndex))
                    continue;
                //  如果目标 light 不是平行光, 且开启了 shadow, 且 shadow Strength 非0, 则可执行下方代码:

                if (visibleLights[visibleLightIndex].lightType == LightType.Point)
                    ++numberOfShadowedPointLights;
                if (visibleLights[visibleLightIndex].light.shadows == LightShadows.Soft)
                    ++numberOfSoftShadowedLights;
                if (renderingData.shadowData.resolution[visibleLightIndex] == 0128)
                    ++numberOfShadowsWithResolution0128;
                if (renderingData.shadowData.resolution[visibleLightIndex] == 0256)
                    ++numberOfShadowsWithResolution0256;
                if (renderingData.shadowData.resolution[visibleLightIndex] == 0512)
                    ++numberOfShadowsWithResolution0512;
                if (renderingData.shadowData.resolution[visibleLightIndex] == 1024)
                    ++numberOfShadowsWithResolution1024;
                if (renderingData.shadowData.resolution[visibleLightIndex] == 2048)
                    ++numberOfShadowsWithResolution2048;
                if (renderingData.shadowData.resolution[visibleLightIndex] == 4096)
                    ++numberOfShadowsWithResolution4096;
            }
            ulong shadowRequestsHash = ResolutionLog2ForHash(renderingData.shadowData.additionalLightsShadowmapWidth) - 8; // bits [00~02]
            shadowRequestsHash |= numberOfShadowedPointLights << 03;        // bits [03~10]
            shadowRequestsHash |= numberOfSoftShadowedLights << 11;         // bits [11~18]
            shadowRequestsHash |= numberOfShadowsWithResolution0128 << 19;  // bits [19~26]
            shadowRequestsHash |= numberOfShadowsWithResolution0256 << 27;  // bits [27~34]
            shadowRequestsHash |= numberOfShadowsWithResolution0512 << 35;  // bits [35~42]
            shadowRequestsHash |= numberOfShadowsWithResolution1024 << 43;  // bits [43~49]
            shadowRequestsHash |= numberOfShadowsWithResolution2048 << 50;  // bits [50~56]
            shadowRequestsHash |= numberOfShadowsWithResolution4096 << 57;  // bits [57~63]
            return shadowRequestsHash;
        }//  函数完__



        public bool Setup(ref RenderingData renderingData) //    读完__
        {
            using var profScope = new ProfilingScope(null, m_ProfilingSetupSampler);

            Clear();

            m_ShadowmapWidth = renderingData.shadowData.additionalLightsShadowmapWidth;// 其实就是 shadow resolution
            m_ShadowmapHeight = renderingData.shadowData.additionalLightsShadowmapHeight;// 其实就是 shadow resolution

            /*
                In order to apply shadow fade to AdditionalLights, we need to set constants "_MainLightShadowParams.zw" 
                used by function "GetShadowFade()" in Shadows.hlsl.
                However, we also have to make sure not to override "_MainLightShadowParams.xy" constants, 
                that are used by MainLight only. Therefore we need to store these values in m_MainLightShadowParams 
                and set them again during SetupAdditionalLightsShadowReceiverConstants.
                --------
                计算 shadow 相关的数据: 强度, 距离, 衰减计算组件 (需要被传入 shader 中)
                -- main light 使用 xyzw 4个数据;
                -- add light 只是用 zw 分量数据;

                    x: 阴影的强度, [0,1], 为 0 时阴影完全消失, 为 1 时阴影最强烈;
                    y: 1:支持 soft shadow, 0: 不支持
                    z: 计算 阴影衰减 的组件: oneOverFadeDist
                    w: 计算 阴影衰减 的组件: minusStartFade
            */
            m_MainLightShadowParams = ShadowUtils.GetMainLightShadowParams(ref renderingData);

            var visibleLights = renderingData.lightData.visibleLights;
            int additionalLightsCount = renderingData.lightData.additionalLightsCount;//visibleLights 中, add light 的数量;

            int atlasWidth = renderingData.shadowData.additionalLightsShadowmapWidth;// 其实就是 shadow resolution

            
            /* 
                Number of shadow slices that we would need for all shadowed additional (punctual) lights in the scene. 
                We might have to ignore some of those requests if they do not fit in the shadow atlas.
                ---
                收集起来的 所有精确光源的 shadowmap slices (tiles) 的总个数; ( spot光需要 1 个 tile, point光需要 6 个 )
            */
            int totalShadowResolutionRequestsCount = 0; 

            m_ShadowResolutionRequests.Clear();

            // Check changes in the shadow requests and shadow atlas configuration - compute shadow request/configuration hash
            if (!renderingData.cameraData.isPreviewCamera)
            {// 不是 editor 中的 预览窗口 使用的 camera

                ulong newShadowRequestHash = ComputeShadowRequestHash(ref renderingData);
                ulong oldShadowRequestHash = 0;

                // 取出 oldShadowRequestHash:
                m_ShadowRequestsHashes.TryGetValue(renderingData.cameraData.camera.GetHashCode(), out oldShadowRequestHash);
                if (oldShadowRequestHash != newShadowRequestHash)
                {
                    // 当 visibleLights 中关键信息发生改变时, 计算出的 hash 值就会改变;

                    m_ShadowRequestsHashes[renderingData.cameraData.camera.GetHashCode()] = newShadowRequestHash;

                    // congif changed ; reset error message flags as we might need to issue those messages again
                    m_IssuedMessageAboutPointLightHardShadowResolutionTooSmall = false;
                    m_IssuedMessageAboutPointLightSoftShadowResolutionTooSmall = false;
                    m_IssuedMessageAboutShadowMapsRescale = false;
                    m_IssuedMessageAboutShadowMapsTooBig = false;
                    m_IssuedMessageAboutShadowSlicesTooMany = false;
                    m_IssuedMessageAboutRemovedShadowSlices = false;
                }
            }

            if (m_AdditionalLightIndexToVisibleLightIndex.Length < visibleLights.Length)
            {
                /*
                    Array "visibleLights" is returned by ScriptableRenderContext.Cull()
                    The maximum number of "visibleLights" that ScriptableRenderContext.Cull() should return, 
                    is defined by parameter "ScriptableCullingParameters.maximumVisibleLights";
                    urp sets this "ScriptableCullingParameters.maximumVisibleLights" value during "ScriptableRenderer.SetupCullingParameters";
                    When using Deferred rendering, it is possible to specify a very high number of visible lights.
                    --
                    在延迟渲染中, visibleLights.Length 这个值可以无限高;
                */
                m_AdditionalLightIndexToVisibleLightIndex = new int[visibleLights.Length];
                m_VisibleLightIndexToAdditionalLightIndex = new int[visibleLights.Length];
                m_AdditionalLightIndexToShadowParams = new Vector4[visibleLights.Length];
                m_VisibleLightIndexToCameraSquareDistance = new float[visibleLights.Length];
                m_VisibleLightIndexToSortedShadowResolutionRequestsFirstSliceIndex = new int[visibleLights.Length];
            }

            // reset m_VisibleLightIndexToCameraSquareDistance
            // 先初始化为 极大值;
            for (int visibleLightIndex = 0; visibleLightIndex < m_VisibleLightIndexToCameraSquareDistance.Length; ++visibleLightIndex)
                m_VisibleLightIndexToCameraSquareDistance[visibleLightIndex] = float.MaxValue;


            for (int visibleLightIndex = 0; visibleLightIndex < visibleLights.Length; ++visibleLightIndex)
            {
                if (visibleLightIndex == renderingData.lightData.mainLightIndex)
                    // Skip main directional light as it is not packed into the shadow atlas
                    continue;

                //  如果目标 light 不是平行光, 且开启了 shadow, 且 shadow Strength 非 0, 则返回 true;
                if (IsValidShadowCastingLight(ref renderingData.lightData, visibleLightIndex))
                {
                    // spot光需要 1 张 tile, point光需要 6 张 tile;
                    int shadowSlicesCountForThisLight = GetPunctualLightShadowSlicesCount(visibleLights[visibleLightIndex].lightType);
                    // 所有 shadow tile(slice) 个数
                    totalShadowResolutionRequestsCount += shadowSlicesCountForThisLight;

                    // 每个 light 内部的每个 shadow tile(slice)
                    for (int perLightShadowSliceIndex=0; perLightShadowSliceIndex<shadowSlicesCountForThisLight; ++perLightShadowSliceIndex)
                    {
                        m_ShadowResolutionRequests.Add(new ShadowResolutionRequest(
                            visibleLightIndex, 
                            perLightShadowSliceIndex, 
                            renderingData.shadowData.resolution[visibleLightIndex],// shadow tile 分辨率上限值 (pix)
                            (visibleLights[visibleLightIndex].light.shadows == LightShadows.Soft), 
                            (visibleLights[visibleLightIndex].lightType == LightType.Point)
                        ));
                    }
                    // mark this light as casting shadows
                    // (cameraPos - lightPos) 的长度的平方;
                    m_VisibleLightIndexToCameraSquareDistance[visibleLightIndex] = 
                        (renderingData.cameraData.camera.transform.position - visibleLights[visibleLightIndex].light.transform.position).sqrMagnitude;
                }
            }

            // 按需扩容
            if (m_SortedShadowResolutionRequests==null || m_SortedShadowResolutionRequests.Length < totalShadowResolutionRequestsCount)
                m_SortedShadowResolutionRequests = new ShadowResolutionRequest[totalShadowResolutionRequestsCount];
            // 复制
            for (int shadowRequestIndex = 0; shadowRequestIndex < m_ShadowResolutionRequests.Count; ++shadowRequestIndex)
                m_SortedShadowResolutionRequests[shadowRequestIndex] = m_ShadowResolutionRequests[shadowRequestIndex];
            // 所有尾部元素都标记 0
            for (int sortedArrayIndex = totalShadowResolutionRequestsCount; sortedArrayIndex < m_SortedShadowResolutionRequests.Length; ++sortedArrayIndex)
                m_SortedShadowResolutionRequests[sortedArrayIndex].requestedResolution = 0; // reset unused entries

            
            InsertionSort(m_SortedShadowResolutionRequests, 0, totalShadowResolutionRequestsCount);

            /*
                To avoid visual artifacts when there is not enough place in the atlas, 
                we remove shadow slices that would be allocated a too small resolution.
                When not using structured buffers, "m_AdditionalLightShadowSliceIndexTo_WorldShadowMatrix".Length 
                maps to "_AdditionalLightsWorldToShadow" in Shadows.hlsl
                In that case we have to limit its size because uniform buffers cannot be higher than 64kb for some platforms.

                Number of shadow slices that we will actually be able to fit in the shadow atlas without causing visual artifacts.
                ---
                因为 uniform buffers 不能大于 64kb, 放弃掉尾部的一些 shadow tiles(slices)
            */
            int totalShadowSlicesCount = m_UseStructuredBuffer ? 
                    totalShadowResolutionRequestsCount : 
                    Math.Min(totalShadowResolutionRequestsCount, 
                            MAX_PUNCTUAL_LIGHT_SHADOW_SLICES_IN_UBO // 16, 32, or 545
                    );  
            

            /*
                --------------------------------------------------------------------:
                Find biggest end index in m_SortedShadowResolutionRequests array, 
                under which all "shadow requests" can be allocated a big enough shadow atlas slot, to not cause rendering artifacts
                ---
                以下这段代码, 试图计算出一个 "预测缩放因子", 通过这个因子来缩放每一个 shadow tiles, 使得它们能放入 shadow atlas 中;
                同时, 如果 array尾部的 shadow tiles 在缩放后尺寸太小了, 失去了意义, 就把这个 light 的所有 shadow tiles 全部丢弃;
                (所以还会修改 "totalShadowSlicesCount" )
            */
            bool allShadowsAfterStartIndexHaveEnoughResolution = false;

            // 预测缩放因子, 是 2 的倍数, 大于 1, 作用于每一块 shadow tile(slice) 之上;
            // 以便把所有需要的 shadow tiles(slices) 放入 shadow atlas 体内;
            int estimatedScaleFactor = 1;
            while (!allShadowsAfterStartIndexHaveEnoughResolution && totalShadowSlicesCount > 0)
            {
                estimatedScaleFactor = EstimateScaleFactorNeededToFitAllShadowsInAtlas(
                    m_SortedShadowResolutionRequests, 
                    totalShadowSlicesCount, 
                    atlasWidth
                );

                // check if resolution of the least priority shadow slice request would be acceptable
                // array 中最后一块 tile 往往也是 分辨率最小的一块, 如果他在得到缩放后, 还能大于 tile 要求的最小值
                // 那么缩放工作就彻底完成了;
                if (m_SortedShadowResolutionRequests[totalShadowSlicesCount-1].requestedResolution >= 
                    estimatedScaleFactor * MinimalPunctualLightShadowResolution(m_SortedShadowResolutionRequests[totalShadowSlicesCount-1].softShadow)
                )
                    allShadowsAfterStartIndexHaveEnoughResolution = true;
                else 
                    // Skip shadow requests for this light ; their resolution is too small to look any good
                    // 把尾部的 关于这个光的所有 shadow tile(slice) 都放弃掉;
                    // 因为就算把它们放入 shadow atlas 中, 它们实际分配到的 分辨率也小的 看不清了
                    totalShadowSlicesCount -= GetPunctualLightShadowSlicesCount(
                                                    m_SortedShadowResolutionRequests[totalShadowSlicesCount-1].pointLightShadow ? 
                                                    LightType.Point : // 6 个
                                                    LightType.Spot    // 1 个
                                                );
            }


            // 当在上段运算之后, 确实删减了一部分 尾部 shadow tiles 时, 就会曝出 此 warning,
            // 提醒用户: 要么提高 shadow atlas 分辨率, 要么 减少场景中的 add light 数量;
            if (totalShadowSlicesCount < totalShadowResolutionRequestsCount)
            {
                if (!m_IssuedMessageAboutRemovedShadowSlices)
                {
                    Debug.LogWarning($"Too many additional punctual lights shadows to look good, URP removed {totalShadowResolutionRequestsCount - totalShadowSlicesCount } shadow maps to make the others fit in the shadow atlas. To avoid this, increase shadow atlas size, remove some shadowed lights, replace soft shadows by hard shadows ; or replace point lights by spot lights");
                    m_IssuedMessageAboutRemovedShadowSlices = true;  // Only output this once per shadow requests configuration
                }
            }

            // Reset entries that we cannot fit in the atlas
            // 把 array 尾部不需要的元素都标记 0
            for (int sortedArrayIndex = totalShadowSlicesCount; sortedArrayIndex < m_SortedShadowResolutionRequests.Length; ++sortedArrayIndex)
                m_SortedShadowResolutionRequests[sortedArrayIndex].requestedResolution = 0; 

            // Reset the reverse lookup array
            // 先把元素统统初始化为 -1; 
            for (int visibleLightIndex = 0; visibleLightIndex < m_VisibleLightIndexToSortedShadowResolutionRequestsFirstSliceIndex.Length; ++visibleLightIndex)
                m_VisibleLightIndexToSortedShadowResolutionRequestsFirstSliceIndex[visibleLightIndex] = -1;
            // Update the reverse lookup array (starting from the end of the array, 
            // in order to use index of slice#0 in case a same visibleLight has several shadowSlices)
            // ---
            // 遍历 每个 shadow tiles, 同时是反向遍历, 以此保证最终存入的一定是: 每个 light 的 第一个 shadow tile 的 idx 值;
            for (int sortedArrayIndex = totalShadowSlicesCount - 1; sortedArrayIndex >= 0; --sortedArrayIndex)
                m_VisibleLightIndexToSortedShadowResolutionRequestsFirstSliceIndex[m_SortedShadowResolutionRequests[sortedArrayIndex].visibleLightIndex] = sortedArrayIndex;


            // 将每个 shadow tile(slice) 真的放置到 shadowmap stlas 中去;
            // 计算好每个 tile 的 offset 和 实际分辨率, 写入 "m_SortedShadowResolutionRequests" 对应元素中:
            AtlasLayout(atlasWidth, totalShadowSlicesCount, estimatedScaleFactor);
    


            if (m_AdditionalLightsShadowSlices == null || m_AdditionalLightsShadowSlices.Length < totalShadowSlicesCount)
                m_AdditionalLightsShadowSlices = new ShadowSliceData[totalShadowSlicesCount];

            // m_AdditionalLightShadowSliceIndexTo_WorldShadowMatrix can be resized when using SSBO to pass shadow data (no size limitation)
            if (m_AdditionalLightShadowSliceIndexTo_WorldShadowMatrix == null ||
                (m_UseStructuredBuffer && (m_AdditionalLightShadowSliceIndexTo_WorldShadowMatrix.Length<totalShadowSlicesCount))
            ){
                m_AdditionalLightShadowSliceIndexTo_WorldShadowMatrix = new Matrix4x4[totalShadowSlicesCount];
            }

            // initialize _AdditionalShadowParams
            Vector4 defaultShadowParams = new Vector4(
                0, // shadowStrength; used in "RenderAdditionalShadowMapAtlas" to skip shadow map rendering for non-shadow-casting lights
                                    // 这函数没找到, 可能变名了
                0, 
                0, 
                -1  // perLightFirstShadowSliceIndex; used in Lighting shader to find if Additional light casts shadows
                    // 用 -1 来表示, 这个值没有被 改写过;
            );
            
            for (int i = 0; i < visibleLights.Length; ++i)
                m_AdditionalLightIndexToShadowParams[i] = defaultShadowParams;

            int validShadowCastingLightsCount = 0;
            bool supportsSoftShadows = renderingData.shadowData.supportsSoftShadows;
            int additionalLightIndex = -1;

            for (   int visibleLightIndex = 0; 
                    visibleLightIndex < visibleLights.Length && m_ShadowSliceToAdditionalLightIndex.Count < totalShadowSlicesCount; 
                    ++visibleLightIndex
            ){
                VisibleLight shadowLight = visibleLights[visibleLightIndex];

                // Skip main directional light as it is not packed into the shadow atlas
                if (visibleLightIndex == renderingData.lightData.mainLightIndex)
                {
                    m_VisibleLightIndexToAdditionalLightIndex[visibleLightIndex] = -1;
                    continue;
                }

                ++additionalLightIndex; // ForwardLights.SetupAdditionalLightConstants skips main Light and thus uses a different index for additional lights
                m_AdditionalLightIndexToVisibleLightIndex[additionalLightIndex] = visibleLightIndex;
                m_VisibleLightIndexToAdditionalLightIndex[visibleLightIndex] = additionalLightIndex;

                LightType lightType = shadowLight.lightType;
                int perLightShadowSlicesCount = GetPunctualLightShadowSlicesCount(lightType);// point光 6个, spot光 1个


                // shadow tiles 还是太多了, 放不下
                if (    (m_ShadowSliceToAdditionalLightIndex.Count + perLightShadowSlicesCount) > totalShadowSlicesCount && 
                        //  如果目标 light 不是平行光, 且开启了 shadow, 且 shadow Strength 非0, 则返回 true;
                        IsValidShadowCastingLight(ref renderingData.lightData, visibleLightIndex)
                ){
                    if (!m_IssuedMessageAboutShadowSlicesTooMany)
                    {
                        // This case can especially happen in Deferred, where there can be a high number of visibleLights
                        Debug.Log($"There are too many shadowed additional punctual lights active at the same time, URP will not render all the shadows. To ensure all shadows are rendered, reduce the number of shadowed additional lights in the scene ; make sure they are not active at the same time ; or replace point lights by spot lights (spot lights use less shadow maps than point lights).");
                        m_IssuedMessageAboutShadowSlicesTooMany = true; // Only output this once
                    }
                    break;
                }

                // shadowSliceIndex within the global array of all additional light shadow slices
                int perLightFirstShadowSliceIndex = m_ShadowSliceToAdditionalLightIndex.Count; 

                bool isValidShadowCastingLight = false;
                // 针对每个 light 的每个 tile
                for (int perLightShadowSlice = 0; perLightShadowSlice < perLightShadowSlicesCount; ++perLightShadowSlice)
                {
                    // shadowSliceIndex within the global array of all additional light shadow slices
                    int globalShadowSliceIndex = m_ShadowSliceToAdditionalLightIndex.Count; 

                    // 在 shadow distance 范围内, 光源可能没有遇见任何 shadow caster.
                    // 此函数将 检测到的 shadow casters 装入一个 AABB 盒, 从2号参数 输出 (此处我们不会用到)
                    // 同时,  若参数 b 不为空, 本函数返回 true. (表示本光源 确实投射出了投影)
                    // ----
                    // catilike: 2019.4 之后, 在处理平行光时, 即便没有捕捉到 shader caster, 此函数仍然返回 true
                    // 这可能失去了 一部分优化功能
                    bool lightRangeContainsShadowCasters = renderingData.cullResults.GetShadowCasterBounds(visibleLightIndex, out var shadowCastersBounds);
                    if (lightRangeContainsShadowCasters)
                    {
                        // We need to iterate the lights even though additional lights are disabled because
                        // cullResults.GetShadowCasterBounds() does the fence sync for the shadow culling jobs.
                        if (!renderingData.shadowData.supportsAdditionalLightShadows)
                            continue;

                        //  如果目标 light 不是平行光, 且开启了 shadow, 且 shadow Strength 非0, 则返回 true;
                        if (IsValidShadowCastingLight(ref renderingData.lightData, visibleLightIndex))
                        {
                            if (m_VisibleLightIndexToSortedShadowResolutionRequestsFirstSliceIndex[visibleLightIndex] == -1)
                            {
                                // We could not find place in the shadow atlas for shadow maps of this light Skip it.

                                // 这个 light 不渲染 shadow tile;
                            }
                            else if (lightType == LightType.Spot)
                            {
                                bool success = ShadowUtils.ExtractSpotLightMatrix(
                                    ref renderingData.cullResults,
                                    ref renderingData.shadowData,
                                    visibleLightIndex,
                                    out var shadowTransform,  // posWS -> posSTS 那个
                                    out m_AdditionalLightsShadowSlices[globalShadowSliceIndex].viewMatrix,
                                    out m_AdditionalLightsShadowSlices[globalShadowSliceIndex].projectionMatrix,
                                    out m_AdditionalLightsShadowSlices[globalShadowSliceIndex].splitData
                                );

                                if (success)
                                {
                                    m_ShadowSliceToAdditionalLightIndex.Add(additionalLightIndex);
                                    m_GlobalShadowSliceIndexToPerLightShadowSliceIndex.Add(perLightShadowSlice);
                                    var light = shadowLight.light;
                                    float shadowStrength = light.shadowStrength;
                                    float softShadows = (supportsSoftShadows && light.shadows==LightShadows.Soft) ? 1.0f : 0.0f;

                                    Vector4 shadowParams = new Vector4(
                                        shadowStrength,                         // shadow强度, 1表示光线全通过, 
                                        softShadows,                            // soft 为 1, hard 为 0;
                                        LightTypeIdentifierInShadowParams_Spot, // spot光 为 0, point光 为 1;
                                        perLightFirstShadowSliceIndex           // 每个 light 的第一个 shadow tile 的 idx
                                    );
                                    m_AdditionalLightShadowSliceIndexTo_WorldShadowMatrix[globalShadowSliceIndex] = shadowTransform;
                                    m_AdditionalLightIndexToShadowParams[additionalLightIndex] = shadowParams;
                                    isValidShadowCastingLight = true;
                                }
                            }
                            else if (lightType == LightType.Point)
                            {
                                var sliceResolution = m_SortedShadowResolutionRequests[m_VisibleLightIndexToSortedShadowResolutionRequestsFirstSliceIndex[visibleLightIndex]].allocatedResolution;
                                float fovBias = GetPointLightShadowFrustumFovBiasInDegrees(sliceResolution, (shadowLight.light.shadows == LightShadows.Soft));
                                // Note: the same fovBias will also be used to compute ShadowUtils.GetShadowBias

                                bool success = ShadowUtils.ExtractPointLightMatrix(ref renderingData.cullResults,
                                    ref renderingData.shadowData,
                                    visibleLightIndex,
                                    (CubemapFace)perLightShadowSlice,
                                    fovBias,
                                    out var shadowTransform, // posWS -> posSTS 那个
                                    out m_AdditionalLightsShadowSlices[globalShadowSliceIndex].viewMatrix,
                                    out m_AdditionalLightsShadowSlices[globalShadowSliceIndex].projectionMatrix,
                                    out m_AdditionalLightsShadowSlices[globalShadowSliceIndex].splitData);

                                if (success)
                                {
                                    m_ShadowSliceToAdditionalLightIndex.Add(additionalLightIndex);
                                    m_GlobalShadowSliceIndexToPerLightShadowSliceIndex.Add(perLightShadowSlice);
                                    var light = shadowLight.light;
                                    float shadowStrength = light.shadowStrength;
                                    float softShadows = (supportsSoftShadows && light.shadows == LightShadows.Soft) ? 1.0f : 0.0f;
                                    Vector4 shadowParams = new Vector4(
                                        shadowStrength,                         // shadow强度, 1表示光线全通过, 
                                        softShadows,                            // soft 为 1, hard 为 0;
                                        LightTypeIdentifierInShadowParams_Point,// spot光 为 0, point光 为 1;
                                        perLightFirstShadowSliceIndex           // 每个 light 的第一个 shadow tile 的 idx
                                    );
                                    m_AdditionalLightShadowSliceIndexTo_WorldShadowMatrix[globalShadowSliceIndex] = shadowTransform;
                                    m_AdditionalLightIndexToShadowParams[additionalLightIndex] = shadowParams;
                                    isValidShadowCastingLight = true;
                                }
                            }
                        }
                    }
                }

                if (isValidShadowCastingLight)
                    validShadowCastingLightsCount++;
            }

            // Lights that need to be rendered in the shadow map atlas
            if (validShadowCastingLightsCount == 0)
                return false;

            int shadowCastingLightsBufferCount = m_ShadowSliceToAdditionalLightIndex.Count;

            // Trim shadow atlas dimensions if possible (to avoid allocating texture space that will not be used)
            // 修建 shadow atlas, 去点边角部分;
            int atlasMaxX = 0;
            int atlasMaxY = 0;
            for (int sortedShadowResolutionRequestIndex = 0; sortedShadowResolutionRequestIndex < totalShadowSlicesCount; ++sortedShadowResolutionRequestIndex)
            {
                var shadowResolutionRequest = m_SortedShadowResolutionRequests[sortedShadowResolutionRequestIndex];
                atlasMaxX = Mathf.Max(atlasMaxX, shadowResolutionRequest.offsetX + shadowResolutionRequest.allocatedResolution);
                atlasMaxY = Mathf.Max(atlasMaxY, shadowResolutionRequest.offsetY + shadowResolutionRequest.allocatedResolution);
            }
            // ...but make sure we still use power-of-two dimensions (might perform better on some hardware)
            // 返回 大于等于 参数 的 2^x 值;
            m_ShadowmapWidth = Mathf.NextPowerOfTwo(atlasMaxX);
            m_ShadowmapHeight = Mathf.NextPowerOfTwo(atlasMaxY);

            float oneOverAtlasWidth = 1.0f / m_ShadowmapWidth;
            float oneOverAtlasHeight = 1.0f / m_ShadowmapHeight;

            Matrix4x4 sliceTransform;
            for (int globalShadowSliceIndex = 0; globalShadowSliceIndex < shadowCastingLightsBufferCount; ++globalShadowSliceIndex)
            {
                additionalLightIndex = m_ShadowSliceToAdditionalLightIndex[globalShadowSliceIndex];

                // We can skip the slice if strength is zero.
                if (Mathf.Approximately(m_AdditionalLightIndexToShadowParams[additionalLightIndex].x, 0.0f)  || 
                    // .w == -1 表示这个元素是空的, 它不参与渲染;
                    Mathf.Approximately(m_AdditionalLightIndexToShadowParams[additionalLightIndex].w, -1.0f))
                    continue;

                // 各种 idx
                int visibleLightIndex = m_AdditionalLightIndexToVisibleLightIndex[additionalLightIndex];
                int sortedShadowResolutionRequestFirstSliceIndex = m_VisibleLightIndexToSortedShadowResolutionRequestsFirstSliceIndex[visibleLightIndex];
                int perLightSliceIndex = m_GlobalShadowSliceIndexToPerLightShadowSliceIndex[globalShadowSliceIndex];
                var shadowResolutionRequest = m_SortedShadowResolutionRequests[sortedShadowResolutionRequestFirstSliceIndex + perLightSliceIndex];
                int sliceResolution = shadowResolutionRequest.allocatedResolution;

                sliceTransform = Matrix4x4.identity;

                // xy 分量 缩放因子, 从整张 atlas 缩小到具体的 tile(slice) 尺寸;
                sliceTransform.m00 = sliceResolution * oneOverAtlasWidth;
                sliceTransform.m11 = sliceResolution * oneOverAtlasHeight;

                m_AdditionalLightsShadowSlices[globalShadowSliceIndex].offsetX = shadowResolutionRequest.offsetX;
                m_AdditionalLightsShadowSlices[globalShadowSliceIndex].offsetY = shadowResolutionRequest.offsetY;
                m_AdditionalLightsShadowSlices[globalShadowSliceIndex].resolution = sliceResolution;

                // xy 分量的偏移值, [0,1] 区间
                sliceTransform.m03 = m_AdditionalLightsShadowSlices[globalShadowSliceIndex].offsetX * oneOverAtlasWidth;
                sliceTransform.m13 = m_AdditionalLightsShadowSlices[globalShadowSliceIndex].offsetY * oneOverAtlasHeight;

                // We bake scale and bias to each shadow map in the atlas in the matrix.
                // saves some instructions in shader.
                m_AdditionalLightShadowSliceIndexTo_WorldShadowMatrix[globalShadowSliceIndex] 
                    = sliceTransform * m_AdditionalLightShadowSliceIndexTo_WorldShadowMatrix[globalShadowSliceIndex];
            }

            return true;
        }//  函数完__




        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)//  读完__
        {
            m_AdditionalLightsShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(
                m_ShadowmapWidth,     // shadow atlas 最终分辨率: w
                m_ShadowmapHeight,    // shadow atlas 最终分辨率: h
                k_ShadowmapBufferBits // texel 存储精度 16-bits
            );
            ConfigureTarget(new RenderTargetIdentifier(m_AdditionalLightsShadowmapTexture));// colorAttachment
            ConfigureClear(ClearFlag.All, Color.black);
        }



        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)//  读完__
        {
            if (renderingData.shadowData.supportsAdditionalLightShadows)
                RenderAdditionalShadowmapAtlas(
                    ref context, ref renderingData.cullResults, ref renderingData.lightData, ref renderingData.shadowData
                );
        }



        public override void OnCameraCleanup(CommandBuffer cmd)//  读完__
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (m_AdditionalLightsShadowmapTexture)
            {
                RenderTexture.ReleaseTemporary(m_AdditionalLightsShadowmapTexture);
                m_AdditionalLightsShadowmapTexture = null;
            }
        }

        // Get the "additional light index" (used to index arrays _AdditionalLightsPosition, _AdditionalShadowParams, ...) 
        // from the "global" visible light index Function called by Deferred Renderer
        public int GetShadowLightIndexFromLightIndex(int visibleLightIndex)//   读完__
        {
            if (visibleLightIndex < 0 || visibleLightIndex >= m_VisibleLightIndexToAdditionalLightIndex.Length)
                return -1;

            return m_VisibleLightIndexToAdditionalLightIndex[visibleLightIndex];
        }


        void Clear()// 读完__
        {
            m_ShadowSliceToAdditionalLightIndex.Clear();
            m_GlobalShadowSliceIndexToPerLightShadowSliceIndex.Clear();
            m_AdditionalLightsShadowmapTexture = null;
        }



        void RenderAdditionalShadowmapAtlas( //  读完__
                                ref ScriptableRenderContext context, 
                                ref CullingResults cullResults, 
                                ref LightData lightData, 
                                ref ShadowData shadowData
        ){
            NativeArray<VisibleLight> visibleLights = lightData.visibleLights;

            bool additionalLightHasSoftShadows = false;
            // NOTE: Do NOT mix ProfilingScope with named CommandBuffers i.e. CommandBufferPool.Get("name").
            // Currently there's an issue which results in mismatched markers.
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.AdditionalLightsShadow)))
            {
                bool anyShadowSliceRenderer = false;
                int shadowSlicesCount = m_ShadowSliceToAdditionalLightIndex.Count;
                for (int globalShadowSliceIndex = 0; globalShadowSliceIndex < shadowSlicesCount; ++globalShadowSliceIndex)
                {
                    int additionalLightIndex = m_ShadowSliceToAdditionalLightIndex[globalShadowSliceIndex];

                    // we do the shadow strength check here again here because we might have zero strength for non-shadow-casting lights.
                    // In that case we need the shadow data buffer but we can skip rendering them to shadowmap.
                    if (Mathf.Approximately(m_AdditionalLightIndexToShadowParams[additionalLightIndex].x, 0.0f)  || 
                        // .w == -1 表示这个元素是空的, 它不参与渲染;
                        Mathf.Approximately(m_AdditionalLightIndexToShadowParams[additionalLightIndex].w, -1.0f))
                        continue;

                    int visibleLightIndex = m_AdditionalLightIndexToVisibleLightIndex[additionalLightIndex];

                    VisibleLight shadowLight = visibleLights[visibleLightIndex];

                    ShadowSliceData shadowSliceData = m_AdditionalLightsShadowSlices[globalShadowSliceIndex];

                    var settings = new ShadowDrawingSettings(cullResults, visibleLightIndex);
                    settings.splitData = shadowSliceData.splitData;

                    // 得到 ( depthBias, normalBias, 0, 0 )
                    Vector4 shadowBias = ShadowUtils.GetShadowBias(
                        ref shadowLight, 
                        visibleLightIndex,
                        ref shadowData, 
                        shadowSliceData.projectionMatrix, 
                        shadowSliceData.resolution
                    );

                    // 向 shader 写入: "_ShadowBias", "_LightDirection", "_LightPosition";
                    ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref shadowLight, shadowBias);

                    CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.CastingPunctualLightShadow, true);//"_CASTING_PUNCTUAL_LIGHT_SHADOW"

                    // 针对单个 shadow tile, 
                    // 设置 "Const Depth Bias", "Slope Bias", viewport, proj矩阵, view矩阵, 
                    // 然后执行真正的 "context.DrawShadows()";
                    ShadowUtils.RenderShadowSlice(
                        cmd, 
                        ref context, 
                        ref shadowSliceData, 
                        ref settings
                    );

                    additionalLightHasSoftShadows |= shadowLight.light.shadows == LightShadows.Soft;
                    anyShadowSliceRenderer = true;
                }

                // We share soft shadow settings for main light and additional lights to save keywords.
                // So we check here if pipeline supports soft shadows and either main light or any additional light has soft shadows
                // to enable the keyword.
                // TODO: In PC and Consoles we can upload shadow data per light and branch on shader. That will be more likely way faster.
                bool mainLightHasSoftShadows =  shadowData.supportsMainLightShadows &&
                                                lightData.mainLightIndex != -1 &&
                                                visibleLights[lightData.mainLightIndex].light.shadows == LightShadows.Soft;

                bool softShadows =  shadowData.supportsSoftShadows &&
                                    (mainLightHasSoftShadows || additionalLightHasSoftShadows);

                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.AdditionalLightShadows, anyShadowSliceRenderer);//"_ADDITIONAL_LIGHT_SHADOWS"
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, softShadows);//"_SHADOWS_SOFT"

                if (anyShadowSliceRenderer)
                    SetupAdditionalLightsShadowReceiverConstants(cmd, ref shadowData, softShadows);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }//  函数完__



        /*
            Set constant buffer data that will be used during the lighting/shadowing pass    
            --
            设置: shadow receiver 中使用的 const 数据;
        */
        void SetupAdditionalLightsShadowReceiverConstants( //   读完__
                                                        CommandBuffer cmd, 
                                                        ref ShadowData shadowData, 
                                                        bool softShadows
        ){
            float invShadowAtlasWidth = 1.0f / shadowData.additionalLightsShadowmapWidth;
            float invShadowAtlasHeight = 1.0f / shadowData.additionalLightsShadowmapHeight;
            float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
            float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;

            cmd.SetGlobalTexture(m_AdditionalLightsShadowmap.id, // "_AdditionalLightsShadowmapTexture"
                                m_AdditionalLightsShadowmapTexture);

            // set shadow fade (shadow distance) parameters
            ShadowUtils.SetupShadowReceiverConstantBuffer(cmd, m_MainLightShadowParams);// "_MainLightShadowParams"

            if (m_UseStructuredBuffer)// 暂为 false
            {   
                /*    tpr
                // per-light data
                var shadowParamsBuffer = ShaderData.instance.GetAdditionalLightShadowParamsStructuredBuffer(m_AdditionalLightIndexToShadowParams.Length);
                shadowParamsBuffer.SetData(m_AdditionalLightIndexToShadowParams);
                cmd.SetGlobalBuffer(m_AdditionalShadowParams_SSBO, shadowParamsBuffer);

                // per-shadow-slice data
                var shadowSliceMatricesBuffer = ShaderData.instance.GetAdditionalLightShadowSliceMatricesStructuredBuffer(m_AdditionalLightShadowSliceIndexTo_WorldShadowMatrix.Length);
                shadowSliceMatricesBuffer.SetData(m_AdditionalLightShadowSliceIndexTo_WorldShadowMatrix);
                cmd.SetGlobalBuffer(m_AdditionalLightsWorldToShadow_SSBO, shadowSliceMatricesBuffer);
                */
            }
            else
            {
                cmd.SetGlobalVectorArray(AdditionalShadowsConstantBuffer._AdditionalShadowParams, //"_AdditionalShadowParams"
                    m_AdditionalLightIndexToShadowParams); // per-light data

                cmd.SetGlobalMatrixArray(AdditionalShadowsConstantBuffer._AdditionalLightsWorldToShadow, //"_AdditionalLightsWorldToShadow"
                        m_AdditionalLightShadowSliceIndexTo_WorldShadowMatrix); // per-shadow-slice data
            }

            if (softShadows)
            {
                if (m_SupportsBoxFilterForShadows)// "移动平台 和 switch" 设为 true,
                {
                    cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowOffset0,//"_AdditionalShadowOffset0"
                        new Vector4(-invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, 0.0f, 0.0f));

                    cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowOffset1,//"_AdditionalShadowOffset1"
                        new Vector4(invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, 0.0f, 0.0f));

                    cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowOffset2,//"_AdditionalShadowOffset2"
                        new Vector4(-invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, 0.0f, 0.0f));

                    cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowOffset3,//"_AdditionalShadowOffset3"
                        new Vector4(invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, 0.0f, 0.0f));
                }
                
                /*
                    Currently only used when !SHADER_API_MOBILE but risky to not set them as it's generic
                    enough so custom shaders might use it.
                    ---
                    目前仅被用于 !SHADER_API_MOBILE 时, 但不设置它们存在风险, 因为这个数据比较通用, castom shaders 可能用到它;
                */
                cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowmapSize, //"_AdditionalShadowmapSize"
                    new Vector4(
                        invShadowAtlasWidth, 
                        invShadowAtlasHeight,
                        shadowData.additionalLightsShadowmapWidth, 
                        shadowData.additionalLightsShadowmapHeight
                    )
                );
            }
        }//  函数完__


        
        //  如果目标 light 不是平行光, 且开启了 shadow, 且 shadow Strength 非0, 则返回 true;
        bool IsValidShadowCastingLight( //   读完__
                                    ref LightData lightData, 
                                    int i // light 在 visibleLights 中的 idx
        ){
            if (i == lightData.mainLightIndex)
                return false;

            VisibleLight shadowLight = lightData.visibleLights[i];

            // Directional and light shadows are not supported in the shadow map atlas
            if (shadowLight.lightType == LightType.Directional)
                return false;

            Light light = shadowLight.light;
            return  light != null && 
                    light.shadows != LightShadows.None && 
                    !Mathf.Approximately(light.shadowStrength, 0.0f);
        }//  函数完__
    }
}
