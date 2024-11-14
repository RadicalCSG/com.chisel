using System.Collections.Generic;

using UnityEngine.Pool;

namespace Chisel.Components
{
	public class QueuePool<T>
	{
		internal readonly static ObjectPool<Queue<T>> s_Pool = new(() => new Queue<T>(), null, delegate (Queue<T> l)
		{
			l.Clear();
		});

		public static Queue<T> Get()
		{
			return s_Pool.Get();
		}

		public static PooledObject<Queue<T>> Get(out Queue<T> value)
		{
			return s_Pool.Get(out value);
		}

		public static void Release(Queue<T> toRelease)
		{
			s_Pool.Release(toRelease);
		}
	}
}