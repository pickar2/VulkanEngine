using System.Numerics;

namespace Core.UI.Controls.Panels;

public abstract class RootPanel : AbsolutePanel
{
	public Matrix4x4 ViewModel { get; set; }
	public Vector<float> CursorPos { get; set; }
}
