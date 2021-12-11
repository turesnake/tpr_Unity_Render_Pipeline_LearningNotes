using System;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.Rendering.Universal
{


    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public struct ShadowSliceData//ShadowSliceData__
    {

        // "cullResults.ComputeDirectionalShadowMatricesAndCullingPrimitives()" 直接创建的两个矩阵
        // 未做后续改动
        public Matrix4x4 viewMatrix;
        public Matrix4x4 projectionMatrix;

        /*            
            传入一个 顶点/fragment 的 posWS (camera 视角), 经由此矩阵转换, 可得到对应的 shadowmap 中的 uv值;
            ( posWS -> posSTS (shadow texture: tile) )

            由 GetShadowTransform() + ApplySliceTransform() 计算生成;
        */
        public Matrix4x4 shadowTransform;

        // tile 在 atlas 中的 offset; (pix)
        public int offsetX;
        public int offsetY;

        public int resolution;// tile 分辨率(pix)
        public ShadowSplitData splitData; // splitData contains culling information

        public void Clear()
        {
            viewMatrix = Matrix4x4.identity;
            projectionMatrix = Matrix4x4.identity;
            shadowTransform = Matrix4x4.identity;
            offsetX = offsetY = 0;
            resolution = 1024;
        }
    }



    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public static class ShadowUtils//ShadowUtils__RR
    {


        // "RenderTextureFormat.Shadowmap" 或 "RenderTextureFormat.Depth";
        private static readonly RenderTextureFormat m_ShadowmapFormat;
        private static readonly bool m_ForceShadowPointSampling;// ios Metal 可能要强制使用 点采样: point sampler


        static ShadowUtils()//   读完__
        {
            /*
                "RenderTextureFormat.Shadowmap" 是专门用于 shadowmap 功能的 原生 render texture format; 
                unity 优先选择此 format;
                当不支持时, 选择 "RenderTextureFormat.Depth"
            */
            m_ShadowmapFormat = 
                RenderingUtils.SupportsRenderTextureFormat(RenderTextureFormat.Shadowmap) && 
                (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES2)
                    ? RenderTextureFormat.Shadowmap
                    : RenderTextureFormat.Depth;

            // ios Metal 可能要强制使用 点采样: point sampler
            m_ForceShadowPointSampling = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal &&
                GraphicsSettings.HasShaderDefine(Graphics.activeTier, BuiltinShaderDefine.UNITY_METAL_SHADOWS_USE_POINT_FILTERING);
        }



        // 重载-1-, 仅仅是对 重载-2- 的简单封装;
        public static bool ExtractDirectionalLightMatrix( //    读完__
                                            ref CullingResults cullResults, 
                                            ref ShadowData shadowData, 
                                            int shadowLightIndex, 
                                            int cascadeIndex, 
                                            int shadowmapWidth, 
                                            int shadowmapHeight, 
                                            int shadowResolution, 
                                            float shadowNearPlane, 
                                            out Vector4 cascadeSplitDistance, 
                                            out ShadowSliceData shadowSliceData, 
                                            out Matrix4x4 viewMatrix, 
                                            out Matrix4x4 projMatrix
        ){
            bool result = ExtractDirectionalLightMatrix(
                ref cullResults, 
                ref shadowData, 
                shadowLightIndex, 
                cascadeIndex, 
                shadowmapWidth, 
                shadowmapHeight, 
                shadowResolution, 
                shadowNearPlane, 
                out cascadeSplitDistance, 
                out shadowSliceData
            );
            viewMatrix = shadowSliceData.viewMatrix;
            projMatrix = shadowSliceData.projectionMatrix;
            return result;
        }


        /*
            重载-2-

            参数: shadowmapWidth, shadowmapHeight:
                举例: shadowmap resolution = 4096, 
                当 cascade count = 2时,  一定会分配一张: 4096x2048 的矩形 map, 竖着切一刀分为左右两个 tiles;
                当 cascade count = 1/3/4 时, 最终会分配一张 4096x4096 的 map, 然后在内部分割;

            ret:
                If false, the shadow map for this cascade does not need to be rendered this frame.
        */
        public static bool ExtractDirectionalLightMatrix( //   读完__
                                            ref CullingResults cullResults, 
                                            ref ShadowData shadowData, 
                                            int shadowLightIndex, // light 在 visibleLights 中的 idx
                                            int cascadeIndex,     // i
                                            int shadowmapWidth, 
                                            int shadowmapHeight, 
                                            int shadowResolution,  //  shadowmap tile resolution
                                            float shadowNearPlane, //  The near plane offset for the light.
                                            out Vector4 cascadeSplitDistance, // xyz: sphere posWS;  w: sphere radius
                                            out ShadowSliceData shadowSliceData
        ){
            // 为一个 平行光 生成一个 clip space cube, 使得它既能沿着 平行光的方向, 
            // 又能覆盖 "camera 正看到的 平截头体",
            // 同时支持 cascade 功能
            bool success = cullResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                shadowLightIndex,                       // light 在 visibleLights 中的 idx
                cascadeIndex,                           // i
                shadowData.mainLightShadowCascadesCount, // cascade 有几层, 区间[1,4]; (比如: 4个重叠的球体) 
                shadowData.mainLightShadowCascadesSplit, // cascade split ratios, 
                shadowResolution,                       //  shadowmap tile resolution
                shadowNearPlane,                        //  The near plane offset for the light.
                out shadowSliceData.viewMatrix,         //  
                out shadowSliceData.projectionMatrix,   //
                out shadowSliceData.splitData           //  Describes the culling information for a given shadow split
            );

            cascadeSplitDistance = shadowSliceData.splitData.cullingSphere; // xyz: sphere posWS;  w: sphere radius

            // tile 在 atlas 中的 offset;
            shadowSliceData.offsetX = (cascadeIndex % 2) * shadowResolution;
            shadowSliceData.offsetY = (cascadeIndex / 2) * shadowResolution;

            shadowSliceData.resolution = shadowResolution;
            shadowSliceData.shadowTransform = GetShadowTransform(shadowSliceData.projectionMatrix, shadowSliceData.viewMatrix);

            // This used to be fixed to .6f, but is now configureable.
            // It is the culling sphere radius multiplier for shadow cascade blending
            shadowSliceData.splitData.shadowCascadeBlendCullingFactor = 0.6f;

            // If we have shadow cascades baked into the atlas we bake cascade transform
            // in each shadow matrix to save shader ALU and L/S
            // ---
            // 如果 main light shadowmap 启用了 cascade, 
            // 则需要将每个 shadow tile 从: "布满整张map" 缩小偏移到 "tile 所在的区域";
            if (shadowData.mainLightShadowCascadesCount > 1)
                ApplySliceTransform(ref shadowSliceData, shadowmapWidth, shadowmapHeight);

            return success;
        }//   函数完__



        public static bool ExtractSpotLightMatrix( //   读完__
                                                ref CullingResults cullResults, 
                                                ref ShadowData shadowData, 
                                                int shadowLightIndex, 
                                                out Matrix4x4 shadowMatrix, 
                                                out Matrix4x4 viewMatrix, 
                                                out Matrix4x4 projMatrix, 
                                                out ShadowSplitData splitData
        ){
            // returns false if input parameters are incorrect (rare)
            // 几乎不会返回 false
            bool success = cullResults.ComputeSpotShadowMatricesAndCullingPrimitives(
                shadowLightIndex, 
                out viewMatrix, 
                out projMatrix, 
                out splitData
            ); 
            shadowMatrix = GetShadowTransform(projMatrix, viewMatrix);
            return success;
        }




        public static bool ExtractPointLightMatrix(  //   读完__
                                                ref CullingResults cullResults, 
                                                ref ShadowData shadowData, 
                                                int shadowLightIndex, 
                                                CubemapFace cubemapFace, 
                                                float fovBias, 
                                                out Matrix4x4 shadowMatrix, 
                                                out Matrix4x4 viewMatrix, 
                                                out Matrix4x4 projMatrix, 
                                                out ShadowSplitData splitData
        ){
            bool success = cullResults.ComputePointShadowMatricesAndCullingPrimitives(
                shadowLightIndex, 
                cubemapFace,        // cubemap face index
                fovBias,            // field of view Bias
                out viewMatrix, 
                out projMatrix, 
                out splitData
            ); // returns false if input parameters are incorrect (rare)

            /*
                In native API "CullingResults.ComputeSpotShadowMatricesAndCullingPrimitives()", 
                there is code that inverts the 3rd component of shadow-casting spot light's "world-to-local" matrix 
                (it was so since its original addition to the code base):
                https://github.cds.internal.unity3d.com/unity/unity/commit/34813e063526c4be0ef0448dfaae3a911dd8be58#diff-cf0b417fc6bd8ee2356770797e628cd4R331
                (the same transformation has also always been used in the Built-In Render Pipeline)
            
                However native API "CullingResults.ComputePointShadowMatricesAndCullingPrimitives()" does not contain this transformation.
                As a result, the view matrices returned for a point light shadow face, 
                and for a spot light with same direction as that face, have opposite 3rd component.
            
                This causes normalBias to be incorrectly applied to shadow caster vertices during the point light shadow pass.
                To counter this effect, we invert the point light shadow view matrix component here:
                -----------
                上文中, 为何指向 矩阵的 "3rd component" ??? 
                -------
                unity 绘制 点光源的 shadow tile(slice), 是上下颠倒的, 这个颠倒导致了 三角形顶点顺序的颠倒;
                进而导致 正向面 和 背向面 两者关系的反转; 这么一来, 我们只能渲染 原来的背向面了; 
                
                这导致了 点光源的shadowmap, 就算不施加 bias, 也是几乎没有 "自投影"; 
                但缺点是 容易漏光, 尤其是当 shadow caster 和 receiver 很近时. 
                (若物体设置为 Two Sided 渲染, 则不受此问题困扰)
                ---
                所以要反转 view矩阵的 第二行, 使得 posWS 与 view矩阵相乘后, y值反转;
            */
            {
                viewMatrix.m10 = -viewMatrix.m10; // 其实值为 0, catlike 选择忽略此行
                viewMatrix.m11 = -viewMatrix.m11;
                viewMatrix.m12 = -viewMatrix.m12;
                viewMatrix.m13 = -viewMatrix.m13;
            }

            shadowMatrix = GetShadowTransform(projMatrix, viewMatrix);
            return success;
        }//   函数完__



        // 针对单个 shadow tile, 
        // 设置 "Const Depth Bias", "Slope Bias", viewport, proj矩阵, view矩阵, 
        // 然后执行真正的 "context.DrawShadows()";
        public static void RenderShadowSlice(  //   读完__
                                        CommandBuffer cmd, 
                                        ref ScriptableRenderContext context,
                                        ref ShadowSliceData shadowSliceData, 
                                        ref ShadowDrawingSettings settings,
                                        Matrix4x4 proj, 
                                        Matrix4x4 view
        ){
            /*
                these values match HDRP defaults 
                (see https://github.com/Unity-Technologies/Graphics/blob/9544b8ed2f98c62803d285096c91b44e9d8cbc47/com.unity.render-pipelines.high-definition/Runtime/Lighting/Shadow/HDShadowAtlas.cs#L197 )
                这是个 源码链接
            */
            
            // "Const Depth Bias" + "Slope Bias"
            // Adds a command to set the global depth bias.
            cmd.SetGlobalDepthBias(1.0f, 2.5f); 

            // 本次 shadow tile 的 viewport
            cmd.SetViewport(new Rect(
                shadowSliceData.offsetX, shadowSliceData.offsetY, 
                shadowSliceData.resolution, shadowSliceData.resolution
            ));
            cmd.SetViewProjectionMatrices(view, proj);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            context.DrawShadows(ref settings);// Schedules the drawing of shadow casters for a single Light.
            cmd.DisableScissorRect(); // disable the hardware scissor rectangle.

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // Restore previous depth bias values
            // 设置回原始值
            cmd.SetGlobalDepthBias(0.0f, 0.0f); 
        }//  读完__


        public static void RenderShadowSlice(
                                            CommandBuffer cmd, 
                                            ref ScriptableRenderContext context,
                                            ref ShadowSliceData shadowSliceData, 
                                            ref ShadowDrawingSettings settings)
        {
            RenderShadowSlice(
                cmd, 
                ref context, 
                ref shadowSliceData, 
                ref settings,
                shadowSliceData.projectionMatrix, 
                shadowSliceData.viewMatrix
            );
        }


        /*
            将 shadowmap atlas 细分成 "tileCount" 个 小正方体 (tile);
            返回 每个 tile 的分辨率; (边长,pix)
        */
        /// <param name="tileCount"> 需要将 atlas 细分成几块, (不是细分几次)</param>
        public static int GetMaxTileResolutionInAtlas(int atlasWidth, int atlasHeight, int tileCount)//  读完__
        {
            int resolution = Mathf.Min(atlasWidth, atlasHeight);// atlas 单边尺寸(pix)

            // 使用上面的 "resolution" 能把 atlas 分成几份;
            // 比如, 当参数 atlasWidth = 350; atlasHeight = 100; 此处可计算得 3; 
            int currentTileCount = atlasWidth / resolution * atlasHeight / resolution;

            // 用上面得分割法还无法得到需要得 块数, 再做下述细分;
            while (currentTileCount < tileCount)
            {
                resolution = resolution >> 1;// 除2, 地板除
                currentTileCount = atlasWidth / resolution * atlasHeight / resolution;
            }
            return resolution;
        }


        /*
            如果 main light shadowmap 启用了 cascade, 
            则需要将每个 shadow tile 从: "布满整张map" 缩小偏移到 "tile 所在的区域";
            本函数就是执行这个 缩小偏移的工作;
            
            参数 atlasWidth, atlasHeight:
                举例: shadowmap resolution = 4096, 
                当 cascade count = 2时,  一定会分配一张: 4096x2048 的矩形 map, 竖着切一刀分为左右两个 tiles;
                当 cascade count = 1/3/4 时, 最终会分配一张 4096x4096 的 map, 然后在内部分割;
        */
        public static void ApplySliceTransform(ref ShadowSliceData shadowSliceData, int atlasWidth, int atlasHeight)//  读完__
        {
            Matrix4x4 sliceTransform = Matrix4x4.identity;
            float oneOverAtlasWidth = 1.0f / atlasWidth;
            float oneOverAtlasHeight = 1.0f / atlasHeight;
            /*
                由于新矩阵 sliceTransform 的操作对象(也是个矩阵), 其元素都位于 [0,1] 区间;
                所以矩阵 sliceTransform 执行的 scale,offset 修改, 也都是在 [0,1] 区间内的; 
                   =A=,  0,   0,  =C=
                    0,  =B=,  0,  =D=
                    0,   0,   1,   0
                    0,   0,   0,   1
            */
            // scale
            sliceTransform.m00 = shadowSliceData.resolution * oneOverAtlasWidth;
            sliceTransform.m11 = shadowSliceData.resolution * oneOverAtlasHeight;
            // offset
            sliceTransform.m03 = shadowSliceData.offsetX * oneOverAtlasWidth;
            sliceTransform.m13 = shadowSliceData.offsetY * oneOverAtlasHeight;

            // Apply shadow slice scale and offset
            shadowSliceData.shadowTransform = sliceTransform * shadowSliceData.shadowTransform;
        }



        // 计算 三种light 的 shadow 的: depthBias, normalBias
        public static Vector4 GetShadowBias(
                                            ref VisibleLight shadowLight,   // light
                                            int shadowLightIndex,           // light idx
                                            ref ShadowData shadowData, 
                                            Matrix4x4 lightProjectionMatrix,// ShadowSliceData.projectionMatrix
                                            float shadowResolution          // tile 分辨率(pix)
        ){
            if ( shadowLightIndex<0 || shadowLightIndex>=shadowData.bias.Count )
            {
                Debug.LogWarning(string.Format("{0} is not a valid light index.", shadowLightIndex));
                return Vector4.zero;
            }

            float frustumSize;
            if (shadowLight.lightType == LightType.Directional)
            {
                /*
                    Frustum size is guaranteed to be a cube as we wrap shadow frustum around a sphere

                    首先, 平行光的 投影矩阵 是个正交矩阵;
                    投影矩阵.m00 内容为: 1/(Aspect*Size);
                    其中:
                        -- Aspect = w/h
                        -- Size: frustum.height/2; (米为单位)
                    
                    所以, frustumSize = Aspect * frustum.height = "frustum.width";
                */
                frustumSize = 2.0f / lightProjectionMatrix.m00;
            }
            else if (shadowLight.lightType == LightType.Spot)
            {  
                /*
                    For perspective projections, shadow texel size varies with depth;
                    It will only work well if done in "receiver side in the pixel shader". 
                    Currently urp do bias on caster side in vertex shader. 
                    When we add shader quality tiers we can properly handle this. 
                    For now, as a poor approximation we do a constant bias and compute the size of the frustum 
                    as if it was orthogonal considering the size at mid point between near and far planes.
                    Depending on how big the light range is, it will be good enough with some tweaks in bias
                    ---
                    在透视空间中, shadow texel 的尺寸随 depth 的变化而变化; 只有在 "shadow 接收端的 frag-shader" 中才能正确处理此问题;
                    但是目前 urp 选择在 "shadow cast 端的 vertex-shader" 中来施加 bias 影响;
                    等我们以后添加了 "shader quality tiers", 我们就能正确地解决此问题了;
                    对于目前来说, 我们用一个简陋的办法: 使用一个 constant bias, 
                    同时根据 near 和 far 的中点的大小 来计算 frustum 的 size, 就好像这个 frustum 是个正交透视一样;
                    最后根据 light range 的大小, 稍微调整一下 bias 就能获得不错的效果;
                */

                // half-width (in world-space units) of shadow frustum's "far plane"
                // 计算结果为: spot frustum 远平面的 half-width;
                frustumSize = Mathf.Tan(shadowLight.spotAngle * 0.5f * Mathf.Deg2Rad) * shadowLight.range; 
                
            }
            else if (shadowLight.lightType == LightType.Point)
            {
                /*
                    [Copied from above case:]
                        For perspective projections, shadow texel size varies with depth;
                        It will only work well if done in "receiver side in the pixel shader". 
                        Currently urp do bias on caster side in vertex shader. 
                        When we add shader quality tiers we can properly handle this. 
                        For now, as a poor approximation we do a constant bias and compute the size of the frustum 
                        as if it was orthogonal considering the size at mid point between near and far planes.
                        Depending on how big the light range is, it will be good enough with some tweaks in bias
                        ---
                        在透视空间中, shadow texel 的尺寸随 depth 的变化而变化; 只有在 "shadow 接收端的 frag-shader" 中才能正确处理此问题;
                        但是目前 urp 选择在 "shadow cast 端的 vertex-shader" 中来施加 bias 影响;
                        等我们以后添加了 "shader quality tiers", 我们就能正确地解决此问题了;
                        对于目前来说, 我们用一个简陋的办法: 使用一个 constant bias, 
                        同时根据 near 和 far 的中点的大小 来计算 frustum 的 size, 就好像这个 frustum 是个正交透视一样;
                        最后根据 light range 的大小, 稍微调整一下 bias 就能获得不错的效果;
                        ---
                    Note: HDRP uses normalBias both in "HDShadowUtils.CalcGuardAnglePerspective" 
                    and "HDShadowAlgorithms/EvalShadow_NormalBias" (receiver bias)
                */

                float fovBias = Internal.AdditionalLightsShadowCasterPass.GetPointLightShadowFrustumFovBiasInDegrees(
                    (int)shadowResolution, 
                    (shadowLight.light.shadows == LightShadows.Soft)
                );
                
                // Note: the same fovBias was also used to compute "ShadowUtils.ExtractPointLightMatrix()"
                float cubeFaceAngle = 90 + fovBias;
                // half-width (in world-space units) of shadow frustum's "far plane"
                // 计算结果为: point frustum 远平面的 half-width;
                frustumSize = Mathf.Tan(cubeFaceAngle * 0.5f * Mathf.Deg2Rad) * shadowLight.range; 
                
            }
            else
            {
                Debug.LogWarning("Only point, spot and directional shadow casters are supported in universal pipeline");
                frustumSize = 0.0f;
            }


            // depth and normal bias scale is in shadowmap texel size in world space
            float texelSize = frustumSize / shadowResolution;// 每个 texel 边长 (米为单位) (其实也是 world-space 的单位)
            float depthBias = -shadowData.bias[shadowLightIndex].x * texelSize;
            float normalBias = -shadowData.bias[shadowLightIndex].y * texelSize;

            /*
                The current implementation of NormalBias in urp is the same as in Unity Built-In RP
                (i.e moving shadow caster vertices along normals when projecting them to the shadow map).
                This does not work well with Point Lights, which is why NormalBias value is hard-coded to 0.0 in Built-In RP 
                (see value of unity_LightShadowBias.z in FrameDebugger, 
                and native code that sets it: 
                https://github.cds.internal.unity3d.com/unity/unity/blob/a9c916ba27984da43724ba18e70f51469e0c34f5/Runtime/Camera/Shadows.cpp#L1686 )
                We follow the same convention in Universal RP:
            */
            if (shadowLight.lightType == LightType.Point)
                normalBias = 0.0f;

            if (shadowData.supportsSoftShadows && shadowLight.light.shadows==LightShadows.Soft)
            {
                /*
                    TODO: depth and normal bias assume sample is no more than 1 texel away from shadowmap
                    This is not true with PCF. Ideally we need to do either
                    cone base bias (based on distance to center sample)
                    or receiver place bias based on derivatives.
                    For now we scale it by the PCF kernel size of non-mobile platforms (5x5)
                */
                const float kernelRadius = 2.5f;
                depthBias *= kernelRadius;
                normalBias *= kernelRadius;
            }

            return new Vector4(depthBias, normalBias, 0.0f, 0.0f);
        }//   函数完__





        // 向 shader 写入: "_ShadowBias", "_LightDirection", "_LightPosition";
        public static void SetupShadowCasterConstantBuffer(  //   读完__
                                                        CommandBuffer cmd, 
                                                        ref VisibleLight shadowLight, 
                                                        Vector4 shadowBias // ( depthBias, normalBias, 0, 0 )
        ){
            // ( depthBias, normalBias, 0, 0 )
            cmd.SetGlobalVector("_ShadowBias", shadowBias);

            // Light direction is currently used in shadow caster pass to apply shadow normal offset (normal bias).
            // 方向向量: fragment -> lightPosWS
            Vector3 lightDirection = -shadowLight.localToWorldMatrix.GetColumn(2);
            cmd.SetGlobalVector("_LightDirection", new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));

            // For punctual lights, computing light direction at each vertex position provides more consistent results 
            // (shadow shape does not change when "rotating the point light" for example)
            Vector3 lightPosition = shadowLight.localToWorldMatrix.GetColumn(3);
            cmd.SetGlobalVector("_LightPosition", new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, 1.0f));
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
        internal static Vector4 GetMainLightShadowParams(ref RenderingData renderingData)//   读完__
        {
            // Main Light shadow params
            float mainLightShadowStrength = 0f;//阴影的强度, [0,1], 为 0 时阴影完全消失, 为 1 时阴影最强烈;
            float mainLightSoftShadowsProp = 0f; // 1:支持 soft shadow, 0: 不支持
            if (renderingData.lightData.mainLightIndex != -1)
            {
                mainLightShadowStrength = renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex].light.shadowStrength;

                if (renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex].light.shadows==LightShadows.Soft && 
                    renderingData.shadowData.supportsSoftShadows
                )
                    mainLightSoftShadowsProp = 1f;
            }

            // Shadow params used by both MainLight and AdditionalLights
            float maxShadowDistance = renderingData.cameraData.maxShadowDistance * renderingData.cameraData.maxShadowDistance;// d^2
            //To make the shadow fading fit into a single MAD instruction:
            //distanceCamToPixel2 * oneOverFadeDist + minusStartFade (single MAD)
            float startFade = maxShadowDistance * 0.9f;
            float oneOverFadeDist = 1 / (maxShadowDistance - startFade);
            float minusStartFade = -startFade * oneOverFadeDist;

            return new Vector4(mainLightShadowStrength, mainLightSoftShadowsProp, oneOverFadeDist, minusStartFade);
        }//   函数完__



        
        // in shadowmap receiver shader
        internal static void SetupShadowReceiverConstantBuffer(CommandBuffer cmd, Vector4 mainLightShadowParams)// 读完__
        {
            cmd.SetGlobalVector("_MainLightShadowParams", mainLightShadowParams);
        }



        public static RenderTexture GetTemporaryShadowTexture(int width, int height, int bits)//  读完__
        {
            var shadowTexture = RenderTexture.GetTemporary(
                width, height, 
                bits,             // 16-bits
                m_ShadowmapFormat // "RenderTextureFormat.Shadowmap" 或 "RenderTextureFormat.Depth";
            );

            shadowTexture.filterMode = m_ForceShadowPointSampling ? FilterMode.Point : FilterMode.Bilinear;
            shadowTexture.wrapMode = TextureWrapMode.Clamp;// clamp to [0,1]
            return shadowTexture;
        }



        /*
            此函数内容 + "ApplySliceTransform()", 联合实现的功能, 和 catlike srp 的: "ConvertToAtlasMatrix()"  几乎一致:
                (但缺少了下方的 第 -3- 部分)

            ----------------------------------
            生成一个矩阵, 可以执行空间转换: posWS (物体的) -> posSTS (shadow texture/tile space)

            参数 m 为 P*V 矩阵,  理想状态下, 可直接通过: mul( m, posWS ) 计算出 posSTS. 
            但是目前的 m 矩阵 并不完美:
            -1- 
                作为自定义矩阵, 我们需要手动为其实现 reverse-z 功能
            -2- 
                平行光的 HCS, 其 xyz 三轴皆位于 [-1,1] 区间内 (长得很像 NDC)
                而 depth texture, 其 xy 两轴位于 [0,1] 区间, z值则位于 [0,1] 或 [1,0] 区间 (取决于 reverse-z)
                因此需要将 HCS 的 xyz 三轴 先缩小为 0.5 倍, 再朝三个轴的正方向 移动 0.5 个单位 

            -3- (这一部分在 "ApplySliceTransform()" 中完成 ):
                现在得到了针对整张 depth texture 的 posSTS,
                然后针对每一个 tile, 还要基于自身在 atlas 中的位置, 再次将自己缩小为 0.5倍, 然后做一段偏移. 
                (如果整个 atlas 只有一个 tile, 则不需要这一步) 
        */
        static Matrix4x4 GetShadowTransform(Matrix4x4 proj, Matrix4x4 view)//   读完__
        {
            // Currently CullResults ComputeDirectionalShadowMatricesAndCullingPrimitives() doesn't
            // apply z reversal to projection matrix. We need to do it manually here.
            if (SystemInfo.usesReversedZBuffer)
            {
                // 将 第三行 的元素 取反, 最终会将 参与计算的 向量 float4 的 第三个元素(Z) 取反. 
                // 但是, 此处操作仅仅 将 z值 取反, 还需配合下方 操作才能完成 "Reversed Z" 工作; 
                proj.m20 = -proj.m20;
                proj.m21 = -proj.m21;
                proj.m22 = -proj.m22;
                proj.m23 = -proj.m23;
            }

            Matrix4x4 worldToShadow = proj * view;

            /*
                textureScaleAndBias maps texture space coordinates from [-1,1] to [0,1]
                这个矩阵如下:
                    0.5,    0,    0,   0.5
                      0,  0.5,    0,   0.5 
                      0,    0,  0.5,   0.5 
                      0,    0,    0,     1 
                能让对象 缩小为 0.5倍, 再向3个轴的正方向移动 0.5 个单位;
                -- 让常规分量区间从 [-1,1] 变成 [0,1]
                -- 让上面的反转的 z分量从 [1,-1] 变成 [1,0]
            */
            var textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m22 = 0.5f;
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m23 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;
           
            // Apply texture scale and offset to save a MAD in shader.
            return textureScaleAndBias * worldToShadow;
        }//   函数完__
    }
}
