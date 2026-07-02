using System.Collections.Generic;
using System.Linq;

public sealed class ItemContainerItemDisplayInfo
{
	public string ItemName { get; set; }
	public int Quantity { get; set; }
	public byte Freshness { get; set; }
	public byte Damage { get; set; }
}

public sealed class ItemContainerDisplayInfo
{
	public string ContainerName { get; set; }
	public bool HasContainer { get; set; }
	public IReadOnlyList<ItemContainerItemDisplayInfo> Items { get; set; }
	public IReadOnlyList<ManifestContainerItemDisplayInfo> ManifestItems { get; set; }
}

public sealed class ManifestContainerItemDisplayInfo
{
	public string OrderId { get; set; }
	public string ItemName { get; set; }
	public string InBoxQuantity { get; set; }
	public string OrderProgress { get; set; }
	public int WeeksLeft { get; set; }
}

public static class ItemContainerDisplayUtility
{
	public static IReadOnlyList<ItemContainerItemDisplayInfo> BuildItemRows(IItemContainer container)
	{
		if (container?.Stacks == null)
			return new List<ItemContainerItemDisplayInfo>();

		return container.Stacks
			.Where(stack => stack != null)
			.Select(stack => new ItemContainerItemDisplayInfo
			{
				ItemName = ResolveItemName(stack.ItemID),
				Quantity = stack.Quantity,
				Freshness = stack.Freshness,
				Damage = stack.Damage,
			})
			.ToList();
	}

	public static IReadOnlyList<ManifestContainerItemDisplayInfo> BuildManifestRows(BoxBase box)
	{
		List<ManifestContainerItemDisplayInfo> rows = new();
		if (box == null ||
			GameContext.HasInstance == false ||
			GameContext.Instance.OBWorkflowSvc == null ||
			GameContext.Instance.OBWorkflowSvc.TryGetPickingManifest(box, out PickingManifest manifest) == false ||
			manifest?.Lines == null)
		{
			return rows;
		}

		int currentWeek = GameContext.Instance.GameTime != null ? GameContext.Instance.GameTime.WeeksPassed : 0;
		IReadOnlyList<PickingManifestLine> lines = manifest.Lines;
		for (int i = 0; i < lines.Count; ++i)
		{
			PickingManifestLine line = lines[i];
			OrderLine orderLine = line?.OrderLine;
			if (line == null || orderLine == null)
				continue;

			int pickedWaiting = System.Math.Max(0, orderLine.PickingCompletedQuantity - orderLine.PackagingCompletedQuantity);
			int packed = orderLine.PackagingCompletedQuantity;
			rows.Add(new ManifestContainerItemDisplayInfo
			{
				OrderId = $"#{orderLine.ParentOrder?.OrderID ?? 0}",
				ItemName = ResolveItemName(line.ItemId),
				InBoxQuantity = line.PackableQuantity > 0
					? $"{line.PackableQuantity} picked"
					: $"{line.PackedQuantity} packed",
				OrderProgress = $"Pick {pickedWaiting} / Pack {packed} / {orderLine.Quantity}",
				WeeksLeft = System.Math.Max(0, orderLine.DueWeek - currentWeek),
			});
		}

		return rows;
	}

	private static string ResolveItemName(uint itemId)
	{
		if (GameContext.HasInstance == false || GameContext.Instance.ItemDB == null)
			return $"Item {itemId}";

		return GameContext.Instance.ItemDB.GetItemData(itemId, out ItemDefinition definition) && definition != null
			? definition.name
			: $"Item {itemId}";
	}
}
