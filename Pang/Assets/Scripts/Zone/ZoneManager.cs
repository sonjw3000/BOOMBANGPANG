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

public static class WorkerSpawnZoneType
{
	public static bool IsWorkerSpawnZone(this ZoneType zoneType)
	{
		return zoneType == ZoneType.HumanSpawn || zoneType == ZoneType.RobotSpawn;
	}

	public static ZoneType ToSpawnZoneType(this WorkerType workerType)
	{
		return workerType == WorkerType.Robot ? ZoneType.RobotSpawn : ZoneType.HumanSpawn;
	}
}

public class ZoneManager : MonoBehaviour
{
	[SerializeField] private List<ZoneArea> registeredZones = new();

	private readonly Dictionary<int, Dictionary<ZoneType, List<ZoneArea>>> zones = new();

	public IReadOnlyList<ZoneArea> RegisteredZones => registeredZones;

	private void Awake()
	{
		RebuildZoneLookup();
	}

#if UNITY_EDITOR
	private void OnValidate()
	{
		RebuildZoneLookup();
	}
#endif

	private List<ZoneArea> TargetZoneList(ZoneArea zone) => CheckHavingZone(zone) ? zones[zone.Floor][zone.Type] : null;

	private bool CheckHavingZone(ZoneArea zone)
	{
		if (zone == null ||
			zones.ContainsKey(zone.Floor) == false ||
			zones[zone.Floor].ContainsKey(zone.Type) == false ||
			zones[zone.Floor][zone.Type].Contains(zone) == false)
		{
			Debug.Log("No zone!!");
			return false;
		}

		return true;
	}

	public void RebuildZoneLookup()
	{
		zones.Clear();

		foreach (var zone in registeredZones)
		{
			RegisterZone(zone);
		}
	}

	private void RegisterZone(ZoneArea zone)
	{
		if (zone == null)
			return;

		if (zones.ContainsKey(zone.Floor) == false)
			zones[zone.Floor] = new();

		if (zones[zone.Floor].ContainsKey(zone.Type) == false)
			zones[zone.Floor][zone.Type] = new();

		if (zones[zone.Floor][zone.Type].Contains(zone) == false)
			zones[zone.Floor][zone.Type].Add(zone);
	}

	private bool HasOverlap(int floor, in RectInt bound, ZoneArea ignore = null)
	{
		if (zones.ContainsKey(floor) == false)
			return false;

		foreach (var list in zones[floor].Values)
		{
			foreach (var other in list)
			{
				if (other == ignore)
					continue;

				if (bound.Overlaps(other.Bounds))
					return true;
			}
		}

		return false;
	}

	public ZoneArea AddZone(string name, ZoneType type, in RectInt bound, int floor)
	{
		if (HasOverlap(floor, bound))
		{
			Debug.Log($"Zone {name} is overlapped by another zone");
			return null;
		}

		ZoneArea res = new(name, type, bound, floor);
		registeredZones.Add(res);
		RegisterZone(res);

		return res;
	}

	public bool RemoveZone(ZoneArea zone)
	{
		var targetZones = TargetZoneList(zone);

		if (targetZones == null)
			return false;

		targetZones.Remove(zone);
		registeredZones.Remove(zone);
		return true;
	}

	public bool ResizeZone(ZoneArea zone, in RectInt newBound)
	{
		if (CheckHavingZone(zone) == false)
			return false;

		if (HasOverlap(zone.Floor, newBound, zone))
		{
			Debug.Log("Zone Resize Failed!, Out of bound!");
			return false;
		}

		zone.Resize(newBound);
		return true;
	}

	public bool TryGetZones(out IReadOnlyList<ZoneArea> result, int floor, ZoneType zoneType)
	{
		result = null;

		if (zones.ContainsKey(floor) == false || zones[floor].ContainsKey(zoneType) == false)
			return false;

		result = zones[floor][zoneType];
		return result.Count > 0;
	}

	public bool TryGetAvailableZone(out ZoneArea result, int floor, ZoneType zoneType, Predicate<ZoneArea> pred = null)
	{
		result = null;

		if (TryGetZones(out var targetZones, floor, zoneType) == false)
			return false;

		foreach (var zone in targetZones)
		{
			if (pred == null || pred(zone))
			{
				result = zone;
				return true;
			}
		}

		return false;
	}
}
