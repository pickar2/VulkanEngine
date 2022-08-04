﻿using Core.Vulkan;
using SimpleMath.Vectors;

namespace Core.UI.Controls.Panels;

public class FullScreenRootPanel : RootPanel
{
	public FullScreenRootPanel() => Selectable = false;

	public override void Update()
	{
		var screenSize = new Vector2<float>(Context2.Window.WindowWidth, Context2.Window.WindowHeight);
		Size = screenSize;
		base.Update();

		PropagateScale(Scale);
		ComputeSizeAndArea(screenSize);
		ArrangeChildren(screenSize);
		UpdateChildrenMask(new Vector2<float>(float.NegativeInfinity), new Vector2<float>(float.PositiveInfinity));
	}
}