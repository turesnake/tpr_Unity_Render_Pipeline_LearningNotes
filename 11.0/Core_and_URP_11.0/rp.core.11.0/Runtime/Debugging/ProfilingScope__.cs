/*
    TProfilingSampler<TEnum>.samples should just be an array. 
    Unfortunately, Enum cannot be converted to int without generating garbage.
    This could be worked around by using Unsafe but it's not available at the moment.
    So in the meantime we use a Dictionary with a perf hit...
    --- 

    "TProfilingSampler<TEnum>.samples" 应该必须是个 array;
    不幸的是, Enum 无法在不引发 GC 的情况下转换成 int;
    如果使用 unsafe 代码, 可以绕过这点, 但目前为止它来不可行;
    所有目前, 我们使用一个 性能良好的 Dictionary
*/
//#define USE_UNSAFE      unity 自己注释掉的


#if UNITY_2020_1_OR_NEWER
    #define UNITY_USE_RECORDER
#endif

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Profiling;


namespace UnityEngine.Rendering
{

    /*
        仅在本文件内 被使用
    */
    class TProfilingSampler<TEnum> //TProfilingSampler__RR
        : ProfilingSampler 
        where TEnum : Enum
    {
#if USE_UNSAFE
        /*   tpr
        internal static TProfilingSampler<TEnum>[] samples;
        */
#else
        /*
            TEnum 的每个成员, 都会被生成一个对应 name 的 TProfilingSampler<TEnum> 实例, 组成 pair 存储于此
        */
        internal static Dictionary<TEnum, TProfilingSampler<TEnum>> samples = new Dictionary<TEnum, TProfilingSampler<TEnum>>();
#endif

        // static 构造函数
        static TProfilingSampler()
        {
            var names = Enum.GetNames(typeof(TEnum)); // string array
#if USE_UNSAFE
            /*   tpr
            var values = Enum.GetValues(typeof(TEnum)).Cast<int>().ToArray();
            samples = new TProfilingSampler<TEnum>[values.Max() + 1];
            */
#else
            var values = Enum.GetValues(typeof(TEnum)); // Array
#endif

            for (int i = 0; i < names.Length; i++)
            {
                var sample = new TProfilingSampler<TEnum>(names[i]);
#if USE_UNSAFE
                /*   tpr
                samples[values[i]] = sample;
                */
#else
                samples.Add( (TEnum)values.GetValue(i), sample );
#endif
            }
        }

        // 实例 构造函数
        public TProfilingSampler(string name)
            : base(name)
        {}

    }// TProfilingSampler end





    /*
        =================================================< ProfilingSampler >========================================:
        Wrapper around CPU and GPU profiling samplers.
        ---
        配合使用 "ProfilingScope" to profile a piece of code.

        有两种新建实例的方法:
        -1-:
            ProfilingSampler.Get( EnumA.val_1 );
        -2-:
            ProfilingSampler p = new ProfilingSampler( "Deferred Pass" );


    */
    public class ProfilingSampler//ProfilingSampler__RR
    {
        /*
            Get the sampler for the corresponding enumeration value.
        */
        /// <typeparam name="TEnum">Type of the enumeration.</typeparam>
        /// <param name="marker">Enumeration value.</param>
        /// <returns>The profiling sampler for the given enumeration value.</returns>
        public static ProfilingSampler Get<TEnum>(TEnum marker)
            where TEnum : Enum
        {
#if USE_UNSAFE
            /*   tpr
            return TProfilingSampler<TEnum>.samples[Unsafe.As<TEnum, int>(ref marker)];
            */
#else
            TProfilingSampler<TEnum>.samples.TryGetValue(marker, out var sampler);
            return sampler;
#endif
        }

        
        /*
            构造函数
        */
        /// <param name="name">Name of the profiling sampler.</param>
        public ProfilingSampler(string name)
        {
            /*
                Caution: Name of sampler MUST not match name provide to cmd.BeginSample(), 
                otherwise we get a mismatch of marker when enabling the profiler.
                --
                注意: 参数 name 不能和 cmd.BeginSample() 的参数 name 同值;
                否则在启用 profiler 时会遭遇 marker 不清楚的问题;
            */

// 2020_1 OR NEWER
#if UNITY_USE_RECORDER
            sampler = CustomSampler.Create(name, true); // Event markers, command buffer CPU profiling and GPU profiling
#else
            /*     tpr
            // In this case, we need to use the BeginSample(string) API, since it creates a new sampler by that name under the hood,
            // we need rename this sampler to not clash with the implicit one (it won't be used in this case)
            sampler = CustomSampler.Create($"Dummy_{name}");
            */
#endif
            inlineSampler = CustomSampler.Create($"Inl_{name}"); // Profiles code "immediately"
            this.name = name;

// 2020_1 OR NEWER
#if UNITY_USE_RECORDER
            m_Recorder = sampler.GetRecorder();
            m_Recorder.enabled = false;
            m_InlineRecorder = inlineSampler.GetRecorder();
            m_InlineRecorder.enabled = false;
#endif
        }//构造函数 end


