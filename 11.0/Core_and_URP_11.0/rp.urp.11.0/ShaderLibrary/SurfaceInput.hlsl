#ifndef UNIVERSAL_INPUT_SURFACE_INCLUDED
#define UNIVERSAL_INPUT_SURFACE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
TEXTURE2D(_EmissionMap);        SAMPLER(sampler_EmissionMap);

///////////////////////////////////////////////////////////////////////////////
//                      Material Property Helpers                            //
///////////////////////////////////////////////////////////////////////////////





/*
    获得并返回 "半透明" 信息;
    此信息存储在 albedo map 和 固有色 color 中, (依情况而用)
    同时还可能执行 cutoff 模式的 clip() 操作;
*/
half Alpha(half albedoAlpha, half4 color, half cutoff) // 读完__
{
/*
    _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A:
        Smoothness 信息, 存储在 albedo texture 的 alpha 通道中, 而不是在 SpecularMetallic texture 的 alpha 通道中;
    _GLOSSINESS_FROM_BASE_ALPHA:
        SimpleLit 使用:  glossiness 信息存储在 Base map 的 alpha 通道中, 而不是存储在 Specular map 的 alpha 通道中;
*/
#if !defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A) && !defined(_GLOSSINESS_FROM_BASE_ALPHA)
    // 因为 albedo map.alpha 通道存储的就是 半透明信息,
    // 所以要结合 "albedo map" 和 "固有色 color" 两者的 半透明信息;
    half alpha = albedoAlpha * color.a;
#else
    // 因为 albedo map.alpha 通道存储了 Smoothness 信息, 故只能使用 固有色 color 的 alpha信息;
    half alpha = color.a;
#endif

#if defined(_ALPHATEST_ON)
    clip(alpha - cutoff);
#endif

    return alpha;
}



// 内容上看就是个普通的 texture 采样操作;
half4 SampleAlbedoAlpha( //  读完__
                float2 uv, 
                TEXTURE2D_PARAM(albedoAlphaMap, sampler_albedoAlphaMap)// 创建 texture 对象 和 sample 对象;
){
    return SAMPLE_TEXTURE2D(albedoAlphaMap, sampler_albedoAlphaMap, uv);
}



half3 SampleNormal(float2 uv, TEXTURE2D_PARAM(bumpMap, sampler_bumpMap), half scale = 1.0h)
{
#ifdef _NORMALMAP
    half4 n = SAMPLE_TEXTURE2D(bumpMap, sampler_bumpMap, uv);
    #if BUMP_SCALE_NOT_SUPPORTED
        return UnpackNormal(n);
    #else
        return UnpackNormalScale(n, scale);
    #endif
#else
    return half3(0.0h, 0.0h, 1.0h);
#endif
}

half3 SampleEmission(float2 uv, half3 emissionColor, TEXTURE2D_PARAM(emissionMap, sampler_emissionMap))
{
#ifndef _EMISSION
    return 0;
#else
    return SAMPLE_TEXTURE2D(emissionMap, sampler_emissionMap, uv).rgb * emissionColor;
#endif
}

#endif
