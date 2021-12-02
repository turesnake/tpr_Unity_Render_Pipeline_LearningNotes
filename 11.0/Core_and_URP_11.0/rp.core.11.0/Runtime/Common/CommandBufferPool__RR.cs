using System.Collections.Generic;
using UnityEngine.Events;

namespace UnityEngine.Rendering
{

    /*
        Command Buffer Pool
    */
    public static class CommandBufferPool//CommandBufferPool__RR
    {
        static ObjectPool<CommandBuffer> s_BufferPool = new ObjectPool<CommandBuffer>(null, x => x.Clear());


        /*
            Get a new Command Buffer.
        */ 
        public static CommandBuffer Get()
        {
            var cmd = s_BufferPool.Get();
            // Set to empty on purpose, does not create profiling markers.
            // 故意设置为 "", 不创建 profiling markers
            cmd.name = "";
            return cmd;
        }


        /*
            Get a new Command Buffer and assign a name to it.
            Named Command Buffers will add profiling makers implicitly for the buffer execution.
        */
        public static CommandBuffer Get(string name)
        {
            var cmd = s_BufferPool.Get();
            cmd.name = name;
            return cmd;
        }


        /*
            Release a Command Buffer.
        */
        public static void Release(CommandBuffer buffer)
        {
            s_BufferPool.Release(buffer);
        }
    }
}
