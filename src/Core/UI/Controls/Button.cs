using System;
using Core.UI.Animations;
using Core.UI.Controls.Panels;
using Core.UI.Reactive;
using SDL2;

namespace Core.UI.Controls;

public class Button : UiControl
{
	public Rectangle Rectangle { get; }
	public AlignPanel AlignPanel { get; }
	public Label Label { get; }

	public bool ChangeCursorOnHover { get; set; } = true;

	public Alignment TextAlignment { get => AlignPanel.Alignment; set => AlignPanel.Alignment = value; }
	public string Text { get => Label.Text; set => Label.Text = value; }

	private Color _backgroundColor;

	private Color _hoveredColor;
	// private Color _clickedColor;

	public Color BackgroundColor
	{
		get => _backgroundColor;
		set
		{
			_backgroundColor = Rectangle.Color = value;
			UpdateHoverAnimation();
		}
	}

	public Color HoveredColor
	{
		get => _hoveredColor;
		set
		{
			_hoveredColor = value;
			// UpdateClickAnimation();
			UpdateHoverAnimation();
		}
	}

	// public Color ClickedColor
	// {
	// 	get => _clickedColor;
	// 	set
	// 	{
	// 		_clickedColor = value;
	// 		UpdateClickAnimation();
	// 	}
	// }

	public Animation? HoverAnimation { get; private set; }
	// public Animation? ClickAnimation { get; private set; }

	public Button(UiContext context) : base(context)
	{
		Rectangle = new Rectangle(context);
		AddChild(Rectangle);

		Rectangle.OnHover((_, _, type) =>
		{
			if (ChangeCursorOnHover)
			{
				Vulkan.Context.Window.SetCursor(type == HoverType.Start
					? SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND
					: SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW);
			}

			if (HoveredColor == default) return;

			if (type == HoverType.Start)
				HoverAnimation?.ResetDirection();
			else
				HoverAnimation?.ReverseDirection();
			HoverAnimation?.Start();
		});

		// Rectangle.OnClick(((_, button, _, _, type, _) =>
		// {
		// 	if (button != MouseButton.Left) return false;
		//
		// 	if (type == ClickType.Start)
		// 		ClickAnimation?.ResetDirection();
		// 	else if (type == ClickType.End)
		// 		ClickAnimation?.ReverseDirection();
		// 	ClickAnimation?.Start();
		//
		// 	return false;
		// }));

		AlignPanel = new AlignPanel(context) {Alignment = Alignment.Center};
		Rectangle.AddChild(AlignPanel);

		Label = new Label(context)
		{
			Text = "Button",
			OffsetZ = 1
		};
		AlignPanel.AddChild(Label);
	}

	private void UpdateHoverAnimation()
	{
		HoverAnimation?.Stop();
		HoverAnimation = Animation.OfRGB(value => Rectangle.Color = value, BackgroundColor, HoveredColor, TimeSpan.FromMilliseconds(70));
	}

	public override void BeforeUpdate()
	{
		Label.Size = Rectangle.ComputedSize / Rectangle.CombinedScale;
		base.BeforeUpdate();
	}

	// private void UpdateClickAnimation()
	// {
	// 	ClickAnimation?.Stop();
	// 	ClickAnimation = Animation.OfRGB(value => Rectangle.Color = value, HoveredColor, ClickedColor, TimeSpan.FromMilliseconds(50));
	// }

	public override void Dispose()
	{
		base.Dispose();
		HoverAnimation?.Stop();
	}
}
