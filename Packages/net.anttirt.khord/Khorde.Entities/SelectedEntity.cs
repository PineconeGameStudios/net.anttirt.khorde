using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Entities;

namespace Khorde.Entities
{
	public static class SelectedEntity
	{
		struct SingleEntityCtx { }
		private static readonly SharedStatic<Entity> Holder = SharedStatic<Entity>.GetOrCreate<SingleEntityCtx>();
		public static ref Entity Value => ref Holder.Data;
	}
}
