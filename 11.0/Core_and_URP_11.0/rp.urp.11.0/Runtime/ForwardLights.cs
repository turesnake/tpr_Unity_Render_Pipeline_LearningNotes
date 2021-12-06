using UnityEngine.Experimental.GlobalIllumination;
using Unity.Collections;

namespace UnityEngine.Rendering.Universal.Internal
{
    /*
        Computes and submits lighting data to the GPU.
    */
    public class ForwardLights//ForwardLights__RR
    {
        static class LightConstantBuffer
        {
            // DeferredLights.LightConstantBuffer also refers to the same ShaderPropertyID
            // - TODO: move this definition to a common location shared by other UniversalRP classes
            public static int _MainLightPosition; //"_MainLightPosition"

            // DeferredLights.LightConstantBuffer also refers to the same ShaderPropertyID
            // - TODO: move this definition to a common location shared by other UniversalRP classes
            public static int _MainLightColor; //"_MainLightColor"  

            // Deferred?
            public static int _MainLightOcclusionProbesChannel; //"_MainLightOcclusionProbes"   

            public static int _AdditionalLightsCount;//"_AdditionalLightsCount"
            public static int _AdditionalLightsPosition;//"_AdditionalLightsPosition"
            public static int _AdditionalLightsColor;//"_AdditionalLightsColor"
            public static int _AdditionalLightsAttenuation;//"_AdditionalLightsAttenuation"
            public static int _AdditionalLightsSpotDir;//"_AdditionalLightsSpotDir"
            public static int _AdditionalLightOcclusionProbeChannel;//"_AdditionalLightsOcclusionProbes"
        }

        int m_AdditionalLightsBufferId;//"_AdditionalLightsBuffer"
        int m_AdditionalLightsIndicesId;//"_AdditionalLightsIndices"

        const string k_SetupLightConstants = "Setup Light Constants";
        private static readonly ProfilingSampler m_ProfilingSampler = new ProfilingSampler(k_SetupLightConstants);

        // enum: None, ShadowMask, Subtractive; 默认为 None, 
        // None 也包含 "MixedLightingMode.IndirectOnly" 的意思: "Mixed光 只烘焙 间接光"
        MixedLightingSetup m_MixedLightingSetup;

        Vector4[] m_AdditionalLightPositions;
        Vector4[] m_AdditionalLightColors;
        Vector4[] m_AdditionalLightAttenuations;
        Vector4[] m_AdditionalLightSpotDirections;
        Vector4[] m_AdditionalLightOcclusionProbeChannels;

        bool m_UseStructuredBuffer;// 暂为 false

        public ForwardLights()// 读完__
        {
            m_UseStructuredBuffer = RenderingUtils.useStructuredBuffer;// 暂为 false

            LightConstantBuffer._MainLightPosition = Shader.PropertyToID("_MainLightPosition");
            LightConstantBuffer._MainLightColor = Shader.PropertyToID("_MainLightColor");
            LightConstantBuffer._MainLightOcclusionProbesChannel = Shader.PropertyToID("_MainLightOcclusionProbes");
            LightConstantBuffer._AdditionalLightsCount = Shader.PropertyToID("_AdditionalLightsCount");

            if (m_UseStructuredBuffer)
            {
                m_AdditionalLightsBufferId = Shader.PropertyToID("_AdditionalLightsBuffer");
                m_AdditionalLightsIndicesId = Shader.PropertyToID("_AdditionalLightsIndices");
            }
            else
            {
                LightConstantBuffer._AdditionalLightsPosition = Shader.PropertyToID("_AdditionalLightsPosition");
                LightConstantBuffer._AdditionalLightsColor = Shader.PropertyToID("_AdditionalLightsColor");
                LightConstantBuffer._AdditionalLightsAttenuation = Shader.PropertyToID("_AdditionalLightsAttenuation");
                LightConstantBuffer._AdditionalLightsSpotDir = Shader.PropertyToID("_AdditionalLightsSpotDir");
                LightConstantBuffer._AdditionalLightOcclusionProbeChannel = Shader.PropertyToID("_AdditionalLightsOcclusionProbes");

                int maxLights = UniversalRenderPipeline.maxVisibleAdditionalLights; // 可能为: 16, 32, 256
                m_AdditionalLightPositions = new Vector4[maxLights];
                m_AdditionalLightColors = new Vector4[maxLights];
                m_AdditionalLightAttenuations = new Vector4[maxLights];
                m_AdditionalLightSpotDirections = new Vector4[maxLights];
                m_AdditionalLightOcclusionProbeChannels = new Vector4[maxLights];
            }
        }// 函数完__


