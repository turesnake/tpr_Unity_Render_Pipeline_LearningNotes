namespace UnityEngine.Rendering.Universal
{
    /*
        Invokes "OnRenderObject" callback;
        这是 MonoBehaviour 的一个回调函数, 
        在 built-in 管线中:
            "在所有 “regular 场景渲染” 都完成后被调用。
            可在这个时间点调用 GL 类，或 Graphics.DrawMeshNow 来绘制 自定义几何体"
    */
    internal class InvokeOnRenderObjectCallbackPass //InvokeOnRenderObjectCallbackPass__
        : ScriptableRenderPass
    {


        // 暂时只被 "ForwardRenderer" 调用, 它传入的参数是: "BeforeRenderingPostProcessing"
        // 这意味着本 render pass 实现的 "OnRenderObject", 其执行点, 和 built-in 管线中是一样的;
        public InvokeOnRenderObjectCallbackPass(// 读完__
                                            RenderPassEvent evt // 设置 render pass 何时执行
        ){
            base.profilingSampler = new ProfilingSampler(nameof(InvokeOnRenderObjectCallbackPass));
            renderPassEvent = evt;
        }


        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)// 读完__
        {
            // 调用此函数来 触发 callback: MonoBehaviour.OnRenderObject();
            context.InvokeOnRenderObjectCallback();
        }
    }
}
