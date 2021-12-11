#ifndef UNIVERSAL_DEPTH_ONLY_PASS_INCLUDED
#define UNIVERSAL_DEPTH_ONLY_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


struct Attributes
{
    float4 positionOS       : POSITION;
    float4 tangentOS        : TANGENT;
    float2 texcoord         : TEXCOORD0;// uv
    float3 normal           : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS       : SV_POSITION;
    float2 uv               : TEXCOORD1;
    float3 normalWS         : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    /*UNITY_VERTEX_OUTPUT_STEREO   tpr  */
};



Varyings DepthNormalsVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    /*UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);   tpr */

    output.uv         = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normal, input.tangentOS);
    output.normalWS = NormalizeNormalPerVertex(normalInput.normalWS);

    return output;
}



/*
    此 fs 会以 camera 为视角, 计算每个 fragment 的 depth 值, normal 值, 
    -- depth 值 写入 render texture: "_CameraDepthTexture";
    -- normal 值写入 render texture: "_CameraNormalsTexture"; {R:16bits, G:16bits}

    depth 的计算过程是自动执行的, fs 中不需要编写相关代码;
*/
float4 DepthNormalsFragment(Varyings input) : SV_TARGET
{
    /*UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);    tpr */

    //  宏: TEXTURE2D_ARGS: 放在函数参数输入端, 传递: textureName, samplerName;

    // 获得并返回 "半透明" 信息;
    // 但是此函数的返回值没有被利用到;
    // 调用此函数仅仅是为了执行 可能存在的 cutoff 模式的 clip() 操作;
    Alpha(
        SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, 
        _BaseColor, 
        _Cutoff
    );

    // 使用 八面体压缩算法, 把 归一化的 法线向量(x,y,z), 
    // 压缩为一个 real2 向量, 分量区间: [-1,1]
    return float4(
        PackNormalOctRectEncode(TransformWorldToViewDir(input.normalWS, true)), 
        0.0, 
        0.0
    );
}
#endif