        /*
            begin the profiling block.
        */
        /// <param name="cmd">Command buffer used by the profiling block.</param>
        public void Begin(CommandBuffer cmd)
        {
            if (cmd != null){

// 2020_1 OR NEWER
#if UNITY_USE_RECORDER
                if (sampler != null && sampler.isValid)
                    cmd.BeginSample(sampler);
                else
                    cmd.BeginSample(name);
#else
                /*     tpr
                cmd.BeginSample(name);
                */
#endif
            }
            inlineSampler?.Begin();
        }



        /// <summary>
        /// End the profiling block.
        /// </summary>
        /// <param name="cmd">Command buffer used by the profiling block.</param>
        public void End(CommandBuffer cmd)
        {
            if (cmd != null)
            {
// 2020_1 OR NEWER
#if UNITY_USE_RECORDER
                if (sampler != null && sampler.isValid)
                    cmd.EndSample(sampler);
                else
                    cmd.EndSample(name);
#else
                /*     tpr
                m_Cmd.EndSample(name);
                */
#endif
            }
            inlineSampler?.End();
        }


        internal bool IsValid() { return (sampler != null && inlineSampler != null); }

        internal CustomSampler sampler { get; private set; }
        internal CustomSampler inlineSampler { get; private set; }
        
        public string name { get; private set; }//Name of the Profiling Sampler

// 2020_1 OR NEWER
#if UNITY_USE_RECORDER
        Recorder m_Recorder;
        Recorder m_InlineRecorder;// 当 cmd==null, 调用此值;
#endif

        
        // Set to true to enable recording of profiling sampler timings.
        public bool enableRecording
        {
            set
            {
// 2020_1 OR NEWER
#if UNITY_USE_RECORDER
                m_Recorder.enabled = value;
                m_InlineRecorder.enabled = value;
#endif

            }
        }

// 2020_1 OR NEWER
#if UNITY_USE_RECORDER

        // GPU Elapsed time in milliseconds.
        public float gpuElapsedTime => m_Recorder.enabled ? m_Recorder.gpuElapsedNanoseconds / 1000000.0f : 0.0f;
        
        // Number of times the Profiling Sampler has hit on the GPU
        public int gpuSampleCount => m_Recorder.enabled ? m_Recorder.gpuSampleBlockCount : 0;
        
        // CPU Elapsed time in milliseconds (Command Buffer execution).
        public float cpuElapsedTime => m_Recorder.enabled ? m_Recorder.elapsedNanoseconds / 1000000.0f : 0.0f;
        
        // Number of times the Profiling Sampler has hit on the CPU in the command buffer.
        public int cpuSampleCount => m_Recorder.enabled ? m_Recorder.sampleBlockCount : 0;
        
        // CPU Elapsed time in milliseconds (Direct execution).
        public float inlineCpuElapsedTime => m_InlineRecorder.enabled ? m_InlineRecorder.elapsedNanoseconds / 1000000.0f : 0.0f;
       
        // Number of times the Profiling Sampler has hit on the CPU.
        public int inlineCpuSampleCount => m_InlineRecorder.enabled ? m_InlineRecorder.sampleBlockCount : 0;
#else
        /*     tpr
        public float gpuElapsedTime => 0.0f; // GPU Elapsed time in milliseconds.
        
        public int gpuSampleCount => 0; // Number of times the Profiling Sampler has hit on the GPU
        
        public float cpuElapsedTime => 0.0f; // CPU Elapsed time in milliseconds (Command Buffer execution).
        
        // Number of times the Profiling Sampler has hit on the CPU in the command buffer.
        public int cpuSampleCount => 0;
        
        public float inlineCpuElapsedTime => 0.0f; // CPU Elapsed time in milliseconds (Direct execution).
        public int inlineCpuSampleCount => 0; // Number of times the Profiling Sampler has hit on the CPU.
        */
#endif

