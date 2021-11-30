using System;
using System.Collections.Generic;

namespace UnityEngine.Experimental.Rendering.RenderGraphModule
{
   
    /*
        被所有 render passes 使用的, 位于 "RenderGraphContext" 体内的 辅助class;
        它允许你在一次 render pass 内为各种 obj 分配临时资源
    */
    public sealed class RenderGraphObjectPool//RenderGraphObjectPool__RR
    {
        class SharedObjectPool<T> //SharedObjectPool__
            where T : new()
        {
            Stack<T> m_Pool = new Stack<T>();

            public T Get()
            {
                var result = m_Pool.Count==0 ? new T() : m_Pool.Pop();
                return result;
            }

            public void Release(T value)
            {
                m_Pool.Push(value);
            }

            // 延迟初始化, 支持多线程
            static readonly Lazy<SharedObjectPool<T>> s_Instance = new Lazy<SharedObjectPool<T>>();
            public static SharedObjectPool<T> sharedPool => s_Instance.Value;
        }



        Dictionary<(Type, int), Stack<object>>  m_ArrayPool = new Dictionary<(Type, int), Stack<object>>();
        List<(object, (Type, int))>             m_AllocatedArrays = new List<(object, (Type, int))>();
        List<MaterialPropertyBlock>             m_AllocatedMaterialPropertyBlocks = new List<MaterialPropertyBlock>();

       
        internal RenderGraphObjectPool() {}

        /*
            分配一个 临时 array, 元素类型为 T, 元素个数为 size 个;
            unity 会在 render pass 结束时, 释放这个 array;

            可用于:
            -- 这个临时array 中装着各种参数, 传递给 materials
            -- 这个临时array 中装着一组 

            returns a temporary array of type T and size size. 
            This can be useful to RenderTargetIdentifier, 用来创建 "multiple render targets";
            ---
            
            返回的资源 的生命周期由 系统维护, 在本 render pass 执行结束后 即失效;
        */
        public T[] GetTempArray<T>(int size)
        {
            if (!m_ArrayPool.TryGetValue((typeof(T), size), out var stack))
            {
                // 如果发现没有目标元素,  创建一个新的
                stack = new Stack<object>();
                m_ArrayPool.Add((typeof(T), size), stack);
            }
            
            //  注意此处的 "(T[])stack.Pop()"; 想要这句话成立, stack 体内的元素 必须是一个 T[] 类型的 array;
            var result = stack.Count>0 ? (T[])stack.Pop() : new T[size];
            m_AllocatedArrays.Add((result, (typeof(T), size)));
            return result;
        }


        /*
            Allocate a temporary MaterialPropertyBlock for the Render Pass.

            returns a "clean material property block" that you can use to set up parameters for a Material. 
            这一点特别重要，因为可能有多个 pass 要使用这种 material，而且每个 pass 都可用不同的参数去使用这个 material;

            Because the rendering code execution is deferred via command buffers, 
            copying material property blocks into the command buffer is mandatory to preserve data integrity on execution.
            --
            因为 "渲染代码的执行" 被 commandbuffer 缓存起来了 (最后一起执行),
            所以必须将 material property blocks 中的数据复制进 commandbuffer 中, 以便最终执行阶段, 数据的完整性
            ---

            返回的资源 的生命周期由 系统维护, 在本 render pass 执行结束后 即失效;
        */
        /// <returns>A new clean MaterialPropertyBlock.</returns>
        public MaterialPropertyBlock GetTempMaterialPropertyBlock()
        {
            var result = SharedObjectPool<MaterialPropertyBlock>.sharedPool.Get();
            result.Clear();
            m_AllocatedMaterialPropertyBlocks.Add(result);
            return result;
        }


        internal void ReleaseAllTempAlloc()
        {
            foreach (var arrayDesc in m_AllocatedArrays)
            {
                bool result = m_ArrayPool.TryGetValue(arrayDesc.Item2, out var stack);
                Debug.Assert(result, "Correct stack type should always be allocated.");
                stack.Push(arrayDesc.Item1);
            }

            m_AllocatedArrays.Clear();

            foreach (var mpb in m_AllocatedMaterialPropertyBlocks)
            {
                SharedObjectPool<MaterialPropertyBlock>.sharedPool.Release(mpb);
            }

            m_AllocatedMaterialPropertyBlocks.Clear();
        }


        // Regular pooling API. Only internal use for now 目前仅内部使用
        internal T Get<T>() where T : new()
        {
            var pool = SharedObjectPool<T>.sharedPool;
            return pool.Get();
        }


        internal void Release<T>(T value) where T : new()
        {
            var pool = SharedObjectPool<T>.sharedPool;
            pool.Release(value);
        }
    }
}
