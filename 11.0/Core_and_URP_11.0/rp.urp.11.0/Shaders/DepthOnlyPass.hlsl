#ifndef UNIVERSAL_DEPTH_ONLY_PASS_INCLUDED
#define UNIVERSAL_DEPTH_ONLY_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"



struct Attributes
{
    float4 position     : POSITION;// posOS
    float2 texcoord     : TEXCOORD0;// uv
    UNITY_VERTEX_INPUT_INSTANCE_ID
};



struct Varyings
{
    float2 uv           : TEXCOORD0;
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    /*UNITY_VERTEX_OUTPUT_STEREO   tpr  */
};



Varyings DepthOnlyVertex(Attributes input) //   读完__
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    /*UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);   tpr */

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionCS = TransformObjectToHClip(input.position.xyz);
    return output;
}


/*
    此 fs 会以 camera 为视角, 计算每个 fragment 的 depth 值, 
    并将此数据 写入 render texture: "_CameraDepthTexture";
    这个计算过程是自动执行的, fs 中不需要编写相关代码;

    目前 fs 中存在的代码, 是用来处理 cutoff 模式的 clip() 操作的;
*/
half4 DepthOnlyFragment(Varyings input) : SV_TARGET //   读完__
{
    /*UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);    tpr */

    //  宏: TEXTURE2D_ARGS: 放在函数参数输入端, 传递: textureName, samplerName;
    
    // 获得并返回 "半透明" 信息;
    // 但是此函数的返回值没有被利用到;
    // 调用此函数仅仅是为了执行 可能存在的 cutoff 模式的 clip() 操作;
    Alpha(
        SampleAlbedoAlpha( input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap) ).a, 
        _BaseColor, 
        _Cutoff
    );

    // 本 fs 不写入 color buffer 中, 所以返回 0
    return 0;
}


#endif