        // Keep the constructor private
        // 不许外部调用 默认构造函数
        ProfilingSampler() {}
    }// ProfilingSampler end





#if DEVELOPMENT_BUILD || UNITY_EDITOR
    /*
        =================================================< ProfilingScope >==============================================:
        Scoped Profiling markers
        开发者版, Editor 版;

        使用方式:
        -1-:
            using (new ProfilingScope(...))
            {
                // do something...
            }

        -2-: c#8.0:
            void Foo(){
                using var a = new ProfilingScope(...);
                // do something...
            }
        
        两种用法本质是相同的: 新建的 ProfilingScope 实例是个 未托管资源, 使用 using 语法来管理这个资源的 存活范围;
        在这个范围内的代码, 就等于是被这个 ProfilingScope 实例 "捕获"了, 类似于一个 Begin/End pair;
        block 内部的代码 的运行时间, 会被记录下来;

        在本质上, 就是调用了 "ProfilingSampler.Begin()" 和 "ProfilingSampler.End()";

    */ 
    public struct ProfilingScope //ProfilingScope__RR_1
        : IDisposable
    {
        CommandBuffer       m_Cmd;
        bool                m_Disposed;// 是否已经执行 Dispose();
        ProfilingSampler    m_Sampler;

        /*
            构造函数

            如果 参数 cmd == null, 内部就调用 "CustomSampler.Begin() / End()" 来实现采样;
            否则, 就调用 "cmd.BeginSample() / End()" 来实现采样; 

        */
        /// <param name="cmd">Command buffer used to add markers and compute execution timings.</param>
        /// <param name="sampler">Profiling Sampler to be used for this scope.</param>
        public ProfilingScope(CommandBuffer cmd, ProfilingSampler sampler)
        {
            /*
            // NOTE: Do not mix with named CommandBuffers.
            // Currently there's an issue which results in mismatched markers.
            // The named CommandBuffer will close its "profiling scope" on execution.
            // That will orphan ProfilingScope markers as the named CommandBuffer marker is their "parent".
            // Resulting in following pattern:
            // exec(cmd.start, scope.start, cmd.end) and exec(cmd.start, scope.end, cmd.end)
                -----
                注意, 不要和 "命名的 CommandBuffer" 混淆;
                目前存在一个问题, 它会导致 mismatched markers; (markers 不匹配)
                "命名的 cmd" 会在执行期间关闭自己的 "profiling scope";
                这将孤立 ProfilingScope markers, 因为 "命名的 cmd" 是它们的 "parent".
                导致以下模式:
                    exec(cmd.start, scope.start, cmd.end); 和 exec(cmd.start, scope.end, cmd.end);
            */
            m_Cmd = cmd;
            m_Disposed = false;
            m_Sampler = sampler;
            m_Sampler?.Begin(m_Cmd);
        }



        /// <summary>
        ///  Dispose pattern implementation
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        // Protected implementation of Dispose pattern.
        void Dispose(bool disposing)
        {
            if (m_Disposed)
                return;

            // As this is a struct, it could have been initialized using an empty constructor so we
            // need to make sure `cmd` isn't null to avoid a crash. Switching to a class would fix
            // this but will generate garbage on every frame (and this struct is used quite a lot).
            if (disposing){
                m_Sampler?.End(m_Cmd);
            }

            m_Disposed = true;
        }
    }


#else

    /*
        ======================================================================================================:
        Scoped Profiling markers
        正式Build 版:
            两函数都是空的, 以此来删减掉所有 Profile 调用;
    */ 
    public struct ProfilingScope //ProfilingScope__RR_2
        : IDisposable
    {
        public ProfilingScope(CommandBuffer cmd, ProfilingSampler sampler)
        {}
        public void Dispose()
        {}
    }


#endif

    /*     tpr
    /// <summary>
    /// Profiling Sampler class.
    /// </summary>
    [System.Obsolete("Please use ProfilingScope")]
    public struct ProfilingSample : IDisposable
    {
        readonly CommandBuffer m_Cmd;
        readonly string m_Name;

        bool m_Disposed;
        CustomSampler m_Sampler;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="cmd">Command Buffer.</param>
        /// <param name="name">Name of the profiling sample.</param>
        /// <param name="sampler">Custom sampler for CPU profiling.</param>
        public ProfilingSample(CommandBuffer cmd, string name, CustomSampler sampler = null)
        {
            m_Cmd = cmd;
            m_Name = name;
            m_Disposed = false;
            if (cmd != null && name != "")
                cmd.BeginSample(name);
            m_Sampler = sampler;
            m_Sampler?.Begin();
        }

        // Shortcut to string.Format() using only one argument (reduces Gen0 GC pressure)
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="cmd">Command Buffer.</param>
        /// <param name="format">Formating of the profiling sample.</param>
        /// <param name="arg">Parameters for formating the name.</param>
        public ProfilingSample(CommandBuffer cmd, string format, object arg) : this(cmd, string.Format(format, arg))
        {
        }

        // Shortcut to string.Format() with variable amount of arguments - for performance critical
        // code you should pre-build & cache the marker name instead of using this
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cmd">Command Buffer.</param>
        /// <param name="format">Formating of the profiling sample.</param>
        /// <param name="args">Parameters for formating the name.</param>
        public ProfilingSample(CommandBuffer cmd, string format, params object[] args) : this(cmd, string.Format(format, args))
        {
        }

        /// <summary>
        ///  Dispose pattern implementation
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        // Protected implementation of Dispose pattern.
        void Dispose(bool disposing)
        {
            if (m_Disposed)
                return;

            // As this is a struct, it could have been initialized using an empty constructor so we
            // need to make sure `cmd` isn't null to avoid a crash. Switching to a class would fix
            // this but will generate garbage on every frame (and this struct is used quite a lot).
            if (disposing)
            {
                if (m_Cmd != null && m_Name != "")
                    m_Cmd.EndSample(m_Name);
                m_Sampler?.End();
            }

            m_Disposed = true;
        }
    }
    */
}
