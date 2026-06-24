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

		return zoneManager.TryGetZoneAt(facility.GridPosition, out ZoneArea zone)
			&& zone != null
			&& zone.IsFilterCapable(this);
	}
}
