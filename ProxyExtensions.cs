using Autofac;
using Autofac.Builder;
using Autofac.Core;
using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CastleWithAsyncInterceptors
{
	public static class ProxyExtensions
	{
		private class InterceptionAttributeInfo
		{
			public Type InstanceType { get; set; }
			public MethodInfo Method { get; set; }
			public InterceptionAttribute Attribute { get; set; }
			public IList<MethodInfo> IgnoreMethods { get; set; } = new List<MethodInfo>();
		}

		private static readonly ProxyGenerator ProxyGenerator = new ProxyGenerator();

		public static IRegistrationBuilder<TLimit, TActivatorData, TSingleRegistrationStyle> EnableAttributeInterceptors<TLimit, TActivatorData, TSingleRegistrationStyle>(
			 this IRegistrationBuilder<TLimit, TActivatorData, TSingleRegistrationStyle> registration)
		{
			if (registration == null)
			{
				throw new ArgumentNullException(nameof(registration));
			}

			registration.RegistrationData.ActivatingHandlers.Add((sender, e) =>
			{
				EnsureInterfaceInterceptionApplies(e.Component);

				var proxiedInterfaces = e.Instance
					 .GetType()
					 .GetInterfaces()
					 .Where(i => i.GetTypeInfo().IsVisible)
					 .ToArray();

				if (!proxiedInterfaces.Any())
				{
					return;
				}

				var methodSelector = new AttributeMethodSelector();
				var proxyOptions = ProxyGenerationOptions.Default;
				proxyOptions.Selector = methodSelector;

				var interceptors = FindAttributeInterceptors(e.Instance.GetType(), proxiedInterfaces)
					 .Select(attrInfo =>
					 {
						 var interceptor = e.Context.Resolve(attrInfo.Attribute.GetInterceptorType()) as Interceptor;
						 interceptor.Init(attrInfo.Attribute.GetInitializer(attrInfo.InstanceType));
						 methodSelector.MapMethod(attrInfo.Method, interceptor, attrInfo.IgnoreMethods);
						 return interceptor;
					 })
					 .Cast<IInterceptor>()
					 .ToArray();

				var firstInterface = proxiedInterfaces.First();
				var additionalInterfaces = proxiedInterfaces.Skip(1).ToArray();

				e.Instance = ProxyGenerator.CreateInterfaceProxyWithTarget(firstInterface, additionalInterfaces, e.Instance, proxyOptions, interceptors);
			});

			return registration;
		}

		private static IEnumerable<InterceptionAttributeInfo> FindAttributeInterceptors(Type instanceType, Type[] interfaces)
		{
			var methodAttributes = new List<InterceptionAttributeInfo>();

			var classAttributes = instanceType.GetCustomAttributes<InterceptionAttribute>(true)
				 .Select(attr => new InterceptionAttributeInfo() { InstanceType = instanceType, Attribute = attr }).ToList(); // IMPORTANT: this needs to be a List for later assignement to items in foreach loop

			foreach (var methodInfo in instanceType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
			{
				methodAttributes.AddRange(methodInfo.GetCustomAttributes<InterceptionAttribute>()
						  .Select(attr => new InterceptionAttributeInfo() { InstanceType = instanceType, Method = methodInfo, Attribute = attr }));

				var ignoringAttributes = methodInfo.GetCustomAttributes<IgnoreInterceptionAttribute>();

				foreach (var classAttributeInfo in classAttributes.Where(info => ignoringAttributes.Any(ignore => ignore.InterceptionAttributeType == info.Attribute.GetType())))
				{
					classAttributeInfo.IgnoreMethods.Add(methodInfo);
				}
			}

			return classAttributes.Concat(methodAttributes);
		}

		private static void EnsureInterfaceInterceptionApplies(IComponentRegistration componentRegistration)
		{
			if (componentRegistration.Services
				 .OfType<IServiceWithType>()
				 .Select(s => s.ServiceType.GetTypeInfo())
				 .Any(s => !s.IsInterface))
			{
				throw new InvalidOperationException($"The component '{componentRegistration}' cannot use interface interception as it provides services that are not publicly visible interfaces. Check your registration of the component to ensure you're not enabling interception and registering it as an internal/private interface type.");
			}
		}
	}
}
