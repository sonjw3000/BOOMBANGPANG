
using System.Collections.Generic;
using UnityEngine;

public class InboundLine
{
	public CargoPort CargoPort;
	public uint ItemID;
	public int Quantity;
}

public class InboundRequestService : MonoBehaviour, ICollectRequestSource<InboundLine>
{
	private readonly List<InboundLine> inboundRequests = new();
	private readonly Dictionary<uint, List<InboundLine>> itemPerReqLine = new();

	public void OnPortItemPresentChanged(ShelfBase port, uint itemId, bool present)
	{
		if (present)
		{
			OnPortItemAdded(port, itemId);
		}
		else
		{
			OnPortItemRemoved(port, itemId);
		}
	}

	public void OnPortItemAdded(ShelfBase port, uint itemId)
	{
		SyncLine(port as CargoPort, itemId);
	}

	public void OnPortItemRemoved(ShelfBase port, uint itemId)
	{
		RemoveLine(port as CargoPort, itemId);
	}

	public void OnPortItemQuantityChanged(ShelfBase port, uint itemId, int quantityDelta)
	{
		SyncLine(port as CargoPort, itemId);
	}

	public void OnPortItemReservedChanged(ShelfBase port, uint itemId, int reservedQuantityDelta)
	{
		SyncLine(port as CargoPort, itemId);
	}

	public IEnumerable<uint> GetRequestedItemIds()
	{
		foreach (var kvp in itemPerReqLine)
		{
			if (kvp.Value != null && kvp.Value.Count > 0)
				yield return kvp.Key;
		}
	}

	public IEnumerable<InboundLine> GetRequestLines(uint itemId)
	{
		if (itemPerReqLine.TryGetValue(itemId, out var lines) == false)
			yield break;

		for (int i = 0; i < lines.Count; ++i)
		{
			InboundLine line = lines[i];
			if (line != null && line.Quantity > 0)
				yield return line;
		}
	}

	public float GetOutstandingTotalSize(ItemDatabase itemDatabase)
	{
		if (itemDatabase == null)
			return 0.0f;

		float totalSize = 0.0f;
		foreach (InboundLine line in inboundRequests)
		{
			if (line == null || line.Quantity <= 0)
				continue;

			totalSize += itemDatabase.GetItemSize(line.ItemID) * line.Quantity;
		}

		return totalSize;
	}

	public int GetAllocatableQuantity(InboundLine requestLine) => requestLine != null ? Mathf.Max(0, requestLine.Quantity) : 0;

	public int Allocate(InboundLine requestLine, int quantity)
	{
		if (requestLine == null || quantity <= 0)
			return 0;

		return Mathf.Clamp(quantity, 0, requestLine.Quantity);
	}

	public WorkLine CreateWorkLine(ShelfBase source, uint itemId, int quantity, InboundLine requestLine)
	{
		return source == null || quantity <= 0 ? null : new WorkLine(source, itemId, quantity);
	}

	private void SyncLine(CargoPort port, uint itemId)
	{
		if (port == null)
			return;

		int pickable = Mathf.Max(0, port.GetPickableQuantity(itemId));
		InboundLine line = FindLine(port, itemId);
		if (pickable <= 0)
		{
			RemoveLine(port, itemId);
			return;
		}

		if (line == null)
		{
			line = new InboundLine
			{
				CargoPort = port,
				ItemID = itemId,
				Quantity = pickable,
			};
			inboundRequests.Add(line);

			if (itemPerReqLine.TryGetValue(itemId, out var lines) == false)
			{
				lines = new List<InboundLine>();
				itemPerReqLine[itemId] = lines;
			}

			lines.Add(line);
			return;
		}

		line.Quantity = pickable;
	}

	private void RemoveLine(CargoPort port, uint itemId)
	{
		if (port == null)
			return;

		InboundLine line = FindLine(port, itemId);
		if (line == null)
			return;

		inboundRequests.Remove(line);
		if (itemPerReqLine.TryGetValue(itemId, out var lines))
		{
			lines.Remove(line);
			if (lines.Count <= 0)
				itemPerReqLine.Remove(itemId);
		}
	}

	private InboundLine FindLine(CargoPort port, uint itemId)
	{
		if (port == null || itemPerReqLine.TryGetValue(itemId, out var lines) == false)
			return null;

		for (int i = 0; i < lines.Count; ++i)
		{
			InboundLine line = lines[i];
			if (line != null && line.CargoPort == port)
				return line;
		}

		return null;
	}

	public void ResetRuntimeState()
	{
		inboundRequests.Clear();
		itemPerReqLine.Clear();
	}
}
