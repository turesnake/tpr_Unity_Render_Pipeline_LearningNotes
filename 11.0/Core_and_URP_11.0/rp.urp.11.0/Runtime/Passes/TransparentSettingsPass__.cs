namespace UnityEngine.Rendering.Universal
{
    
    /*
        Applies relevant settings before rendering transparent objects
    */
    internal class TransparentSettingsPass //TransparentSettingsPass__
        : ScriptableRenderPass
    {
        bool m_shouldReceiveShadows;

        const string m_ProfilerTag = "Transparent Settings Pass";
        private static readonly ProfilingSampler m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);


        // 构造函数
        public TransparentSettingsPass(//   读完__
                                    RenderPassEvent evt,  // 设置 render pass 何时执行
                                    bool shadowReceiveSupported 
        ){
            base.profilingSampler = new ProfilingSampler(nameof(TransparentSettingsPass));
            renderPassEvent = evt; // base class 中的;
            m_shouldReceiveShadows = shadowReceiveSupported;
        }


        // 只有当 半透明物体 "不能接受 shadow" 时, 返回 true;
        public bool Setup(ref RenderingData renderingData)//   读完__
        {
            /*
                Currently we only need to enqueue this pass when the user doesn't want transparent objects to receive shadows
                ---
                目前阶段, 只有当用户 "不希望半透明物体能接受 shadow" 时, 本 render pass 才会被渲染; 
            */
            return !m_shouldReceiveShadows;
        }



        public override void Execute(//  读完__
                            ScriptableRenderContext context, 
                            ref RenderingData renderingData
        ){
            // Get a command buffer...
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                // 如上所属, 由于目前仅在 "半透明物体 不接受 shadow" 时, 本 render pass 才会被执行;
                // 此时, 下面这些 keywords 都会被设置为 disable;

                // Toggle light shadows enabled based on the renderer setting set in the constructor
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, m_shouldReceiveShadows);//"_MAIN_LIGHT_SHADOWS"
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowCascades, m_shouldReceiveShadows);//"_MAIN_LIGHT_SHADOWS_CASCADE"
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.AdditionalLightShadows, m_shouldReceiveShadows);//"_ADDITIONAL_LIGHT_SHADOWS"
            }

            // Execute and release the command buffer...
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
