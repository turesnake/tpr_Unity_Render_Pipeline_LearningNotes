Shader "Hidden/Universal Render Pipeline/LutBuilderHdr"
{
    HLSLINCLUDE

        #pragma exclude_renderers gles
        #pragma multi_compile_local _ _TONEMAP_ACES _TONEMAP_NEUTRAL

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ACES.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        // x: utHeight,  -- 像素个数, 比如为 32;
        // y: 0.5f / lutWidth, 
        // z: 0.5f / lutHeight,
        // w: lutHeight / (lutHeight - 1f)
        float4 _Lut_Params;         
        float4 _ColorBalance;       // xyz: LMS coeffs, w: unused
        float4 _ColorFilter;        // xyz: color, w: unused        "colorAdjustments.colorFilter"
        float4 _ChannelMixerRed;    // xyz: rgb coeffs, w: unused
        float4 _ChannelMixerGreen;  // xyz: rgb coeffs, w: unused
        float4 _ChannelMixerBlue;   // xyz: rgb coeffs, w: unused

        // x: colorAdjustments.hueShift / 360f, 
        // y: colorAdjustments.saturation / 100f + 1f, 
        // z: colorAdjustments.contrast / 100f + 1f, 
        // w: 0f
        float4 _HueSatCon;          // x: hue shift, y: saturation, z: contrast, w: unused

        float4 _Lift;               // xyz: color, w: unused
        float4 _Gamma;              // xyz: color, w: unused
        float4 _Gain;               // xyz: color, w: unused
        float4 _Shadows;            // xyz: color, w: unused
        float4 _Midtones;           // xyz: color, w: unused
        float4 _Highlights;         // xyz: color, w: unused

        // x: shadowsMidtonesHighlights.shadowsStart,
        // y: shadowsMidtonesHighlights.shadowsEnd,
        // z: shadowsMidtonesHighlights.highlightsStart,
        // w: shadowsMidtonesHighlights.highlightsEnd
        float4 _ShaHiLimits;        // xy: shadows min/max, zw: highlight min/max

        float4 _SplitShadows;       // xyz: color, w: balance
        float4 _SplitHighlights;    // xyz: color, w: unused


        // --- ColorCurves 模块 的数据:

        // 下面8个 texture, 都是由曲线转换而得; 
        // (w=128,h=1); 将曲线的 x轴[0,1] 区间均匀分为 128份, 每个节点计算自己的 y轴值, 存入对用的 texel.r 通道;
        TEXTURE2D(_CurveMaster);
        TEXTURE2D(_CurveRed);
        TEXTURE2D(_CurveGreen);
        TEXTURE2D(_CurveBlue);
        TEXTURE2D(_CurveHueVsHue);
        TEXTURE2D(_CurveHueVsSat);
        TEXTURE2D(_CurveSatVsSat);
        TEXTURE2D(_CurveLumVsSat);



        float EvaluateCurve(TEXTURE2D(curve), float t)
        {
            // 简单地采样 curve texture 的 r通道值, 
            float x = SAMPLE_TEXTURE2D(curve, sampler_LinearClamp, float2(t, 0.0)).x;
            return saturate(x);
        }//  函数完__


        /*
            Note: when the ACES tonemapper is selected the grading steps will be done using ACES spaces

            对颜色值执行各种后处理; 返回的是 colorLinear: [0, 58.856];

            参数: colorLutSpace:
                一个颜色值; rgb:[0/31, 31/31];
        */
        float3 ColorGrade(float3 colorLutSpace)//    读完__ 粗略, 每个后处理模块 没细看
        {
            // Switch back to linear
            // 传入一个 [0,1] 颜色值, 获得一个 [0, 58.856] 颜色值; 非常匹配 float-16 格式的存储精度;
            // 这个过程也称不上是 "LogC->Linear" 的转换... 
            float3 colorLinear = LogCToLinear(colorLutSpace);

            
            // ===============================:
            // White balance in LMS space 
            // LMS space: 它将颜色描述为人眼中 三种感光锥细胞 的反应。
            float3 colorLMS = LinearToLMS(colorLinear);
            colorLMS *= _ColorBalance.xyz;
            colorLinear = LMSToLinear(colorLMS);

            // Do contrast in log after white balance
            #if _TONEMAP_ACES
                float3 colorLog = ACES_to_ACEScc(unity_to_ACES(colorLinear));
            #else
                float3 colorLog = LinearToLogC(colorLinear);
            #endif

            colorLog = (colorLog - ACEScc_MIDGRAY) * _HueSatCon.z + ACEScc_MIDGRAY;

            #if _TONEMAP_ACES
                colorLinear = ACES_to_ACEScg(ACEScc_to_ACES(colorLog));
            #else
                colorLinear = LogCToLinear(colorLog);
            #endif

            // ===============================:
            // "colorAdjustments.colorFilter"
            // Color filter is just an unclipped multiplier 未裁剪的乘数
            colorLinear *= _ColorFilter.xyz;

            // Do NOT feed negative values to the following color ops
            colorLinear = max(0.0, colorLinear);

            // ===============================:
            // Split toning
            // As counter-intuitive as it is,(与直接相反) to make split-toning work the same way it does in Adobe products
            // we have to do all the maths in gamma-space...
            // ---
            // 与直接相反, 想要获得与 adobe 软件相同的效果, "Split toning" 必须在 gamma 空间内执行;
            float balance = _SplitShadows.w;
            float3 colorGamma = PositivePow(colorLinear, 1.0 / 2.2);

            float luma = saturate(GetLuminance(saturate(colorGamma)) + balance);
            float3 splitShadows = lerp((0.5).xxx, _SplitShadows.xyz, 1.0 - luma);
            float3 splitHighlights = lerp((0.5).xxx, _SplitHighlights.xyz, luma);
            colorGamma = SoftLight(colorGamma, splitShadows);
            colorGamma = SoftLight(colorGamma, splitHighlights);

            colorLinear = PositivePow(colorGamma, 2.2);

            // ===============================:
            // Channel mixing (Adobe style)
            colorLinear = float3(
                dot(colorLinear, _ChannelMixerRed.xyz),
                dot(colorLinear, _ChannelMixerGreen.xyz),
                dot(colorLinear, _ChannelMixerBlue.xyz)
            );

            // ===============================:
            // Shadows, midtones, highlights
            luma = GetLuminance(colorLinear);
            float shadowsFactor = 1.0 - smoothstep(_ShaHiLimits.x, _ShaHiLimits.y, luma);
            float highlightsFactor = smoothstep(_ShaHiLimits.z, _ShaHiLimits.w, luma);
            float midtonesFactor = 1.0 - shadowsFactor - highlightsFactor;
            colorLinear = colorLinear * _Shadows.xyz * shadowsFactor
                        + colorLinear * _Midtones.xyz * midtonesFactor
                        + colorLinear * _Highlights.xyz * highlightsFactor;

            // ===============================:
            // Lift, gamma, gain
            colorLinear = colorLinear * _Gain.xyz + _Lift.xyz;
            colorLinear = sign(colorLinear) * pow(abs(colorLinear), _Gamma.xyz);

            // ===============================:
            // HSV operations
            float satMult;
            float3 hsv = RgbToHsv(colorLinear);
            {
                // Hue Vs Sat
                satMult = EvaluateCurve(_CurveHueVsSat, hsv.x) * 2.0;

                // Sat Vs Sat
                satMult *= EvaluateCurve(_CurveSatVsSat, hsv.y) * 2.0;

                // Lum Vs Sat
                satMult *= EvaluateCurve(_CurveLumVsSat, Luminance(colorLinear)) * 2.0;

                // Hue Shift & Hue Vs Hue
                float hue = hsv.x + _HueSatCon.x;
                float offset = EvaluateCurve(_CurveHueVsHue, hue) - 0.5;
                hue += offset;
                hsv.x = RotateHue(hue, 0.0, 1.0);
            }
            colorLinear = HsvToRgb(hsv);


            // ===============================:
            // Global saturation
            luma = GetLuminance(colorLinear);
            colorLinear = luma.xxx + (_HueSatCon.yyy * satMult) * (colorLinear - luma.xxx);

            // YRGB curves
            // Conceptually these need to be in range [0;1] and from an artist-workflow perspective
            // it's easier to deal with
            colorLinear = FastTonemap(colorLinear);
            {
                const float kHalfPixel = (1.0 / 128.0) / 2.0;
                float3 c = colorLinear;

                // Y (master)
                c += kHalfPixel.xxx;
                float mr = EvaluateCurve(_CurveMaster, c.r);
                float mg = EvaluateCurve(_CurveMaster, c.g);
                float mb = EvaluateCurve(_CurveMaster, c.b);
                c = float3(mr, mg, mb);

                // RGB
                c += kHalfPixel.xxx;
                float r = EvaluateCurve(_CurveRed, c.r);
                float g = EvaluateCurve(_CurveGreen, c.g);
                float b = EvaluateCurve(_CurveBlue, c.b);
                colorLinear = float3(r, g, b);
            }
            colorLinear = FastTonemapInvert(colorLinear);


            colorLinear = max(0.0, colorLinear);
            return colorLinear;
        }//  函数完__



        float3 Tonemap(float3 colorLinear)
        {
            #if _TONEMAP_NEUTRAL
            {
                colorLinear = NeutralTonemap(colorLinear);
            }
            #elif _TONEMAP_ACES
            {
                // Note: input is actually ACEScg (AP1 w/ linear encoding)
                float3 aces = ACEScg_to_ACES(colorLinear);
                colorLinear = AcesTonemap(aces);
            }
            #endif

            return colorLinear;
        }//  函数完__



        // 绘制 lut 查找表本身,
        // 本质上就是在绘制一个 (w=1024,h=32) 的 quad, 一共4个顶点;
        // 将最终计算的数据写入 render target: "_InternalGradingLut";

        float4 Frag(Varyings input) : SV_Target
        {
            // Lut space
            // We use Alexa LogC (El 1000) to store the LUT as it provides a good enough range
            // (~58.85666) and is good enough to be stored in fp16 without losing precision in the darks

            // 本 fragment 对应的在 lut表 中的颜色值; rgb: [0/31, 31/31]
            float3 colorLutSpace = GetLutStripValue(
                // 一个 (w=1024,h=32) 的 quad 中的每个像素点, 的 uv值; (假设 lut 精度为 32)
                // x: [ 0.5/1024, 1023.5/1024 ]
                // y: [ 0.5/32,   31.5/32 ]
                input.uv,
                _Lut_Params
            );

            // Color grade & tonemap
            // 对这个颜色值, 执行一系列 后处理;
            float3 gradedColor = ColorGrade(colorLutSpace);

            gradedColor = Tonemap(gradedColor);

            // 这个 color 值到底是不是 hdr 啊 ???
            
            return float4(gradedColor, 1.0);
        }//  函数完__

    ENDHLSL




    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "LutBuilderHdr"

            HLSLPROGRAM
                #pragma vertex      FullscreenVert
                #pragma fragment    Frag
            ENDHLSL
        }
    }
}
