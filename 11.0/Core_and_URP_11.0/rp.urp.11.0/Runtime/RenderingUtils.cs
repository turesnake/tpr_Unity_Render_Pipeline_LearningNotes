using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.Rendering.Universal
{
    
    /*
        Contains properties and helper functions that you can use when rendering.
    */
    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public static class RenderingUtils//RenderingUtils__RR
    {
        static List<ShaderTagId> m_LegacyShaderPassNames = new List<ShaderTagId>()
        {
            new ShaderTagId("Always"),
            new ShaderTagId("ForwardBase"),
            new ShaderTagId("PrepassBase"),
            new ShaderTagId("Vertex"),
            new ShaderTagId("VertexLMRGBM"),
            new ShaderTagId("VertexLM"),
        };

        
        static Mesh s_FullscreenMesh = null;
        /*
            Returns a mesh that you can use with: ""CommandBuffer.DrawMesh(Mesh, Matrix4x4, Material);""
            to render full-screen effects.

            一个理想的 quad mesh:
                posOS.xy: [-1,1]
                posOS.z:  0
                uv.xy:    [0,1] 左下角起步
        */
        public static Mesh fullscreenMesh//fullscreenMesh__
        {
            get
            {
                if (s_FullscreenMesh != null)
                    return s_FullscreenMesh;

                float topV = 1.0f;
                float bottomV = 0.0f; // 从下向上, opengl 规范

                s_FullscreenMesh = new Mesh { name = "Fullscreen Quad" };
                s_FullscreenMesh.SetVertices(new List<Vector3>
                {
                    new Vector3(-1.0f, -1.0f, 0.0f),
                    new Vector3(-1.0f,  1.0f, 0.0f),
                    new Vector3(1.0f, -1.0f, 0.0f),
                    new Vector3(1.0f,  1.0f, 0.0f)
                });

                s_FullscreenMesh.SetUVs(0, new List<Vector2>
                {
                    new Vector2(0.0f, bottomV),
                    new Vector2(0.0f, topV),
                    new Vector2(1.0f, bottomV),
                    new Vector2(1.0f, topV)
                });

                s_FullscreenMesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);

                // 立刻更新 mesh 数据, 参数true 意味着以后再也不更新本 mesh 的数据了;
                s_FullscreenMesh.UploadMeshData(true);
                return s_FullscreenMesh;
            }
        }


        // 即便到 12.1, 此值仍为 false
        internal static bool useStructuredBuffer
        {
            // There are some performance issues with StructuredBuffers in some platforms.
            // We fallback to UBO in those cases.
            get
            {
                // TODO: For now disabling SSBO until figure out Vulkan binding issues.
                // When enabling this also enable USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA in shader side in Input.hlsl
                return false;

                // We don't use SSBO in D3D because we can't figure out without adding shader variants if platforms is D3D10.
                //GraphicsDeviceType deviceType = SystemInfo.graphicsDeviceType;
                //return !Application.isMobilePlatform &&
                //    (deviceType == GraphicsDeviceType.Metal || deviceType == GraphicsDeviceType.Vulkan ||
                //     deviceType == GraphicsDeviceType.PlayStation4 || deviceType == GraphicsDeviceType.PlayStation5 || deviceType == GraphicsDeviceType.XboxOne);
            }
        }

        

        static Material s_ErrorMaterial;
        static Material errorMaterial
        {
            get
            {
                if (s_ErrorMaterial == null)
                {
                    // TODO: When importing project, AssetPreviewUpdater::CreatePreviewForAsset will be called multiple times.
                    // This might be in a point that some resources required for the pipeline are not finished importing yet.
                    // Proper fix is to add a fence on asset import.
                    try
                    {
                        s_ErrorMaterial = new Material(Shader.Find("Hidden/Universal Render Pipeline/FallbackError"));
                    }
                    catch {}
                }

                return s_ErrorMaterial;
            }
        }


        /*
            Set view and projection matrices.
            This function will set "UNITY_MATRIX_V", "UNITY_MATRIX_P", "UNITY_MATRIX_VP" to given view and projection matrices.
            If "setInverseMatrices" is set to true this function will also set "UNITY_MATRIX_I_V" and "UNITY_MATRIX_I_VP".
        */
        /// <param name="setInverseMatrices">Set this to true if you also need to set inverse camera matrices.</param>
        public static void SetViewAndProjectionMatrices(
                                                    CommandBuffer cmd, 
                                                    Matrix4x4 viewMatrix, 
                                                    Matrix4x4 projectionMatrix, 
                                                    bool setInverseMatrices
        ){
            Matrix4x4 viewAndProjectionMatrix = projectionMatrix * viewMatrix;
            cmd.SetGlobalMatrix(ShaderPropertyId.viewMatrix, viewMatrix);
            cmd.SetGlobalMatrix(ShaderPropertyId.projectionMatrix, projectionMatrix);
            cmd.SetGlobalMatrix(ShaderPropertyId.viewAndProjectionMatrix, viewAndProjectionMatrix);

            if (setInverseMatrices)
            {
                Matrix4x4 inverseViewMatrix = Matrix4x4.Inverse(viewMatrix);
                Matrix4x4 inverseProjectionMatrix = Matrix4x4.Inverse(projectionMatrix);
                Matrix4x4 inverseViewProjection = inverseViewMatrix * inverseProjectionMatrix;
                cmd.SetGlobalMatrix(ShaderPropertyId.inverseViewMatrix, inverseViewMatrix);
                cmd.SetGlobalMatrix(ShaderPropertyId.inverseProjectionMatrix, inverseProjectionMatrix);
                cmd.SetGlobalMatrix(ShaderPropertyId.inverseViewAndProjectionMatrix, inverseViewProjection);
            }
        }// 函数完__


/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
        internal static readonly int UNITY_STEREO_MATRIX_V = Shader.PropertyToID("unity_StereoMatrixV");
        internal static readonly int UNITY_STEREO_MATRIX_IV = Shader.PropertyToID("unity_StereoMatrixInvV");
        internal static readonly int UNITY_STEREO_MATRIX_P = Shader.PropertyToID("unity_StereoMatrixP");
        internal static readonly int UNITY_STEREO_MATRIX_IP = Shader.PropertyToID("unity_StereoMatrixInvP");
        internal static readonly int UNITY_STEREO_MATRIX_VP = Shader.PropertyToID("unity_StereoMatrixVP");
        internal static readonly int UNITY_STEREO_MATRIX_IVP = Shader.PropertyToID("unity_StereoMatrixInvVP");
        internal static readonly int UNITY_STEREO_CAMERA_PROJECTION = Shader.PropertyToID("unity_StereoCameraProjection");
        internal static readonly int UNITY_STEREO_CAMERA_INV_PROJECTION = Shader.PropertyToID("unity_StereoCameraInvProjection");
        internal static readonly int UNITY_STEREO_VECTOR_CAMPOS = Shader.PropertyToID("unity_StereoWorldSpaceCameraPos");

        // Hold the stereo matrices in this class to avoid allocating arrays every frame
        internal class StereoConstants
        {
            public Matrix4x4[] viewProjMatrix = new Matrix4x4[2];
            public Matrix4x4[] invViewMatrix = new Matrix4x4[2];
            public Matrix4x4[] invProjMatrix = new Matrix4x4[2];
            public Matrix4x4[] invViewProjMatrix = new Matrix4x4[2];
            public Matrix4x4[] invCameraProjMatrix = new Matrix4x4[2];
            public Vector4[] worldSpaceCameraPos = new Vector4[2];
        };

        static readonly StereoConstants stereoConstants = new StereoConstants();

        /// <summary>
        /// Helper function to set all view and projection related matrices
        /// Should be called before draw call and after cmd.SetRenderTarget
        /// Internal usage only, function name and signature may be subject to change
        /// </summary>
        /// <param name="cmd">CommandBuffer to submit data to GPU.</param>
        /// <param name="viewMatrix">View matrix to be set. Array size is 2.</param>
        /// <param name="projectionMatrix">Projection matrix to be set.Array size is 2.</param>
        /// <param name="cameraProjectionMatrix">Camera projection matrix to be set.Array size is 2. Does not include platform specific transformations such as depth-reverse, depth range in post-projective space and y-flip. </param>
        /// <param name="setInverseMatrices">Set this to true if you also need to set inverse camera matrices.</param>
        /// <returns>Void</c></returns>
        internal static void SetStereoViewAndProjectionMatrices(CommandBuffer cmd, Matrix4x4[] viewMatrix, Matrix4x4[] projMatrix, Matrix4x4[] cameraProjMatrix, bool setInverseMatrices)
        {
            for (int i = 0; i < 2; i++)
            {
                stereoConstants.viewProjMatrix[i] = projMatrix[i] * viewMatrix[i];
                stereoConstants.invViewMatrix[i] = Matrix4x4.Inverse(viewMatrix[i]);
                stereoConstants.invProjMatrix[i] = Matrix4x4.Inverse(projMatrix[i]);
                stereoConstants.invViewProjMatrix[i] = Matrix4x4.Inverse(stereoConstants.viewProjMatrix[i]);
                stereoConstants.invCameraProjMatrix[i] = Matrix4x4.Inverse(cameraProjMatrix[i]);
                stereoConstants.worldSpaceCameraPos[i] = stereoConstants.invViewMatrix[i].GetColumn(3);
            }

            cmd.SetGlobalMatrixArray(UNITY_STEREO_MATRIX_V, viewMatrix);
            cmd.SetGlobalMatrixArray(UNITY_STEREO_MATRIX_P, projMatrix);
            cmd.SetGlobalMatrixArray(UNITY_STEREO_MATRIX_VP, stereoConstants.viewProjMatrix);

            cmd.SetGlobalMatrixArray(UNITY_STEREO_CAMERA_PROJECTION, cameraProjMatrix);

            if (setInverseMatrices)
            {
                cmd.SetGlobalMatrixArray(UNITY_STEREO_MATRIX_IV, stereoConstants.invViewMatrix);
                cmd.SetGlobalMatrixArray(UNITY_STEREO_MATRIX_IP, stereoConstants.invProjMatrix);
                cmd.SetGlobalMatrixArray(UNITY_STEREO_MATRIX_IVP, stereoConstants.invViewProjMatrix);

                cmd.SetGlobalMatrixArray(UNITY_STEREO_CAMERA_INV_PROJECTION, stereoConstants.invCameraProjMatrix);
            }
            cmd.SetGlobalVectorArray(UNITY_STEREO_VECTOR_CAMPOS, stereoConstants.worldSpaceCameraPos);
        }

#endif
*/

        internal static void Blit(//   读完__
            CommandBuffer cmd,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            Material material,
            int passIndex = 0,
            bool useDrawProcedural = false,
            RenderBufferLoadAction colorLoadAction = RenderBufferLoadAction.Load,
            RenderBufferStoreAction colorStoreAction = RenderBufferStoreAction.Store,
            RenderBufferLoadAction depthLoadAction = RenderBufferLoadAction.Load,
            RenderBufferStoreAction depthStoreAction = RenderBufferStoreAction.Store)
        {
            cmd.SetGlobalTexture(
                ShaderPropertyId.sourceTex, //"_SourceTex"
                source
            );
            if (useDrawProcedural) // xr 专用,
            {   /*      tpr
                // x=1: 不执行texture uv 值得 "y-flip", 
                Vector4 scaleBias =   new Vector4( 1, 1, 0, 0 );
                Vector4 scaleBiasRt = new Vector4( 1, 1, 0, 0 );
                cmd.SetGlobalVector(ShaderPropertyId.scaleBias, scaleBias);
                cmd.SetGlobalVector(ShaderPropertyId.scaleBiasRt, scaleBiasRt);


                cmd.SetRenderTarget(new RenderTargetIdentifier(destination, 0, CubemapFace.Unknown, -1),
                    colorLoadAction, colorStoreAction, depthLoadAction, depthStoreAction);
                cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Quads, 4, 1, null);
                */
            }
            else
            {
                cmd.SetRenderTarget(
                    destination, 
                    colorLoadAction, 
                    colorStoreAction, 
                    depthLoadAction, 
                    depthStoreAction
                );
                cmd.Blit(
                    source, //"_SourceTex"
                    BuiltinRenderTextureType.CurrentActive, // 就是上一行刚设置的新 render target
                    material, 
                    passIndex
                );
            }
        }// 函数完__


        /*
            This is used to render materials that contain built-in shader passes not compatible with URP.
            It will render those legacy passes with error/pink shader.
            ---
            仅在 editor, development_build 模式下才被执行;
        */
        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        internal static void RenderObjectsWithError(
                                            ScriptableRenderContext context, 
                                            ref CullingResults cullResults, 
                                            Camera camera, 
                                            FilteringSettings filterSettings, 
                                            SortingCriteria sortFlags
        ){
            // TODO: When importing project, AssetPreviewUpdater::CreatePreviewForAsset will be called multiple times.
            // This might be in a point that some resources required for the pipeline are not finished importing yet.
            // Proper fix is to add a fence on asset import.
            if (errorMaterial == null)
                return;

            SortingSettings sortingSettings = new SortingSettings(camera) { criteria = sortFlags };
            DrawingSettings errorSettings = new DrawingSettings(m_LegacyShaderPassNames[0], sortingSettings)
            {
                perObjectData = PerObjectData.None,
                overrideMaterial = errorMaterial,
                overrideMaterialPassIndex = 0
            };
            for (int i = 1; i < m_LegacyShaderPassNames.Count; ++i)
                errorSettings.SetShaderPassName(i, m_LegacyShaderPassNames[i]);

            context.DrawRenderers(cullResults, ref errorSettings, ref filterSettings);
        }// 函数完__




        /*
            Caches render texture format support. 
            "SystemInfo.SupportsRenderTextureFormat()" and "IsFormatSupported()" allocate memory due to boxing.
            ---
            将当前平台对 "各种 render texture formats" 的支持信息 缓存下来, 以便第二次查询时直接得到现成结果;
        */
        static Dictionary<RenderTextureFormat, bool> m_RenderTextureFormatSupport 
                = new Dictionary<RenderTextureFormat, bool>();
        static Dictionary<GraphicsFormat, Dictionary<FormatUsage, bool>> m_GraphicsFormatSupport 
                = new Dictionary<GraphicsFormat, Dictionary<FormatUsage, bool>>();

        internal static void ClearSystemInfoCache()//  读完__
        {
            m_RenderTextureFormatSupport.Clear();
            m_GraphicsFormatSupport.Clear();
        }// 函数完__


        /*
            Checks if a render texture format is supported by the run-time system.
        */
        /// <param name="format">The format to look up.</param>
        /// <returns>Returns true if the graphics card supports the given <c>RenderTextureFormat</c></returns>
        public static bool SupportsRenderTextureFormat(RenderTextureFormat format)// 读完__
        {
            if (!m_RenderTextureFormatSupport.TryGetValue(format, out var support))
            {
                support = SystemInfo.SupportsRenderTextureFormat(format);
                m_RenderTextureFormatSupport.Add(format, support);
            }

            return support;
        }// 函数完__



        /*
            Checks if a texture format is supported by the run-time system.
            Similar to "SystemInfo.IsFormatSupported()", but doesn't allocate memory.
            --
            查询在当前平台中, 参数format 是否支持 "参数usage 所指示的操作", 如果支持,就返回 true;
            "GraphicsFormat" 表示 texture/render texture 的某种实际存储格式;
            在不同平台上, 每一种格式, 都支持一系列操作; (但又不去全支持)
            ---
            和 "SystemInfo.IsFormatSupported()" 功能一致, 区别在于:
            本函数每次执行查询后, 都会将查询信息记录下来, 下次查询相同的内容, 直接查表就行了,
            无需再次调用 "SystemInfo.IsFormatSupported()";
        */
        /// <param name="format">The format to look up.</param>
        /// <param name="usage">The format usage to look up.</param>
        /// <returns>Returns true if the graphics card supports the given <c>GraphicsFormat</c></returns>
        public static bool SupportsGraphicsFormat(GraphicsFormat format, FormatUsage usage)// 读完__
        {
            bool support = false;
            if (!m_GraphicsFormatSupport.TryGetValue(format, out var uses))
            {
                uses = new Dictionary<FormatUsage, bool>();
                support = SystemInfo.IsFormatSupported(format, usage);
                uses.Add(usage, support);
                m_GraphicsFormatSupport.Add(format, uses);
            }
            else{
                if (!uses.TryGetValue(usage, out support))
                {
                    support = SystemInfo.IsFormatSupported(format, usage);
                    uses.Add(usage, support);
                }
            }
            return support;
        }// 函数完__




        /// <summary>
        /// Return the last colorBuffer index actually referring to an existing RenderTarget
        /// </summary>
        /// <param name="colorBuffers"></param>
        /// <returns></returns>
        internal static int GetLastValidColorBufferIndex(RenderTargetIdentifier[] colorBuffers)
        {
            int i = colorBuffers.Length - 1;
            for (; i >= 0; --i)
            {
                if (colorBuffers[i] != 0)
                    break;
            }
            return i;
        }// 函数完__



        
        //  计算并返回, 参数 colorBuffers 中 "有效的 color buffer" 的数据;
        //  id值不为0, 就是有效的
        internal static uint GetValidColorBufferCount(RenderTargetIdentifier[] colorBuffers)//  读完__
        {
            uint nonNullColorBuffers = 0;
            if (colorBuffers != null){
                foreach (var identifier in colorBuffers){
                    if (identifier != 0)
                        ++nonNullColorBuffers;
                }
            }
            return nonNullColorBuffers;
        }// 函数完__


   
        /// Return true if colorBuffers is an actual MRT setup
        internal static bool IsMRT(RenderTargetIdentifier[] colorBuffers)//   读完__
        {
            return GetValidColorBufferCount(colorBuffers) > 1;
        }// 函数完__


        /// <summary>
        /// Return true if value can be found in source (without recurring to Linq)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static bool Contains(RenderTargetIdentifier[] source, RenderTargetIdentifier value)
        {
            foreach (var identifier in source)
            {
                if (identifier == value)
                    return true;
            }
            return false;
        }// 函数完__


       
        // Return the index where value was found source. Otherwise, return -1. 
        // (without recurring to Linq)
        // 如果参数 value 在容器 source 中, 返回它的 idx, 否则返回 -1;
        internal static int IndexOf(RenderTargetIdentifier[] source, RenderTargetIdentifier value)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                if (source[i] == value)
                    return i;
            }
            return -1;
        }// 函数完__


        /*
            Return the number of RenderTargetIdentifiers in "source" that are valid (not 0) and different from "value" 
            (without recurring to Linq)
            容器中 有效且和 参数 value 不相同的元素 的个数;
        */
        internal static uint CountDistinct(//   读完__
                                    RenderTargetIdentifier[] source, 
                                    RenderTargetIdentifier value
        ){
            uint count = 0;
            for (int i = 0; i < source.Length; ++i)
            {
                if (source[i] != value && source[i] != 0)
                    ++count;
            }
            return count;
        }// 函数完__




        // Return the index of last valid (i.e different from 0) RenderTargetIdentifiers in "source" (without recurring to Linq)
        // 如果 参数 source 中不存在 有效的 元素, 本函数返回 -1;
        internal static int LastValid( RenderTargetIdentifier[] source )
        {
            for (int i = source.Length - 1; i >= 0; --i)
            {
                if (source[i] != 0)
                    return i;
            }
            return -1;
        }// 函数完__



        /// <summary>
        /// Return true if ClearFlag a contains ClearFlag b
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        internal static bool Contains(ClearFlag a, ClearFlag b)
        {
            return (a & b) == b;
        }// 函数完__



  
        /// Return true if "left" and "right" are the same (without recurring to Linq)   
        internal static bool SequenceEqual(//   读完__
                                RenderTargetIdentifier[] left, 
                                RenderTargetIdentifier[] right
        ){
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; ++i)
                if (left[i] != right[i])
                    return false;

            return true;
        }// 函数完__
    }
}
