using System;
using System.Collections.Generic;
using Core.Vulkan.Renderers;
using Silk.NET.Vulkan;
using static Core.Vulkan.VulkanUtils;

namespace Core.Vulkan.Api;

public static unsafe class TextureManager
{
	public static uint CurrentDescriptorPoolSize { get; private set; } = 1 << 14;
	public static uint CurrentSetSize { get; private set; } = 1 << 10;
	public static uint TextureCount { get; private set; } = 0;

	private static readonly Dictionary<string, Texture> RegisteredTextures = new();

	public static readonly OnAccessValueReCreator<DescriptorSetLayout> DescriptorSetLayout;
	public static readonly OnAccessValueReCreator<DescriptorPool> DescriptorPool;
	public static readonly OnAccessValueReCreator<DescriptorSet> DescriptorSet;
	public static readonly OnAccessValueReCreator<Sampler> Sampler;

	/*
	 * When MaxTextureCount changes DescriptorSetLayout needs to be rebuilt => Pipelines using this layout needs to be rebuilt
	 * When CurrentTextureCount changes DescriptorSet needs to be rebuilt => CommandBuffers using this set need to be re recorded
	 * When PhysicalDeviceDescriptorIndexingFeaturesEXT.DescriptorBindingPartiallyBound is not available => copy error texture to every available descriptor
	 * When PhysicalDeviceDescriptorIndexingFeaturesEXT.DescriptorBindingVariableDescriptorCount is not available => CurrentDescriptorPoolSize = CurrentSetSize
	 */
	static TextureManager()
	{
		Sampler = ReCreate.InDevice.OnAccessValue(() => CreateImageSampler(16), sampler => sampler.Dispose());

		DescriptorSetLayout = ReCreate.InDevice.OnAccessValue(() => CreateDescriptorSetLayout(), layout => layout.Dispose());
		DescriptorPool = ReCreate.InDevice.OnAccessValue(() => CreateDescriptorPool(), pool => pool.Dispose());
		DescriptorSet = ReCreate.InDevice.OnAccessValue(() => CreateDescriptorSet(DescriptorSetLayout, DescriptorPool));

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

		return texture;
	}

	public static bool TryGetTexture(string name, out Texture texture) => RegisteredTextures.TryGetValue(name, out texture);

	public static uint GetTextureId(string name) => RegisteredTextures.TryGetValue(name, out var texture) ? texture.Id : 0;

	private static void UpdateTextureBinding(Texture texture)
	{
		var imageInfo = new DescriptorImageInfo
		{
			ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
			Sampler = Sampler,
			ImageView = texture.ImageView
		};

		var write = new WriteDescriptorSet
		{
			SType = StructureType.WriteDescriptorSet,
			DescriptorCount = 1,
			DstBinding = 0,
			DstArrayElement = texture.Id,
			DescriptorType = DescriptorType.CombinedImageSampler,
			DstSet = DescriptorSet,
			PImageInfo = &imageInfo
		};

		Context.Vk.UpdateDescriptorSets(Context.Device, 1, write, 0, null);
	}

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
			DescriptorCount = (uint) imageInfo.Length,
			DstBinding = 0,
			DstArrayElement = 0,
			DescriptorType = DescriptorType.CombinedImageSampler,
			DstSet = DescriptorSet,
			PImageInfo = imageInfo[0].AsPointer()
		};

		Context.Vk.UpdateDescriptorSets(Context.Device, 1, write, 0, null);
	}

	private static DescriptorSetLayout CreateDescriptorSetLayout()
	{
		var textureFlags = stackalloc DescriptorBindingFlags[1];
		textureFlags[0] = DescriptorBindingFlags.VariableDescriptorCountBit | DescriptorBindingFlags.UpdateAfterBindBit |
		                  DescriptorBindingFlags.PartiallyBoundBit;

		var textureFlagsCreateInfo = new DescriptorSetLayoutBindingFlagsCreateInfoEXT
		{
			SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfoExt,
			BindingCount = 1,
			PBindingFlags = textureFlags
		};

		var texturesBindings = new DescriptorSetLayoutBinding[]
		{
			new()
			{
				Binding = 0,
				DescriptorCount = CurrentDescriptorPoolSize,
				DescriptorType = DescriptorType.CombinedImageSampler,
				StageFlags = ShaderStageFlags.FragmentBit | ShaderStageFlags.ComputeBit
			}
		};

		var texturesCreateInfo = new DescriptorSetLayoutCreateInfo
		{
			SType = StructureType.DescriptorSetLayoutCreateInfo,
			BindingCount = 1,
			PBindings = texturesBindings[0].AsPointer(),
			PNext = &textureFlagsCreateInfo,
			Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBitExt
		};

		Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &texturesCreateInfo, null, out var layout),
			"Failed to create descriptor set layout.");

		return layout;
	}

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

	private static DescriptorSet CreateDescriptorSet(DescriptorSetLayout layout, DescriptorPool descriptorPool)
	{
		uint* counts = stackalloc uint[] {CurrentSetSize};
		var variableCount = new DescriptorSetVariableDescriptorCountAllocateInfo
		{
			SType = StructureType.DescriptorSetVariableDescriptorCountAllocateInfo,
			DescriptorSetCount = 1,
			PDescriptorCounts = counts
		};

		var texturesAllocInfo = new DescriptorSetAllocateInfo
		{
			SType = StructureType.DescriptorSetAllocateInfo,
			DescriptorPool = descriptorPool,
			DescriptorSetCount = 1,
			PSetLayouts = &layout,
			PNext = &variableCount
		};

		Check(Context.Vk.AllocateDescriptorSets(Context.Device, &texturesAllocInfo, out var set), "Failed to allocate descriptor set.");

		return set;
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