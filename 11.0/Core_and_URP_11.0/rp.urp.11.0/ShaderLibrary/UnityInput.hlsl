// UNITY_SHADER_NO_UPGRADE

#ifndef UNIVERSAL_SHADER_VARIABLES_INCLUDED
#define UNIVERSAL_SHADER_VARIABLES_INCLUDED

/* tpr
#if defined(STEREO_INSTANCING_ON) && (defined(SHADER_API_D3D11) || defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE) || defined(SHADER_API_PSSL) || defined(SHADER_API_VULKAN))
#define UNITY_STEREO_INSTANCING_ENABLED
#endif
*/

/* tpr
#if defined(STEREO_MULTIVIEW_ON) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE) || defined(SHADER_API_VULKAN)) && !(defined(SHADER_API_SWITCH))
    #define UNITY_STEREO_MULTIVIEW_ENABLED
#endif
*/

/* tpr
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
#define USING_STEREO_MATRICES
#endif
*/

/* tpr
#if defined(USING_STEREO_MATRICES)
    // Current pass transforms.
    #define glstate_matrix_projection     unity_StereoMatrixP[unity_StereoEyeIndex] // goes through GL.GetGPUProjectionMatrix()
    #define unity_MatrixV                 unity_StereoMatrixV[unity_StereoEyeIndex]
    #define unity_MatrixInvV              unity_StereoMatrixInvV[unity_StereoEyeIndex]
    #define unity_MatrixInvP              unity_StereoMatrixInvP[unity_StereoEyeIndex]
    #define unity_MatrixVP                unity_StereoMatrixVP[unity_StereoEyeIndex]
    #define unity_MatrixInvVP             unity_StereoMatrixInvVP[unity_StereoEyeIndex]

    // Camera transform (but the same as pass transform for XR).
    #define unity_CameraProjection        unity_StereoCameraProjection[unity_StereoEyeIndex] // Does not go through GL.GetGPUProjectionMatrix()
    #define unity_CameraInvProjection     unity_StereoCameraInvProjection[unity_StereoEyeIndex]
    #define unity_WorldToCamera           unity_StereoMatrixV[unity_StereoEyeIndex] // Should be unity_StereoWorldToCamera but no use-case in XR pass
    #define unity_CameraToWorld           unity_StereoMatrixInvV[unity_StereoEyeIndex] // Should be unity_StereoCameraToWorld but no use-case in XR pass
    #define _WorldSpaceCameraPos          unity_StereoWorldSpaceCameraPos[unity_StereoEyeIndex]
#endif
*/


#define UNITY_LIGHTMODEL_AMBIENT (glstate_lightmodel_ambient * 2)

// ----------------------------------------------------------------------------

// Time (t = time since current level load) values from Unity

float4 _Time; // (t/20, t, t*2, t*3)
float4 _SinTime; // sin(t/8), sin(t/4), sin(t/2), sin(t)
float4 _CosTime; // cos(t/8), cos(t/4), cos(t/2), cos(t)
float4 unity_DeltaTime; // deltaTime, 1/deltaTime, smoothDeltaTime, 1/smoothDeltaTime
float4 _TimeParameters; // t, sin(t), cos(t), 0



#if !defined(USING_STEREO_MATRICES) // 非xr
    float3 _WorldSpaceCameraPos; // camera.transform.position
#endif

/*
    x = 1 or -1 (-1 if projection is flipped)
        标记 投影变换 (posVS -> posHCS) 过程中, 是否把 y轴的方向 上下颠倒了.
        ---
        unity 遵循 OpenGL风格. 
        通过 .x 来指示, unity 有没有帮你翻转 投影变换的 纵坐标 (朝上->朝下)
        值为 -1 就说明没翻好 (当前朝下,需要再手动翻回 朝上):
        if (_ProjectionParams.x < 0){
            pos.y = 1 - pos.y;
        }
     
    y = near plane
    z = far plane
    w = 1/far plane
*/
float4 _ProjectionParams;


/*
    === 原始含义 ===:
    x = width    
    y = height
    z = 1 + 1.0/width
    w = 1 + 1.0/height
    ---
    === urp ===:
        (其实内容 和 上文的原始含义是一致的)
        x: baseCamera.pixelRect.width
        y: baseCamera.pixelRect.height
        z: 1 + 1/x
        w: 1 + 1/y
        ----------
        urp 中还有一个 相似的数据: "_ScaledScreenParams"
*/
float4 _ScreenParams;


