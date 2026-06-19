using System.Linq;

public class CargoCapsule : BoxBase
{
	public event System.Action OnQuantityChanged;

	protected override void UpdateSize()
	{
		size = stacks.Sum(s => itemDB.GetItemSize(s.ItemID) * s.Quantity);
		OnQuantityChanged?.Invoke();
	}
}
