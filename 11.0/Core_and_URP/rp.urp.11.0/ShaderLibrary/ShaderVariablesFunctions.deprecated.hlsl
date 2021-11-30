#ifndef UNITY_SHADER_VARIABLES_FUNCTIONS_DEPRECATED_INCLUDED
#define UNITY_SHADER_VARIABLES_FUNCTIONS_DEPRECATED_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"

// Deprecated: A confusingly named and duplicate function that scales clipspace to unity NDC range. (-w < x(-y) < w --> 0 < xy < w)
// Use GetVertexPositionInputs().positionNDC instead for vertex shader
// Or a similar function in Common.hlsl, ComputeNormalizedDeviceCoordinatesWithZ()

// 已废弃: 一个 名称混乱且重复的函数, 它将 clip-space [-w < x(-y) < w] 变换到 NDC 范围内 [0 < xy < w]
// 在 vertex shader 内, 使用 GetVertexPositionInputs().positionNDC 来代替之
// 或使用位于 Common.hlsl 中的一个类似的函数: ComputeNormalizedDeviceCoordinatesWithZ()

// 此处的实现, 和 8.2.0 中的 是一摸一样的

// 具体原理可查找笔记: "如何获得像素的 posSS 屏幕空间坐标"
 
float4 ComputeScreenPos(float4 positionCS)
{
    float4 o = positionCS * 0.5f;
    o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
    o.zw = positionCS.zw;
    return o;
}

#endif // UNITY_SHADER_VARIABLES_FUNCTIONS_DEPRECATED_INCLUDED
