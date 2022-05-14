#ifndef UNIVERSAL_FORWARD_LIT_DEPTH_NORMALS_PASS_INCLUDED
#define UNIVERSAL_FORWARD_LIT_DEPTH_NORMALS_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


#if defined(_DETAIL_MULX2) || defined(_DETAIL_SCALED)
    #define _DETAIL
#endif


// 启用: viewDirTS:
// GLES2 has limited amount of interpolators
// -- parallax map 要使用 viewDirTS,
// -- 但 gles2 能用的 interpolators 数量有限
#if defined(_PARALLAXMAP) && !defined(SHADER_API_GLES)
    #define REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR
#endif


// 启用: tangentWS;  (xyz: tangent, w: sign)
// -- 当使用 normal map 或 parallax map 时, 有时会用到 tangentWS;
// -- 使用 detail maps 的话, 一定要用到 tangentWS;
#if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL)
    #define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif



struct Attributes
{
    float4 positionOS     : POSITION;
    float4 tangentOS      : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float3 normal       : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};


struct Varyings
{
    float4 positionCS   : SV_POSITION;
    float2 uv           : TEXCOORD1;
    half3 normalWS     : TEXCOORD2;

    // -- 当使用 normal map 或 parallax map 时的某些情况下, 要使用 tangentWS; 
    // -- 当使用了 detail map, 一定要用到 tangentWS;
    #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
        half4 tangentWS    : TEXCOORD4;    // xyz: tangent, w: sign
    #endif

    half3 viewDirWS    : TEXCOORD5;

    // parallax map 要使用 viewDirTS
    #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
        half3 viewDirTS     : TEXCOORD8;
    #endif

    UNITY_VERTEX_INPUT_INSTANCE_ID
    //UNITY_VERTEX_OUTPUT_STEREO
};



Varyings DepthNormalsVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    //UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.uv         = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normal, input.tangentOS);

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
    output.normalWS = half3(normalInput.normalWS);


    // -1.1- 当使用 normal map 或 parallax map 时的某些情况下, 要使用 tangentWS; 
    // -1.2- 当使用了 detail map, 一定要用到 tangentWS;
    // --2-- parallax map 要使用 viewDirTS
    #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
        float sign = input.tangentOS.w * float(GetOddNegativeScale());
        half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
    #endif

    // -- 当使用 normal map 或 parallax map 时的某些情况下, 要使用 tangentWS; 
    // -- 当使用了 detail map, 一定要用到 tangentWS;
    #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
        output.tangentWS = tangentWS;
    #endif

    // parallax map 要使用 viewDirTS
    #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
        half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
        output.viewDirTS = viewDirTS;
    #endif

    return output;
}


half4 DepthNormalsFragment(Varyings input) : SV_TARGET
{
    //UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);

    #if defined(_GBUFFER_NORMALS_OCT)
        float3 normalWS = normalize(input.normalWS);
        float2 octNormalWS = PackNormalOctQuadEncode(normalWS);           // values between [-1, +1], must use fp32 on some platforms
        float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);   // values between [ 0,  1]
        half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);      // values between [ 0,  1]
        return half4(packedNormalWS, 0.0);
    #else
        float2 uv = input.uv;
        #if defined(_PARALLAXMAP)
            // parallax map 要使用 viewDirTS
            #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
                half3 viewDirTS = input.viewDirTS;
            #else
                half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, input.viewDirWS);
            #endif
            ApplyPerPixelDisplacement(viewDirTS, uv);
        #endif

        #if defined(_NORMALMAP) || defined(_DETAIL)
            float sgn = input.tangentWS.w;      // should be either +1 or -1
            float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
            float3 normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);

            #if defined(_DETAIL)
                half detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, uv).a;
                float2 detailUv = uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
                normalTS = ApplyDetailNormal(detailUv, normalTS, detailMask);
            #endif

            float3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
        #else
            float3 normalWS = input.normalWS;
        #endif

        return half4(NormalizeNormalPerPixel(normalWS), 0.0);
    #endif
}


#endif
