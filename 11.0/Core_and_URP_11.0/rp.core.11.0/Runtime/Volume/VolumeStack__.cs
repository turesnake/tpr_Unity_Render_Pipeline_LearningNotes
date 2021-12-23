using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering
{
    /*
        Holds the state of a Volume blending update. 
        A global stack is available by default in "VolumeManager" but you can also create your own using
        "VolumeManager.CreateStack" if you need to update the manager with specific
        settings and store the results for later use.
    */
    public sealed class VolumeStack //VolumeStack__
        : IDisposable
    {

        // Holds the state of _all_ component types you can possibly add on volumes
        // 被 VolumeManager 使用时, 存入了 "系统中所有继承自 VolumeComponent 的类型" 的 信息和实例;
        // 这些实例 是可以被外部修改的
        internal Dictionary<Type, VolumeComponent> components;

        internal VolumeStack()
        {
        }

        /*
            传入的参数仅是一组 Types, 他们的实例要在本函数内 手动新建;
            参数 type 必须能转换成 VolumeComponent 类型才行
            ---
            在 VolumeManager 中被调用, 传入的参数是: 系统中所有继承自 VolumeComponent 的类型;
        */
        internal void Reload(Type[] baseTypes)//  读完__
        {
            if (components == null)
                components = new Dictionary<Type, VolumeComponent>();
            else
                components.Clear();

            foreach (var type in baseTypes)
            {
                // 若无法转换成目标类型, 将在运行时报错;
                var inst = (VolumeComponent)ScriptableObject.CreateInstance(type);
                components.Add(type, inst);
            }
        }

        /*
            Gets the current state of the "VolumeComponent" of type "T" in the stack.
            找不到就返回 null
        */
        /// <typeparam name="T">A type of "VolumeComponent".</typeparam>
        /// <returns>The current state of the "VolumeComponent" of type "T" in the stack.</returns>
        public T GetComponent<T>()//   读完__
            where T : VolumeComponent
        {
            var comp = GetComponent(typeof(T));
            return (T)comp;
        }


        /*
            Gets the current state of the "VolumeComponent" of the specified type in the stack.
            找不到就返回 null
        */
        /// <param name="type">The type of "VolumeComponent" to look for.</param>
        /// <returns>The current state of the "VolumeComponent" of the specified type, or "null" if the type is invalid.</returns>
        public VolumeComponent GetComponent(Type type)//   读完__
        {
            components.TryGetValue(type, out var comp);
            return comp;
        }




        /// Cleans up the content of this stack. Once a "VolumeStack" is disposed, it souldn't be used anymore.
        public void Dispose()//   读完__
        {
            foreach (var component in components)
                CoreUtils.Destroy(component.Value);

            components.Clear();
        }
    }
}
