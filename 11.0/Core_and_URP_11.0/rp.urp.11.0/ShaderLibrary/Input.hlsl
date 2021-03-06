#ifndef UNIVERSAL_INPUT_INCLUDED
#define UNIVERSAL_INPUT_INCLUDED

#define MAX_VISIBLE_LIGHTS_UBO  32
#define MAX_VISIBLE_LIGHTS_SSBO 256

// Keep in sync with RenderingUtils.useStructuredBuffer
#define USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA 0

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderTypes.cs.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Deprecated.hlsl"


// 16, 32, or 256
#if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES30))
    #define MAX_VISIBLE_LIGHTS 16
#elif defined(SHADER_API_MOBILE) || (defined(SHADER_API_GLCORE) && !defined(SHADER_API_SWITCH)) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) // Workaround for bug on Nintendo Switch where SHADER_API_GLCORE is mistakenly defined
    #define MAX_VISIBLE_LIGHTS 32
#else
    #define MAX_VISIBLE_LIGHTS 256
#endif


struct InputData
{
    float3  positionWS;
    half3   normalWS;
    half3   viewDirectionWS;
    float4  shadowCoord;
    half    fogCoord;
    half3   vertexLighting;
    half3   bakedGI;
    float2  normalizedScreenSpaceUV;
    half4   shadowMask;
};

///////////////////////////////////////////////////////////////////////////////
//                      Constant Buffers                                     //
///////////////////////////////////////////////////////////////////////////////


// 启用了 keyword: "_GlossyEnvironmentColor", 无法使用 反射探针时, 
// 此时使用 本 const color 来计算 间接镜反光; 
half4 _GlossyEnvironmentColor;

// Lighting Mode 选择 "Subtractive" 时才被使用;
half4 _SubtractiveShadowColor;

#define _InvCameraViewProj unity_MatrixInvVP

/*
    这个数据和 "_ScreenParams" 相似, 但是额外考虑进了 "pipeline RenderScale"
    ----
    === urp ===:
        x: scaledCameraWidth   叠加了 renderScale 和 DynamicResolution(如果有)
        y: scaledCameraHeight  叠加了 renderScale 和 DynamicResolution(如果有)
        z: 1 + 1/x;
        w: 1 + 1/y;
*/
float4 _ScaledScreenParams;



float4 _MainLightPosition;
half4 _MainLightColor;
half4 _MainLightOcclusionProbes;

// xyz are currently unused
// w: directLightStrength
half4 _AmbientOcclusionParam;

/*
    x: "逐物体 add light" 个数的上限值;  
        并不代表 "IndexMap 中 "非-1" 的元素的个数" (visibleLights 中实际存在的 "合格add light" 的个数 )
*/
half4 _AdditionalLightsCount;


#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    StructuredBuffer<LightData> _AdditionalLightsBuffer;
    StructuredBuffer<int> _AdditionalLightsIndices;
#else
    // GLES3 causes a performance regression in some devices when using CBUFFER.
    // 使用 CBUFFER 时, GLES3 会导致某些设备的性能下降。
    #ifndef SHADER_API_GLES3
    CBUFFER_START(AdditionalLights)
    #endif
        // 16, 32, or 256 个元素
        float4 _AdditionalLightsPosition[MAX_VISIBLE_LIGHTS];
        half4 _AdditionalLightsColor[MAX_VISIBLE_LIGHTS];
        half4 _AdditionalLightsAttenuation[MAX_VISIBLE_LIGHTS];
        half4 _AdditionalLightsSpotDir[MAX_VISIBLE_LIGHTS];
        half4 _AdditionalLightsOcclusionProbes[MAX_VISIBLE_LIGHTS];
    #ifndef SHADER_API_GLES3
    CBUFFER_END
    #endif

#endif


#define UNITY_MATRIX_M     unity_ObjectToWorld
#define UNITY_MATRIX_I_M   unity_WorldToObject
#define UNITY_MATRIX_V     unity_MatrixV
#define UNITY_MATRIX_I_V   unity_MatrixInvV
#define UNITY_MATRIX_P     OptimizeProjectionMatrix(glstate_matrix_projection)
#define UNITY_MATRIX_I_P   unity_MatrixInvP
#define UNITY_MATRIX_VP    unity_MatrixVP
#define UNITY_MATRIX_I_VP  unity_MatrixInvVP
#define UNITY_MATRIX_MV    mul(UNITY_MATRIX_V, UNITY_MATRIX_M)
#define UNITY_MATRIX_T_MV  transpose(UNITY_MATRIX_MV)
#define UNITY_MATRIX_IT_MV transpose(mul(UNITY_MATRIX_I_M, UNITY_MATRIX_I_V))
#define UNITY_MATRIX_MVP   mul(UNITY_MATRIX_VP, UNITY_MATRIX_M)

// Note: #include order is important here.
// UnityInput.hlsl must be included before UnityInstancing.hlsl, so constant buffer
// declarations don't fail because of instancing macros.
// UniversalDOTSInstancing.hlsl must be included after UnityInstancing.hlsl
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalDOTSInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

#endif
