﻿using CommandLine;
using Core.Vulkan;
using SimpleMath.Vectors;

namespace Core.UI.Controls.Panels;

public class FullScreenRootPanel : RootPanel
{
	public FullScreenRootPanel(UiComponentManager componentManager, MaterialManager materialManager, GlobalDataManager globalDataManager) : base(
		componentManager, materialManager, globalDataManager)
	{
		Selectable = false;
	}

	public override void Update()
	{
		Size = Context.State.WindowSize.Value.Cast<uint, float>();
		base.Update();

		PropagateScale(Scale);
		ComputeSizeAndArea(Size);
		ArrangeChildren(Size);
		UpdateChildrenMask(new Vector2<float>(float.NegativeInfinity), new Vector2<float>(float.PositiveInfinity));
	}
}
