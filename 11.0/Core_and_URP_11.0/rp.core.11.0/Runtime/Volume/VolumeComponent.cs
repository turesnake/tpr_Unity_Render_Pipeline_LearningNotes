using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Linq;

namespace UnityEngine.Rendering
{
    /*
        This attribute allows you to add commands to the "Add Override" popup menu on Volumes.
        ---
        在 volume 组件的 inspector 中, 当选定具体的 "Profile" 对象后, 将会多出一个 "Add Override" 按钮;
        点此函数, 弹出一个候选菜单, 用户可选择使用某一个 postprocessing 功能;
        ---
        而那些具体的 postprocessing class, 他们会使用本 attribute, 如:

            [Serializable, VolumeComponentMenu("Post-processing/AAA")]
            public sealed class AAA
                : VolumeComponent, IPostProcessComponent
            {...}

        通过本 attribute, 这些实现类能让自己出现在 "Add Override" 按钮 绑定的菜单中;

        传入的参数 name 是个 string, 其实就是 菜单中的放置路径;
    */
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class VolumeComponentMenu//VolumeComponentMenu__
        : Attribute
    {
        
        // The name of the entry in the override list. You can use slashes to create sub-menus.
        public readonly string menu;

        // TODO: Add support for component icons
        
        //  Creates a new "VolumeComponentMenu" instance.
        /// <param name="menu">The name of the entry in the override list. You can use slashes to create sub-menus.</param>
        public VolumeComponentMenu(string menu)
        {
            this.menu = menu;
        }
    }




    /*
    /// <summary>
    /// An attribute set on deprecated volume components.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class VolumeComponentDeprecated : Attribute
    {
    }
    */



