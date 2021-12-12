#ifndef UNIVERSAL_COPY_DEPTH_PASS_INCLUDED
#define UNIVERSAL_COPY_DEPTH_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#if defined(_DEPTH_MSAA_2)
    #define MSAA_SAMPLES 2
#elif defined(_DEPTH_MSAA_4)
    #define MSAA_SAMPLES 4
#elif defined(_DEPTH_MSAA_8)
    #define MSAA_SAMPLES 8
#else
    #define MSAA_SAMPLES 1
#endif


struct Attributes
{
// xr 才启用
#if _USE_DRAW_PROCEDURAL
    /*   tpr
    uint vertexID     : SV_VertexID;
    */
#else
    float4 positionHCS : POSITION;
    float2 uv         : TEXCOORD0;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};


struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    /*UNITY_VERTEX_OUTPUT_STEREO   tpr  */
};

// ========================================== Vertex Shader ============================================= //

Varyings vert(Attributes input)//  读完__
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    /*UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);   tpr */

    /*
        Note: CopyDepth pass is setup with a mesh already in CS; Therefore, we can just output vertex position

        We need to handle y-flip in a way that all existing shaders using _ProjectionParams.x work.
        Otherwise we get flipping issues like this one (case https://issuetracker.unity3d.com/issues/lwrp-depth-texture-flipy)

        Unity flips projection matrix in non-OpenGL platforms and when rendering to a render texture.
        If URP is rendering to RT:
        -   Source Depth is upside down. 
            We need to copy depth by using a shader that has flipped matrix as well so we have same orientaiton for source and copy depth.
        -   This also guarantess to be standard across if we are using a depth prepass.
        -   When shaders (including shader graph) render objects that sample depth they adjust uv sign with  _ProjectionParams.x. 
            (https://docs.unity3d.com/Manual/SL-PlatformDifferences.html)
        -   All good.

        If URP is NOT rendering to RT neither rendering with OpenGL:
        - Source Depth is NOT fliped. We CANNOT flip when copying depth and don't flip when sampling. (ProjectionParams.x == 1)
        ----------------------------------
        注意:
        本 pass 开始时处理的 就是一个位于 Clip-space 的 mesh, 所以不需要做任何转换, 就能得到 posCS;

        现在所有 shader 都是用 "_ProjectionParams.x" 来处理 y-flip 问题;
        (但是通过下方代码可知, 本pass 似乎没有直接用这个 参数)

        当: (1)在 "非 opengl 平台", (2)同时需要渲染到一个 render texture 时, unity 会翻转 projection矩阵;
        如果 urp 正在渲染进 rt:
        -- src 的 depth 是倒置的; 
            我们需要使用一个 "带有翻转矩阵 的 shader" 来 复制 depth 数据, 
            so we have same orientaiton for source and copy depth.
        
        --  如果我们使用一个 depth prepass, 这也能保证是标准的;

        -- 当 shader 渲染 "采样深度值的 物体" 时, 它们使用 _ProjectionParams.x 来调整 uv 值;

        -- All good.

        如果 urp 并不渲染到 rt, 也不使用 opengl:
        -- Source Depth 不会被翻转, 我们不能在 copy depth 时翻转, 采样时也不能翻转, 此时 _ProjectionParams.x 为 1;
    */

// xr 才启用
#if _USE_DRAW_PROCEDURAL
    /*   tpr
    output.positionCS = GetQuadVertexPosition(input.vertexID);
    output.positionCS.xy = output.positionCS.xy * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f); //convert to -1..1
    output.uv = GetQuadTexCoord(input.vertexID);
    */
#else
    output.positionCS = float4(input.positionHCS.xyz, 1.0);
    output.uv = input.uv;
#endif

    output.positionCS.y *= _ScaleBiasRt.x;
    return output;
}// 函数完__




#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    /* tpr
    #define DEPTH_TEXTURE_MS(name, samples) Texture2DMSArray<float, samples> name
    #define DEPTH_TEXTURE(name) TEXTURE2D_ARRAY_FLOAT(name)
    #define LOAD(uv, sampleIndex) LOAD_TEXTURE2D_ARRAY_MSAA(_CameraDepthAttachment, uv, unity_StereoEyeIndex, sampleIndex)
    #define SAMPLE(uv) SAMPLE_TEXTURE2D_ARRAY(_CameraDepthAttachment, sampler_CameraDepthAttachment, uv, unity_StereoEyeIndex).r
    */
#else
    #define DEPTH_TEXTURE_MS(name, samples) Texture2DMS<float, samples> name
    #define DEPTH_TEXTURE(name) TEXTURE2D_FLOAT(name)
    #define LOAD(uv, sampleIndex) LOAD_TEXTURE2D_MSAA(_CameraDepthAttachment, uv, sampleIndex)
    #define SAMPLE(uv) SAMPLE_DEPTH_TEXTURE(_CameraDepthAttachment, sampler_CameraDepthAttachment, uv)
#endif




#if MSAA_SAMPLES == 1
    DEPTH_TEXTURE(_CameraDepthAttachment);
    SAMPLER(sampler_CameraDepthAttachment);
#else
    // multi-sample

    // 有点奇怪, 在 c#端, 这个texture 只是一个普通的 texture, 但是在 shader 端, 却可以将其绑定为一个 Texture2DMS
    DEPTH_TEXTURE_MS(_CameraDepthAttachment, MSAA_SAMPLES);

    // 此值为: ( 1/w, 1/h, w, h ); (w/h表示 texture 的长宽, pix单位)
    float4 _CameraDepthAttachment_TexelSize;
#endif



#if UNITY_REVERSED_Z
    #define DEPTH_DEFAULT_VALUE 1.0
    #define DEPTH_OP min
#else
    #define DEPTH_DEFAULT_VALUE 0.0
    #define DEPTH_OP max
#endif


// "_CameraDepthAttachment" 是 src 
// "_CameraDepthTexture" 是 dest

float SampleDepth(float2 uv)//   读完__
{
#if MSAA_SAMPLES == 1
    // 简单地把 src 数据 复制到 dest 中去;
    return SAMPLE(uv);
#else
    /*
        multi-sample
        此时不仅执行 depth copy 工作, 还执行了 msaa 滤波
        但是, 对 depth 数据做滤波 是被允许的吗 ??? 
            往下看就会发现, 其实并没有做 "多次采样取平均", 而是 "多次采样取最远值";
            这个操作在 depth 数据上 还是能接受的;
    */
    int2 coord = int2(uv * _CameraDepthAttachment_TexelSize.zw);// posSS pix为单位
    float outDepth = DEPTH_DEFAULT_VALUE;

    UNITY_UNROLL // 展开静态循环: 循环的次数是固定值, 要求编译时将此循环展开
    for (int i = 0; i < MSAA_SAMPLES; ++i)
        // 此处的 LOAD() 函数就是: "Texture2DMS::Load()";
        // 每调用一次, 就传入不同的 sampleIndex, 获得对应的 subsample 值信息;
        // 最后选择所有采样的 depth 中, 最 "远" 的那个;
        outDepth = DEPTH_OP(LOAD(coord, i), outDepth);
    return outDepth;
#endif
}// 函数完__


// ========================================== Fragment Shader ============================================= //

float frag(Varyings input) : SV_Depth
{
    /*UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);    tpr */
    return SampleDepth(input.uv);
}

#endif
