using System;
using System.Collections.Generic;
using Core.General;
using Core.UI.Controls;
using Core.UI.Controls.Panels;
using Core.Utils;
using SimpleMath.Vectors;

namespace Core.UI;

public static partial class UiManager
{
	public static readonly UiControl Root = new AbsolutePanel {Selectable = false};
	public static readonly IComparer<UiControl> ZComparer = new ZComparer();

	static UiManager()
	{
		InitEvents();
		InitTestScene();
	}

	public static void Update()
	{
		EventsPreUpdate();

		var screenSize = new Vector2<float>(Context.Window.WindowWidth, Context.Window.WindowHeight);
		Root.UpdateControl(new Vector2<float>(1), screenSize);

		EventsPostUpdate();
	}

	public static void UpdateControl(this UiControl control, Vector2<float> scale, Vector2<float> area)
	{
		control.PropagateScale(scale);
		control.ComputeSizeAndArea(area);
		control.ArrangeChildren(area);
		control.UpdateChildrenMask(new Vector2<float>(float.NegativeInfinity), new Vector2<float>(float.PositiveInfinity));
	}

	public static MList<UiControl> ControlsOnPos(Vector2<float> point, UiControl? startControl, MList<UiControl> list)
	{
		if (startControl is null) return list;

		if (startControl.Selectable && IsControlPartVisible(startControl, point) && IsInsideControl(startControl, point))
			list.BinaryInsert(startControl, ZComparer);
		foreach (var child in startControl.Children) ControlsOnPos(point, child, list);

		return list;
	}

	public static bool IsControlPartVisible(UiControl control, Vector2<float> point) => point.IsInside(control.MaskStart, control.MaskEnd);

	public static bool IsInsideControl(UiControl control, Vector2<float> point) =>
		point.IsInside(control.CombinedPos, control.CombinedPos + control.ComputedSize);
}

public class ZComparer : IComparer<UiControl>
{
	public int Compare(UiControl? c1, UiControl? c2)
	{
		if (c1 is null || c2 is null) throw new ArgumentException("Comparing null controls");
		int diff = c2.CombinedZ - c1.CombinedZ;
		return diff == 0 ? 1 : diff;
	}
}
