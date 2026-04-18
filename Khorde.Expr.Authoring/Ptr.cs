using Unity.Collections.LowLevel.Unsafe;

namespace Khorde.Expr.Authoring
{
	/// <summary>
	/// Pointer to an unmanaged value with an optional safety handle
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public unsafe struct Ptr<T> where T : unmanaged
	{
	    private T* m_Pointer;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	    private AtomicSafetyHandle m_Safety;
#endif

	    public Ptr(ref T value)
	    {
	        fixed(T* ptr = &value)
	            m_Pointer = ptr;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
	        m_Safety = default;
#endif
	    }

	    public Ptr(ref T value, AtomicSafetyHandle safety)
	    {
	        fixed(T* ptr = &value)
	            m_Pointer = ptr;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
	        m_Safety = safety;
#endif
	    }

	    public ref T ValueRO
	    {
	        get
	        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	            if(!AtomicSafetyHandle.IsDefaultValue(m_Safety))
	                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
	            return ref *m_Pointer;
	        }
	    }
		
	    public ref T ValueRW
	    {
	        get
	        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	            if(!AtomicSafetyHandle.IsDefaultValue(m_Safety))
	                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
	            return ref *m_Pointer;
	        }
	    }

		public T* UnsafePointer
		{
			get
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	            if(!AtomicSafetyHandle.IsDefaultValue(m_Safety))
	                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

				return m_Pointer;
			}
		}

		public override string ToString()
		{
			return string.Format("Ptr<{0}>(0x{1:X8})", typeof(T).Name, (nint)m_Pointer);
		}
	}

	public static unsafe class Ptr
	{
		public static Ptr<T> Make<T>(ref T value) where T : unmanaged => new Ptr<T>(ref value);
	}
}