        public void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            int additionalLightsCount = renderingData.lightData.additionalLightsCount;
            bool additionalLightsPerVertex = renderingData.lightData.shadeAdditionalLightsPerVertex;// add光 mode 是 逐顶点吗 ?
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                SetupShaderLightConstants(cmd, ref renderingData);

                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.AdditionalLightsVertex,
                    additionalLightsCount > 0 && additionalLightsPerVertex);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.AdditionalLightsPixel,
                    additionalLightsCount > 0 && !additionalLightsPerVertex);

                bool isShadowMask = renderingData.lightData.supportsMixedLighting && m_MixedLightingSetup == MixedLightingSetup.ShadowMask;
                bool isShadowMaskAlways = isShadowMask && QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask;
                bool isSubtractive = renderingData.lightData.supportsMixedLighting && m_MixedLightingSetup == MixedLightingSetup.Subtractive;
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.LightmapShadowMixing, isSubtractive || isShadowMaskAlways);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.ShadowsShadowMask, isShadowMask);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MixedLightingSubtractive, isSubtractive); // Backward compatibility
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }// 函数完__


        /*
            一次处理一个光源的: lights[lightIndex], 初始化它的一部分数据;
        */
        /// <param name="lights"></param>
        /// <param name="lightIndex"> light 在 visibleLights 中的 idx </param>
        /// <param name="lightPos"></param>
        /// <param name="lightColor"></param>
        /// <param name="lightAttenuation"></param>
        /// <param name="lightSpotDir"></param>
        /// <param name="lightOcclusionProbeChannel"> 
        ///         一个 "通道筛选器", 类似 (0,0,1,0), 可以从一个 float4 中提取出对应分量的数值; 
        ///         算是 array idx 的另类版本;  真正的 shadowmask 数据 并不存储于此;
        /// </param>
        void InitializeLightConstants( //    读完__
                                    NativeArray<VisibleLight> lights, 
                                    int lightIndex,  
                                    out Vector4 lightPos, 
                                    out Vector4 lightColor, 
                                    out Vector4 lightAttenuation, 
                                    out Vector4 lightSpotDir, 
                                    out Vector4 lightOcclusionProbeChannel
        ){

            UniversalRenderPipeline.InitializeLightConstants_Common(
                lights, lightIndex, 
                out lightPos, out lightColor, out lightAttenuation, out lightSpotDir, out lightOcclusionProbeChannel
            );

            // When no lights are visible, main light will be set to -1.
            // In this case we initialize it to default values and return
            // ---
            // 感觉描述不够严谨, idx == -1 好像只能说明: 没有在场景中找到 main light, 还是可能存在 oth光源的
            // 当然如果 lightIndex = -1, 说明当前正在处理的就是一个 不存在的 main light; 就不用处理了直接返回吧
            if (lightIndex < 0)
                return;

            // ---- 说明是个有效的光源 -----:
            VisibleLight lightData = lights[lightIndex];
            Light light = lightData.light;

            if (light == null)
                return;

            if (light.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed &&
                lightData.light.shadows != LightShadows.None && //enum: None, Hard, Soft;
                m_MixedLightingSetup == MixedLightingSetup.None)// None: 仅表示 "尚未设置"
            {
                // enum: IndirectOnly, Subtractive, Shadowmask;
                switch (light.bakingOutput.mixedLightingMode)
                {
                    case MixedLightingMode.Subtractive:
                        m_MixedLightingSetup = MixedLightingSetup.Subtractive;
                        break;
                    case MixedLightingMode.Shadowmask:
                        m_MixedLightingSetup = MixedLightingSetup.ShadowMask;
                        break;
                }
            }
        }// 函数完__


        void SetupShaderLightConstants(CommandBuffer cmd, ref RenderingData renderingData)
        {
            m_MixedLightingSetup = MixedLightingSetup.None;

            // Main light has an optimized shader path for main light. This will benefit games that only care about a single light.
            // urp also supports only a single shadow light, if available it will be the main light.
            SetupMainLightConstants(cmd, ref renderingData.lightData);
         
            SetupAdditionalLightConstants(cmd, ref renderingData);
        }



        void SetupMainLightConstants(CommandBuffer cmd, ref LightData lightData)//   读完__
        {
            Vector4 lightPos, lightColor, lightAttenuation, lightSpotDir, lightOcclusionChannel;
            InitializeLightConstants(
                lightData.visibleLights, 
                lightData.mainLightIndex, 
                out lightPos, 
                out lightColor, 
                out lightAttenuation, // main light 始终为 1, 无需传递给 shader 
                out lightSpotDir,     // main light 用不到,   无需传递给 shader 
                out lightOcclusionChannel
            );

            cmd.SetGlobalVector(LightConstantBuffer._MainLightPosition, lightPos);//"_MainLightPosition"
            cmd.SetGlobalVector(LightConstantBuffer._MainLightColor, lightColor);//"_MainLightColor" 
            cmd.SetGlobalVector(LightConstantBuffer._MainLightOcclusionProbesChannel, lightOcclusionChannel);////"_MainLightOcclusionProbes"   
        }



        void SetupAdditionalLightConstants(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ref LightData lightData = ref renderingData.lightData;
            var cullResults = renderingData.cullResults;
            var lights = lightData.visibleLights;
            int maxAdditionalLightsCount = UniversalRenderPipeline.maxVisibleAdditionalLights;
            int additionalLightsCount = SetupPerObjectLightIndices(cullResults, ref lightData);
            if (additionalLightsCount > 0)
            {
                if (m_UseStructuredBuffer)
                {
                    NativeArray<ShaderInput.LightData> additionalLightsData = new NativeArray<ShaderInput.LightData>(additionalLightsCount, Allocator.Temp);
                    for (int i = 0, lightIter = 0; i < lights.Length && lightIter < maxAdditionalLightsCount; ++i)
                    {
                        VisibleLight light = lights[i];
                        if (lightData.mainLightIndex != i)
                        {
                            ShaderInput.LightData data;
                            InitializeLightConstants(
                                lights, i,
                                out data.position, out data.color, out data.attenuation,
                                out data.spotDirection, out data.occlusionProbeChannels
                            );
                            additionalLightsData[lightIter] = data;
                            lightIter++;
                        }
                    }

                    var lightDataBuffer = ShaderData.instance.GetLightDataBuffer(additionalLightsCount);
                    lightDataBuffer.SetData(additionalLightsData);

                    int lightIndices = cullResults.lightAndReflectionProbeIndexCount;
                    var lightIndicesBuffer = ShaderData.instance.GetLightIndicesBuffer(lightIndices);

                    cmd.SetGlobalBuffer(m_AdditionalLightsBufferId, lightDataBuffer);
                    cmd.SetGlobalBuffer(m_AdditionalLightsIndicesId, lightIndicesBuffer);

                    additionalLightsData.Dispose();
                }
                else
                {
                    for (int i = 0, lightIter = 0; i < lights.Length && lightIter < maxAdditionalLightsCount; ++i)
                    {
                        VisibleLight light = lights[i];
                        if (lightData.mainLightIndex != i)
                        {
                            InitializeLightConstants(lights, i, out m_AdditionalLightPositions[lightIter],
                                out m_AdditionalLightColors[lightIter],
                                out m_AdditionalLightAttenuations[lightIter],
                                out m_AdditionalLightSpotDirections[lightIter],
                                out m_AdditionalLightOcclusionProbeChannels[lightIter]);
                            lightIter++;
                        }
                    }

                    cmd.SetGlobalVectorArray(LightConstantBuffer._AdditionalLightsPosition, m_AdditionalLightPositions);
                    cmd.SetGlobalVectorArray(LightConstantBuffer._AdditionalLightsColor, m_AdditionalLightColors);
                    cmd.SetGlobalVectorArray(LightConstantBuffer._AdditionalLightsAttenuation, m_AdditionalLightAttenuations);
                    cmd.SetGlobalVectorArray(LightConstantBuffer._AdditionalLightsSpotDir, m_AdditionalLightSpotDirections);
                    cmd.SetGlobalVectorArray(LightConstantBuffer._AdditionalLightOcclusionProbeChannel, m_AdditionalLightOcclusionProbeChannels);//"_AdditionalLightsOcclusionProbes"
                }

                cmd.SetGlobalVector(LightConstantBuffer._AdditionalLightsCount, new Vector4(lightData.maxPerObjectAdditionalLightsCount,
                    0.0f, 0.0f, 0.0f));
            }
            else
            {
                cmd.SetGlobalVector(LightConstantBuffer._AdditionalLightsCount, Vector4.zero);
            }
        }// 函数完__
        


        int SetupPerObjectLightIndices(CullingResults cullResults, ref LightData lightData)
        {
            if (lightData.additionalLightsCount == 0)
                return lightData.additionalLightsCount;

            var visibleLights = lightData.visibleLights;
            var perObjectLightIndexMap = cullResults.GetLightIndexMap(Allocator.Temp);
            int globalDirectionalLightsCount = 0;
            int additionalLightsCount = 0;

            // Disable all directional lights from the perobject light indices
            // Pipeline handles main light globally and there's no support for additional directional lights atm.
            for (int i = 0; i < visibleLights.Length; ++i)
            {
                if (additionalLightsCount >= UniversalRenderPipeline.maxVisibleAdditionalLights)
                    break;

                VisibleLight light = visibleLights[i];
                if (i == lightData.mainLightIndex)
                {
                    perObjectLightIndexMap[i] = -1;
                    ++globalDirectionalLightsCount;
                }
                else
                {
                    perObjectLightIndexMap[i] -= globalDirectionalLightsCount;
                    ++additionalLightsCount;
                }
            }

            // Disable all remaining lights we cannot fit into the global light buffer.
            for (int i = globalDirectionalLightsCount + additionalLightsCount; i < perObjectLightIndexMap.Length; ++i)
                perObjectLightIndexMap[i] = -1;

            cullResults.SetLightIndexMap(perObjectLightIndexMap);

            if (m_UseStructuredBuffer && additionalLightsCount > 0)
            {
                int lightAndReflectionProbeIndices = cullResults.lightAndReflectionProbeIndexCount;
                Assertions.Assert.IsTrue(lightAndReflectionProbeIndices > 0, "Pipelines configures additional lights but per-object light and probe indices count is zero.");
                cullResults.FillLightAndReflectionProbeIndices(ShaderData.instance.GetLightIndicesBuffer(lightAndReflectionProbeIndices));
            }

            perObjectLightIndexMap.Dispose();
            return additionalLightsCount;
        }// 函数完__
    }
}
