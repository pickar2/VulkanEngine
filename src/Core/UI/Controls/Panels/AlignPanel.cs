using Core.UI.Reactive;
using SimpleMath.Vectors;

namespace Core.UI.Controls.Panels;

public class AlignPanel : UiControl
{
	public Alignment Alignment = Alignment.TopLeft;

	public override void ArrangeChildren(Vector2<float> area)
	{
		foreach (var child in Children)
		{
			child.BasePos = CombinedPos;
			child.BaseZ = CombinedZ;

			switch (Alignment)
			{
				case Alignment.TopLeft: break;
				case Alignment.TopCenter:
					child.LocalPos = new Vector2<float>((ComputedArea.X - child.ComputedArea.X) / 2, 0);
					break;
				case Alignment.TopRight:
					child.LocalPos = new Vector2<float>(ComputedArea.X - child.ComputedArea.X, 0);
					break;
				case Alignment.CenterLeft:
					child.LocalPos = new Vector2<float>(0, (ComputedArea.Y - child.ComputedArea.Y) / 2);
					break;
				case Alignment.Center:
					child.LocalPos = (ComputedArea - child.ComputedArea) / 2;
					break;
				case Alignment.CenterRight:
					child.LocalPos = new Vector2<float>(ComputedArea.X - child.ComputedArea.X, (ComputedArea.Y - child.ComputedArea.Y) / 2);
					break;
				case Alignment.BottomLeft:
					child.LocalPos = new Vector2<float>(0, ComputedArea.Y - child.ComputedArea.Y);
					break;
				case Alignment.BottomCenter:
					child.LocalPos = new Vector2<float>((ComputedArea.X - child.ComputedArea.X) / 2, ComputedArea.Y - child.ComputedArea.Y);
					break;
				case Alignment.BottomRight:
					child.LocalPos = ComputedArea - child.ComputedArea;
					break;
			}

			child.LocalZ = child.OffsetZ;

			child.ArrangeChildren(child.ComputedSize);
		}
	}

	public AlignPanel(UiContext context) : base(context) { }
}

public enum Alignment
{
	TopLeft, TopCenter, TopRight,
	CenterLeft, Center, CenterRight,
	BottomLeft, BottomCenter, BottomRight
}
