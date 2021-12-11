using UnityEngine.Experimental.GlobalIllumination;
using Unity.Collections;

namespace UnityEngine.Rendering.Universal.Internal
{
    /*
        Computes and submits lighting data to the GPU.
    */
    public class ForwardLights//ForwardLights__
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

        // 构造函数
        public ForwardLights()// 读完__
        {
            m_UseStructuredBuffer = RenderingUtils.useStructuredBuffer;// 暂为 false

            LightConstantBuffer._MainLightPosition = Shader.PropertyToID("_MainLightPosition");
            LightConstantBuffer._MainLightColor = Shader.PropertyToID("_MainLightColor");
            LightConstantBuffer._MainLightOcclusionProbesChannel = Shader.PropertyToID("_MainLightOcclusionProbes");
            LightConstantBuffer._AdditionalLightsCount = Shader.PropertyToID("_AdditionalLightsCount");

            if (m_UseStructuredBuffer)
            {
                /*    tpr
                m_AdditionalLightsBufferId = Shader.PropertyToID("_AdditionalLightsBuffer");
                m_AdditionalLightsIndicesId = Shader.PropertyToID("_AdditionalLightsIndices");
                */
            }
            else
            {
                LightConstantBuffer._AdditionalLightsPosition = Shader.PropertyToID("_AdditionalLightsPosition");
                LightConstantBuffer._AdditionalLightsColor = Shader.PropertyToID("_AdditionalLightsColor");
                LightConstantBuffer._AdditionalLightsAttenuation = Shader.PropertyToID("_AdditionalLightsAttenuation");
                LightConstantBuffer._AdditionalLightsSpotDir = Shader.PropertyToID("_AdditionalLightsSpotDir");
                LightConstantBuffer._AdditionalLightOcclusionProbeChannel = Shader.PropertyToID("_AdditionalLightsOcclusionProbes");

                int maxLights = UniversalRenderPipeline.maxVisibleAdditionalLights; // 16, 32, or 256
                m_AdditionalLightPositions = new Vector4[maxLights];
                m_AdditionalLightColors = new Vector4[maxLights];
                m_AdditionalLightAttenuations = new Vector4[maxLights];
                m_AdditionalLightSpotDirections = new Vector4[maxLights];
                m_AdditionalLightOcclusionProbeChannels = new Vector4[maxLights];
            }
        }// 函数完__


