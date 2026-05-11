using System;
using System.Collections.Generic;
using UnityEngine;

public enum ZoneType
{
	HumanSpawn,
	RobotSpawn,

	Resting,
	Charge,

	RocketLanding,
	Storage,
}

public class ZoneManager : MonoBehaviour
{
	private readonly Dictionary<int, Dictionary<ZoneType, List<ZoneArea>>> zones = new();

	private List<ZoneArea> TargetZoneList(ZoneArea zone) => CheckHavingZone(zone) ? null : zones[zone.Floor][zone.Type];

	private bool CheckHavingZone(ZoneArea zone)
	{
		if (zones.ContainsKey(zone.Floor) == false || 
			zones[zone.Floor].ContainsKey(zone.Type) == false ||
			zones[zone.Floor][zone.Type].Contains(zone) == false)
		{
			Debug.Log("No zone!!");
			return false;
		}

		return true;
	}

	public ZoneArea AddZone(string name, ZoneType type, in RectInt bound, int floor)
	{
		if (zones.ContainsKey(floor) == false)
			zones[floor] = new();

		if (zones[floor].ContainsKey(type) == false)
			zones[floor][type] = new();

		var list = zones[floor][type];

		// check for intersect
		foreach (var other in list)
		{
			if (bound.Overlaps(other.Bounds))
			{
				Debug.Log($"Zone{name} is Overlapped by other{other.DisplayName}");
				return null;
			}
		}

		ZoneArea res = new(name, type, bound, floor);
		list.Add(res);

		return res;
	}

	public bool RemoveZone(ZoneArea zone)
	{
		var zones = TargetZoneList(zone);

		if (zones == null)
			return false;
		
		zones.Remove(zone);
		return true;
	}

	public bool ResizeZone(ZoneArea zone, in RectInt newBound)
	{
		if (CheckHavingZone(zone) == false)
			return false;

		foreach (var lists in zones[zone.Floor])
		{
			foreach (var otherZone in lists.Value)
			{
				if (otherZone == zone) continue;

				if (newBound.Overlaps(otherZone.Bounds))
				{
					Debug.Log("Zone Resize Failed!, Out of bound!");
					return false;
				}
			}
		}

		zone.Resize(newBound);
		return true;
	}

	public bool TryGetAvailableZone(out ZoneArea result, int floor, ZoneType zoneType, Predicate<ZoneArea> pred = null)
	{
		result = null;

		if (zones.ContainsKey(floor) == false || zones[floor].ContainsKey(zoneType) == false) 
			return false;

		foreach (var zone in zones[floor][zoneType])
		{
			if (pred == null || pred(zone) == true)
			{
				result = zone;
				return true;
			}
		}
		
		return false;
	}

}