/*
    (http://www.humus.name/temp/Linearize%20depth.txt) 参考笔记图: "Linear01Depth.jpg"
    -1-:
    Values used to linearize the Z buffer 
    此时 SystemInfo.usesReversedZBuffer == false;
    这是 manual 中记录的格式:
        x = 1-far/near
        y = far/near
        z = x/far
        w = y/far
    --------------------------
    or in case of a reversed depth buffer (UNITY_REVERSED_Z is 1)
    此时 SystemInfo.usesReversedZBuffer == true;
    这是大多数平台 的 主要格式:
        x = -1+far/near
        y = 1
        z = x/far
        w = 1/far
    --------------------------
    # 为什么会被设计成这样 ？？？
    当我们分别将 两种版本的 值，送入函数: LinearEyeDepth(), Linear01Depth()
    发现了有趣的现象：
        不管 SystemInfo.usesReversedZBuffer 是否为 true 这两个函数 都能稳定地工作, 调用方将无法察觉到这层差异;
*/
float4 _ZBufferParams;


/*
    === urp ===:
    正交透视时 使用的数据:
    x = orthographic camera's width:   camera.orthographicSize * cameraData.aspectRatio
    y = orthographic camera's height:  camera.orthographicSize
    z = 0.0 (unused)
    w = 1 为 正交, 0 为透视
*/
float4 unity_OrthoParams;


/*
    scaleBias.x = flipSign; 
    scaleBias.y = scale
    scaleBias.z = bias
    scaleBias.w = unused
    -----
    其实这几个分量都被用到了...
*/
uniform float4 _ScaleBias;

/*
    这个值有时可能和 _ScaleBias 是相同得;
    ---
    x:  如果: 非opengl平台 且要写入 render texture, 此值为 -1, 
        表示需要 用户手动执行 uv 值 y方向的翻转;
*/
uniform float4 _ScaleBiasRt;



float4 unity_CameraWorldClipPlanes[6];



#if !defined(USING_STEREO_MATRICES) // 非xr
    // Projection matrices of the camera. Note that this might be different from projection matrix
    // that is set right now, e.g. while rendering shadows the matrices below are still the projection
    // of original camera.
    float4x4 unity_CameraProjection;
    float4x4 unity_CameraInvProjection;

    // 在 "unity_MatrixV" 的基础上, "unity_WorldToCamera" 额外翻转了自己的左右手特性;
    float4x4 unity_WorldToCamera; // 没在 urp 中见到此值被使用
    float4x4 unity_CameraToWorld;
#endif


// ----------------------------------------------------------------------------

// Block Layout should be respected due to SRP Batcher
CBUFFER_START(UnityPerDraw)
    // ----- Space block Feature -----
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    // x is the fade value
    //   对于 左侧区的物体(本体): [  1 <- 0 ]
    //   对于 右侧区的物体(前体): [ -1 <- 0 ]
    //   注意方向, 从右向左,表示相机从远及近. 
    // y is x quantized into 16 levels
    float4   unity_LODFade;       
    
    /*
        w: is usually 1.0, or -1.0; (for odd-negative scale transforms)
        ---
        catlike: 如果此值为 -1, 你需要将 binormal 翻转;
        (搜索笔记此变量)
        还可看此文:
        https://forum.unity.com/threads/what-is-tangent-w-how-to-know-whether-its-1-or-1-tangent-w-vs-unity_worldtransformparams-w.468395/
    */
    real4    unity_WorldTransformParams; 

    // ----- Light Indices block feature -----
    // These are set internally by the engine 
    // upon request by RendererConfiguration.

    /*
        y: 光源数量 (?)
    */
    real4 unity_LightData;
    real4 unity_LightIndices[2];
    float4 unity_ProbesOcclusion;

    // ----- Reflection Probe 0 block feature -----
    // HDR environment map decode instructions
    // 主要作为 "DecodeHDREnvironment()" 的参数;
    // 需要去网页查找 它的具体填入数据...
    real4 unity_SpecCube0_HDR;


    // ----- Lightmap block feature -----
    float4 unity_LightmapST;
    float4 unity_LightmapIndex;
    float4 unity_DynamicLightmapST;

    // ----- SH block feature -----
    real4 unity_SHAr;
    real4 unity_SHAg;
    real4 unity_SHAb;
    real4 unity_SHBr;
    real4 unity_SHBg;
    real4 unity_SHBb;
    real4 unity_SHC;
CBUFFER_END




/* tpr
#if defined(USING_STEREO_MATRICES)
CBUFFER_START(UnityStereoViewBuffer)
    float4x4 unity_StereoMatrixP[2];
    float4x4 unity_StereoMatrixInvP[2];
    float4x4 unity_StereoMatrixV[2];
    float4x4 unity_StereoMatrixInvV[2];
    float4x4 unity_StereoMatrixVP[2];
    float4x4 unity_StereoMatrixInvVP[2];

    float4x4 unity_StereoCameraProjection[2];
    float4x4 unity_StereoCameraInvProjection[2];

    float3   unity_StereoWorldSpaceCameraPos[2];
    float4   unity_StereoScaleOffset[2];
CBUFFER_END
#endif
*/


