using UnityEngine;

public class ZoneSelectionProxy : MonoBehaviour
{
	private ZoneManager zoneManager;
	private ZoneArea zone;

	public ZoneManager ZoneManager => zoneManager;
	public ZoneArea Zone => zone;

	public void Bind(ZoneManager manager, ZoneArea targetZone)
	{
		zoneManager = manager;
		zone = targetZone;
		name = targetZone != null ? $"ZoneSelection_{targetZone.DisplayName}" : "ZoneSelection";
	}

	public bool IsBoundTo(ZoneArea targetZone) => zone == targetZone;
}
