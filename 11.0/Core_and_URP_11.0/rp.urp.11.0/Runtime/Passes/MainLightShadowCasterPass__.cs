using System;

namespace UnityEngine.Rendering.Universal.Internal
{
    /// <summary>
    /// Renders a shadow map for the main Light.
    /// </summary>
    public class MainLightShadowCasterPass //MainLightShadowCasterPass__
        : ScriptableRenderPass
    {
        private static class MainLightShadowConstantBuffer
        {
            public static int _WorldToShadow;//"_MainLightWorldToShadow"
            public static int _CascadeShadowSplitSpheres0;//"_CascadeShadowSplitSpheres0"
            public static int _CascadeShadowSplitSpheres1;//"_CascadeShadowSplitSpheres1"
            public static int _CascadeShadowSplitSpheres2;//"_CascadeShadowSplitSpheres2"
            public static int _CascadeShadowSplitSpheres3;//"_CascadeShadowSplitSpheres3"
            public static int _CascadeShadowSplitSphereRadii;//"_CascadeShadowSplitSphereRadii"
            public static int _ShadowOffset0;//"_MainLightShadowOffset0"
            public static int _ShadowOffset1;//"_MainLightShadowOffset1"
            public static int _ShadowOffset2;//"_MainLightShadowOffset2"
            public static int _ShadowOffset3;//"_MainLightShadowOffset3"
            public static int _ShadowmapSize;//"_MainLightShadowmapSize"
        }

        const int k_MaxCascades = 4; // cascade 最多能分成几层
        const int k_ShadowmapBufferBits = 16; // texel 存储精度 16-bits

        
        /*
            计算 shadow 相关的数据: 强度, 距离, 衰减计算组件 (需要被传入 shader 中)
            -- main light 使用 xyzw 4个数据;
            -- add light 只是用 zw 分量数据;

                x: 阴影的强度, [0,1], 为 0 时阴影完全消失, 为 1 时阴影最强烈;
                y: 1:支持 soft shadow, 0: 不支持
                z: 计算 阴影衰减 的组件: oneOverFadeDist
                w: 计算 阴影衰减 的组件: minusStartFade
        */
        Vector4 m_MainLightShadowParams;

        /*
            shadowmap atlas 分辨率(pix)
                举例: shadowmap resolution = 4096, 
                当 cascade count = 2时,  一定会分配一张: 4096x2048 的矩形 map, 竖着切一刀分为左右两个 tiles;
                当 cascade count = 1/3/4 时, 最终会分配一张 4096x4096 的 map, 然后在内部分割;
        */
        int m_ShadowmapWidth;
        int m_ShadowmapHeight;

        int m_ShadowCasterCascadesCount;// cascade 有几层, 区间[1,4]; (比如: 4个重叠的球体) 
        bool m_SupportsBoxFilterForShadows;// "移动平台 和 switch" 设为 true, 

        RenderTargetHandle m_MainLightShadowmap;// "_MainLightShadowmapTexture", 只是个 handle
        RenderTexture m_MainLightShadowmapTexture; // rt本体


        // 前面填 cascade count 个 转换矩阵 posWS -> posSTS (shadow texture: tile):
        // 尾部矩阵全写 no-op matrix;
        Matrix4x4[] m_MainLightShadowMatrices;// 共5个元素
        ShadowSliceData[] m_CascadeSlices;
        Vector4[] m_CascadeSplitDistances;// xyz: cull sphere posWS;  w: sphere radius

        ProfilingSampler m_ProfilingSetupSampler = new ProfilingSampler("Setup Main Shadowmap");


        /*
            构造函数
        */
        public MainLightShadowCasterPass(RenderPassEvent evt)//   读完__
        {
            base.profilingSampler = new ProfilingSampler(nameof(MainLightShadowCasterPass));
            renderPassEvent = evt; // base class 中的

            m_MainLightShadowMatrices = new Matrix4x4[k_MaxCascades + 1];// 5个元素
            m_CascadeSlices = new ShadowSliceData[k_MaxCascades];// 4 个元素
            m_CascadeSplitDistances = new Vector4[k_MaxCascades];// 4 个元素

            MainLightShadowConstantBuffer._WorldToShadow = Shader.PropertyToID("_MainLightWorldToShadow");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres0 = Shader.PropertyToID("_CascadeShadowSplitSpheres0");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres1 = Shader.PropertyToID("_CascadeShadowSplitSpheres1");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres2 = Shader.PropertyToID("_CascadeShadowSplitSpheres2");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres3 = Shader.PropertyToID("_CascadeShadowSplitSpheres3");
            MainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii = Shader.PropertyToID("_CascadeShadowSplitSphereRadii");
            MainLightShadowConstantBuffer._ShadowOffset0 = Shader.PropertyToID("_MainLightShadowOffset0");
            MainLightShadowConstantBuffer._ShadowOffset1 = Shader.PropertyToID("_MainLightShadowOffset1");
            MainLightShadowConstantBuffer._ShadowOffset2 = Shader.PropertyToID("_MainLightShadowOffset2");
            MainLightShadowConstantBuffer._ShadowOffset3 = Shader.PropertyToID("_MainLightShadowOffset3");
            MainLightShadowConstantBuffer._ShadowmapSize = Shader.PropertyToID("_MainLightShadowmapSize");

            m_MainLightShadowmap.Init("_MainLightShadowmapTexture");

            m_SupportsBoxFilterForShadows = Application.isMobilePlatform || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Switch;
        }//  函数完__


        
        /// <param name="renderingData"></param>
        /// <returns> main light 是否支持 shadow </returns>
        public bool Setup(ref RenderingData renderingData)//   读完__
        {
            using var profScope = new ProfilingScope(null, m_ProfilingSetupSampler);

            if (!renderingData.shadowData.supportsMainLightShadows)
                return false;

            Clear();
            int shadowLightIndex = renderingData.lightData.mainLightIndex;
            // 如果不存在 main light
            if (shadowLightIndex == -1)
                return false;

            // 本次要处理的 light, (其实就是 main light)
            VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
            Light light = shadowLight.light;
            // Light inspector 上选择不渲染 shadow
            if (light.shadows == LightShadows.None)
                return false;

            if (shadowLight.lightType != LightType.Directional)
            {
                Debug.LogWarning("Only directional lights are supported as main light.");
            }

            // 在 shadow distance 范围内, 光源可能没有遇见任何 shadow caster.
            // 此函数将 检测到的 shadow casters 装入一个 AABB 盒, 从参数 bounds 输出 (此处我们不会用到)
            // 同时,  若参数 b 不为空, 本函数返回 true. (表示本光源 确实投射出了投影)
            // ----
            // catilike: 2019.4 之后, 在处理平行光时, 即便没有捕捉到 shader caster, 此函数仍然返回 true
            // 这可能失去了 一部分优化功能
            Bounds bounds;
            if (!renderingData.cullResults.GetShadowCasterBounds(shadowLightIndex, out bounds))
                return false;

            // cascade 有几层, 区间[1,4]; (比如: 4个重叠的球体) 
            m_ShadowCasterCascadesCount = renderingData.shadowData.mainLightShadowCascadesCount;

            // shadowmap tile 的分辨率; (边长,pix)
            int shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(
                renderingData.shadowData.mainLightShadowmapWidth,  // 其实就是 shadow resolution
                renderingData.shadowData.mainLightShadowmapHeight, // 其实就是 shadow resolution
                m_ShadowCasterCascadesCount   // 也表达: 将 shadowmap atlas 分成几块 tiles, (不是切割几刀); 区间[1,4]
            );

            /*
                举例: shadowmap resolution = 4096, 
                当 cascade count = 2时,  一定会分配一张: 4096x2048 的矩形 map, 竖着切一刀分为左右两个 tiles;
                当 cascade count = 1/3/4 时, 最终会分配一张 4096x4096 的 map, 然后在内部分割;
            */
            m_ShadowmapWidth = renderingData.shadowData.mainLightShadowmapWidth;
            m_ShadowmapHeight = (m_ShadowCasterCascadesCount == 2) ?
                renderingData.shadowData.mainLightShadowmapHeight >> 1 :
                renderingData.shadowData.mainLightShadowmapHeight;

            for (int cascadeIndex = 0; cascadeIndex < m_ShadowCasterCascadesCount; ++cascadeIndex)
            {
                bool success = ShadowUtils.ExtractDirectionalLightMatrix(
                    ref renderingData.cullResults, 
                    ref renderingData.shadowData,
                    shadowLightIndex, 
                    cascadeIndex, 
                    m_ShadowmapWidth, 
                    m_ShadowmapHeight, 
                    shadowResolution, 
                    light.shadowNearPlane,
                    out m_CascadeSplitDistances[cascadeIndex], // xyz: sphere posWS;  w: sphere radius
                    out m_CascadeSlices[cascadeIndex]
                );

                if (!success)
                    return false;
            }

            /*
                计算 shadow 相关的数据: 强度, 距离, 衰减计算组件 (需要被传入 shader 中)
                -- main light 使用 xyzw 4个数据;
                -- add light 只是用 zw 分量数据;

                    x: 阴影的强度, [0,1], 为 0 时阴影完全消失, 为 1 时阴影最强烈;
                    y: 1:支持 soft shadow, 0: 不支持
                    z: 计算 阴影衰减 的组件: oneOverFadeDist
                    w: 计算 阴影衰减 的组件: minusStartFade
            */
            m_MainLightShadowParams = ShadowUtils.GetMainLightShadowParams(ref renderingData);

            return true;
        }//  函数完__



        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)// 读完__
        {
            m_MainLightShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(
                m_ShadowmapWidth,
                m_ShadowmapHeight, 
                k_ShadowmapBufferBits// 16-bits
            );
            ConfigureTarget(new RenderTargetIdentifier(m_MainLightShadowmapTexture));// colorAttachment
            ConfigureClear(ClearFlag.All, Color.black);
        }



        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)//   读完__
        {
            RenderMainLightCascadeShadowmap(
                ref context, ref renderingData.cullResults, ref renderingData.lightData, ref renderingData.shadowData
            );
        }


        /// <inheritdoc/>
        public override void OnCameraCleanup(CommandBuffer cmd) //   读完__
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (m_MainLightShadowmapTexture)
            {
                RenderTexture.ReleaseTemporary(m_MainLightShadowmapTexture);
                m_MainLightShadowmapTexture = null;
            }
        }


        void Clear()//   读完__
        {
            m_MainLightShadowmapTexture = null;

            for (int i = 0; i < m_MainLightShadowMatrices.Length; ++i)
                m_MainLightShadowMatrices[i] = Matrix4x4.identity;

            for (int i = 0; i < m_CascadeSplitDistances.Length; ++i)
                m_CascadeSplitDistances[i] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

            for (int i = 0; i < m_CascadeSlices.Length; ++i)
                m_CascadeSlices[i].Clear();
        }



        void RenderMainLightCascadeShadowmap( //   读完__
                                            ref ScriptableRenderContext context, 
                                            ref CullingResults cullResults, 
                                            ref LightData lightData, 
                                            ref ShadowData shadowData
        ){
            int shadowLightIndex = lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return;

            VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];

            // NOTE: Do NOT mix ProfilingScope with named CommandBuffers i.e. CommandBufferPool.Get("name").
            // Currently there's an issue which results in mismatched markers.
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.MainLightShadow)))
            {
                var settings = new ShadowDrawingSettings(cullResults, shadowLightIndex);

                for (int cascadeIndex = 0; cascadeIndex < m_ShadowCasterCascadesCount; ++cascadeIndex)
                {
                    settings.splitData = m_CascadeSlices[cascadeIndex].splitData;

                    // 得到 ( depthBias, normalBias, 0, 0 )
                    Vector4 shadowBias = ShadowUtils.GetShadowBias(
                        ref shadowLight, 
                        shadowLightIndex, 
                        ref shadowData, 
                        m_CascadeSlices[cascadeIndex].projectionMatrix, 
                        m_CascadeSlices[cascadeIndex].resolution
                    );

                    // 向 shader 写入: "_ShadowBias", "_LightDirection", "_LightPosition";
                    ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref shadowLight, shadowBias);

                    CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.CastingPunctualLightShadow, false);//"_CASTING_PUNCTUAL_LIGHT_SHADOW"

                    // 针对单个 shadow tile, 
                    // 设置 "Const Depth Bias", "Slope Bias", viewport, proj矩阵, view矩阵, 
                    // 然后执行真正的 "context.DrawShadows()";
                    ShadowUtils.RenderShadowSlice(
                        cmd, 
                        ref context, 
                        ref m_CascadeSlices[cascadeIndex],
                        ref settings, 
                        m_CascadeSlices[cascadeIndex].projectionMatrix, 
                        m_CascadeSlices[cascadeIndex].viewMatrix
                    );
                }

                bool softShadows = shadowLight.light.shadows==LightShadows.Soft && shadowData.supportsSoftShadows;

                // main light shadowmap 无 cascade
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows,//"_MAIN_LIGHT_SHADOWS"
                        shadowData.mainLightShadowCascadesCount == 1);

                // main light shadowmap 多级 cascade
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowCascades, //"_MAIN_LIGHT_SHADOWS_CASCADE"
                        shadowData.mainLightShadowCascadesCount > 1);

                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, //"_SHADOWS_SOFT"
                        softShadows);

                SetupMainLightShadowReceiverConstants(cmd, shadowLight, shadowData.supportsSoftShadows);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }//  函数完__


        /*
            设置: shadow receiver 中使用的 const 数据;
        */
        void SetupMainLightShadowReceiverConstants( //    读完__
                                                    CommandBuffer cmd, 
                                                    VisibleLight shadowLight, 
                                                    bool supportsSoftShadows
        ){
            int cascadeCount = m_ShadowCasterCascadesCount;// cascade 有几层, 区间[1,4]; (比如: 4个重叠的球体) 
            for (int i = 0; i < cascadeCount; ++i)
                m_MainLightShadowMatrices[i] = m_CascadeSlices[i].shadowTransform; // posWS -> posSTS 那个

            /*
                We setup and additional a no-op WorldToShadow matrix in the last index
                because the "ComputeCascadeIndex()" function in Shadows.hlsl can return an index
                out of bounds. (position not inside any cascade) and we want to avoid branching
                ---
                为了避免 hlsl 代码中出现 分支语句, 做的设置;

                        0,   0,   0,   0
                        0,   0,   0,   0
                        0,   0, 1/0,   0
                        0,   0,   0,   0
            */
            Matrix4x4 noOpShadowMatrix = Matrix4x4.zero;
            noOpShadowMatrix.m22 = (SystemInfo.usesReversedZBuffer) ? 1.0f : 0.0f;//设置的是 近平面的 depth;
            // 尾部都填这个矩阵
            for (int i = cascadeCount; i <= k_MaxCascades; ++i)
                m_MainLightShadowMatrices[i] = noOpShadowMatrix;

            float invShadowAtlasWidth = 1.0f / m_ShadowmapWidth;
            float invShadowAtlasHeight = 1.0f / m_ShadowmapHeight;
            float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
            float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;

            cmd.SetGlobalTexture(m_MainLightShadowmap.id, m_MainLightShadowmapTexture);// "_MainLightShadowmapTexture"

            cmd.SetGlobalMatrixArray(MainLightShadowConstantBuffer._WorldToShadow, //"_MainLightWorldToShadow"
                        m_MainLightShadowMatrices);

            ShadowUtils.SetupShadowReceiverConstantBuffer(cmd, m_MainLightShadowParams);// "_MainLightShadowParams"


            if (m_ShadowCasterCascadesCount > 1)// cascade 有几层, 区间[1,4]; (比如: 4个重叠的球体) 
            {
                // xyz: sphere posWS;  w: sphere radius

                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres0,//"_CascadeShadowSplitSpheres0"
                    m_CascadeSplitDistances[0]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres1,//"_CascadeShadowSplitSpheres1"
                    m_CascadeSplitDistances[1]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres2,//"_CascadeShadowSplitSpheres2"
                    m_CascadeSplitDistances[2]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres3,//"_CascadeShadowSplitSpheres3"
                    m_CascadeSplitDistances[3]);

                // radius^2
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii,//"_CascadeShadowSplitSphereRadii"
                    new Vector4(
                        m_CascadeSplitDistances[0].w * m_CascadeSplitDistances[0].w,
                        m_CascadeSplitDistances[1].w * m_CascadeSplitDistances[1].w,
                        m_CascadeSplitDistances[2].w * m_CascadeSplitDistances[2].w,
                        m_CascadeSplitDistances[3].w * m_CascadeSplitDistances[3].w)
                );
            }

            /*
                Inside shader, soft shadows are controlled through global keyword.
                If any "additional light" has soft shadows, it will force soft shadows on main light too.
                As it is not trivial finding out which additional light has soft shadows,  (找出哪些 add lights 具有 soft shadow 并不容易)
                we will pass main light properties if soft shadows are supported.
                This workaround will be removed once we will support soft shadows per light.
                ----
                一旦我们为每个 光都支持 soft shadow 设定, 这个 临时办法就会被取消掉;
            */
            if (supportsSoftShadows)
            {
                if (m_SupportsBoxFilterForShadows)// "移动平台 和 switch" 设为 true, 
                {
                    cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowOffset0,//"_MainLightShadowOffset0"
                        new Vector4(-invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, 0.0f, 0.0f));

                    cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowOffset1,//"_MainLightShadowOffset1"
                        new Vector4(invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, 0.0f, 0.0f));

                    cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowOffset2,//"_MainLightShadowOffset2"
                        new Vector4(-invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, 0.0f, 0.0f));

                    cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowOffset3,//"_MainLightShadowOffset3"
                        new Vector4(invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, 0.0f, 0.0f));
                }

                /*
                    Currently only used when !SHADER_API_MOBILE 
                    but risky to not set them as it's generic enough so custom shaders might use it.
                    ---
                    目前仅被用于 !SHADER_API_MOBILE 时, 但不设置它们存在风险, 因为这个数据比较通用, castom shaders 可能用到它;
                */
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowmapSize, //"_MainLightShadowmapSize"
                    new Vector4(
                        invShadowAtlasWidth,
                        invShadowAtlasHeight,
                        m_ShadowmapWidth, 
                        m_ShadowmapHeight
                    )
                );
            }
        }//  函数完__
    };
}