/* tpr
#if defined(USING_STEREO_MATRICES) && defined(UNITY_STEREO_MULTIVIEW_ENABLED)
CBUFFER_START(UnityStereoEyeIndices)
    float4 unity_StereoEyeIndices[2];
CBUFFER_END
#endif
*/

/* tpr
#if defined(UNITY_STEREO_MULTIVIEW_ENABLED) && defined(SHADER_STAGE_VERTEX)
// OVR_multiview
// In order to convey this info over the DX compiler, we wrap it into a cbuffer.
#if !defined(UNITY_DECLARE_MULTIVIEW)
#define UNITY_DECLARE_MULTIVIEW(number_of_views) CBUFFER_START(OVR_multiview) uint gl_ViewID; uint numViews_##number_of_views; CBUFFER_END
#define UNITY_VIEWID gl_ViewID
#endif
#endif
*/


/* tpr
#if defined(UNITY_STEREO_MULTIVIEW_ENABLED) && defined(SHADER_STAGE_VERTEX)
    #define unity_StereoEyeIndex UNITY_VIEWID
    UNITY_DECLARE_MULTIVIEW(2);
#elif defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    static uint unity_StereoEyeIndex;
#elif defined(UNITY_SINGLE_PASS_STEREO)
    CBUFFER_START(UnityStereoEyeIndex)
        int unity_StereoEyeIndex;
    CBUFFER_END
#endif
*/


float4x4 glstate_matrix_transpose_modelview0;

// ----------------------------------------------------------------------------

real4 glstate_lightmodel_ambient;

// 环境光: 顶光, 赤道光, 底光 (暂未看到被 urp 使用)
real4 unity_AmbientSky;
real4 unity_AmbientEquator;
real4 unity_AmbientGround;

real4 unity_IndirectSpecColor;
float4 unity_FogParams;
real4  unity_FogColor;


#if !defined(USING_STEREO_MATRICES) // 非xr
    float4x4 glstate_matrix_projection;
    float4x4 unity_MatrixV;
    float4x4 unity_MatrixInvV;
    float4x4 unity_MatrixInvP;
    float4x4 unity_MatrixVP;
    float4x4 unity_MatrixInvVP;
    float4  unity_StereoScaleOffset;
    int     unity_StereoEyeIndex;
#endif

real4 unity_ShadowColor;

// ----------------------------------------------------------------------------

// Unity specific
TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);

// Main lightmap
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);
TEXTURE2D_ARRAY(unity_Lightmaps);
SAMPLER(samplerunity_Lightmaps);

// Dual or directional lightmap (always used with unity_Lightmap, so can share sampler)
TEXTURE2D(unity_LightmapInd);
TEXTURE2D_ARRAY(unity_LightmapsInd);

TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);
TEXTURE2D_ARRAY(unity_ShadowMasks);
SAMPLER(samplerunity_ShadowMasks);

// ----------------------------------------------------------------------------

// TODO: all affine matrices should be 3x4.
// TODO: sort these vars by the frequency of use (descending), and put commonly used vars together.
// Note: please use UNITY_MATRIX_X macros instead of referencing matrix variables directly.
float4x4 _PrevViewProjMatrix;
float4x4 _ViewProjMatrix;
float4x4 _NonJitteredViewProjMatrix;
float4x4 _ViewMatrix;
float4x4 _ProjMatrix;
float4x4 _InvViewProjMatrix;
float4x4 _InvViewMatrix;
float4x4 _InvProjMatrix;
float4   _InvProjParam;
float4   _ScreenSize;       // {w, h, 1/w, 1/h}
float4   _FrustumPlanes[6]; // {(a, b, c) = N, d = -dot(N, P)} [L, R, T, B, N, F]


float4x4 OptimizeProjectionMatrix(float4x4 M)
{
    // Matrix format (x = non-constant value).
    // Orthographic Perspective  Combined(OR)
    // | x 0 0 x |  | x 0 x 0 |  | x 0 x x |
    // | 0 x 0 x |  | 0 x x 0 |  | 0 x x x |
    // | x x x x |  | x x x x |  | x x x x | <- oblique projection row
    // | 0 0 0 1 |  | 0 0 x 0 |  | 0 0 x x |
    // Notice that some values are always 0.
    // We can avoid loading and doing math with constants.
    M._21_41 = 0;
    M._12_42 = 0;
    return M;
}

#endif // UNIVERSAL_SHADER_VARIABLES_INCLUDED
