#ifndef UNIVERSAL_FULLSCREEN_INCLUDED
#define UNIVERSAL_FULLSCREEN_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


/*      tpr
#if _USE_DRAW_PROCEDURAL
    void GetProceduralQuad(in uint vertexID, out float4 positionCS, out float2 uv)
    {
        positionCS = GetQuadVertexPosition(vertexID);
        positionCS.xy = positionCS.xy * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f);
        uv = GetQuadTexCoord(vertexID) * _ScaleBias.xy + _ScaleBias.zw;
    }
#endif
*/



struct Attributes
{
// xr 才启用
#if _USE_DRAW_PROCEDURAL
    /*   tpr
    uint vertexID     : SV_VertexID;
    */
#else
    float4 positionOS : POSITION;  // posOS
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


// 没看出来有啥 特殊点 ...
Varyings FullscreenVert(Attributes input)// 读完__
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    /*UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);   tpr */

// xr 才启用
#if _USE_DRAW_PROCEDURAL
    /*   tpr
    output.positionCS = GetQuadVertexPosition(input.vertexID);
    output.positionCS.xy = output.positionCS.xy * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f); //convert to -1..1
    output.uv = GetQuadTexCoord(input.vertexID) * _ScaleBias.xy + _ScaleBias.zw;
    */
#else
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = input.uv;
#endif

    return output;
}



Varyings Vert(Attributes input)
{
    return FullscreenVert(input);
}

#endif
