using System;
using System.Collections;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;

namespace Core.Serializer.Entities;

internal static class CallSiteCache
{
	private static readonly Hashtable Getters = new(), Setters = new();

	internal static object GetValue(string name, object target)
	{
		// ReSharper disable once InconsistentlySynchronizedField
		var callSite = (CallSite<Func<CallSite, object, object>>?) Getters[name];
		if (callSite != null) return callSite.Target(callSite, target);

		var newSite = CallSite<Func<CallSite, object, object>>.Create(
			Binder.GetMember(
				CSharpBinderFlags.None,
				name,
				typeof(CallSiteCache),
				new[] {CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)}));

		lock (Getters)
		{
			callSite = (CallSite<Func<CallSite, object, object>>?) Getters[name];
			if (callSite == null)
			{
				Getters[name] = callSite = newSite;
			}
		}

		return callSite.Target(callSite, target);
	}

	internal static void SetValue(string name, object target, object value)
	{
		// ReSharper disable once InconsistentlySynchronizedField
		var callSite = (CallSite<Func<CallSite, object, object, object>>?) Setters[name];
		if (callSite == null)
		{
			var newSite = CallSite<Func<CallSite, object, object, object>>.Create(
				Binder.SetMember(
					CSharpBinderFlags.None,
					name,
					typeof(CallSiteCache),
					new[]
					{
						CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
						CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null)
					}));
			lock (Setters)
			{
				callSite = (CallSite<Func<CallSite, object, object, object>>?) Setters[name];
				if (callSite == null)
				{
					Setters[name] = callSite = newSite;
				}
			}
		}

		callSite.Target(callSite, target, value);
	}
}
