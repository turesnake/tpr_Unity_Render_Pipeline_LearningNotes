#ifndef UNITY_DEFERRED_LIBRARY_INCLUDED
#define UNITY_DEFERRED_LIBRARY_INCLUDED

// Deferred lighting / shading helpers


// --------------------------------------------------------
// Vertex shader

struct unity_v2f_deferred {
    float4 pos : SV_POSITION;
    float4 uv : TEXCOORD0;
    float3 ray : TEXCOORD1; // 这到底是啥
};

// 1: 正在处理 平行光
// 0: 正在处理 spot光, point光
float _LightAsQuad;



unity_v2f_deferred vert_deferred (  float4 vertex : POSITION, 
                                    float3 normal : NORMAL
){
    unity_v2f_deferred o;
    o.pos = UnityObjectToClipPos(vertex);
    o.uv = ComputeScreenPos(o.pos); // 此函数返回的是 posSS 的半成品, 未除w


    // 为何要乘以 (-1,-1,1) ?
    // 前半部分计算得到 顶点的 posVS, 它位于 view-space, z轴是反向的, 此值得 z分量为负值;
    // 在后续的 UnityDeferredCalculateLightParams() 函数中, 需要基于此 ray 变量 计算
    // 每个 frag 的 posVS, 中间有一步是: ray / ray.z; 因为 z分量是负的, 这个除法最终会导致 xy分量也取反;
    // 为了避免 xy分量取反, 在这里先将 xy分量 取反, 以便后续计算;
    o.ray = UnityObjectToViewPos(vertex) * float3(-1,-1,1);

    // normal contains a ray pointing from the camera to one of near plane's corners in camera space 
    // when we are drawing a full screen quad.
    // Otherwise, when rendering 3D shapes, use the ray calculated here.
    // ++++++++
    // normal:
    // 若在 平行光 pass, normal 携带一个 方向向量(带模长):
    // 它从 camera 射向 near plane quad 四端点之一; 且这个方向向量是位于 camera-space (注意!, 是左手系空间)
    // 所以 normal 不需要乘以 (-1,-1,1)... 
    // ---
    // 否则, 当本 pass 处理的是一个 3d模型 (spot光, point光), 则使用上面计算出的 ray 值;
    o.ray = lerp(o.ray, normal, _LightAsQuad);

    return o;
}


// --------------------------------------------------------
// Shared uniforms


UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

float4 _LightDir; // 注意方向: light->frag, 使用时要取反

// xyz: posWS;
// w:  存储 light range相关信息, 猜测是: 1/range^2 (spot光中)
float4 _LightPos;
float4 _LightColor;
float4 unity_LightmapFade;
float4x4 unity_WorldToLight;
sampler2D_float _LightTextureB0;

#if defined (POINT_COOKIE)
samplerCUBE_float _LightTexture0;
#else
sampler2D_float _LightTexture0;
#endif

#if defined (SHADOWS_SCREEN)
sampler2D _ShadowMapTexture;
#endif

#if defined (SHADOWS_SHADOWMASK)
sampler2D _CameraGBufferTexture4;
#endif

// --------------------------------------------------------
// Shadow/fade helpers

// Receiver plane depth bias create artifacts when depth is retrieved from
// the depth buffer. see UnityGetReceiverPlaneDepthBias in UnityShadowLibrary.cginc
#ifdef UNITY_USE_RECEIVER_PLANE_BIAS
    #undef UNITY_USE_RECEIVER_PLANE_BIAS
#endif

#include "UnityShadowLibrary.cginc"


//Note :
// SHADOWS_SHADOWMASK + LIGHTMAP_SHADOW_MIXING -> ShadowMask mode
// SHADOWS_SHADOWMASK only -> Distance shadowmask mode

// --------------------------------------------------------
half UnityDeferredSampleShadowMask(float2 uv)
{
    half shadowMaskAttenuation = 1.0f;

    #if defined (SHADOWS_SHADOWMASK)
        half4 shadowMask = tex2D(_CameraGBufferTexture4, uv);
        shadowMaskAttenuation = saturate(dot(shadowMask, unity_OcclusionMaskSelector));
    #endif

    return shadowMaskAttenuation;
}

// --------------------------------------------------------
half UnityDeferredSampleRealtimeShadow(half fade, float3 vec, float2 uv)
{
    half shadowAttenuation = 1.0f;

    #if defined (DIRECTIONAL) || defined (DIRECTIONAL_COOKIE)
        #if defined(SHADOWS_SCREEN)
            shadowAttenuation = tex2D(_ShadowMapTexture, uv).r;
        #endif
    #endif

    #if defined(UNITY_FAST_COHERENT_DYNAMIC_BRANCHING) && defined(SHADOWS_SOFT) && !defined(LIGHTMAP_SHADOW_MIXING)
    //avoid expensive shadows fetches in the distance where coherency will be good
    UNITY_BRANCH
    if (fade < (1.0f - 1e-2f))
    {
    #endif

        #if defined(SPOT)
            #if defined(SHADOWS_DEPTH)
                float4 shadowCoord = mul(unity_WorldToShadow[0], float4(vec, 1));
                shadowAttenuation = UnitySampleShadowmap(shadowCoord);
            #endif
        #endif

        #if defined (POINT) || defined (POINT_COOKIE)
            #if defined(SHADOWS_CUBE)
                shadowAttenuation = UnitySampleShadowmap(vec);
            #endif
        #endif

    #if defined(UNITY_FAST_COHERENT_DYNAMIC_BRANCHING) && defined(SHADOWS_SOFT) && !defined(LIGHTMAP_SHADOW_MIXING)
    }
    #endif

    return shadowAttenuation;
}

