using Core.UI.Controls.Panels;
using Core.UI.Reactive;

namespace Core.UI.Controls;

public class Button : Rectangle
{
	public AlignPanel AlignPanel { get; }
	public Label Label { get; }

	public Alignment TextAlignment { get => AlignPanel.Alignment; set => AlignPanel.Alignment = value; }
	public string Text { get => Label.Text; set => Label.Text = value; }

	public Button(UiContext context) : base(context)
	{
		AlignPanel = new AlignPanel(context) {Alignment = Alignment.Center};
		AddChild(AlignPanel);

		Label = new Label(context)
		{
			Text = "Button",
			OffsetZ = 1
		};
		AlignPanel.AddChild(Label);
	}
}
