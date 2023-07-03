using Core.UI.Controls;

namespace Core.UI.Reactive;

public unsafe class ReactiveCustomBox<TVertData, TFragData> : CustomBox
	where TVertData : unmanaged
	where TFragData : unmanaged
{
	public readonly UnmanagedSignal<TVertData> VertexDataSignal;
	public readonly UnmanagedSignal<TFragData> FragmentDataSignal;

	public TVertData VertexData { get => VertexDataSignal.Get(); set => VertexDataSignal.Set(value); }
	public TFragData FragmentData { get => FragmentDataSignal.Get(); set => FragmentDataSignal.Set(value); }

	public ReactiveCustomBox(UiContext context, MaterialDataFactory vertFactory, MaterialDataFactory fragFactory) : base(context)
	{
		UseSubContext();

		VertMaterial = vertFactory.Create();
		VertexDataSignal = new UnmanagedSignal<TVertData>(VertMaterial.GetMemPtr<TVertData>());
		Context.CreateEffect(() => VertMaterial.MarkForGPUUpdate(), VertexDataSignal);

		FragMaterial = fragFactory.Create();
		FragmentDataSignal = new UnmanagedSignal<TFragData>(FragMaterial.GetMemPtr<TFragData>());
		Context.CreateEffect(() => FragMaterial.MarkForGPUUpdate(), FragmentDataSignal);
	}

	public override void Dispose()
	{
		Context.Dispose();
		base.Dispose();
	}
}

public struct Empty { }
