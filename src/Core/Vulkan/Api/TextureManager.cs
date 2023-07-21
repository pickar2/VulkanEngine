using System;
using System.Collections.Generic;
using Core.Vulkan.Descriptors;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Api;

public static unsafe class TextureManager
{
	public static uint CurrentDescriptorPoolSize { get; private set; } = 1 << 14;
	public static uint CurrentSetSize { get; private set; } = 1 << 10;
	public static uint TextureCount { get; private set; } = 0;

	private static readonly Dictionary<string, Texture> RegisteredTextures = new();

	public static readonly ReCreator<DescriptorSetLayout> DescriptorSetLayout;
	public static readonly ReCreator<DescriptorPool> DescriptorPool;
	public static readonly ReCreator<DescriptorSet> DescriptorSet;
	public static readonly ReCreator<Sampler> Sampler;

	/*
	 * TODO: 
	 * When MaxTextureCount changes DescriptorSetLayout needs to be rebuilt => Pipelines using this layout needs to be rebuilt
	 * When CurrentTextureCount changes DescriptorSet needs to be rebuilt => CommandBuffers using this set need to be re recorded
	 * When PhysicalDeviceDescriptorIndexingFeaturesEXT.DescriptorBindingPartiallyBound is not available => copy error texture to every available descriptor
	 * When PhysicalDeviceDescriptorIndexingFeaturesEXT.DescriptorBindingVariableDescriptorCount is not available => CurrentDescriptorPoolSize = CurrentSetSize
	 */
	static TextureManager()
	{
		Sampler = ReCreate.InDevice.Auto(() => CreateImageSampler(16), sampler => sampler.Dispose());

		DescriptorSetLayout = ReCreate.InDevice.Auto(() => CreateDescriptorSetLayout(), layout => layout.Dispose());
		DescriptorPool = ReCreate.InDevice.Auto(() => CreateDescriptorPool(), pool => pool.Dispose());
		DescriptorSet = ReCreate.InDevice.Auto(() => AllocateVariableDescriptorSet(DescriptorSetLayout, DescriptorPool, CurrentSetSize));

		// FullSetUpdate();
		Context.DeviceEvents.AfterCreate += () => TextureCount = 0;
	}

	public static Texture RegisterTexture(string name, ImageView view, uint id = UInt32.MaxValue)
	{
		if (id == uint.MaxValue) id = TextureCount++;
		Debug.SetObjectName(view.Handle, ObjectType.ImageView, name);

		var texture = new Texture(id, view);
		RegisteredTextures[name] = texture;
		UpdateTextureBinding(texture);

		// Logger.Info($"Registered texture `{name}`.");

		return texture;
	}

	public static bool TryGetTexture(string name, out Texture texture) => RegisteredTextures.TryGetValue(name, out texture);

	public static uint GetTextureId(string name) => RegisteredTextures.TryGetValue(name, out var texture) ? texture.Id : 0;

	private static void UpdateTextureBinding(Texture texture) =>
		DescriptorSetUtils.UpdateBuilder()
			.WriteImage(DescriptorSet, 0, texture.Id, 1, DescriptorType.CombinedImageSampler,
				ImageLayout.ShaderReadOnlyOptimal, texture.ImageView, Sampler)
			.Update();

	private static void FullSetUpdate()
	{
		if (TextureCount == 0) return;

		var imageInfo = new DescriptorImageInfo[TextureCount];

		int index = 0;
		foreach ((string? _, var texture) in RegisteredTextures)
		{
			imageInfo[index++] = new DescriptorImageInfo
			{
				ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
				Sampler = Sampler,
				ImageView = texture.ImageView
			};
		}

		var write = new WriteDescriptorSet
		{
			SType = StructureType.WriteDescriptorSet,
			DescriptorType = DescriptorType.CombinedImageSampler,
			DstSet = DescriptorSet,
			DstBinding = 0,
			DstArrayElement = 0,
			DescriptorCount = (uint) imageInfo.Length,
			PImageInfo = imageInfo[0].AsPointer()
		};

		Context.Vk.UpdateDescriptorSets(Context.Device, 1, write, 0, null);
	}

	private static DescriptorSetLayout CreateDescriptorSetLayout() =>
		VulkanDescriptorSetLayout.Builder(DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit)
			.AddBinding(0, DescriptorType.CombinedImageSampler, CurrentDescriptorPoolSize, ShaderStageFlags.FragmentBit | ShaderStageFlags.ComputeBit,
				DescriptorBindingFlags.PartiallyBoundBit | DescriptorBindingFlags.UpdateAfterBindBit | DescriptorBindingFlags.VariableDescriptorCountBit)
			.Build();

	private static DescriptorPool CreateDescriptorPool()
	{
		var texturesPoolSizes = new DescriptorPoolSize
		{
			DescriptorCount = Context.SwapchainImageCount * CurrentDescriptorPoolSize,
			Type = DescriptorType.CombinedImageSampler
		};

		var texturesCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = Context.SwapchainImageCount,
			PoolSizeCount = 1,
			PPoolSizes = &texturesPoolSizes,
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBitExt
		};

		Check(Context.Vk.CreateDescriptorPool(Context.Device, &texturesCreateInfo, null, out var pool),
			"Failed to create descriptor pool.");

		return pool;
	}
}

public readonly struct Texture
{
	public readonly uint Id;
	public readonly ImageView ImageView;

	public Texture(uint id, ImageView imageView)
	{
		Id = id;
		ImageView = imageView;
	}
}
