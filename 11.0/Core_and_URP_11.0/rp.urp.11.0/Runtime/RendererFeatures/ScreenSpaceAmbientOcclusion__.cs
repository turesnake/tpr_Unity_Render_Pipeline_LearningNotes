using System;

namespace UnityEngine.Rendering.Universal
{


    [Serializable]
    internal class ScreenSpaceAmbientOcclusionSettings//ScreenSpaceAmbientOcclusionSettings__
    {
        // Parameters
        // 这组数据 对应 inspector 中的配置;
        [SerializeField] internal bool Downsample = false;
        [SerializeField] internal DepthSource Source = DepthSource.DepthNormals;
        [SerializeField] internal NormalQuality NormalSamples = NormalQuality.Medium;
        [SerializeField] internal float Intensity = 3.0f;
        [SerializeField] internal float DirectLightingStrength = 0.25f;
        [SerializeField] internal float Radius = 0.035f;
        [SerializeField] internal int SampleCount = 6;

        // enums: Depth, DepthNormals;
        internal enum DepthSource
        {
            Depth = 0,
            DepthNormals = 1,
            //GBuffer = 2
        }

        // enum: Low, Medium, High;
        internal enum NormalQuality
        {
            Low,
            Medium,
            High
        }
    }// class: SSAO Settings 完__



    /*
        SSAO Renderer Feature
    */
    [DisallowMultipleRendererFeature]
    internal class ScreenSpaceAmbientOcclusion//ScreenSpaceAmbientOcclusion__
        : ScriptableRendererFeature
    {
        // Serialized Fields
        [SerializeField, HideInInspector] private Shader m_Shader = null;
        [SerializeField] private ScreenSpaceAmbientOcclusionSettings m_Settings = new ScreenSpaceAmbientOcclusionSettings();

        // Private Fields
        private Material m_Material;
        private ScreenSpaceAmbientOcclusionPass m_SSAOPass = null;

        // Constants
        private const string k_ShaderName = "Hidden/Universal Render Pipeline/ScreenSpaceAmbientOcclusion";
        private const string k_OrthographicCameraKeyword = "_ORTHOGRAPHIC";
        private const string k_NormalReconstructionLowKeyword = "_RECONSTRUCT_NORMAL_LOW";
        private const string k_NormalReconstructionMediumKeyword = "_RECONSTRUCT_NORMAL_MEDIUM";
        private const string k_NormalReconstructionHighKeyword = "_RECONSTRUCT_NORMAL_HIGH";
        private const string k_SourceDepthKeyword = "_SOURCE_DEPTH";
        private const string k_SourceDepthNormalsKeyword = "_SOURCE_DEPTH_NORMALS";
        private const string k_SourceGBufferKeyword = "_SOURCE_GBUFFER";



        /// <inheritdoc/>
        public override void Create()//   读完__
        {
            // Create the pass...
            if (m_SSAOPass == null)
            {
                m_SSAOPass = new ScreenSpaceAmbientOcclusionPass();
            }

            GetMaterial();
            m_SSAOPass.profilerTag = name;
            m_SSAOPass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        }//   函数完__


        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)//   读完__
        {
            if (!GetMaterial())
            {// material 创建失败
                Debug.LogErrorFormat(
                    "{0}.AddRenderPasses(): Missing material. {1} render pass will not be added. Check for missing reference in the renderer resources.",
                    GetType().Name, m_SSAOPass.profilerTag);
                return;
            }

            bool shouldAdd = m_SSAOPass.Setup(m_Settings);
            if (shouldAdd)
            {
                renderer.EnqueuePass(m_SSAOPass);
            }
        }//   函数完__



        /// <inheritdoc/>
        protected override void Dispose(bool disposing)//   读完__
        {
            CoreUtils.Destroy(m_Material);
        }


        // 如果 m_Material 已经存在,啥也不做直接返回 true
        // 否则就创建这个 m_Material;
        // 如果 创建成功, 返回 true;
        private bool GetMaterial()//   读完__
        {
            if (m_Material != null)
            {
                return true;
            }

            if (m_Shader == null)
            {
                m_Shader = Shader.Find(k_ShaderName); // "Hidden/Universal Render Pipeline/ScreenSpaceAmbientOcclusion"
                if (m_Shader == null)
                {
                    return false;
                }
            }

            m_Material = CoreUtils.CreateEngineMaterial(m_Shader);
            m_SSAOPass.material = m_Material;
            return m_Material != null;
        }//   函数完__




