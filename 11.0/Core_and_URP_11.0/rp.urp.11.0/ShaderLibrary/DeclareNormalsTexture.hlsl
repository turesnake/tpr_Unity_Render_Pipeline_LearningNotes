#ifndef UNITY_DECLARE_NORMALS_TEXTURE_INCLUDED
#define UNITY_DECLARE_NORMALS_TEXTURE_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D_X_FLOAT(_CameraNormalsTexture);
SAMPLER(sampler_CameraNormalsTexture);



/*
    目前版本, 本函数在 mac(Metal) 下存在 bug: 
        此时, 本函数得到的值 不是 view-space 下的法线, 而是 world-space 下的法线;
        这个问题 应该是在更早之前, 向 _CameraNormalsTexture 写入数据时 留下的;
*/
float3 SampleSceneNormals(float2 uv)
{
    return UnpackNormalOctRectEncode(
        SAMPLE_TEXTURE2D_X(
            _CameraNormalsTexture, 
            sampler_CameraNormalsTexture, 
            UnityStereoTransformScreenSpaceTex(uv) // 等于 uv, 对于 "非xr程序", 此宏啥也不做;
        ).xy
    ) 
    * float3(1.0, 1.0, -1.0); // 仅仅是反转 z轴方向, 
}


float3 LoadSceneNormals(uint2 uv)
{
    return UnpackNormalOctRectEncode(LOAD_TEXTURE2D_X(_CameraNormalsTexture, uv).xy) * float3(1.0, 1.0, -1.0);
}
#endif