// --------------------------------------------------------
half UnityDeferredComputeShadow(float3 vec, float fadeDist, float2 uv)
{

    half fade                      = UnityComputeShadowFade(fadeDist);
    half shadowMaskAttenuation     = UnityDeferredSampleShadowMask(uv);
    half realtimeShadowAttenuation = UnityDeferredSampleRealtimeShadow(fade, vec, uv);

    return UnityMixRealtimeAndBakedShadows(realtimeShadowAttenuation, shadowMaskAttenuation, fade);
}



// --------------------------------------------------------
// Common lighting data calculation (direction, attenuation, ...)
void UnityDeferredCalculateLightParams (
    unity_v2f_deferred i,
    out float3 outWorldPos,
    out float2 outUV,
    out half3 outLightDir,
    out float outAtten,
    out float outFadeDist)
{

    // ++++++++++++++++++++++++++++++++++++++++
    // 注意: 
    // 在下文语境中, camera-space 是 左手系的, 不是我们认识中的那个 view-space (右手系)
    // 这使得 下面不少向量, 矩阵的 z轴都是反转的...

    // _ProjectionParams.z: far plane
    // 计算得到的 ray: 从 camera 射向各 frag 方向, 终点位于 far plane;
    // 注意, 这个 ray 的 z值现在是正数
    i.ray = i.ray * (_ProjectionParams.z / i.ray.z);
    float2 uv = i.uv.xy / i.uv.w;// 完整的 uvSS

    // read depth and reconstruct world position
    float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
    depth = Linear01Depth (depth);

    // 本 frag 的 posVS 和 posWS
    float4 vpos = float4(i.ray * depth,1); // 不是真正的 posVS, 它的 z轴反转过了

    // unity_CameraToWorld 矩阵 不是真正的 "camera->world矩阵", 它的z轴翻转了;
    // 原本我们可以使用 "正确的" UNITY_MATRIX_I_V 矩阵 来取代;
    // 在 spot光, point光 pass 中, 确实能这么做,
    // 但在 平行光 pass 中, UNITY_MATRIX_I_V 是个单位矩阵, unity 压根就没往里面写入数据....
    // 感觉这是一个 bug.....
    // 所以还是在这里老老实实使用 这个 z轴翻转过的矩阵吧...
    float3 wpos = mul (unity_CameraToWorld, vpos).xyz;// 真正的 posWS
    

    float fadeDist = UnityComputeShadowFadeDistance(wpos, vpos.z);

    // spot light case
    #if defined (SPOT)
        float3 tolight = _LightPos.xyz - wpos;
        half3 lightDir = normalize (tolight);

        float4 uvCookie = mul (unity_WorldToLight, float4(wpos,1));
        // negative bias because http://aras-p.info/blog/2010/01/07/screenspace-vs-mip-mapping/
        float atten = tex2Dbias (_LightTexture0, float4(uvCookie.xy / uvCookie.w, 0, -8)).w;

        // 其实照亮了两个 方锥区, 另一个在反方向上(类似漏斗), 需要把反方向的那个剔除掉;
        // 否则, 如果一个物体在 ws 中位于 反方锥区, 在 screen-space 中又恰好位于 被渲染的区, 
        // 那么它也会被错误地照亮
        // 猜测是 比较运算得到的 true,false 能转换成 0,1 值;
        atten *= uvCookie.w < 0;

        float att = dot(tolight, tolight) * _LightPos.w;
        atten *= tex2D (_LightTextureB0, att.rr).r;

        atten *= UnityDeferredComputeShadow (wpos, fadeDist, uv);

    // directional light case
    #elif defined (DIRECTIONAL) || defined (DIRECTIONAL_COOKIE)
        half3 lightDir = -_LightDir.xyz;
        float atten = 1.0;

        atten *= UnityDeferredComputeShadow (wpos, fadeDist, uv);

        #if defined (DIRECTIONAL_COOKIE)
        atten *= tex2Dbias (_LightTexture0, float4(mul(unity_WorldToLight, half4(wpos,1)).xy, 0, -8)).w;
        #endif //DIRECTIONAL_COOKIE

    // point light case
    #elif defined (POINT) || defined (POINT_COOKIE)
        float3 tolight = wpos - _LightPos.xyz;
        half3 lightDir = -normalize (tolight);

        float att = dot(tolight, tolight) * _LightPos.w;
        float atten = tex2D (_LightTextureB0, att.rr).r;

        atten *= UnityDeferredComputeShadow (tolight, fadeDist, uv);

        #if defined (POINT_COOKIE)
        atten *= texCUBEbias(_LightTexture0, float4(mul(unity_WorldToLight, half4(wpos,1)).xyz, -8)).w;
        #endif //POINT_COOKIE
    #else
        half3 lightDir = 0;
        float atten = 0;
    #endif

    outWorldPos = wpos;
    outUV = uv;
    outLightDir = lightDir;
    outAtten = atten;
    outFadeDist = fadeDist;
}



#endif // UNITY_DEFERRED_LIBRARY_INCLUDED
