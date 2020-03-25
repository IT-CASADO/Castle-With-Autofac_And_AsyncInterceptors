using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CastleWithAsyncInterceptors
{
	public abstract class Interceptor : IAsyncInterceptor
	{
		private bool initialized = false;

		/// <summary>
		/// Interceptors with higher priority (smaller values) will start execution before lower priority interceptors
		/// </summary>
		public virtual int Priority { get; } = 1;

		public virtual void Intercept(IInvocation invocation)
		{
			invocation.Proceed();
		}

		public Interceptor Init(Action<Interceptor> initializer)
		{
			if (initialized)
			{
				throw new ProxyGenerationException($"Interceptor instance '{GetType().Name}' is already initialized!");
			}

			initializer(this);

			initialized = true;

			return this;
		}


		public static ProxyGenerationOptions CreateProxyGenerateOptions()
		{
			var proxyOptions = ProxyGenerationOptions.Default;

			var methodSelector = new AttributeMethodSelector();
			proxyOptions.Selector = methodSelector;

			return proxyOptions;
		}
	}

	internal class AttributeMethodSelector : IInterceptorSelector
	{
		private class MethodInfoEqualityComparer : IEqualityComparer<MethodInfo>
		{
			public bool Equals(MethodInfo x, MethodInfo y)
			{
				return x.ToString() == y.ToString();
			}

			public int GetHashCode(MethodInfo obj)
			{
				return obj.ToString().GetHashCode();
			}
		}

		private readonly IList<Tuple<Interceptor, HashSet<MethodInfo>>> _classInterceptors = new List<Tuple<Interceptor, HashSet<MethodInfo>>>();
		private readonly IDictionary<MethodInfo, IList<Interceptor>> _methodInterceptors = new Dictionary<MethodInfo, IList<Interceptor>>(new MethodInfoEqualityComparer());

		public void MapMethod(MethodInfo method, Interceptor interceptor, IEnumerable<MethodInfo> ignoreMethods)
		{
			if (method == null)
			{
				_classInterceptors.Add(new Tuple<Interceptor, HashSet<MethodInfo>>(interceptor, new HashSet<MethodInfo>(ignoreMethods, new MethodInfoEqualityComparer())));
				return;
			}


			if (!_methodInterceptors.ContainsKey(method))
			{
				_methodInterceptors.Add(method, new List<Interceptor>());
			}

			_methodInterceptors[method].Add(interceptor);
		}

		public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] interceptors)
		{
			var selectedInterceptors = new List<Interceptor>();

			// add method interceptors (if any exist)
			if (_methodInterceptors.ContainsKey(method))
			{
				selectedInterceptors.AddRange(_methodInterceptors[method]);
			}

			// add class interceptors that are non-duplicate (not overriden) and not ignored
			selectedInterceptors.AddRange(
				 _classInterceptors
					  .Where(i => !i.Item2.Contains(method)) // filter ignored interceptors for method
					  .Where(i => !selectedInterceptors.Any(mi => mi.GetType() == i.Item1.GetType())) // filter duplicate interceptor (overriden)
					  .Select(i => i.Item1));

			return selectedInterceptors.OrderBy(i => i.Priority).ToArray();
		}
	}
}
