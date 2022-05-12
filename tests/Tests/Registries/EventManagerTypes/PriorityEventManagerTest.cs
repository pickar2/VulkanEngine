using System.Reflection;
using Core;
using Core.Registries;
using Core.Registries.API;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Registries.EventManagerTypes;
using Core.Registries.EventManagerTypes.PriorityEventManagerAPI;
using Core.Serializer.Entities.MapperWorkers;

namespace Tests.Registries.EventManagerTypes;

public class PriorityEventManagerTest
{
	[Test]
	public void PriorityTest()
	{
		var callOrder = new Queue<string>();
		var eventManager = new PriorityEventManager<TestData>();
		eventManager.CreateEvent("core:test",
			"core:event-1",
			(_, _, _) => callOrder.Enqueue("core:event-1"),
			new[] {"core:event-2", "core:event-3"},
			new[] {"core:event-0"});

		eventManager.CreateEvent("core:test",
			"core:event-2",
			(_, _, _) => callOrder.Enqueue("core:event-2"),
			new[] {"core:event-3"},
			new[] {"core:event-0"});

		eventManager.CreateEvent("core:test",
			"core:event-3",
			(_, _, _) => callOrder.Enqueue("core:event-3"));

		var namespacedName = typeof(NamespacedName).GetMethod("UnsafeCreateWithFullName",
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static)!
			.Invoke(null, new object?[] {"core", "test"}) as NamespacedName;

		Assert.That(namespacedName, Is.Not.Null);

		var method = typeof(PriorityEventManager<TestData>).GetMethod(
			"Core.Registries.IEventManager<TMainType>.CallEvents",
			BindingFlags.Instance | BindingFlags.NonPublic);

		Assert.That(method, Is.Not.Null);

		method!.Invoke(eventManager, new object?[]
		{
			new TestReg(NamespacedName.SerializerEmpty),
			new TestData {Identifier = namespacedName!},
			ElementChangedType.Register
		});

		Assert.That(callOrder.ToArray(), Is.EqualTo(new[]
		{
			"core:event-2",
			"core:event-3",
			"core:event-1"
		}));
	}

	private sealed class TestReg : SimpleRegistry<NoneEventManager<TestData>, TestData>
	{
		public TestReg(Mapper mapper) : base(mapper)
		{
		}

		public TestReg(Patcher patcher) : base(patcher)
		{
		}

		public TestReg(NamespacedName identifier) : base(identifier)
		{
		}
	}

	private sealed class TestData : IEntry
	{
		public NamespacedName Identifier { get; init; } = null!;
	}
}