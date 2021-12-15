using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace UnityEngine.Rendering
{
    /// <summary>
    /// An Asset which holds a set of settings to use with a "Volume";
    /// </summary>
    [HelpURL(Documentation.baseURLHDRP + Documentation.version + Documentation.subURL + "Volume-Profile" + Documentation.endURL)]
    public sealed class VolumeProfile//VolumeProfile__
        : ScriptableObject
    {
      
        // A list of every setting that this Volume Profile stores.
        public List<VolumeComponent> components = new List<VolumeComponent>();

        /*
            A dirty check used to redraw the profile inspector when something has changed.
            This is currently only used in the editor.
        */
        [NonSerialized]
        public bool isDirty = true; // Editor only, doesn't have any use outside of it


        void OnEnable()//  读完__
        {
            // Make sure every setting is valid. If a profile holds a script that doesn't exist
            // anymore, nuke it to keep the volume clean. Note that if you delete a script that is
            // currently in use in a volume you'll still get a one-time error in the console, it's
            // harmless and happens because Unity does a redraw of the editor (and thus the current
            // frame) before the recompilation step.
            components.RemoveAll(x => x == null);
        }//  函数完__


  
        /// Resets the dirty state of the Volume Profile. Unity uses this to force-refresh and redraw the
        /// Volume Profile editor when you modify the Asset via script instead of the Inspector.
        public void Reset()//  读完__
        {
            isDirty = true;
        }


        /*
            Adds a "VolumeComponent" to this Volume Profile.
            You can only have a single component of the same type per Volume Profile.
            ---
            创建并配置一个 类型为 type 的 VolumeComponent实例, 将其添加到 components 中;
            返回这个新建的 实例;
            ---
            components 中, 一种类型的 元素, 只能存储一个;
        */
        /// <typeparam name="T">A type of "VolumeComponent".</typeparam>
        /// <param name="overrides">Specifies whether Unity should automatically override all the settings when
        ///                     you add a "VolumeComponent" to the Volume Profile.
        /// </param>
        /// <returns>The instance for the given type that you added to the Volume Profile</returns>
        public T Add<T>(bool overrides = false)//   读完__
            where T : VolumeComponent
        {
            return (T)Add(typeof(T), overrides);
        }


        /*
            Adds a "VolumeComponent" to this Volume Profile.
            You can only have a single component of the same type per Volume Profile.
            ---
            创建并配置一个 类型为 type 的 VolumeComponent实例, 将其添加到 components 中;
            返回这个新建的 实例;
            ---
            components 中, 一种类型的 元素, 只能存储一个;
        */
        /// <param name="type">A type that inherits from "VolumeComponent".</param>
        /// <param name="overrides">Specifies whether Unity should automatically override all the settings when
        ///                     you add a "VolumeComponent" to the Volume Profile.
        /// </param>
        /// <returns>The instance created for the given type that has been added to the profile</returns>
        public VolumeComponent Add( Type type, bool overrides = false )//   读完__
        {
            // 查到有重复的了
            if (Has(type))
                throw new InvalidOperationException("Component already exists in the volume");

            var component = (VolumeComponent)CreateInstance(type); // ScriptableObject.CreateInstance()
#if UNITY_EDITOR
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            component.name = type.Name;
#endif
            component.SetAllOverridesTo(overrides);
            //---
            components.Add(component);
            isDirty = true;
            return component;
        }//  函数完__



        /*
            Removes a "VolumeComponent" from this Volume Profile.
            This method does nothing if the type does not exist in the Volume Profile.
            --
            遍历本实例的 components, 如果某一个 component 的类型为 参数 T, 就将这个 component 从 list 中移除;
            而且只移除找到的第一个;
            如果一个也没找到, 本函数啥也不做;
        */
        /// <typeparam name="T">A type of "VolumeComponent".</typeparam>
        public void Remove<T>()//   读完__
            where T : VolumeComponent
        {
            Remove(typeof(T));
        }


        /*
            Removes a "VolumeComponent" from this Volume Profile.
            This method does nothing if the type does not exist in the Volume Profile.
            --
            遍历本实例的 components, 如果某一个 component 的类型为 参数 type, 就将这个 component 从 list 中移除;
            而且只移除找到的第一个;
            如果一个也没找到, 本函数啥也不做;
        */
        /// <param name="type">A type that inherits from "VolumeComponent".</param>
        public void Remove(Type type)//   读完__
        {
            int toRemove = -1;

            for (int i = 0; i < components.Count; i++)
            {
                if (components[i].GetType() == type)
                {
                    toRemove = i;
                    break;
                }
            }

            if (toRemove >= 0)
            {
                components.RemoveAt(toRemove);
                isDirty = true;
            }
        }//  函数完__


    
        /// Checks if this Volume Profile contains the "VolumeComponent" you pass in.
        /// <typeparam name="T">A type of "VolumeComponent".</typeparam>
        /// <returns> true if the "VolumeComponent" exists in the Volume Profile,</returns>
        public bool Has<T>() //   读完__
            where T : VolumeComponent
        {
            return Has(typeof(T));
        }

  
        /// Checks if this Volume Profile contains the "VolumeComponent" you pass in.
        /// <param name="type">A type that inherits from "VolumeComponent".</param>
        /// <returns> true if the "VolumeComponent" exists in the Volume Profile, </returns>
        public bool Has(Type type)//  读完__
        {
            foreach (var component in components)
            {
                if (component.GetType() == type)
                    return true;
            }
            return false;
        }//  函数完__


        /*
            Checks if this Volume Profile contains the "VolumeComponent", which is a subclass of "type", that you pass in.
            --
            遍历本实例的 components, 如果某个 component 属于 参数 type 的派生类, 就返回 true;
            没找到就返回 false;
        */
        /// <param name="type">A type that inherits from "VolumeComponent".</param>
        /// <returns> true if the "VolumeComponent" exists in the Volume Profile,</returns>
        public bool HasSubclassOf(Type type)//   读完__
        {
            foreach (var component in components)
            {
                if (component.GetType().IsSubclassOf(type))
                    return true;
            }

            return false;
        }//  函数完__



        /*
            Gets the "VolumeComponent" of the specified type, if it exists.
            ---
            遍历本实例的 components, 如果某个 component 类型等于 T,
            就将这个 component 类型转换为 T 之后存入 out 参数中; 且本函数返回 true
            ---
            若没找到, out参数输出 null, 本函数返回 false;
        */
        /// <typeparam name="T">A type of "VolumeComponent".</typeparam>
        /// <param name="component">The output argument that contains the "VolumeComponent" or null.</param>
        /// <returns>true if the "VolumeComponent" is in the Volume Profile,</returns>
        public bool TryGet<T>(out T component)//  读完__
            where T : VolumeComponent
        {
            return TryGet(typeof(T), out component);
        }


        /*
            Gets the "VolumeComponent" of the specified type, if it exists.
            ---
            遍历本实例的 components, 如果某个 component 类型等于 参数type,
            就将这个 component 类型转换为 T 之后存入 out 参数中; 且本函数返回 true
            ---
            若没找到, out参数输出 null, 本函数返回 false;
        */
        /// <typeparam name="T">A type of "VolumeComponent"</typeparam>
        /// <param name="type">A type that inherits from "VolumeComponent".</param>
        /// <param name="component">The output argument that contains the "VolumeComponent" or null.</param>
        /// <returns>true if the "VolumeComponent" is in the Volume Profile, </returns>
        public bool TryGet<T>(Type type, out T component)//  读完__
            where T : VolumeComponent
        {
            component = null;

            foreach (var comp in components)
            {
                if (comp.GetType() == type)
                {
                    component = (T)comp;
                    return true;
                }
            }

            return false;
        }//  函数完__



        /*
            Gets the "VolumeComponent", which is a subclass of "type", if it exists.
            ---
            遍历本实例的 components, 如果找到一个 component, 它是 参数 type 的派生类, 
            就将这个 component 类型转换为 T 之后从 out 参数输出, 同时返回 true;
            ---
            若没找到, 函数返回 false, 且 out参数输出 null;
        */
        /// <typeparam name="T">A type of "VolumeComponent".</typeparam>
        /// <param name="type">A type that inherits from "VolumeComponent".</param>
        /// <param name="component">The output argument that contains the "VolumeComponent" or null.</param>
        /// <returns> true if the "VolumeComponent" is in the Volume Profile, </returns>
        public bool TryGetSubclassOf<T>(Type type, out T component)//  读完__
            where T : VolumeComponent
        {
            component = null;

            foreach (var comp in components)
            {
                if (comp.GetType().IsSubclassOf(type))
                {
                    component = (T)comp;
                    return true;
                }
            }
            return false;
        }//  函数完__



        /*
            Gets all the "VolumeComponent" that are subclasses of the specified type, if there are any.
            ---
            将本类的 components 中的 所有属于 参数 type 的派生类的 component, 添加到 参数容器 result 中;
            result 中原本存储的数据会被保留, 新添加的元素放入尾部;

            当发生添加行为后, 本函数返回 true;
        */
        /// <typeparam name="T">A type of "VolumeComponent".</typeparam>
        /// <param name="type">A type that inherits from "VolumeComponent".</param>
        /// <param name="result">The output list that contains all the "VolumeComponent"
        /// if any. Note that Unity does not clear this list.</param>
        /// <returns>true if any "VolumeComponent" have been found in the profile,
        /// </returns>
        public bool TryGetAllSubclassOf<T>(Type type, List<T> result)//   读完__
            where T : VolumeComponent
        {
            Assert.IsNotNull(components);// unity 断言函数, 若 components 为 null, 抛出异常;
            int count = result.Count;

            foreach (var comp in components)
            {
                if (comp.GetType().IsSubclassOf(type))
                    result.Add((T)comp);
            }

            return count != result.Count;
        }//  函数完__




        /// A custom hashing function that Unity uses to compare the state of parameters.
        /// <returns>A computed hash code for the current instance.</returns>
        public override int GetHashCode()//   读完__
        {
            unchecked
            {
                int hash = 17;

                for (int i = 0; i < components.Count; i++)
                    hash = hash * 23 + components[i].GetHashCode();
                return hash;
            }
        }//  函数完__



        internal int GetComponentListHashCode()//  读完__
        {
            unchecked
            {
                int hash = 17;

                for (int i = 0; i < components.Count; i++)
                    hash = hash * 23 + components[i].GetType().GetHashCode();

                return hash;
            }
        }//  函数完__
    }
}
