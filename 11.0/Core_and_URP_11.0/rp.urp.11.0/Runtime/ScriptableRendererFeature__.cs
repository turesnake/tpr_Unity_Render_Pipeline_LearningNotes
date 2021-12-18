using System;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.Rendering.Universal
{
   
    /*
        可以向 "ScriptableRenderer" (比如 Forward Renderer) 
        添加若干个 "ScriptableRendererFeature" 的派生类的实例;

        Use this "scriptable renderer feature" to inject "render passes" into the renderer.


        具体示范可参考 主笔记中的 "ColorContrast.cs" 文件;

        本 "派生 Renderer Feature" 通常要和 "ScriptableRenderPass 的派生类" 一起使用才行;

        查看:
        "ScriptableRenderer"
        "ScriptableRenderPass"
    */
    [ExcludeFromPreset]
    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public abstract class ScriptableRendererFeature//ScriptableRendererFeature__
        : ScriptableObject, IDisposable
    {
        [SerializeField, HideInInspector] private bool m_Active = true;

       
        /*
            -- true:  the feature is active
            -- false: the feature is inactive
            使用函数 "ScriptableRenderFeature.SetActive()" 来改写此值;
        */
        public bool isActive => m_Active;

        
        /*
            ---------------------------------------------------------- +++
            本函数必须被 "ScriptableRendererFeature" 的派生类 覆写;
            ===
            可在本函数实现体内,
            初始化本 feature 的资源, 每当 serialization (序列化) 发生时, 本函数被调用;
        */
        public abstract void Create();

        
        /*
            ---------------------------------------------------------- +++
            本函数必须被 "ScriptableRendererFeature" 的派生类 覆写;
            ===
            在派生类的实现体中:
                可将 数个 "ScriptableRenderPass" 注入到本 feature 中;
                代码:
                    renderer.EnqueuePass( m_pass );
                    此处
                    "renderer" 就是本函数提供的的参数;
                    m_pass 就是一个 "ScriptableRenderPass" 或其继承者的 实例;

            参数:
            renderer:
                如 "ForwardRenderer"
            renderingData:
                Rendering state. Use this to setup render passes.
        */
        public abstract void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData);



        void OnEnable()
        {
            Create();
        }

        void OnValidate()
        {
            Create();
        }


        /*
            -- true:  the feature is active
            -- false: the feature is inactive
            只有 active 的 feature 才会被添加进 renderer; 否则会在渲染流程中被忽略
        */
        public void SetActive(bool active)
        {
            m_Active = active;
        }



        // Disposable pattern implementation.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        protected virtual void Dispose(bool disposing)
        {}

    }
}
