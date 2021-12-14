using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace UnityEngine.Rendering
{
    /*
        We need this base class to be able to store a list of VolumeParameter in collections as we
        can't store VolumeParameter<T> with variable T types in the same collection. As a result some
        of the following is a bit hacky...
    */
    // The base class for all parameters types stored in a "VolumeComponent".
    public abstract class VolumeParameter// 不能直接继承这个. 
    {

        // A beautified string for debugger output. This is set on a "DebuggerDisplay" on every parameter types.
        public const string k_DebuggerDisplay = "{m_Value} ({m_OverrideState})";

       
        [SerializeField]protected bool m_OverrideState;
        // The current override state for this parameter. 
        // The Volume system considers overriden parameters for blending, and ignores non-overriden ones.
        //
        // You can override this property to define custom behaviors when the override state changes.
        public virtual bool overrideState
        {
            get => m_OverrideState;
            set => m_OverrideState = value;
        }

        // 猜测是 插值;
        internal abstract void Interp(VolumeParameter from, VolumeParameter to, float t);


        /*
            Casts and gets the typed value of this parameter.
            不安全, 不做任何 类型检测
        */
        /// <typeparam name="T">The type of the value stored in this parameter</typeparam>
        /// <returns> A value of type "T". </returns>
        public T GetValue<T>()
        {
            return ((VolumeParameter<T>) this).value;
        }

       
        // Sets the value of this parameter to the value in 参数 "parameter".
        /// <param name="parameter">The "VolumeParameter" to copy the value from.</param>
        public abstract void SetValue(VolumeParameter parameter);

        
        /*
            Unity calls this method when the parent "VolumeComponent" loads.

            Use this if you need to access fields and properties that you can not access in the constructor of a "ScriptableObject".
            ("VolumeParameter" are generally declared and initialized in a "VolumeComponent", which is a "ScriptableObject"). 
            Unity calls this right after it constructs the parent "VolumeComponent", 
            thus allowing access to previously inaccessible fields and properties.
            ---
            当父类 "VolumeComponent" 执行 load 时, 本函数被调用;

            unity 在 构造完 "VolumeComponent" 之后就立即调用本函数,
            从而允许访问以前无法访问的字段和属性。
        */
        protected internal virtual void OnEnable()
        {
        }

   
        /// Unity calls this method when the parent "VolumeComponent" goes out of scope;
        protected internal virtual void OnDisable()
        {
        }


        /*
            Checks if a given type is an "ObjectParameter{T}".
            (关于这个 "ObjectParameter{T}", 参见本文件最下方)
            
            只要参数 type, 或者其某一层基类 属于 "ObjectParameter{T}", 本函数就返回 true;

            <递归函数>
        */
        /// <param name="type">The type to check.</param>
        public static bool IsObjectParameter(Type type) //   读完__
        {
            if( type.IsGenericType && // 参数 type 是否为 泛型类型
                // 参数 type 的 "generic type definition" 类型, 是否为 "ObjectParameter<>"
                type.GetGenericTypeDefinition() == typeof(ObjectParameter<>)
            ){
                return true;
            }

            return type.BaseType != null             // 参数 type 的类型不能为 System.Object, 或 接口
                && IsObjectParameter(type.BaseType); // 递归查询 参数 type 的基类...
        }


        /// Override this method to free all allocated resources
        public virtual void Release() {}
    }//  类读完__



    /*
        Custom parameters should derive from this class and implement their own behavior.

        "T" should a "serializable type". (比如 int, float...)
        Due to limitations with the serialization system in Unity, you should not use this class
        directly to declare parameters in a "VolumeComponent". Instead, use one of the
        pre-flatten types (like "FloatParameter", or make your own by extending this class;
    */ 
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class VolumeParameter<T>//VolumeParameter__   读完__
        : VolumeParameter, IEquatable<VolumeParameter<T>>
    {
        
        [SerializeField]
        protected T m_Value; // The value stored and serialized by this parameter.

        // The value that this parameter stores.
        // You can override this property to define custom behaviors when the value is changed.
        public virtual T value
        {
            get => m_Value;
            set => m_Value = value;
        }


        // Creates a new "VolumeParameter{T}" instance.
        public VolumeParameter()
            : this(default, false)
        {
        }

      
        /// Creates a new "VolumeParameter{T}" instance.
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        protected VolumeParameter( T value, bool overrideState )
        {
            m_Value = value;
            this.overrideState = overrideState;
        }

        internal override void Interp(VolumeParameter from, VolumeParameter to, float t)
        {
            // Note: this is relatively unsafe (assumes that from and to are both holding type T)
            Interp(from.GetValue<T>(), to.GetValue<T>(), t);
        }


        /*
            Interpolates two values using a factor "t".
            By default, this method does a "snap"(折断) interpolation,
            meaning it returns the value "to" if "t" is higher than 0, and "from" otherwise.
        */
        /// <param name="t">The interpolation factor in range [0,1].</param>
        public virtual void Interp( T from, T to, float t )
        {
            // Default interpolation is naive
            m_Value = t > 0f ? to : from;
        }


      
        //  Sets the value for this parameter and sets its override state to true.
        /// <param name="x">The value to assign to this parameter.</param>
        public void Override(T x)
        {
            overrideState = true;
            m_Value = x;
        }


     
        /// Sets the value of this parameter to the value in "parameter".
        /// <param name="parameter">The "VolumeParameter" to copy the value from.</param>
        public override void SetValue( VolumeParameter parameter )
        {
            m_Value = parameter.GetValue<T>();
        }

      
        //  Returns a hash code for the current object.
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + overrideState.GetHashCode();

                if (!EqualityComparer<T>.Default.Equals(value, default)) // Catches null for references with boxing of value types
                    hash = hash * 23 + value.GetHashCode();

                return hash;
            }
        }

      
        /// Returns a string that represents the current object.
        public override string ToString() => $"{value} ({overrideState})";


        /// Compares the value in a parameter with another value of the same type.
        /// <param name="lhs">The first value in a "VolumeParameter".</param>
        /// <param name="rhs">The second value.</param>
        /// <returns> true if both values are equal, false otherwise.</returns>
        public static bool operator==( VolumeParameter<T> lhs, T rhs ) => 
            lhs != null && 
            !ReferenceEquals(lhs.value, null) && // lhs.value 不能为 null
            lhs.value.Equals(rhs);

        /// Compares the value store in a parameter with another value of the same type.
        public static bool operator!=(VolumeParameter<T> lhs, T rhs) => !(lhs == rhs);


        /// Checks if this parameter is equal to another.
        /// <param name="other">The other parameter to check against.</param>
        /// <returns><c>true</c> if both parameters are equal, <c>false</c> otherwise</returns>
        public bool Equals( VolumeParameter<T> other )
        {
            if (ReferenceEquals(null, other))// other 为 null
                return false;
            if (ReferenceEquals(this, other))// this 和 other 指向统一对象, 或者都为 null
                return true;
            return EqualityComparer<T>.Default.Equals(m_Value, other.m_Value);
        }

       
        //  Determines whether two object instances are equal.
        /// <param name="obj">The object to compare with the current object.</param>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))// obj 为 null
                return false;
            if (ReferenceEquals(this, obj))// this 和 obj 指向统一对象, 或者都为 null
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((VolumeParameter<T>)obj);
        }

       
        // Explicitly downcast a "VolumeParameter{T}" to a value of type
        /// <param name="prop">The parameter to downcast.</param>
        /// <returns>A value of type "T".</returns>
        public static explicit operator T(VolumeParameter<T> prop) => prop.m_Value;
    }



    /*
        The serialization system in Unity can't serialize generic types, the workaround is to extend
        and flatten pre-defined generic types.
        For enums it's recommended to make your own types on the spot, like so:
    
        [Serializable]
        public sealed class MyEnumParameter : VolumeParameter<MyEnum> { }
        public enum MyEnum { One, Two }
    */



    // ------------------------------------------------------------------------------:
    // A "VolumeParameter" that holds a bool value.
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class BoolParameter//BoolParameter__
        : VolumeParameter<bool>
    {
        public BoolParameter(bool value, bool overrideState = false)
            : base(value, overrideState) {}
    }


    // ------------------------------------------------------------------------------:
    /// A "VolumeParameter" that holds a LayerMask value.
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class LayerMaskParameter//LayerMaskParameter__
        : VolumeParameter<LayerMask>
    {
        public LayerMaskParameter(LayerMask value, bool overrideState = false)
            : base(value, overrideState) {}
    }



    // ------------------------------------------------------------------------------:
    /// A "VolumeParameter" that holds an int value.
    /// <seealso cref="MinIntParameter"/>
    /// <seealso cref="MaxIntParameter"/>
    /// <seealso cref="ClampedIntParameter"/>
    /// <seealso cref="NoInterpIntParameter"/>
    /// <seealso cref="NoInterpMinIntParameter"/>
    /// <seealso cref="NoInterpMaxIntParameter"/>
    /// <seealso cref="NoInterpClampedIntParameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class IntParameter//IntParameter__
        : VolumeParameter<int>
    {
        public IntParameter(int value, bool overrideState = false)
            : base(value, overrideState) {}

    
        /// Interpolates between two "int" values.
        /// <param name="t">The interpolation factor in range [0,1]</param>
        public sealed override void Interp(int from, int to, float t)
        {
            /*
                Int snapping(折断) interpolation. 
                Don't use this for enums as they don't necessarily have contiguous values. 
                Use the default interpolator instead (same as bool).
                ---
                不要将此函数用在 enum 类型上, 因为 enum 有可能没有 连续的 int值 的元素;
                (最后计算出的那个 int 值, 无法扎到 enum 中对应的元素)
                改用 默认的就行;
            */
            m_Value = (int)(from + (to - from) * t);
        }
    }


    // ------------------------------------------------------------------------------:
    // A "VolumeParameter" that holds a non-interpolating int value.
    // 和 "IntParameter" 不同, 此版本没有自定义插值函数;
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpIntParameter//NoInterpIntParameter__
        : VolumeParameter<int>
    {
        public NoInterpIntParameter(int value, bool overrideState = false)
            : base(value, overrideState) {}
    }



    // ------------------------------------------------------------------------------:
    // A "VolumeParameter" that holds an int value clamped to a minimum value.
    // 任何传入的值, 都将被 clamp 到 [min, +inf] 区间;
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class MinIntParameter//MinIntParameter__
        : IntParameter
    {
        public int min; // The minimum value to clamp this parameter to.

        // The value that this parameter stores.
        public override int value
        {
            get => m_Value;
            set => m_Value = Mathf.Max(value, min);// 小于 min 的参数都将被丢弃;
        }
        
        public MinIntParameter(int value, int min, bool overrideState = false)
            : base(value, overrideState)
        {
            this.min = min;
        }
    }


    // ------------------------------------------------------------------------------:
    /// A "VolumeParameter" that holds a "non-interpolating int" value that clamped to a minimum value.
    // 存储 没有实现 插值的 int 值, 且被 clamp 到 [min, +inf] 区间;
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpMinIntParameter//NoInterpMinIntParameter__
        : VolumeParameter<int>
    {

        public int min;// The minimum value to clamp this parameter to.


        // The value that this parameter stores.
        public override int value
        {
            get => m_Value;
            set => m_Value = Mathf.Max(value, min);
        }

        public NoInterpMinIntParameter(int value, int min, bool overrideState = false)
            : base(value, overrideState)
        {
            this.min = min;
        }
    }


    // ------------------------------------------------------------------------------:
    // A "VolumeParameter" that holds an int value clamped to a maximum value.
    // 存储的 int 值, 被 clamp 到 [-inf, max] 区间;
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class MaxIntParameter//MaxIntParameter__
        : IntParameter
    {
        public int max;// The maximum value to clamp this parameter to.

        /// The value that this parameter stores.
        public override int value
        {
            get => m_Value;
            set => m_Value = Mathf.Min(value, max);
        }

        public MaxIntParameter(int value, int max, bool overrideState = false)
            : base(value, overrideState)
        {
            this.max = max;
        }
    }


    // ------------------------------------------------------------------------------:
    // A "VolumeParameter" that holds a non-interpolating int value that clamped to a maximum value.
    // 存储 没有实现 插值的 int 值, 且被 clamp 到 [-inf, max] 区间;
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpMaxIntParameter//NoInterpMaxIntParameter__
        : VolumeParameter<int>
    {

        public int max;// The maximum value to clamp this parameter to.


        /// The value that this parameter stores.
        public override int value
        {
            get => m_Value;
            set => m_Value = Mathf.Min(value, max);
        }

        public NoInterpMaxIntParameter(int value, int max, bool overrideState = false)
            : base(value, overrideState)
        {
            this.max = max;
        }
    }



    // ------------------------------------------------------------------------------:
    /// A "VolumeParameter" that holds an int value clamped between a minimum and a maximum value.
    // 存储的 int 值, 被 clamp 到 [min, max] 区间;
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class ClampedIntParameter//ClampedIntParameter__
        : IntParameter
    {
        public int min;// The minimum value to clamp this parameter to.

        public int max;// The maximum value to clamp this parameter to.


        /// The value that this parameter stores.
        public override int value
        {
            get => m_Value;
            set => m_Value = Mathf.Clamp(value, min, max);
        }

        public ClampedIntParameter(int value, int min, int max, bool overrideState = false)
            : base(value, overrideState)
        {
            this.min = min;
            this.max = max;
        }
    }



    // ------------------------------------------------------------------------------:
    /// A <see cref="VolumeParameter"/> that holds a non-interpolating <c>int</c> value
    /// clamped between a minimum and a maximum value.
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpClampedIntParameter : VolumeParameter<int>
    {

        public int min;// The minimum value to clamp this parameter to.

        public int max;// The maximum value to clamp this parameter to.


        /// The value that this parameter stores.
        public override int value
        {
            get => m_Value;
            set => m_Value = Mathf.Clamp(value, min, max);
        }

        public NoInterpClampedIntParameter(int value, int min, int max, bool overrideState = false)
            : base(value, overrideState)
        {
            this.min = min;
            this.max = max;
        }
    }



    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <c>float</c> value.
    /// </summary>
    /// <seealso cref="MinFloatParameter"/>
    /// <seealso cref="MaxFloatParameter"/>
    /// <seealso cref="ClampedFloatParameter"/>
    /// <seealso cref="FloatRangeParameter"/>
    /// <seealso cref="NoInterpFloatParameter"/>
    /// <seealso cref="NoInterpMinFloatParameter"/>
    /// <seealso cref="NoInterpMaxFloatParameter"/>
    /// <seealso cref="NoInterpClampedFloatParameter"/>
    /// <seealso cref="NoInterpFloatRangeParameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class FloatParameter : VolumeParameter<float>
    {
        /// <summary>
        /// Creates a new <seealso cref="FloatParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter</param>
        /// <param name="overrideState">The initial override state for the parameter</param>
        public FloatParameter(float value, bool overrideState = false)
            : base(value, overrideState) {}

        /// <summary>
        /// Interpolates between two <c>float</c> values.
        /// </summary>
        /// <param name="from">The start value</param>
        /// <param name="to">The end value</param>
        /// <param name="t">The interpolation factor in range [0,1]</param>
        public sealed override void Interp(float from, float to, float t)
        {
            m_Value = from + (to - from) * t;
        }
    }



    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a non-interpolating <c>float</c> value.
    /// </summary>
    /// <seealso cref="FloatParameter"/>
    /// <seealso cref="MinFloatParameter"/>
    /// <seealso cref="MaxFloatParameter"/>
    /// <seealso cref="ClampedFloatParameter"/>
    /// <seealso cref="FloatRangeParameter"/>
    /// <seealso cref="NoInterpMinFloatParameter"/>
    /// <seealso cref="NoInterpMaxFloatParameter"/>
    /// <seealso cref="NoInterpClampedFloatParameter"/>
    /// <seealso cref="NoInterpFloatRangeParameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpFloatParameter : VolumeParameter<float>
    {
        /// <summary>
        /// Creates a new <seealso cref="NoInterpFloatParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public NoInterpFloatParameter(float value, bool overrideState = false)
            : base(value, overrideState) {}
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <c>float</c> value clamped to a minimum value.
    /// </summary>
    /// <seealso cref="FloatParameter"/>
    /// <seealso cref="MaxFloatParameter"/>
    /// <seealso cref="ClampedFloatParameter"/>
    /// <seealso cref="FloatRangeParameter"/>
    /// <seealso cref="NoInterpFloatParameter"/>
    /// <seealso cref="NoInterpMinFloatParameter"/>
    /// <seealso cref="NoInterpMaxFloatParameter"/>
    /// <seealso cref="NoInterpClampedFloatParameter"/>
    /// <seealso cref="NoInterpFloatRangeParameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class MinFloatParameter : FloatParameter
    {
        /// <summary>
        /// The minimum value to clamp this parameter to.
        /// </summary>
        public float min;

        /// <summary>
        /// The value that this parameter stores.
        /// </summary>
        /// <remarks>
        /// You can override this property to define custom behaviors when the value is changed.
        /// </remarks>
        public override float value
        {
            get => m_Value;
            set => m_Value = Mathf.Max(value, min);
        }

        /// <summary>
        /// Creates a new <seealso cref="MinFloatParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="min">The minimum value to clamp the parameter to.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public MinFloatParameter(float value, float min, bool overrideState = false)
            : base(value, overrideState)
        {
            this.min = min;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a non-interpolating <c>float</c> value clamped to
    /// a minimum value.
    /// </summary>
    /// <seealso cref="FloatParameter"/>
    /// <seealso cref="MinFloatParameter"/>
    /// <seealso cref="MaxFloatParameter"/>
    /// <seealso cref="ClampedFloatParameter"/>
    /// <seealso cref="FloatRangeParameter"/>
    /// <seealso cref="NoInterpFloatParameter"/>
    /// <seealso cref="NoInterpMaxFloatParameter"/>
    /// <seealso cref="NoInterpClampedFloatParameter"/>
    /// <seealso cref="NoInterpFloatRangeParameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpMinFloatParameter : VolumeParameter<float>
    {
        /// <summary>
        /// The minimum value to clamp this parameter to.
        /// </summary>
        public float min;

        /// <summary>
        /// The value that this parameter stores.
        /// </summary>
        /// <remarks>
        /// You can override this property to define custom behaviors when the value is changed.
        /// </remarks>
        public override float value
        {
            get => m_Value;
            set => m_Value = Mathf.Max(value, min);
        }

        /// <summary>
        /// Creates a new <seealso cref="NoInterpMinFloatParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to storedin the parameter.</param>
        /// <param name="min">The minimum value to clamp the parameter to.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public NoInterpMinFloatParameter(float value, float min, bool overrideState = false)
            : base(value, overrideState)
        {
            this.min = min;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <c>float</c> value clamped to a max value.
    /// </summary>
    /// <seealso cref="FloatParameter"/>
    /// <seealso cref="MinFloatParameter"/>
    /// <seealso cref="ClampedFloatParameter"/>
    /// <seealso cref="FloatRangeParameter"/>
    /// <seealso cref="NoInterpFloatParameter"/>
    /// <seealso cref="NoInterpMinFloatParameter"/>
    /// <seealso cref="NoInterpMaxFloatParameter"/>
    /// <seealso cref="NoInterpClampedFloatParameter"/>
    /// <seealso cref="NoInterpFloatRangeParameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class MaxFloatParameter : FloatParameter
    {
        /// <summary>
        /// The maximum value to clamp this parameter to.
        /// </summary>
        public float max;

        /// <summary>
        /// The value that this parameter stores.
        /// </summary>
        /// <remarks>
        /// You can override this property to define custom behaviors when the value is changed.
        /// </remarks>
        public override float value
        {
            get => m_Value;
            set => m_Value = Mathf.Min(value, max);
        }

        /// <summary>
        /// Creates a new <seealso cref="MaxFloatParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="max">The maximum value to clamp the parameter to.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public MaxFloatParameter(float value, float max, bool overrideState = false)
            : base(value, overrideState)
        {
            this.max = max;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a non-interpolating <c>float</c> value clamped to
    /// a maximum value.
    /// </summary>
    /// <seealso cref="FloatParameter"/>
    /// <seealso cref="MinFloatParameter"/>
    /// <seealso cref="MaxFloatParameter"/>
    /// <seealso cref="ClampedFloatParameter"/>
    /// <seealso cref="FloatRangeParameter"/>
    /// <seealso cref="NoInterpFloatParameter"/>
    /// <seealso cref="NoInterpMinFloatParameter"/>
    /// <seealso cref="NoInterpClampedFloatParameter"/>
    /// <seealso cref="NoInterpFloatRangeParameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpMaxFloatParameter : VolumeParameter<float>
    {
        /// <summary>
        /// The maximum value to clamp this parameter to.
        /// </summary>
        public float max;

        /// <summary>
        /// The value that this parameter stores.
        /// </summary>
        /// <remarks>
        /// You can override this property to define custom behaviors when the value is changed.
        /// </remarks>
        public override float value
        {
            get => m_Value;
            set => m_Value = Mathf.Min(value, max);
        }

        /// <summary>
        /// Creates a new <seealso cref="NoInterpMaxFloatParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="max">The maximum value to clamp the parameter to.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public NoInterpMaxFloatParameter(float value, float max, bool overrideState = false)
            : base(value, overrideState)
        {
            this.max = max;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <c>float</c> value clamped between a minimum and a
    /// maximum value.
    /// </summary>
    /// <seealso cref="FloatParameter"/>
    /// <seealso cref="MinFloatParameter"/>
    /// <seealso cref="MaxFloatParameter"/>
    /// <seealso cref="FloatRangeParameter"/>
    /// <seealso cref="NoInterpFloatParameter"/>
    /// <seealso cref="NoInterpMinFloatParameter"/>
    /// <seealso cref="NoInterpMaxFloatParameter"/>
    /// <seealso cref="NoInterpClampedFloatParameter"/>
    /// <seealso cref="NoInterpFloatRangeParameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class ClampedFloatParameter : FloatParameter
    {
        /// <summary>
        /// The minimum value to clamp this parameter to.
        /// </summary>
        public float min;

        /// <summary>
        /// The maximum value to clamp this parameter to.
        /// </summary>
        public float max;

        /// <summary>
        /// The value that this parameter stores.
        /// </summary>
        /// <remarks>
        /// You can override this property to define custom behaviors when the value is changed.
        /// </remarks>
        public override float value
        {
            get => m_Value;
            set => m_Value = Mathf.Clamp(value, min, max);
        }

        /// <summary>
        /// Creates a new <seealso cref="ClampedFloatParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="min">The minimum value to clamp the parameter to</param>
        /// <param name="max">The maximum value to clamp the parameter to.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public ClampedFloatParameter(float value, float min, float max, bool overrideState = false)
            : base(value, overrideState)
        {
            this.min = min;
            this.max = max;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a non-interpolating <c>float</c> value clamped between
    /// a minimum and a maximum value.
    /// </summary>
    /// <seealso cref="FloatParameter"/>
    /// <seealso cref="MinFloatParameter"/>
    /// <seealso cref="MaxFloatParameter"/>
    /// <seealso cref="ClampedFloatParameter"/>
    /// <seealso cref="FloatRangeParameter"/>
    /// <seealso cref="NoInterpFloatParameter"/>
    /// <seealso cref="NoInterpMinFloatParameter"/>
    /// <seealso cref="NoInterpMaxFloatParameter"/>
    /// <seealso cref="NoInterpFloatRangeParameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpClampedFloatParameter : VolumeParameter<float>
    {
        /// <summary>
        /// The minimum value to clamp this parameter to.
        /// </summary>
        public float min;

        /// <summary>
        /// The maximum value to clamp this parameter to.
        /// </summary>
        public float max;

        /// <summary>
        /// The value that this parameter stores.
        /// </summary>
        /// <remarks>
        /// You can override this property to define custom behaviors when the value is changed.
        /// </remarks>
        public override float value
        {
            get => m_Value;
            set => m_Value = Mathf.Clamp(value, min, max);
        }

        /// <summary>
        /// Creates a new <seealso cref="NoInterpClampedFloatParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="min">The minimum value to clamp the parameter to</param>
        /// <param name="max">The maximum value to clamp the parameter to.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public NoInterpClampedFloatParameter(float value, float min, float max, bool overrideState = false)
            : base(value, overrideState)
        {
            this.min = min;
            this.max = max;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <c>Vector2</c> value holding a range of two
    /// <c>float</c> values clamped between a minimum and a maximum value.
    /// </summary>
    /// <seealso cref="FloatParameter"/>
    /// <seealso cref="MinFloatParameter"/>
    /// <seealso cref="MaxFloatParameter"/>
    /// <seealso cref="ClampedFloatParameter"/>
    /// <seealso cref="NoInterpFloatParameter"/>
    /// <seealso cref="NoInterpMinFloatParameter"/>
    /// <seealso cref="NoInterpMaxFloatParameter"/>
    /// <seealso cref="NoInterpClampedFloatParameter"/>
    /// <seealso cref="NoInterpFloatRangeParameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class FloatRangeParameter : VolumeParameter<Vector2>
    {
        /// <summary>
        /// The minimum value to clamp this parameter to.
        /// </summary>
        public float min;

        /// <summary>
        /// The maximum value to clamp this parameter to.
        /// </summary>
        public float max;

        /// <summary>
        /// The value that this parameter stores.
        /// </summary>
        /// <remarks>
        /// You can override this property to define custom behaviors when the value is changed.
        /// </remarks>
        public override Vector2 value
        {
            get => m_Value;
            set
            {
                m_Value.x = Mathf.Max(value.x, min);
                m_Value.y = Mathf.Min(value.y, max);
            }
        }

        /// <summary>
        /// Creates a new <seealso cref="FloatRangeParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="min">The minimum value to clamp the parameter to</param>
        /// <param name="max">The maximum value to clamp the parameter to.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public FloatRangeParameter(Vector2 value, float min, float max, bool overrideState = false)
            : base(value, overrideState)
        {
            this.min = min;
            this.max = max;
        }

        /// <summary>
        /// Interpolates between two <c>Vector2</c> values.
        /// </summary>
        /// <param name="from">The start value</param>
        /// <param name="to">The end value</param>
        /// <param name="t">The interpolation factor in range [0,1]</param>
        public override void Interp(Vector2 from, Vector2 to, float t)
        {
            m_Value.x = from.x + (to.x - from.x) * t;
            m_Value.y = from.y + (to.y - from.y) * t;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a non-interpolating <c>Vector2</c> value holding
    /// a range of two <c>float</c> values clamped between a minimum and a maximum value.
    /// </summary>
    /// <seealso cref="FloatParameter"/>
    /// <seealso cref="MinFloatParameter"/>
    /// <seealso cref="MaxFloatParameter"/>
    /// <seealso cref="ClampedFloatParameter"/>
    /// <seealso cref="FloatRangeParameter"/>
    /// <seealso cref="NoInterpFloatParameter"/>
    /// <seealso cref="NoInterpMinFloatParameter"/>
    /// <seealso cref="NoInterpMaxFloatParameter"/>
    /// <seealso cref="NoInterpClampedFloatParameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpFloatRangeParameter : VolumeParameter<Vector2>
    {
        /// <summary>
        /// The minimum value to clamp this parameter to.
        /// </summary>
        public float min;

        /// <summary>
        /// The maximum value to clamp this parameter to.
        /// </summary>
        public float max;

        /// <summary>
        /// The value that this parameter stores.
        /// </summary>
        /// <remarks>
        /// You can override this property to define custom behaviors when the value is changed.
        /// </remarks>
        public override Vector2 value
        {
            get => m_Value;
            set
            {
                m_Value.x = Mathf.Max(value.x, min);
                m_Value.y = Mathf.Min(value.y, max);
            }
        }

        /// <summary>
        /// Creates a new <seealso cref="NoInterpFloatRangeParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="min">The minimum value to clamp the parameter to</param>
        /// <param name="max">The maximum value to clamp the parameter to.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public NoInterpFloatRangeParameter(Vector2 value, float min, float max, bool overrideState = false)
            : base(value, overrideState)
        {
            this.min = min;
            this.max = max;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <c>Color</c> value.
    /// </summary>
    /// <seealso cref="NoInterpColorParameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class ColorParameter : VolumeParameter<Color>
    {
        /// <summary>
        /// Is this color HDR?
        /// </summary>
        public bool hdr = false;

        /// <summary>
        /// Should the alpha channel be editable in the editor?
        /// </summary>
        public bool showAlpha = true;

        /// <summary>
        /// Should the eye dropper be visible in the editor?
        /// </summary>
        public bool showEyeDropper = true;

        /// <summary>
        /// Creates a new <seealso cref="ColorParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public ColorParameter(Color value, bool overrideState = false)
            : base(value, overrideState) {}

        /// <summary>
        /// Creates a new <seealso cref="ColorParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="hdr">Specifies whether the color is HDR or not.</param>
        /// <param name="showAlpha">Specifies whether you can edit the alpha channel in the Inspector or not.</param>
        /// <param name="showEyeDropper">Specifies whether the eye dropper is visible in the editor or not.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public ColorParameter(Color value, bool hdr, bool showAlpha, bool showEyeDropper, bool overrideState = false)
            : base(value, overrideState)
        {
            this.hdr = hdr;
            this.showAlpha = showAlpha;
            this.showEyeDropper = showEyeDropper;
            this.overrideState = overrideState;
        }

        /// <summary>
        /// Interpolates between two <c>Color</c> values.
        /// </summary>
        /// <remarks>
        /// For performance reasons, this function interpolates the RGBA channels directly.
        /// </remarks>
        /// <param name="from">The start value.</param>
        /// <param name="to">The end value.</param>
        /// <param name="t">The interpolation factor in range [0,1].</param>
        public override void Interp(Color from, Color to, float t)
        {
            // Lerping color values is a sensitive subject... We looked into lerping colors using
            // HSV and LCH but they have some downsides that make them not work correctly in all
            // situations, so we stick with RGB lerping for now, at least its behavior is
            // predictable despite looking desaturated when `t ~= 0.5` and it's faster anyway.
            m_Value.r = from.r + (to.r - from.r) * t;
            m_Value.g = from.g + (to.g - from.g) * t;
            m_Value.b = from.b + (to.b - from.b) * t;
            m_Value.a = from.a + (to.a - from.a) * t;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a non-interpolating <c>Color</c> value.
    /// </summary>
    /// <seealso cref="ColorParameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpColorParameter : VolumeParameter<Color>
    {
        /// <summary>
        /// Specifies whether the color is HDR or not.
        /// </summary>
        public bool hdr = false;

        /// <summary>
        /// Specifies whether you can edit the alpha channel in the Inspector or not.
        /// </summary>
        public bool showAlpha = true;

        /// <summary>
        /// Specifies whether the eye dropper is visible in the editor or not.
        /// </summary>
        public bool showEyeDropper = true;

        /// <summary>
        /// Creates a new <seealso cref="NoInterpColorParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public NoInterpColorParameter(Color value, bool overrideState = false)
            : base(value, overrideState) {}

        /// <summary>
        /// Creates a new <seealso cref="NoInterpColorParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="hdr">Specifies whether the color is HDR or not.</param>
        /// <param name="showAlpha">Specifies whether you can edit the alpha channel in the Inspector or not.</param>
        /// <param name="showEyeDropper">Specifies whether the eye dropper is visible in the editor or not.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public NoInterpColorParameter(Color value, bool hdr, bool showAlpha, bool showEyeDropper, bool overrideState = false)
            : base(value, overrideState)
        {
            this.hdr = hdr;
            this.showAlpha = showAlpha;
            this.showEyeDropper = showEyeDropper;
            this.overrideState = overrideState;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <c>Vector2</c> value.
    /// </summary>
    /// <seealso cref="NoInterpVector2Parameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class Vector2Parameter : VolumeParameter<Vector2>
    {
        /// <summary>
        /// Creates a new <seealso cref="Vector2Parameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public Vector2Parameter(Vector2 value, bool overrideState = false)
            : base(value, overrideState) {}

        /// <summary>
        /// Interpolates between two <c>Vector2</c> values.
        /// </summary>
        /// <param name="from">The start value.</param>
        /// <param name="to">The end value.</param>
        /// <param name="t">The interpolation factor in range [0,1].</param>
        public override void Interp(Vector2 from, Vector2 to, float t)
        {
            m_Value.x = from.x + (to.x - from.x) * t;
            m_Value.y = from.y + (to.y - from.y) * t;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a non-interpolating <c>Vector2</c> value.
    /// </summary>
    /// <seealso cref="Vector2Parameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpVector2Parameter : VolumeParameter<Vector2>
    {
        /// <summary>
        /// Creates a new <seealso cref="NoInterpVector2Parameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public NoInterpVector2Parameter(Vector2 value, bool overrideState = false)
            : base(value, overrideState) {}
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <c>Vector3</c> value.
    /// </summary>
    /// <seealso cref="NoInterpVector3Parameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class Vector3Parameter : VolumeParameter<Vector3>
    {
        /// <summary>
        /// Creates a new <seealso cref="Vector3Parameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public Vector3Parameter(Vector3 value, bool overrideState = false)
            : base(value, overrideState) {}

        /// <summary>
        /// Interpolates between two <c>Vector3</c> values.
        /// </summary>
        /// <param name="from">The start value.</param>
        /// <param name="to">The end value.</param>
        /// <param name="t">The interpolation factor in range [0,1].</param>
        public override void Interp(Vector3 from, Vector3 to, float t)
        {
            m_Value.x = from.x + (to.x - from.x) * t;
            m_Value.y = from.y + (to.y - from.y) * t;
            m_Value.z = from.z + (to.z - from.z) * t;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a non-interpolating <c>Vector3</c> value.
    /// </summary>
    /// <seealso cref="Vector3Parameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpVector3Parameter : VolumeParameter<Vector3>
    {
        /// <summary>
        /// Creates a new <seealso cref="Vector3Parameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public NoInterpVector3Parameter(Vector3 value, bool overrideState = false)
            : base(value, overrideState) {}
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <c>Vector4</c> value.
    /// </summary>
    /// <seealso cref="NoInterpVector4Parameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class Vector4Parameter : VolumeParameter<Vector4>
    {
        /// <summary>
        /// Creates a new <seealso cref="Vector4Parameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public Vector4Parameter(Vector4 value, bool overrideState = false)
            : base(value, overrideState) {}

        /// <summary>
        /// Interpolates between two <c>Vector4</c> values.
        /// </summary>
        /// <param name="from">The start value.</param>
        /// <param name="to">The end value.</param>
        /// <param name="t">The interpolation factor in range [0,1].</param>
        public override void Interp(Vector4 from, Vector4 to, float t)
        {
            m_Value.x = from.x + (to.x - from.x) * t;
            m_Value.y = from.y + (to.y - from.y) * t;
            m_Value.z = from.z + (to.z - from.z) * t;
            m_Value.w = from.w + (to.w - from.w) * t;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a non-interpolating <c>Vector4</c> value.
    /// </summary>
    /// <seealso cref="Vector4Parameter"/>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpVector4Parameter : VolumeParameter<Vector4>
    {
        /// <summary>
        /// Creates a new <seealso cref="Vector4Parameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public NoInterpVector4Parameter(Vector4 value, bool overrideState = false)
            : base(value, overrideState) {}
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <c>Texture</c> value.
    /// </summary>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class TextureParameter : VolumeParameter<Texture>
    {
        /// <summary>
        /// Creates a new <seealso cref="TextureParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public TextureParameter(Texture value, bool overrideState = false)
            : base(value, overrideState) {}

        // TODO: Texture interpolation
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a non-interpolating <c>Texture</c> value.
    /// </summary>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpTextureParameter : VolumeParameter<Texture>
    {
        /// <summary>
        /// Creates a new <seealso cref="NoInterpTextureParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public NoInterpTextureParameter(Texture value, bool overrideState = false)
            : base(value, overrideState) {}
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <c>RenderTexture</c> value.
    /// </summary>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class RenderTextureParameter : VolumeParameter<RenderTexture>
    {
        /// <summary>
        /// Creates a new <seealso cref="RenderTextureParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public RenderTextureParameter(RenderTexture value, bool overrideState = false)
            : base(value, overrideState) {}

        // TODO: RenderTexture interpolation
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a non-interpolating <c>RenderTexture</c> value.
    /// </summary>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpRenderTextureParameter : VolumeParameter<RenderTexture>
    {
        /// <summary>
        /// Creates a new <seealso cref="NoInterpRenderTextureParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public NoInterpRenderTextureParameter(RenderTexture value, bool overrideState = false)
            : base(value, overrideState) {}
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <c>Cubemap</c> value.
    /// </summary>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class CubemapParameter : VolumeParameter<Texture>
    {
        /// <summary>
        /// Creates a new <seealso cref="CubemapParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public CubemapParameter(Texture value, bool overrideState = false)
            : base(value, overrideState) {}
        // TODO: Cubemap interpolation
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a non-interpolating <c>Cubemap</c> value.
    /// </summary>
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class NoInterpCubemapParameter : VolumeParameter<Cubemap>
    {
        /// <summary>
        /// Creates a new <seealso cref="NoInterpCubemapParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public NoInterpCubemapParameter(Cubemap value, bool overrideState = false)
            : base(value, overrideState) {}
    }



    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a serializable class or struct.
    /// </summary>
    /// <typeparam name="T">The type of serializable object or struct to hold in this parameter.
    /// </typeparam>
    // TODO: ObjectParameter<T> doesn't seem to be working as expect, debug me
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class ObjectParameter<T> : VolumeParameter<T>
    {
        internal ReadOnlyCollection<VolumeParameter> parameters { get; private set; }

        /// <summary>
        /// The current override state for this parameter. Note that this is always forced enabled
        /// on <see cref="ObjectParameter{T}"/>.
        /// </summary>
        public sealed override bool overrideState
        {
            get => true;
            set => m_OverrideState = true;
        }

        /// <summary>
        /// The value stored by this parameter.
        /// </summary>
        public sealed override T value
        {
            get => m_Value;
            set
            {
                m_Value = value;

                if (m_Value == null)
                {
                    parameters = null;
                    return;
                }

                // Automatically grab all fields of type VolumeParameter contained in this instance
                parameters = m_Value.GetType()
                    .GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(t => t.FieldType.IsSubclassOf(typeof(VolumeParameter)))
                    .OrderBy(t => t.MetadataToken) // Guaranteed order
                    .Select(t => (VolumeParameter)t.GetValue(m_Value))
                    .ToList()
                    .AsReadOnly();
            }
        }

        /// <summary>
        /// Creates a new <seealso cref="ObjectParameter{T}"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        public ObjectParameter(T value)
        {
            m_OverrideState = true;
            this.value = value;
        }

        internal override void Interp(VolumeParameter from, VolumeParameter to, float t)
        {
            if (m_Value == null)
                return;

            var paramOrigin = parameters;
            var paramFrom = ((ObjectParameter<T>)from).parameters;
            var paramTo = ((ObjectParameter<T>)to).parameters;

            for (int i = 0; i < paramFrom.Count; i++)
            {
                // Keep track of the override state for debugging purpose
                paramOrigin[i].overrideState = paramTo[i].overrideState;

                if (paramTo[i].overrideState)
                    paramOrigin[i].Interp(paramFrom[i], paramTo[i], t);
            }
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds an <c>AnimationCurve</c> value.
    /// </summary>
    [Serializable]
    public class AnimationCurveParameter : VolumeParameter<AnimationCurve>
    {
        /// <summary>
        /// Creates a new <seealso cref="AnimationCurveParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to be stored in the parameter</param>
        /// <param name="overrideState">The initial override state for the parameter</param>
        public AnimationCurveParameter(AnimationCurve value, bool overrideState = false)
            : base(value, overrideState) {}

        // TODO: Curve interpolation
    }
}
