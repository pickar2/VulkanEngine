using Core.General;
using Core.UI.Controls;
using Core.UI.Controls.Panels;
using SimpleMath.Vectors;

namespace Core.UI;

public static partial class UiManager
{
	public static readonly UiControl Root = new AbsolutePanel {Selectable = false};

	static UiManager()
	{
		InitEvents();
		InitTestScene();
	}

	public static void Update()
	{
		EventsUpdate();
		
		var screenSize = new Vector2<float>(Context.Window.WindowWidth, Context.Window.WindowHeight);
		Root.UpdateControl(new Vector2<float>(1), screenSize);
		
		EventsAfterUpdate();
	}

	public static void UpdateControl(this UiControl control, Vector2<float> scale, Vector2<float> area)
	{
		control.PropagateScale(scale);
		control.ComputeSizeAndArea(area);
		control.ArrangeAndChildren(area);
	}

	public static UiControl? TopControlOnPos(Vector2<float> point, UiControl? startControl)
	{
		while (startControl != null && startControl.Children.Count > 0)
		{
			UiControl? topControl = null;
			short topZ = -1;
			foreach (var child in startControl.Children)
			{
				if (child.Selectable && IsInsideControl(child, point) && child.CombinedZ > topZ)
				{
					topControl = child;
					topZ = child.CombinedZ;
				}
			}
			if (topControl == null) break;
			startControl = topControl;
		}
		
		return startControl;
	}

	public static bool IsInsideControl(UiControl control, Vector2<float> point) =>
		point.IsInside(control.CombinedPos, control.CombinedPos + control.ComputedSize);
}
