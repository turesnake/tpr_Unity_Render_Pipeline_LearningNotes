using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.Rendering.Universal
{
    /*
        RenderTargetHandle can be thought of as a kind of ShaderProperty string hash
        ---

        本类管理一个 render texture 的 "识别信息", 这个 "识别信息" 
        可以是 nameId ( 调用 "Shader.PropertyToID()" 获得 )
        可以是 rtid   ( 使用 "RenderTargetIdentifier" 创建 )

        === 新建,初始化 ================
        -1-:
            RenderTargetHandle a = new RenderTargetHandle();
            a.Init("_XXXTexture");
            ---
            主要是 构造函数 没有支持 string 参数的版本

        -2-:
            RenderTargetHandle a = new RenderTargetHandle( 某个 int 值 );
            ---
            理论上要传入 rtid 值, 但有些源码里传入的值很奇怪

        === 获取id ====================
        -1-:
            RenderTargetIdentifier outid = a.Identifier();
        -2-:
            a.id; 
            是的, 居然可以直接访问 nameId; 感觉不是很正规


    */
    [MovedFrom("UnityEngine.Rendering.LWRP")] 
    public struct RenderTargetHandle//RenderTargetHandle__
    {
        /*
            "render texture 的识别信息" 同时支持 nameId 和 rtid 两种存储格式的,(一次只使用一种)

            若 id 值为:
                -1:  本实例为 RenderTargetHandle.CameraTarget
                -2:  本实例 将 "识别信息" 存储在 "rtid" 中;
                oth: 本实例 将 "识别信息" 存储在 "id" 中; 
        */
        public int id { set; get; } // nameId, 调用 Shader.PropertyToID("_AAA_Tex"); 获得的那个
        private RenderTargetIdentifier rtid { set; get; }


        public static readonly RenderTargetHandle CameraTarget = new RenderTargetHandle {id = -1 };

        /*
            构造函数
            见到有代码调用 无参数的 构造函数... 不知哪来的
        */
        public RenderTargetHandle(RenderTargetIdentifier renderTargetIdentifier)
        {
            id = -2;
            rtid = renderTargetIdentifier;
        }


        /*
            本函数主要为 XR 服务, 对于常规程序, 直接访问 RenderTargetHandle.CameraTarget 就行了
        */
        internal static RenderTargetHandle GetCameraTarget(XRPass xr)
        {
/*   tpr
#if ENABLE_VR && ENABLE_XR_MODULE
            if (xr.enabled)
                return new RenderTargetHandle(xr.renderTarget);
#endif
*/
            return CameraTarget;
        }


        public void Init(string shaderProperty)
        {
            // Shader.PropertyToID returns what is internally referred to as a "ShaderLab::FastPropertyName".
            // It is a value coming from an internal global std::map<char*,int> that converts shader property strings 
            // into unique integer handles (that are faster to work with).
            id = Shader.PropertyToID(shaderProperty);
        }
        // 和构造函数 一样的操作
        public void Init(RenderTargetIdentifier renderTargetIdentifier)
        {
            id = -2;
            rtid = renderTargetIdentifier;
        }

        // 获得需要的 "识别信息"
        public RenderTargetIdentifier Identifier()
        {
            if (id == -1)
            {
                return BuiltinRenderTextureType.CameraTarget;
            }
            if (id == -2)
            {
                return rtid;
            }
            return new RenderTargetIdentifier(id);
        }

        // 是否存储为 RenderTargetIdentifier 格式;
        public bool HasInternalRenderTargetId()
        {
            return id==-2;
        }


        public bool Equals(RenderTargetHandle other)
        {
            if (id == -2 || other.id == -2)
                return Identifier() == other.Identifier();
            return id == other.id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is RenderTargetHandle && Equals((RenderTargetHandle)obj);
        }


        public override int GetHashCode()
        {
            return id;
        }

        public static bool operator==(RenderTargetHandle c1, RenderTargetHandle c2)
        {
            return c1.Equals(c2);
        }
        public static bool operator!=(RenderTargetHandle c1, RenderTargetHandle c2)
        {
            return !c1.Equals(c2);
        }
    }
}
