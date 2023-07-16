using Core.UI.Reactive;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public abstract unsafe class UiControlOneComponent : UiControl
{
	public readonly UiComponent Component;

	public UiControlOneComponent(UiContext context) : base(context)
	{
		Component = context.Root.CreateComponent();
		Component.GetData()->MaskStart = new Vector2<float>(float.NegativeInfinity);
		Component.GetData()->MaskEnd = new Vector2<float>(float.PositiveInfinity);
		Component.MarkForGPUUpdate();
	}

	public override Vector2<float> BasePos
	{
		get => Component.GetData()->BasePos;
		set
		{
			Component.GetData()->BasePos = value;
			Component.MarkForGPUUpdate();
		}
	}

	public override short BaseZ
	{
		get => Component.GetData()->BaseZ;
		set
		{
			Component.GetData()->BaseZ = value;
			Component.MarkForGPUUpdate();
		}
	}

	public override Vector2<float> LocalPos
	{
		get => Component.GetData()->LocalPos;
		set
		{
			Component.GetData()->LocalPos = value;
			Component.MarkForGPUUpdate();
		}
	}

	public override short LocalZ
	{
		get => Component.GetData()->LocalZ;
		set
		{
			Component.GetData()->LocalZ = value;
			Component.MarkForGPUUpdate();
		}
	}

	public override Vector2<float> ComputedSize
	{
		get => Component.GetData()->Size;
		set
		{
			Component.GetData()->Size = value;
			Component.MarkForGPUUpdate();
		}
	}

	public override Vector2<float> MaskStart
	{
		get => Component.GetData()->MaskStart;
		set
		{
			Component.GetData()->MaskStart = value;
			Component.MarkForGPUUpdate();
		}
	}

	public override Vector2<float> MaskEnd
	{
		get => Component.GetData()->MaskEnd;
		set
		{
			Component.GetData()->MaskEnd = value;
			Component.MarkForGPUUpdate();
		}
	}

	public void Disable()
	{
		Component.GetData()->Flags |= UiComponentFlags.Disabled;
		Component.MarkForGPUUpdate();
	}

	public void Enable()
	{
		Component.GetData()->Flags &= ~UiComponentFlags.Disabled;
		Component.MarkForGPUUpdate();
	}

	public override void Dispose()
	{
		// App.Logger.Debug.Message($"Disposing control frag: ({Component.FragMaterial.VulkanDataIndex}@{Component.FragMaterial.MaterialFactory.Name}), vert: ({Component.VertMaterial.VulkanDataIndex}@{Component.VertMaterial.MaterialFactory.Name})");

		Component.FragMaterial.Dispose();
		Component.VertMaterial.Dispose();
		Component.Dispose();

		base.Dispose();
	}
}
