using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.RenderGraphModule
{
    
    /*
        辅助class, 允许在 render pass 期间访问 default 资源( 比如 black or white texture )
    */
    public class RenderGraphDefaultResources//RenderGraphDefaultResources__
    {
        bool m_IsValid;

        // We need to keep around a RTHandle version of default regular 2D textures 
        // since RenderGraph API is all RTHandle.
        RTHandle m_BlackTexture2D;
        RTHandle m_WhiteTexture2D;

        
        public TextureHandle blackTexture { get; private set; }//Default black 2D texture
       
        public TextureHandle whiteTexture { get; private set; }//Default white 2D texture
        
        public TextureHandle clearTextureXR { get; private set; }//Default clear color XR 2D texture
    
        public TextureHandle magentaTextureXR { get; private set; }//Default magenta(品红) XR 2D texture.
        
        public TextureHandle blackTextureXR { get; private set; }//Default black XR 2D texture
       
        public TextureHandle blackTextureArrayXR { get; private set; }//Default black XR 2D Array texture
       
        public TextureHandle blackUIntTextureXR { get; private set; }//Default black (UInt) XR 2D texture
     
        public TextureHandle blackTexture3DXR { get; private set; }//Default black XR 3D texture
     
        public TextureHandle whiteTextureXR { get; private set; }//Default white XR 2D texture



        internal RenderGraphDefaultResources()
        {
            m_BlackTexture2D = RTHandles.Alloc(Texture2D.blackTexture);
            m_WhiteTexture2D = RTHandles.Alloc(Texture2D.whiteTexture);
        }

        internal void Cleanup()
        {
            m_BlackTexture2D.Release();
            m_WhiteTexture2D.Release();
        }

        internal void InitializeForRendering(RenderGraph renderGraph)
        {
            blackTexture = renderGraph.ImportTexture(m_BlackTexture2D);
            whiteTexture = renderGraph.ImportTexture(m_WhiteTexture2D);

            clearTextureXR = renderGraph.ImportTexture(TextureXR.GetClearTexture());
            magentaTextureXR = renderGraph.ImportTexture(TextureXR.GetMagentaTexture());
            blackTextureXR = renderGraph.ImportTexture(TextureXR.GetBlackTexture());
            blackTextureArrayXR = renderGraph.ImportTexture(TextureXR.GetBlackTextureArray());
            blackUIntTextureXR = renderGraph.ImportTexture(TextureXR.GetBlackUIntTexture());
            blackTexture3DXR = renderGraph.ImportTexture(TextureXR.GetBlackTexture3D());
            whiteTextureXR = renderGraph.ImportTexture(TextureXR.GetWhiteTexture());
        }
    }
}
