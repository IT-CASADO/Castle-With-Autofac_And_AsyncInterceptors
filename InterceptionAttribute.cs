using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CastleWithAsyncInterceptors
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
	public abstract class InterceptionAttribute : Attribute
	{
		protected abstract Type InterceptorType
		{
			get;
		}

		public virtual Action<Interceptor> GetInitializer(Type instanceType)
		{
			return interceptor =>
			{
			};
		}

		public Type GetInterceptorType()
		{
			if (typeof(Interceptor).IsAssignableFrom(InterceptorType))
			{
				return InterceptorType;
			}

			throw new ProxyGenerationException($"The type '{InterceptorType}' is not a valid 'IInterceptor'!");
		}

		protected abstract IEnumerable<object> GetConfigurationComponents();

		public override bool Equals(object obj)
		{
			return this.ComponentEquals(obj, () =>
			{
				var vo = obj as InterceptionAttribute;
				return GetConfigurationComponents().SequenceEqual(vo.GetConfigurationComponents());
			});
		}

		public override int GetHashCode()
		{
			return this.CombineHashCodes(GetConfigurationComponents());
		}
	}

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
	public abstract class IgnoreInterceptionAttribute : Attribute
	{
		public abstract Type InterceptionAttributeType
		{
			get;
		}
	}
}
