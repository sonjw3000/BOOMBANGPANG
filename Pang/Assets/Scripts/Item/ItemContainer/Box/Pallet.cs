
using System.Collections.Generic;
using System.Linq;

public class Pallet : BoxBase
{
	private Stack<BoxBase> boxes = new();

	protected override void UpdateSize()
	{
		size = stacks.Sum(s => itemDB.GetItemSize(s.ItemID) * s.Quantity);
		size += boxes.Sum(s => s.Capacity);
	}

	public override void ResetContainer()
	{
		base.ResetContainer();
		while (boxes.Count > 0)
		{
			var box = boxes.Pop();
			// We should return these nested boxes to the pool as well if they are being cleared
			if (BoxManager != null)
				BoxManager.ReturnToPool(box);
			else
				Destroy(box.gameObject);
		}
	}

	public bool AddBox(BoxBase box)
	{
		if (size + box.Capacity > Capacity) return false;

		boxes.Push(box);

		return true;
	}

	public BoxBase RemoveBox()
	{
		if (boxes.Count == 0) return null;

		return boxes.Pop();
	}
}
