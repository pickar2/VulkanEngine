using System;
using System.Collections.Generic;
using System.Drawing;
using Core.UI.Controls.Panels;
using Core.UI.Reactive;
using Core.Window;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class ComboBox<T> : UiControl
{
	public required Dictionary<string, T> Values { get; init; } = new();
	public Signal<(string Name, T Value)> CurrentSignal { get; protected set; } = new((string.Empty, default!));
	public (string Name, T Value) Current { get => CurrentSignal.Get(); set => CurrentSignal.Set(value); }

	private float _initialHeight;
	private float _valueHeight;
	private float _maxHeight;

	public ComboBox(UiContext context, float valueHeight = 25, float maxHeight = float.PositiveInfinity) : base(context)
	{
		UseSubContext();
		_valueHeight = valueHeight;
		_maxHeight = maxHeight;
	}

	public void Draw()
	{
		_initialHeight = Size.Y;

		var border = new BorderBox(Context, Color.Red700, 2) {OffsetZ = 10};
		AddChild(border);

		var bg = new Rectangle(Context) {Color = Color.Amber800};
		AddChild(bg);

		var stack = new StackPanel(Context)
		{
			Orientation = Orientation.Vertical,
			OffsetZ = 1,
			Overflow = Overflow.Shown,
			TightBox = true
		};
		AddChild(stack);

		var align = new AlignPanel(Context) {Alignment = Alignment.Center};
		align.Size.Y = Size.Y;
		stack.AddChild(align);

		var label = new Label(Context) {OffsetZ = 1};
		align.AddChild(label);

		float headerSpacing = 2;
		var spacer = new Rectangle(Context) {Color = Color.Neutral950};
		spacer.Size.Y = headerSpacing;
		stack.AddChild(spacer);

		Context.CreateEffect(() => label.Text = Current.Name);

		float valueSpacing = 3;
		var valuesContainer = new StackPanel(Context)
		{
			Orientation = Orientation.Vertical,
			Spacing = valueSpacing,
			Size = (Size.X, 10000),
			TightBox = true,
			OffsetZ = 1
		};

		ScrollView? scrollView = null;
		if (float.IsPositiveInfinity(_maxHeight))
		{
			stack.AddChild(valuesContainer);
		}
		else
		{
			scrollView = new ScrollView(Context)
			{
				Size = (Size.X, _maxHeight),
				OffsetZ = 1,
			};
			stack.AddChild(scrollView);
			scrollView.AddChild(valuesContainer);
		}

		bool open = false;

		void Close()
		{
			// ReSharper disable once AccessToModifiedClosure
			if (!open) return;
			if (scrollView is not null) scrollView.ScrollOffset = new Vector2<float>(0f, 0f);
			open = false;
			foreach (var child in valuesContainer.Children) child.Dispose();
			valuesContainer.ClearChildren();
			Size.Y = _initialHeight;
		}

		align.OnClick(((_, button, _, _, type) =>
		{
			if (button != MouseButton.Left) return false;
			if (type != ClickType.End) return false;

			if (!open)
			{
				this.OnClickOutsideOnce(((_, _) => Close()));
				open = true;
				Size.Y = float.IsPositiveInfinity(_maxHeight) ? Values.Count * (valueSpacing + _valueHeight) : _maxHeight;
				Size.Y += _initialHeight + headerSpacing;

				foreach ((string? name, var value) in Values)
				{
					var box = new Rectangle(Context);
					box.Size.Y = _valueHeight;
					box.Color = Color.Amber500;
					box.OnHover(((_, _, hoverType) => box.Color = hoverType == HoverType.Start ? Color.Yellow900 : Color.Amber500));
					valuesContainer.AddChild(box);

					var valueAlign = new AlignPanel(Context) {Alignment = Alignment.Center};
					box.AddChild(valueAlign);

					var valueLabel = new Label(Context) {Text = name, OffsetZ = 1};
					valueAlign.AddChild(valueLabel);

					box.OnClick(((_, _, _, _, _) =>
					{
						if (button != MouseButton.Left) return false;
						if (type != ClickType.End) return false;

						Close();

						Current = (Name: name, Value: value);

						return true;
					}));
				}
			}
			else
			{
				Close();
			}

			return true;
		}));
	}
}
