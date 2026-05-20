using System.Collections.Generic;
using System.Linq;

public sealed class ItemContainerItemDisplayInfo
{
	public string ItemName { get; set; }
	public int Quantity { get; set; }
	public int? RelatedOrderId { get; set; }
}

public sealed class ItemContainerDisplayInfo
{
	public string ContainerName { get; set; }
	public bool HasContainer { get; set; }
	public IReadOnlyList<ItemContainerItemDisplayInfo> Items { get; set; }
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
				RelatedOrderId = ResolveOrderId(stack),
			})
			.ToList();
	}

	private static int? ResolveOrderId(ItemStack stack)
	{
		if (stack is ItemPackage package && package.RelatedOrderLine?.ParentOrder != null)
			return package.RelatedOrderLine.ParentOrder.OrderID;

		return null;
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
