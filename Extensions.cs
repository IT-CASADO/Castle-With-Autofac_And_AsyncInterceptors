using System;
using System.Collections.Generic;

namespace CastleWithAsyncInterceptors
{
	public static class ObjectExtensions
	{
		public static bool ComponentEquals(this object @this, object obj, Func<bool> componentEquals)
		{
			if (object.ReferenceEquals(@this, obj))
			{
				return true;
			}

			if (object.ReferenceEquals(null, obj))
			{
				return false;
			}

			if (@this.GetType() != obj.GetType())
			{
				return false;
			}

			return componentEquals();
		}

		public static int CombineHashCodes(this object @this, IEnumerable<object> identities)
		{
			unchecked
			{
				var hash = 911;
				foreach (var obj in identities)
				{
					hash = hash * 47 + (obj != null ? obj.GetHashCode() : 0);
				}

				return hash;
			}
		}

	}
}
