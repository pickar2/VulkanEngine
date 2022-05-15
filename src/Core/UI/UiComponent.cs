using System;
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

	private UiComponentFactory(int dataSize = 64) : base(dataSize) { }

	public static UiComponent CreateComponent() => Instance.Create();
}

public struct UiComponentData
{
	/*
		struct UiElementData { // 64 bytes (aligned for 4 bytes)
			float baseX;
			float baseY;

			float localX;
			float localY;

			int16_t baseZ;
			int16_t localZ;

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

			int flags;
			int transformationIndex;
		};
	 */

	public Vector2<float> BasePos;
	public Vector2<float> LocalPos;
	public Int16 BaseZ, LocalZ;

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

	public UiComponentFlags Flags;

	public int TransformationIndex;
}

[Flags]
public enum UiComponentFlags
{
	Disabled = 1 << 0,
	HasTransformation = 1 << 3,
	Offscreen = 1 << 13,
	Deleted = 1 << 14
}