        // 设置一组 global shader keywords
        public void Setup(ScriptableRenderContext context, ref RenderingData renderingData)//   读完__
        {
            int additionalLightsCount = renderingData.lightData.additionalLightsCount;

            // add light 是 逐顶点的吗 ? (受 asset inspector 用户配置 的控制, 是全局统一值;)
            bool additionalLightsPerVertex = renderingData.lightData.shadeAdditionalLightsPerVertex;
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                SetupShaderLightConstants(cmd, ref renderingData);

                /*
                    asset inspector 中, 当用户设置 add light 为 "逐像素" 时, 
                        -- "_ADDITIONAL_LIGHTS_VERTEX"  设为 false;
                        -- "_ADDITIONAL_LIGHTS"         设为 true;
                    vice versa;
                */
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.AdditionalLightsVertex,//"_ADDITIONAL_LIGHTS_VERTEX"
                    additionalLightsCount > 0 && additionalLightsPerVertex);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.AdditionalLightsPixel,//"_ADDITIONAL_LIGHTS"
                    additionalLightsCount > 0 && !additionalLightsPerVertex);


                bool isShadowMask = renderingData.lightData.supportsMixedLighting && // 是否支持 Mixed 光 (asset inspector 用户配置的全局统一值)
                                m_MixedLightingSetup == MixedLightingSetup.ShadowMask;// enum: None(Baked Indirect), <ShadowMask>, Subtractive;

                bool isShadowMaskAlways = isShadowMask && 
                                QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask;// enum: Shadowmask, DistanceShadowmask;

                bool isSubtractive = renderingData.lightData.supportsMixedLighting && // 是否支持 Mixed 光 (asset inspector 用户配置的全局统一值)
                                m_MixedLightingSetup == MixedLightingSetup.Subtractive;// enum: None(Baked Indirect), ShadowMask, <Subtractive>;


                // 满足其一即可:
                // -1- {Baked Indirect, ShadowMask, Subtractive} 中选择了 Subtractive;
                // -2- {Baked Indirect, ShadowMask, Subtractive} 中选择了 ShadowMask,
                //     ( 同时在次一级的 { Shadowmask, DistanceShadowmask } 中选择了 Shadowmask;
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.LightmapShadowMixing, //"LIGHTMAP_SHADOW_MIXING"
                                    isSubtractive || isShadowMaskAlways);
                
                // {Baked Indirect, ShadowMask, Subtractive} 中选择了 ShadowMask; 
                // 同时在次一级的 { Shadowmask, DistanceShadowmask } 中, 选择任意一项皆可;
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.ShadowsShadowMask, //"SHADOWS_SHADOWMASK"
                                    isShadowMask);
                
                // {Baked Indirect, ShadowMask, Subtractive} 中选择了 Subtractive;
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MixedLightingSubtractive, //"_MIXED_LIGHTING_SUBTRACTIVE" // Backward compatibility
                                    isSubtractive);

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



        void SetupShaderLightConstants(CommandBuffer cmd, ref RenderingData renderingData)//  读完__
        {
            // enum: None, ShadowMask, Subtractive;
            m_MixedLightingSetup = MixedLightingSetup.None; // 初始值, 下面的函数中会具体配置

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



        void SetupAdditionalLightConstants(CommandBuffer cmd, ref RenderingData renderingData)// 读完__
        {
            ref LightData lightData = ref renderingData.lightData;
            var cullResults = renderingData.cullResults;
            var lights = lightData.visibleLights;
            int maxAdditionalLightsCount = UniversalRenderPipeline.maxVisibleAdditionalLights;// 16, 32, or 256
            
            int additionalLightsCount = SetupPerObjectLightIndices(cullResults, ref lightData);// "合格的 add light" 的数量
            if (additionalLightsCount > 0)
            {
                if (m_UseStructuredBuffer)// 暂为 false
                {// 这部分暂时没看
                    /*    tpr
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
                    */
                }
                else
                {
                    // 16, 32, or 256
                    for (int i=0, lightIter=0; i<lights.Length && lightIter<maxAdditionalLightsCount; ++i)
                    {
                        VisibleLight light = lights[i];
                        if (lightData.mainLightIndex != i)// light 不是 main light
                        {
                            InitializeLightConstants(
                                lights, i, 
                                out m_AdditionalLightPositions[lightIter],
                                out m_AdditionalLightColors[lightIter],
                                out m_AdditionalLightAttenuations[lightIter],
                                out m_AdditionalLightSpotDirections[lightIter],
                                out m_AdditionalLightOcclusionProbeChannels[lightIter]
                            );
                            lightIter++;
                        }
                    }

                    // 下面这堆 arrays, 容器容量为 maxAdditionalLightsCount 个 (16, 32, or 256)
                    // 实际存储的有效元素个数为: "lightIter" 个; (即: IndexMap 中 "非-1" 的元素的个数)
                    cmd.SetGlobalVectorArray(LightConstantBuffer._AdditionalLightsPosition, m_AdditionalLightPositions);//"_AdditionalLightsPosition"
                    cmd.SetGlobalVectorArray(LightConstantBuffer._AdditionalLightsColor, m_AdditionalLightColors);//"_AdditionalLightsColor"
                    cmd.SetGlobalVectorArray(LightConstantBuffer._AdditionalLightsAttenuation, m_AdditionalLightAttenuations);//"_AdditionalLightsAttenuation"
                    cmd.SetGlobalVectorArray(LightConstantBuffer._AdditionalLightsSpotDir, m_AdditionalLightSpotDirections);//"_AdditionalLightsSpotDir"
                    cmd.SetGlobalVectorArray(LightConstantBuffer._AdditionalLightOcclusionProbeChannel, m_AdditionalLightOcclusionProbeChannels);//"_AdditionalLightsOcclusionProbes"
                }

                
                cmd.SetGlobalVector(
                    LightConstantBuffer._AdditionalLightsCount, //"_AdditionalLightsCount"
                    
                    // "逐物体 add light" 个数的上限值;  
                    //并不代表 "IndexMap 中 "非-1" 的元素的个数" (visibleLights 中实际存在的 "合格add light" 的个数 )
                    new Vector4(lightData.maxPerObjectAdditionalLightsCount, 0.0f, 0.0f, 0.0f) // [0,8]
                );
            }
            else
            {// 没有 add lights
                cmd.SetGlobalVector(LightConstantBuffer._AdditionalLightsCount, Vector4.zero);
            }
        }// 函数完__
        


        /*
            检查 visibleLights, 过滤掉 main light 和 超量的 add light, 
            重新设置每一个合格的 add light 位于 IndexMap 中的 idx 值;
            ( main light 和 超量的 add light 的 idx 全部设为 -1, 表示它们不属于合格的 add light 范围 )
            
            最后返回 "合格的 add light" 的数量;
        */
        int SetupPerObjectLightIndices(CullingResults cullResults, ref LightData lightData)//   读完__
        {
            if (lightData.additionalLightsCount == 0)
                return lightData.additionalLightsCount;

            var visibleLights = lightData.visibleLights;
            /*
                假设 visibleLights 里一共有 7 个光源, main light 在 [2] 号位(起始为0), 
                那么 IndexMap 中原始数据为:
                    { 0, 1, 2(m), 3, 4, 5, 6 }
                
                经过下方处理后, main light 位置写上 -1, 表示自己不是 add light;
                在 main light 后面的 add light 的存储值纷纷减一,
                而且因为系统最多只支持 4 个 add light, 所以后方超出的位置也都写上 -1:
                最后得到:
                    { 0, 1, -1(m), 2, 3, -1, -1 }
            */
            var perObjectLightIndexMap = cullResults.GetLightIndexMap(Allocator.Temp);

            // 当前收集到的 visibleLights 中 "main light" 的数据, 感觉只能是 0/1 两种可能; 
            int globalDirectionalLightsCount = 0;
            // 当前收集到的 visibleLights 中 "add 光" 的数量;
            int additionalLightsCount = 0;

            // Disable all directional lights(平行光) from the "perobject light" 逐物体光 indices
            // Pipeline handles main light globally and there's no support for "additional directional lights" atm.
            // ----
            // 上述描述 和 下面的代码不符; main light 之外的平行光, 看起来可以放入 add lights 中;
            for (int i = 0; i < visibleLights.Length; ++i)
            {
                if (additionalLightsCount >= UniversalRenderPipeline.maxVisibleAdditionalLights)// 16, 32, or 256
                    break;

                VisibleLight light = visibleLights[i];
                if (i == lightData.mainLightIndex)
                {
                    // 写 -1 的 idx, 表示自己不是 add 光;
                    perObjectLightIndexMap[i] = -1;
                    ++globalDirectionalLightsCount;
                }
                else
                {
                    // 如果之前出现了 main light, 排在后面的光的 idx 都往前靠一位;
                    perObjectLightIndexMap[i] -= globalDirectionalLightsCount;
                    ++additionalLightsCount;
                }
            }

            // Disable all remaining lights we cannot fit into the global light buffer.
            // 尾部元素都写 -1
            for (int i = globalDirectionalLightsCount+additionalLightsCount; i < perObjectLightIndexMap.Length; ++i)
                perObjectLightIndexMap[i] = -1;

            cullResults.SetLightIndexMap(perObjectLightIndexMap);

            if (m_UseStructuredBuffer && additionalLightsCount > 0)// 暂不支持
            { // 这部分没有看
                /*    tpr
                int lightAndReflectionProbeIndices = cullResults.lightAndReflectionProbeIndexCount;
                Assertions.Assert.IsTrue(lightAndReflectionProbeIndices > 0, "Pipelines configures additional lights but per-object light and probe indices count is zero.");
                cullResults.FillLightAndReflectionProbeIndices(ShaderData.instance.GetLightIndicesBuffer(lightAndReflectionProbeIndices));
                */
            }

            perObjectLightIndexMap.Dispose();
            return additionalLightsCount;
        }// 函数完__
    }
}
