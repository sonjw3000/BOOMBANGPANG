using System.Collections.Generic;

public sealed class ZoneItemFilter
{
	private readonly HashSet<ItemDefinition> itemSet;

	public ItemTag TagFilter { get; }
	public IReadOnlyCollection<ItemDefinition> ItemSet => itemSet;

	public ZoneItemFilter(ItemTag tagFilter, IEnumerable<ItemDefinition> itemSet = null)
	{
		TagFilter = tagFilter;
		this.itemSet = itemSet != null ? new HashSet<ItemDefinition>(itemSet) : null;
	}
}

public sealed class ZoneWorkerFilter
{
	public AIWorker Worker { get; }

	public ZoneWorkerFilter(AIWorker worker)
	{
		Worker = worker;
	}
}

public readonly struct ZoneFilter
{
	public static ZoneFilter None => default;

	public ZoneItemFilter ItemFilter { get; }
	public ZoneWorkerFilter WorkerFilter { get; }

	public ZoneFilter(ZoneItemFilter itemFilter = null, ZoneWorkerFilter workerFilter = null)
	{
		ItemFilter = itemFilter;
		WorkerFilter = workerFilter;
	}

	public bool Matches(ZoneManager zoneManager, IFacility facility)
	{
		if (zoneManager == null || facility == null)
			return false;

		if (zoneManager.TryGetZoneAt(facility.GridPosition, out ZoneArea zone) == false || zone == null)
			return true;

		return zone.IsFilterCapable(this);
	}

	public static ZoneFilter ForWorker(AIWorker worker)
	{
		return worker == null
			? None
			: new ZoneFilter(workerFilter: new ZoneWorkerFilter(worker));
	}

	public static ZoneFilter ForContainer(IItemContainer container, AIWorker worker = null)
	{
		ZoneItemFilter itemFilter = null;
		if (TryBuildItemFilter(container, out ZoneItemFilter builtFilter))
			itemFilter = builtFilter;

		return new ZoneFilter(
			itemFilter,
			worker != null ? new ZoneWorkerFilter(worker) : null);
	}

	private static bool TryBuildItemFilter(IItemContainer container, out ZoneItemFilter itemFilter)
	{
		itemFilter = null;
		if (container == null || GameContext.HasInstance == false || GameContext.Instance.ItemDB == null)
			return false;

		HashSet<ItemDefinition> itemSet = null;
		bool hasItems = false;
		foreach (var itemTotal in container.ItemTotals)
		{
			if (itemTotal.Value <= 0)
				continue;

			hasItems = true;
			if (GameContext.Instance.ItemDB.GetItemData(itemTotal.Key, out ItemDefinition itemDefinition) == false || itemDefinition == null)
				continue;

			itemSet ??= new HashSet<ItemDefinition>();
			itemSet.Add(itemDefinition);
		}

		if (hasItems == false)
			return false;

		itemFilter = new ZoneItemFilter(container.ItemTags, itemSet);
		return true;
	}
}
