﻿using System;
using Core.VulkanData;
using SimpleMath.Vectors;

#pragma warning disable CS0618 // obsolete use

namespace Core.UI;

public unsafe class UiComponent : VulkanDataHolder
{
	private MaterialDataHolder _fragMaterial = default!;
	private MaterialDataHolder _vertMaterial = default!;

	public MaterialDataHolder VertMaterial
	{
		get => _vertMaterial;
		set
		{
			_vertMaterial = value;
			var data = GetData();
			data->VertexMaterialType = value.MaterialId;
			data->VertexDataIndex = value.VulkanDataIndex;
		}
	}

	public MaterialDataHolder FragMaterial
	{
		get => _fragMaterial;
		set
		{
			_fragMaterial = value;
			var data = GetData();
			data->FragmentMaterialType = value.MaterialId;
			data->FragmentDataIndex = value.VulkanDataIndex;
		}
	}

	public UiComponentData* GetData() => VulkanDataFactory.GetPointerToData<UiComponentData>(VulkanDataIndex);
}

public class UiComponentFactory : AbstractVulkanDataFactory<UiComponent>
{
	public static readonly UiComponentFactory Instance = new();

	private UiComponentFactory(int dataSize = 56) : base(dataSize) { }

	public static UiComponent CreateComponent() => Instance.Create();
}

public struct UiComponentData
{
	/*
		struct UiElementData { // 56 bytes (aligned for 4 bytes)
		    int16_t flags;
		    int16_t zIndex;
		    
		    float baseX;
		    float baseY;

		    float localX;
		    float localY;

		    float width;
		    float height;

		    float maskStartX;
		    float maskStartY;

		    float maskEndX;
		    float maskEndY;

		    int16_t vertexMaterialType;
		    int16_t fragmentMaterialType;

		    int vertexDataIndex;
		    int fragmentDataIndex;
		};
	 */

	public UiComponentFlags Flags;
	public Int16 ZIndex;

	public Vector2<float> BasePos;
	public Vector2<float> LocalPos;

	public Vector2<float> Size;

	public Vector2<float> MaskStart;
	public Vector2<float> MaskEnd;

	[Obsolete("Use vertex material setter from UiComponent")]
	public Int16 VertexMaterialType;

	[Obsolete("Use fragment material setter from UiComponent")]
	public Int16 FragmentMaterialType;

	[Obsolete("Use vertex material setter from UiComponent")]
	public int VertexDataIndex;

	[Obsolete("Use fragment material setter from UiComponent")]
	public int FragmentDataIndex;
}

[Flags]
public enum UiComponentFlags : short
{
	Disabled = 1 << 0,
	HasTransformation = 1 << 3,
	Offscreen = 1 << 13,
	Deleted = 1 << 14
}