        // =======================================< SSAO Pass >=================================================:
        // 在 "BeforeRenderingOpaques" 时刻;
        private class ScreenSpaceAmbientOcclusionPass //ScreenSpaceAmbientOcclusionPass__
            : ScriptableRenderPass
        {
            // Public Variables
            internal string profilerTag;
            internal Material material;

            // Private Variables
            private ScreenSpaceAmbientOcclusionSettings m_CurrentSettings;
            private ProfilingSampler m_ProfilingSampler = ProfilingSampler.Get(URPProfileId.SSAO);
            private RenderTargetIdentifier m_SSAOTexture1Target = new RenderTargetIdentifier(
                s_SSAOTexture1ID,       // nameID: "_SSAO_OcclusionTexture1"
                0,                      // mipLevel:
                CubemapFace.Unknown,    // cubeFace: 默认值 Unknown 表示没有具体指定;
                -1                      // depthSlice: 使用 "default slice" 去写入 depth 数据
            );
            private RenderTargetIdentifier m_SSAOTexture2Target = new RenderTargetIdentifier(
                s_SSAOTexture2ID,   // "_SSAO_OcclusionTexture2"
                0, 
                CubemapFace.Unknown, 
                -1
            );
            private RenderTargetIdentifier m_SSAOTexture3Target = new RenderTargetIdentifier(
                s_SSAOTexture3ID,   // "_SSAO_OcclusionTexture3"
                0, 
                CubemapFace.Unknown, 
                -1
            );
            private RenderTextureDescriptor m_Descriptor;

            // Constants
            private const string k_SSAOAmbientOcclusionParamName = "_AmbientOcclusionParam";
            private const string k_SSAOTextureName = "_ScreenSpaceOcclusionTexture";

            // Statics
            private static readonly int s_BaseMapID = Shader.PropertyToID("_BaseMap");
            private static readonly int s_SSAOParamsID = Shader.PropertyToID("_SSAOParams");
            private static readonly int s_SSAOTexture1ID = Shader.PropertyToID("_SSAO_OcclusionTexture1");
            private static readonly int s_SSAOTexture2ID = Shader.PropertyToID("_SSAO_OcclusionTexture2");
            private static readonly int s_SSAOTexture3ID = Shader.PropertyToID("_SSAO_OcclusionTexture3");

            // SSAO 一共需要 4 道 pass;
            private enum ShaderPasses
            {
                AO = 0,
                BlurHorizontal = 1,
                BlurVertical = 2,
                BlurFinal = 3
            }


            internal ScreenSpaceAmbientOcclusionPass()//  读完__
            {
                m_CurrentSettings = new ScreenSpaceAmbientOcclusionSettings();
            }


            /*
                ret:
                    猜测: 若参数数据是合理的, 返回 true;
            */
            internal bool Setup(ScreenSpaceAmbientOcclusionSettings featureSettings)//   读完__
            {
                m_CurrentSettings = featureSettings;
                switch (m_CurrentSettings.Source)
                {
                    case ScreenSpaceAmbientOcclusionSettings.DepthSource.Depth:
                        ConfigureInput(ScriptableRenderPassInput.Depth);
                        break;
                    case ScreenSpaceAmbientOcclusionSettings.DepthSource.DepthNormals:
                        ConfigureInput(ScriptableRenderPassInput.Normal);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                return 
                        material != null
                    &&  m_CurrentSettings.Intensity > 0.0f
                    &&  m_CurrentSettings.Radius > 0.0f
                    &&  m_CurrentSettings.SampleCount > 0;
            }//   函数完__



            /// <inheritdoc/>
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)//   读完__
            {
                // 全 camera stack 唯一一个 descriptor
                RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                int downsampleDivider = m_CurrentSettings.Downsample ? 2 : 1;

                // Update SSAO parameters in the material
                Vector4 ssaoParams = new Vector4(
                    m_CurrentSettings.Intensity,   // Intensity
                    m_CurrentSettings.Radius,      // Radius
                    1.0f / downsampleDivider,      // Downsampling
                    m_CurrentSettings.SampleCount  // Sample count
                );
                material.SetVector(s_SSAOParamsID, ssaoParams); // "_SSAOParams"

                // Update keywords
                CoreUtils.SetKeyword(
                    material, 
                    k_OrthographicCameraKeyword,    // "_ORTHOGRAPHIC"
                    renderingData.cameraData.camera.orthographic // 是否为 正交透视
                );

                // source 选择了 Depth, 就意味着要自己计算 normal 信息
                // (若选择了 DepthNormals, 就可以直接使用 预先计算好的 normal 信息) 
                if (m_CurrentSettings.Source == ScreenSpaceAmbientOcclusionSettings.DepthSource.Depth)
                {
                    // 设置三种档次 对应的 keyword;
                    switch (m_CurrentSettings.NormalSamples)
                    {
                        case ScreenSpaceAmbientOcclusionSettings.NormalQuality.Low:
                            CoreUtils.SetKeyword(material, k_NormalReconstructionLowKeyword, true);// "_RECONSTRUCT_NORMAL_LOW" ---
                            CoreUtils.SetKeyword(material, k_NormalReconstructionMediumKeyword, false);// "_RECONSTRUCT_NORMAL_MEDIUM"
                            CoreUtils.SetKeyword(material, k_NormalReconstructionHighKeyword, false);// "_RECONSTRUCT_NORMAL_HIGH"
                            break;
                        case ScreenSpaceAmbientOcclusionSettings.NormalQuality.Medium:
                            CoreUtils.SetKeyword(material, k_NormalReconstructionLowKeyword, false);
                            CoreUtils.SetKeyword(material, k_NormalReconstructionMediumKeyword, true); // ---
                            CoreUtils.SetKeyword(material, k_NormalReconstructionHighKeyword, false);
                            break;
                        case ScreenSpaceAmbientOcclusionSettings.NormalQuality.High:
                            CoreUtils.SetKeyword(material, k_NormalReconstructionLowKeyword, false);
                            CoreUtils.SetKeyword(material, k_NormalReconstructionMediumKeyword, false);
                            CoreUtils.SetKeyword(material, k_NormalReconstructionHighKeyword, true); // ---
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                switch (m_CurrentSettings.Source)
                {
                    case ScreenSpaceAmbientOcclusionSettings.DepthSource.DepthNormals:
                        CoreUtils.SetKeyword(material, k_SourceDepthKeyword, false);// "_SOURCE_DEPTH"
                        CoreUtils.SetKeyword(material, k_SourceDepthNormalsKeyword, true);// "_SOURCE_DEPTH_NORMALS" ---
                        CoreUtils.SetKeyword(material, k_SourceGBufferKeyword, false);// "_SOURCE_GBUFFER" (这个好像尚未实现)
                        break;
                    default:
                        CoreUtils.SetKeyword(material, k_SourceDepthKeyword, true); // ---
                        CoreUtils.SetKeyword(material, k_SourceDepthNormalsKeyword, false);
                        CoreUtils.SetKeyword(material, k_SourceGBufferKeyword, false); // (这个好像尚未实现)
                        break;
                }

                // Get temporary render textures
                m_Descriptor = cameraTargetDescriptor;
                m_Descriptor.msaaSamples = 1;
                m_Descriptor.depthBufferBits = 0; // 不使用 depth 
                m_Descriptor.width /= downsampleDivider; // 若开启下采样, 则将 rt 尺寸减半
                m_Descriptor.height /= downsampleDivider; // 若开启下采样, 则将 rt 尺寸减半
                m_Descriptor.colorFormat = RenderTextureFormat.ARGB32;

                cmd.GetTemporaryRT(
                    s_SSAOTexture1ID,   // "_SSAO_OcclusionTexture1"   (rt 尺寸减半的)
                    m_Descriptor, 
                    FilterMode.Bilinear
                );

                m_Descriptor.width *= downsampleDivider;
                m_Descriptor.height *= downsampleDivider;
                // 现在, width / height 又变回了原来的值;

                cmd.GetTemporaryRT(
                    s_SSAOTexture2ID,   // "_SSAO_OcclusionTexture2"   (rt 尺寸没有减半)
                    m_Descriptor, 
                    FilterMode.Bilinear
                );

                cmd.GetTemporaryRT(
                    s_SSAOTexture3ID,   // "_SSAO_OcclusionTexture3"   (rt 尺寸没有减半)
                    m_Descriptor, 
                    FilterMode.Bilinear
                );

                // Configure targets and clear color
                // 调用 -3-:
                // 仅绑定 color target;
                ConfigureTarget(s_SSAOTexture2ID); // "_SSAO_OcclusionTexture2"

                ConfigureClear(ClearFlag.None, Color.white);
            }//   函数完__


            /*
                ----------------------------------------------------------------------------:
                写入 render pass 实际运算内容
            */
            /// <inheritdoc/>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)//   读完__
            {
                if (material == null)
                {
                    Debug.LogErrorFormat("{0}.Execute(): Missing material. {1} render pass will not execute. Check for missing reference in the renderer resources.", GetType().Name, profilerTag);
                    return;
                }

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, m_ProfilingSampler))
                {
                    CoreUtils.SetKeyword(
                        cmd, 
                        ShaderKeywordStrings.ScreenSpaceOcclusion, //"_SCREEN_SPACE_OCCLUSION"
                        true
                    );

                    // 设置 gloabl shader 变量: "_SourceSize";
                    //  x: rt.width,
                    //  y: rt.height, 
                    //  z: 1.0f / rt.width,
                    //  w: 1.0f / rt.height
                    PostProcessUtils.SetSourceSize(
                        cmd, 
                        m_Descriptor // 现在的 width/height 是原来的值, 没有被 下采样影响;
                    );

                    // ------------------------< pass 0 >-------------------:
                    // Execute the SSAO
                    // -- 绘制 full screen quad
                    Render(
                        cmd, 
                        m_SSAOTexture1Target,   // "_SSAO_OcclusionTexture1"
                        ShaderPasses.AO         // pass idx = 0;
                    );

                    // ------------------------< pass 1 >-------------------:
                    // Execute the Blur Passes
                    // -- 绘制 full screen quad
                    RenderAndSetBaseMap(
                        cmd, 
                        m_SSAOTexture1Target,       // base:   "_SSAO_OcclusionTexture1"
                        m_SSAOTexture2Target,       // target: "_SSAO_OcclusionTexture2"
                        ShaderPasses.BlurHorizontal // pass idx = 1;
                    );

                    // ------------------------< pass 2 >-------------------:
                    // -- 绘制 full screen quad
                    RenderAndSetBaseMap(
                        cmd, 
                        m_SSAOTexture2Target,       // base:   "_SSAO_OcclusionTexture2"
                        m_SSAOTexture3Target,       // target: "_SSAO_OcclusionTexture3"
                        ShaderPasses.BlurVertical   // pass idx = 2;
                    );

                    // ------------------------< pass 3 >-------------------:
                    // -- 绘制 full screen quad
                    RenderAndSetBaseMap(
                        cmd, 
                        m_SSAOTexture3Target,       // base:   "_SSAO_OcclusionTexture3"
                        m_SSAOTexture2Target,       // target: "_SSAO_OcclusionTexture2"
                        ShaderPasses.BlurFinal      // pass idx = 3;
                    );

                    // 现在, 最终计算出的数据, 存储在 "_SSAO_OcclusionTexture2" 之中;

                    // Set the global SSAO texture and AO Params
                    // 将 rt "_SSAO_OcclusionTexture2" 绑定为 "_ScreenSpaceOcclusionTexture";
                    cmd.SetGlobalTexture(
                        k_SSAOTextureName,      // "_ScreenSpaceOcclusionTexture"
                        m_SSAOTexture2Target    // "_SSAO_OcclusionTexture2"
                    );

                    cmd.SetGlobalVector(
                        k_SSAOAmbientOcclusionParamName, // "_AmbientOcclusionParam"
                        new Vector4(
                            0f, 
                            0f, 
                            0f, 
                            m_CurrentSettings.DirectLightingStrength
                        )
                    );
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }//   函数完__


            private void Render(CommandBuffer cmd, RenderTargetIdentifier target, ShaderPasses pass)//  读完__
            {
                cmd.SetRenderTarget(
                    target, // color
                    RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store, // Store
                    target, // depth
                    RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.DontCare
                );

                cmd.DrawMesh(
                    RenderingUtils.fullscreenMesh, // mesh
                    Matrix4x4.identity, // 转换矩阵 (猜测: OS->WS)
                    material,           // 
                    0,                  // submeshIndex:
                    (int)pass           // pass idx
                );
            }//   函数完__


            private void RenderAndSetBaseMap(//      读完__
                                        CommandBuffer cmd, 
                                        RenderTargetIdentifier baseMap, 
                                        RenderTargetIdentifier target, 
                                        ShaderPasses pass
            ){

                cmd.SetGlobalTexture(s_BaseMapID, baseMap); // "_BaseMap"
                Render(
                    cmd, 
                    target, 
                    pass
                );
            }


            /// <inheritdoc/>
            public override void OnCameraCleanup(CommandBuffer cmd)//   读完__
            {
                if (cmd == null)
                {
                    throw new ArgumentNullException("cmd");
                }

                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.ScreenSpaceOcclusion, false);// //"_SCREEN_SPACE_OCCLUSION"
                cmd.ReleaseTemporaryRT(s_SSAOTexture1ID); // "_SSAO_OcclusionTexture1"
                cmd.ReleaseTemporaryRT(s_SSAOTexture2ID); // "_SSAO_OcclusionTexture2"
                cmd.ReleaseTemporaryRT(s_SSAOTexture3ID); // "_SSAO_OcclusionTexture3"
            }//   函数完__

        }// class:  SSAO Pass 完__


    }// class: SSAO 完__
}