    /*
        The base class for all the components that can be part of a "VolumeProfile".
        The Volume framework automatically handles and interpolates any "VolumeParameter" members found in this class.
        ---
        各种 后处理 class, 如 Bloom, MotionBlur, 都需要继承本class;
    */
    /// <example>
    /// <code>
    /// using UnityEngine.Rendering;
    ///
    /// [Serializable, VolumeComponentMenu("Custom/Example Component")]
    /// public class ExampleComponent : VolumeComponent
    /// {
    ///     public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
    /// }
    /// </code>
    /// </example>
    [Serializable]
    public class VolumeComponent//VolumeComponent__RR
        : ScriptableObject
    {
        /*
            The active state of the set of parameters defined in this class.
            You can use this to quickly turn on or off all the overrides at once.
            ---
            猜测: 就是 那些后处理面板上的: ALL, NONE 按钮;
        */
        public bool active = true;


        /*
            The name displayed in the component header. If you do not set a name, Unity generates one from
            the class name automatically.
        */
        public string displayName { get; protected set; } = "";


        // A read-only collection of all the "VolumeParameter"s defined in this class.
        // 仅仅是一个 List<VolumeParameter> 容器的 "只读包裹层", 本实例 和 原始容器对象, 都指向同一个 堆中容器数据本体;
        public ReadOnlyCollection<VolumeParameter> parameters { get; private set; }


#pragma warning disable 414
        [SerializeField]
        bool m_AdvancedMode = false; // Editor-only
#pragma warning restore 414


        /*
            Extracts(提取) all the "VolumeParameter"s defined in this class and nested classes.
            递归函数:
            递归式地访问 参数o 的每个 field, 
            -- 如果这个 field 属于 "VolumeParameter" 或其派生类类型, 将这个 field 添加到 参数 parameters;
            -- 如果这个 field 不符合上一道检测, 但它仍然是某个class的实例, 那么就递归访问这个 field 对象
                查看它的 fields 中是否有复合要求的;
            ----

            可以用参数 filter 来筛选 field;
        */
        /// <param name="o">The object to find the parameters</param>
        /// <param name="parameters">The list filled with the parameters.</param>
        /// <param name="filter">If you want to filter the parameters</param>
        internal static void FindParameters(//  读完__
                                        object o, 
                                        List<VolumeParameter> parameters, 
                                        Func<FieldInfo, bool> filter = null// 过滤 field 用的
        ){
            if (o == null)
                return;

            // 收集参数 o 的 fields,( Public的, 非Public的, 还有实例 ), 
            // 然后执行排序, 基于 "MetadataToken" (元数据标识)
            var fields = o.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .OrderBy(t => t.MetadataToken); // Guaranteed order


            foreach (var field in fields)
            {
                // 如果 field 的类型 继承于 "VolumeParameter"
                if (field.FieldType.IsSubclassOf(typeof(VolumeParameter)))
                {
                    // ?? : 空接合运算符; 如果函数 Invoke 返回了一个 bool 值,就用之, 如果返回 null, 就改用 true
                    if (filter?.Invoke(field) ?? true)
                        // 将实例 o 的一个指定的 field, 添加到 参数容器 parameters 中去;
                        parameters.Add( (VolumeParameter)field.GetValue(o) );
                }
                else if(    !field.FieldType.IsArray && // array field 不处理 
                            field.FieldType.IsClass
                )
                    FindParameters(field.GetValue(o), parameters, filter);
            }
        }//   函数完__



        /*
            Unity calls this method when it loads the class.
            If you want to override this method, you must call "base.OnEnable()".
        */
        protected virtual void OnEnable()//  读完__
        {
            // Automatically grab all fields of type VolumeParameter for this instance
            var fields = new List<VolumeParameter>();
            FindParameters(this, fields);
            parameters = fields.AsReadOnly();// 获得原始容器的 只读包裹层

            foreach (var parameter in parameters)
            {
                if (parameter != null)
                    parameter.OnEnable();
                else
                    Debug.LogWarning("Volume Component " + GetType().Name + " contains a null parameter; please make sure all parameters are initialized to a default value. Until this is fixed the null parameters will not be considered by the system.");
            }
        }//   函数完__



  
        /// Unity calls this method when the object goes out of scope.
        protected virtual void OnDisable()//  读完__
        {
            if (parameters == null)
                return;

            foreach (var parameter in parameters)
            {
                if (parameter != null)
                    parameter.OnDisable();
            }
        }//   函数完__



        /*
            Interpolates a "VolumeComponent" with this component by an interpolation
            factor and puts the result back into the given "VolumeComponent".

            You can override this method to do your own blending. 
            Either loop through the "parameters" list or reference direct fields. 
            You should only use "VolumeParameter.SetValue" to set parameter values and not assign directly to the state object. 
            you should also manually check "VolumeParameter.overrideState" before you set any values.
            ---
            参数 state 是插值运算的 start, 本类实例 是插值运算的 end, 最终的插值结果,将写入参数 state 中;
        */ 
        /// <param name="state">The internal component to interpolate from. 
        ///     You must store the result of the interpolation in this same component.
        /// </param>
        /// <param name="interpFactor">The interpolation factor in range [0,1].</param>
        public virtual void Override(VolumeComponent state, float interpFactor)//   读完__
        {
            // 此函数有个问题: 就是它假定 state 和 本类实例 持有的 parameters, 元素个数是相同且一一对应的...
            int count = parameters.Count;

            for (int i = 0; i < count; i++)
            {
                var stateParam = state.parameters[i];
                var toParam = parameters[i];

                if (toParam.overrideState)
                {
                    // Keep track of the override state for debugging purpose
                    stateParam.overrideState = toParam.overrideState;
                    stateParam.Interp(stateParam, toParam, interpFactor);
                }
            }
        }//   函数完__


  
        /// Sets the state of all the overrides on this component to a given value.
        /// <param name="state">The value to set the state of the overrides to.</param>
        public void SetAllOverridesTo(bool state)
        {
            SetOverridesTo(parameters, state);
        }


    
        /// Sets the override state of the given parameters on this component to a given value.
        /// <param name="state">The value to set the state of the overrides to.</param>
        internal void SetOverridesTo(IEnumerable<VolumeParameter> enumerable, bool state)
        {
            foreach (var prop in enumerable)
            {
                prop.overrideState = state;
                var t = prop.GetType();

                if (VolumeParameter.IsObjectParameter(t))
                {
                    // This method won't be called a lot but this is sub-optimal, fix me
                    var innerParams = (ReadOnlyCollection<VolumeParameter>)
                        t.GetProperty("parameters", BindingFlags.NonPublic | BindingFlags.Instance)
                            .GetValue(prop, null);

                    if (innerParams != null)
                        SetOverridesTo(innerParams, state);
                }
            }
        }//   函数完__




        /// <summary>
        /// A custom hashing function that Unity uses to compare the state of parameters.
        /// </summary>
        /// <returns>A computed hash code for the current instance.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                //return parameters.Aggregate(17, (i, p) => i * 23 + p.GetHash());

                int hash = 17;

                for (int i = 0; i < parameters.Count; i++)
                    hash = hash * 23 + parameters[i].GetHashCode();

                return hash;
            }
        }//   函数完__


        /// <summary>
        /// Unity calls this method before the object is destroyed.
        /// </summary>
        protected virtual void OnDestroy() => Release();


        /// <summary>
        /// Releases all the allocated resources.
        /// </summary>
        public void Release()
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i] != null)
                    parameters[i].Release();
            }
        }
    }
}
