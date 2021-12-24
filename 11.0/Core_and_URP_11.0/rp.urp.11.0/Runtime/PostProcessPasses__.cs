using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.Universal
{
    /*
        Type acts as wrapper for post process passes. 
        Can we be recreated and destroyed at any point during runtime with post process data.
        ---

        目前只关注它在 ForwardRenderer 中的使用;
        
    */
    internal struct PostProcessPasses //PostProcessPasses__
        : IDisposable
    {
        // -----------------------------------------:
        ColorGradingLutPass m_ColorGradingLutPass;
        PostProcessPass m_PostProcessPass;
        PostProcessPass m_FinalPostProcessPass;

        // -----------------------------------------:
        RenderTargetHandle m_AfterPostProcessColor;//"_AfterPostProcessTexture"
        RenderTargetHandle m_ColorGradingLut;//"_InternalGradingLut"

        //PostProcess 要使用到的 资源对象: shaders, textures
        PostProcessData m_RendererPostProcessData; // renderer 提供的数据
        PostProcessData m_CurrentPostProcessData;// 当前使用的数据
        Material m_BlitMaterial;//"Shaders/Utils/Blit.shader"

        public ColorGradingLutPass colorGradingLutPass { get => m_ColorGradingLutPass; }
        public PostProcessPass postProcessPass { get => m_PostProcessPass; }
        public PostProcessPass finalPostProcessPass { get => m_FinalPostProcessPass; }

        public RenderTargetHandle afterPostProcessColor { get => m_AfterPostProcessColor; }//"_AfterPostProcessTexture"
        public RenderTargetHandle colorGradingLut { get => m_ColorGradingLut; }//"_InternalGradingLut"

        public bool isCreated { get => m_CurrentPostProcessData != null; }


        // 构造函数
        public PostProcessPasses(//   读完__
                            PostProcessData rendererPostProcessData, //PostProcess 要使用到的 资源对象: shaders, textures
                            Material blitMaterial                    //"Shaders/Utils/Blit.shader"
        ){
            m_ColorGradingLutPass = null;
            m_PostProcessPass = null;
            m_FinalPostProcessPass = null;
            m_AfterPostProcessColor = new RenderTargetHandle();
            m_ColorGradingLut = new RenderTargetHandle();
            m_CurrentPostProcessData = null;

            m_AfterPostProcessColor.Init("_AfterPostProcessTexture");
            m_ColorGradingLut.Init("_InternalGradingLut");

            m_RendererPostProcessData = rendererPostProcessData;
            m_BlitMaterial = blitMaterial;


            Recreate(rendererPostProcessData);
        }//   函数完__


        /*
            Recreates post process passes with supplied data. 
            If already contains valid post process passes, they will be replaced by new ones.
            ---
            已经存在的 有效的 post process passes, 将被替换为新建的
        */
        /// <param name="data">Resources used for creating passes. In case of the null, no passes will be created.</param>
        public void Recreate(PostProcessData data)//   读完__
        {
            if (m_RendererPostProcessData)
                data = m_RendererPostProcessData;// renderer 提供的数据

            // 数据同步的, 无需新建了
            if (data == m_CurrentPostProcessData)
                return;

            // 如果已经有数据, 全部清理掉
            if (m_CurrentPostProcessData != null)
            {
                m_ColorGradingLutPass?.Cleanup();
                m_PostProcessPass?.Cleanup();
                m_FinalPostProcessPass?.Cleanup();

                // We need to null post process passes to avoid using them
                m_ColorGradingLutPass = null;
                m_PostProcessPass = null;
                m_FinalPostProcessPass = null;
                m_CurrentPostProcessData = null;
            }

            if (data != null)
            {
                m_ColorGradingLutPass = new ColorGradingLutPass(
                    RenderPassEvent.BeforeRenderingPrePasses, // render pass 何时执行
                    data
                );
                m_PostProcessPass = new PostProcessPass(
                    RenderPassEvent.BeforeRenderingPostProcessing, 
                    data, 
                    m_BlitMaterial//"Shaders/Utils/Blit.shader"
                );
                m_FinalPostProcessPass = new PostProcessPass(
                    RenderPassEvent.AfterRendering + 1, 
                    data, 
                    m_BlitMaterial//"Shaders/Utils/Blit.shader"
                );
                m_CurrentPostProcessData = data;
            }
        }//   函数完__



        public void Dispose()
        {
            // always dispose unmanaged resources
            m_ColorGradingLutPass?.Cleanup();
            m_PostProcessPass?.Cleanup();
            m_FinalPostProcessPass?.Cleanup();
        }//   函数完__
    }
}
