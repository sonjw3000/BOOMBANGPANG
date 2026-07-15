
using System.Collections.Generic;
using System.Linq;

public class Pallet : BoxBase
{
	private Stack<BoxBase> boxes = new();

	protected override void UpdateSize()
	{
		size = stacks.Sum(s => itemDB.GetItemSize(s.ItemID) * s.Quantity);
		size += boxes.Sum(s => s.Capacity);
		RebuildItemTags();
	}

	public override void ResetContainer()
	{
		base.ResetContainer();
		while (boxes.Count > 0)
		{
			var box = boxes.Pop();
			if (BoxMgr != null)
				BoxMgr.DisableBox(box);
			else
				Destroy(box.gameObject);
		}
	}

	public bool AddBox(BoxBase box)
	{
		if (size + box.Capacity > Capacity) return false;

		boxes.Push(box);
		box.OnInvalidated -= HandleContainedBoxInvalidated;
		box.OnInvalidated += HandleContainedBoxInvalidated;

		return true;
	}

	public BoxBase RemoveBox()
	{
		if (boxes.Count == 0) return null;

		BoxBase box = boxes.Pop();
		if (box != null)
			box.OnInvalidated -= HandleContainedBoxInvalidated;

		return box;
	}

	private void HandleContainedBoxInvalidated(BoxBase box)
	{
		if (box == null || boxes.Contains(box) == false)
			return;

		box.OnInvalidated -= HandleContainedBoxInvalidated;
		BoxBase[] topToBottom = boxes.ToArray();
		boxes.Clear();
		for (int i = topToBottom.Length - 1; i >= 0; --i)
		{
			if (topToBottom[i] != null && topToBottom[i] != box)
				boxes.Push(topToBottom[i]);
		}

		UpdateSize();
	}
}
