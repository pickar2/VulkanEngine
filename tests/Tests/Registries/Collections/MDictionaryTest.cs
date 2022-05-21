using Core;
using Core.Registries.Collections;
using Core.Registries.Entities;
using Core.Serializer;
using Core.Serializer.Entities.Accessors;

namespace Tests.Registries.Collections;

public class PooledDictionaryTest
{
	[Test]
	[TestCase("TestKey", "TestValue", ExpectedResult = true)]
	[TestCase(1, "TestValue", ExpectedResult = true)]
	[TestCase(1, 2, ExpectedResult = true)]
	[TestCase(3, 4, ExpectedResult = true)]
	public bool FillTest<TKey, TValue>(TKey key, TValue value)
	{
		var dict = new MDictionary<TKey, TValue>();
		dict.Add(key, value);

		return dict[key]!.Equals(value);
	}

	[Test]
	public void SerializeTest()
	{
		var dict = new MDictionary<string, string>(StringComparer.Ordinal)
		{
			{"First", "Second"},
			{"Third", "Fourth"}
		};

		var serializerRegistry = App.Get<SerializerRegistry>();
		var memoryStream = new MemoryStream();
		serializerRegistry.Serialize(memoryStream, dict, CompressionLevel.L00_FAST);
		var dictInBytes = memoryStream.ToArray();

		using var readerStream = new MemoryStream(dictInBytes);
		var dictV1 = serializerRegistry.Deserialize<MDictionary<string, string>>(readerStream)!;

		var accessor = MemberAccessor.Create(typeof(MDictionary<string, string>));

		T Get<T>(MDictionary<string, string> dictionary, string name)
		{
			return (T) accessor[dictionary, name];
		}

		// Identifier
		Assert.That(Get<NamespacedName>(dict, "Identifier").FullName,
			Is.EqualTo(Get<NamespacedName>(dictV1, "Identifier").FullName));

		// Size
		Assert.That(dict.Count, Is.EqualTo(dictV1.Count));

		using var firstEnumerator = dict.GetEnumerator();
		using var secondEnumerator = dictV1.GetEnumerator();

		while (firstEnumerator.MoveNext() && secondEnumerator.MoveNext())
		{
			Assert.That(firstEnumerator.Current.Key, Is.EqualTo(secondEnumerator.Current.Key));
			Assert.That(firstEnumerator.Current.Value, Is.EqualTo(secondEnumerator.Current.Value));
		}

		Assert.Pass();
	}
}