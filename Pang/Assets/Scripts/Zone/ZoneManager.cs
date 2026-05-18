using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

public enum ZoneType
{
	HumanSpawn,
	RobotSpawn,

	Resting,
	Charge,
	StorageStandby,
	InboundStandby,
	OutboundStandby,

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
	[SerializedDictionary("ZoneType", "OverlayColor")]
	[SerializeField] private SerializedDictionary<ZoneType, Color> zoneColors = new();

	private readonly Dictionary<int, Dictionary<ZoneType, List<ZoneArea>>> zones = new();

	public IReadOnlyList<ZoneArea> RegisteredZones => registeredZones;

	public event Action<ZoneArea> OnZoneAdded;
	public event Action<ZoneArea> OnZoneRemoved;
	public event Action<ZoneArea> OnZoneChanged;
	public event Action OnZonesRebuilt;

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

		OnZonesRebuilt?.Invoke();
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

	public bool CanPlaceZone(int floor, in RectInt bound, ZoneArea ignore = null)
	{
		if (bound.width <= 0 || bound.height <= 0)
			return false;

		return HasOverlap(floor, bound, ignore) == false;
	}

	public ZoneArea AddZone(string name, ZoneType type, in RectInt bound, int floor)
	{
		if (CanPlaceZone(floor, bound) == false)
		{
			Debug.Log($"Zone {name} is overlapped by another zone");
			return null;
		}

		ZoneArea res = new(name, type, bound, floor);
		registeredZones.Add(res);
		RegisterZone(res);
		OnZoneAdded?.Invoke(res);

		return res;
	}

	public ZoneArea AddZone(ZoneType type, in RectInt bound, int floor)
	{
		return AddZone(BuildDefaultZoneName(type), type, bound, floor);
	}

	public bool RemoveZone(ZoneArea zone)
	{
		var targetZones = TargetZoneList(zone);

		if (targetZones == null)
			return false;

		targetZones.Remove(zone);
		registeredZones.Remove(zone);
		OnZoneRemoved?.Invoke(zone);
		return true;
	}

	public bool ResizeZone(ZoneArea zone, in RectInt newBound)
	{
		if (CheckHavingZone(zone) == false)
			return false;

		if (CanPlaceZone(zone.Floor, newBound, zone) == false)
		{
			Debug.Log("Zone Resize Failed!, Out of bound!");
			return false;
		}

		zone.Resize(newBound);
		OnZoneChanged?.Invoke(zone);
		return true;
	}

	public bool RenameZone(ZoneArea zone, string newName)
	{
		if (CheckHavingZone(zone) == false)
			return false;

		zone.Rename(newName);
		OnZoneChanged?.Invoke(zone);
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

	public bool TryGetZoneAt(in Unity.Mathematics.int3 pos, out ZoneArea result)
	{
		result = null;

		if (zones.ContainsKey(pos.y) == false)
			return false;

		foreach (var list in zones[pos.y].Values)
		{
			foreach (var zone in list)
			{
				if (zone.Contains(pos))
				{
					result = zone;
					return true;
				}
			}
		}

		return false;
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

	public Color GetZoneColor(ZoneType zoneType)
	{
		if (zoneColors.TryGetValue(zoneType, out var color))
			return color;

		return new Color(1f, 1f, 1f, 0.2f);
	}

	private string BuildDefaultZoneName(ZoneType zoneType)
	{
		string baseName = zoneType.ToString();
		int suffix = 1;
		string candidate = baseName;

		while (registeredZones.Exists(zone => zone != null && zone.DisplayName == candidate))
		{
			suffix += 1;
			candidate = $"{baseName} {suffix}";
		}

		return candidate;
	}

	public ZoneManagerSaveData CaptureState()
	{
		ZoneManagerSaveData data = new();
		foreach (var zone in registeredZones)
		{
			if (zone == null)
				continue;

			data.Zones.Add(new ZoneSaveData
			{
				Name = zone.DisplayName,
				Type = zone.Type,
				Floor = zone.Floor,
				Bounds = new RectIntSaveData(zone.Bounds.x, zone.Bounds.y, zone.Bounds.width, zone.Bounds.height),
			});
		}

		return data;
	}

	public void RestoreState(ZoneManagerSaveData data)
	{
		ResetRuntimeState();
		if (data == null)
			return;

		foreach (var zoneData in data.Zones)
		{
			AddZone(zoneData.Name, zoneData.Type, new RectInt(zoneData.Bounds.X, zoneData.Bounds.Y, zoneData.Bounds.Width, zoneData.Bounds.Height), zoneData.Floor);
		}
	}

	public void ResetRuntimeState()
	{
		registeredZones.Clear();
		RebuildZoneLookup();
	}
}
