
using System.Collections.Generic;

public struct ZoneItemFilter
{
	public ItemTag TagFilter;
	public HashSet<ItemDefinition> ItemSet;
}

public struct ZoneWorkerFilter
{
	public readonly AIWorker Worker;
}


public readonly struct ZoneFilter
{
	public readonly ZoneItemFilter ItemFilter;
	public readonly ZoneWorkerFilter WorkerFilter;


	public bool Matches(ZoneManager zoneManager, IFacility facility)
	{
		return zoneManager.TryGetZoneAt(facility.GridPosition, out ZoneArea zone)
			&& zone != null
			&& zone.IsFilterCapable(this);
	}
}
