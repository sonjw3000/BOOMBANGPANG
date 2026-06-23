

public readonly struct ZoneFilter
{
	private readonly bool hasRequiredZoneType;
	private readonly ZoneType requiredZoneType;

	public static ZoneFilter None => default;
	public bool HasRequiredZoneType => hasRequiredZoneType;
	public ZoneType RequiredZoneType => requiredZoneType;

	private ZoneFilter(bool hasRequiredZoneType, ZoneType requiredZoneType)
	{
		this.hasRequiredZoneType = hasRequiredZoneType;
		this.requiredZoneType = requiredZoneType;
	}

	public static ZoneFilter Require(ZoneType zoneType)
	{
		return new ZoneFilter(true, zoneType);
	}

	public bool Matches(ZoneManager zoneManager, IFacility facility)
	{
		if (hasRequiredZoneType == false)
			return true;

		if (zoneManager == null || facility == null)
			return false;

		return zoneManager.TryGetZoneAt(facility.GridPosition, out ZoneArea zone)
			&& zone != null
			&& zone.Type == requiredZoneType;
	}
}
