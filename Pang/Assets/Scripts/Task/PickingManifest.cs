using System.Collections.Generic;
using UnityEngine;

public sealed class PickingManifestLine
{
	public readonly OrderLine OrderLine;
	public readonly uint ItemId;
	public int PickedQuantity { get; private set; }
	public int PackedQuantity { get; private set; }
	public int PackableQuantity => Mathf.Max(0, PickedQuantity - PackedQuantity);

	public PickingManifestLine(OrderLine orderLine, uint itemId, int pickedQuantity, int packedQuantity = 0)
	{
		OrderLine = orderLine;
		ItemId = itemId;
		PickedQuantity = Mathf.Max(0, pickedQuantity);
		PackedQuantity = Mathf.Clamp(packedQuantity, 0, PickedQuantity);
	}

	public int AddPicked(int quantity)
	{
		int actual = Mathf.Max(0, quantity);
		PickedQuantity += actual;
		return actual;
	}

	public int RemovePicked(int quantity)
	{
		int actual = Mathf.Clamp(quantity, 0, PackableQuantity);
		PickedQuantity -= actual;
		PackedQuantity = Mathf.Min(PackedQuantity, PickedQuantity);
		return actual;
	}

	public int ReportPacked(int quantity)
	{
		int actual = Mathf.Clamp(quantity, 0, PackableQuantity);
		PackedQuantity += actual;
		return actual;
	}
}

public sealed class PickingManifest
{
	private readonly List<PickingManifestLine> lines = new();

	public IReadOnlyList<PickingManifestLine> Lines => lines;
	public bool IsEmpty => lines.Count <= 0;

	public PickingManifestLine GetOrCreateLine(OrderLine orderLine, uint itemId)
	{
		PickingManifestLine line = FindLine(orderLine, itemId);
		if (line != null)
			return line;

		line = new PickingManifestLine(orderLine, itemId, 0);
		lines.Add(line);
		return line;
	}

	public PickingManifestLine FindLine(OrderLine orderLine, uint itemId)
	{
		for (int i = 0; i < lines.Count; ++i)
		{
			PickingManifestLine line = lines[i];
			if (line != null && line.ItemId == itemId && ReferenceEquals(line.OrderLine, orderLine))
				return line;
		}

		return null;
	}

	public int AddPicked(OrderLine orderLine, uint itemId, int quantity)
	{
		if (orderLine == null || quantity <= 0)
			return 0;

		return GetOrCreateLine(orderLine, itemId).AddPicked(quantity);
	}

	public int RemovePicked(OrderLine orderLine, uint itemId, int quantity)
	{
		PickingManifestLine line = FindLine(orderLine, itemId);
		if (line == null)
			return 0;

		int removed = line.RemovePicked(quantity);
		RemoveLineIfEmpty(line);
		return removed;
	}

	public int ReportPacked(OrderLine orderLine, uint itemId, int quantity)
	{
		PickingManifestLine line = FindLine(orderLine, itemId);
		return line != null ? line.ReportPacked(quantity) : 0;
	}

	public void AddRestoredLine(OrderLine orderLine, uint itemId, int pickedQuantity, int packedQuantity)
	{
		if (orderLine == null || pickedQuantity <= 0)
			return;

		lines.Add(new PickingManifestLine(orderLine, itemId, pickedQuantity, packedQuantity));
	}

	private void RemoveLineIfEmpty(PickingManifestLine line)
	{
		if (line != null && line.PickedQuantity <= 0)
			lines.Remove(line);
	}
}
