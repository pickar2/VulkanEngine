namespace Core.Registries.Collections.UnsafeLinkedListAPI;

public partial class UnsafeLinkedList<T>
{
	public sealed class Node
	{
		internal Node? Next;
		internal Node? Previous;
		public T Value;

		public Node(T value) => Value = value;

		public bool IsExists => Next is not null || Previous is not null;
	}
}
