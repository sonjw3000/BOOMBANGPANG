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
	private readonly Dictionary<uint, List<ZoneArea>> zonesByBuildingId = new();
	private static readonly IReadOnlyList<ZoneArea> EmptyZones = Array.Empty<ZoneArea>();

	public IReadOnlyList<ZoneArea> RegisteredZones => registeredZones;

	private BuildingManager BuildingManager
	{
		get
		{
			if (GameContext.HasInstance)
				return GameContext.Instance.BuildingMgr;

			return FindFirstObjectByType<BuildingManager>();
		}
	}

	private BuildingFootprintService BuildingFootprintService
	{
		get
		{
			if (GameContext.HasInstance)
				return GameContext.Instance.BuildingFootprintService;

			return FindFirstObjectByType<BuildingFootprintService>();
		}
	}

	private GridService GridService
	{
		get
		{
			if (GameContext.HasInstance)
				return GameContext.Instance.GridService;

			return FindFirstObjectByType<GridService>();
		}
	}

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
		zonesByBuildingId.Clear();

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

		if (zone.RuntimeBuildingId != 0)
		{
			if (zonesByBuildingId.TryGetValue(zone.RuntimeBuildingId, out List<ZoneArea> buildingZones) == false)
			{
				buildingZones = new List<ZoneArea>();
				zonesByBuildingId[zone.RuntimeBuildingId] = buildingZones;
			}

			if (buildingZones.Contains(zone) == false)
				buildingZones.Add(zone);
		}
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

	public bool CanPlaceZone(Building ownerBuilding, int floor, in RectInt bound, ZoneArea ignore = null)
	{
		if (ownerBuilding == null || ownerBuilding.RuntimeBuildingId == 0)
			return false;

		if (bound.width <= 0 || bound.height <= 0)
			return false;

		if (BuildingFootprintService == null || BuildingFootprintService.TryGetInteriorBounds(ownerBuilding.RuntimeBuildingId, out RectInt interiorBounds, out int buildingFloor) == false)
			return false;

		if (buildingFloor != floor || ContainsRect(interiorBounds, bound) == false)
			return false;

		if (GridService == null)
			return false;

		for (int z = bound.yMin; z < bound.yMax; ++z)
		{
			for (int x = bound.xMin; x < bound.xMax; ++x)
			{
				GridCell cell = GridService.GetCell(x, floor, z);
				if (cell == null || cell.BuildingId != ownerBuilding.RuntimeBuildingId)
					return false;
			}
		}

		return HasOverlap(floor, bound, ignore) == false;
	}

	public ZoneArea AddZone(Building ownerBuilding, string name, ZoneType type, in RectInt bound, int floor)
	{
		if (CanPlaceZone(ownerBuilding, floor, bound) == false)
		{
			Debug.Log($"Zone {name} is overlapped by another zone");
			return null;
		}

		ZoneArea res = new(name, type, bound, floor, ownerBuilding.RuntimeBuildingId);
		registeredZones.Add(res);
		RegisterZone(res);
		OnZoneAdded?.Invoke(res);

		return res;
	}

	public ZoneArea AddZone(Building ownerBuilding, ZoneType type, in RectInt bound, int floor)
	{
		return AddZone(ownerBuilding, BuildDefaultZoneName(type), type, bound, floor);
	}

	public bool RemoveZone(ZoneArea zone)
	{
		var targetZones = TargetZoneList(zone);

		if (targetZones == null)
			return false;

		targetZones.Remove(zone);
		registeredZones.Remove(zone);
		if (zone.RuntimeBuildingId != 0 && zonesByBuildingId.TryGetValue(zone.RuntimeBuildingId, out List<ZoneArea> buildingZones))
		{
			buildingZones.Remove(zone);
			if (buildingZones.Count <= 0)
				zonesByBuildingId.Remove(zone.RuntimeBuildingId);
		}

		OnZoneRemoved?.Invoke(zone);
		return true;
	}

	public bool ResizeZone(ZoneArea zone, in RectInt newBound)
	{
		if (CheckHavingZone(zone) == false)
			return false;

		if (BuildingManager == null || BuildingManager.TryGetBuilding(zone.RuntimeBuildingId, out Building ownerBuilding) == false)
			return false;

		if (CanPlaceZone(ownerBuilding, zone.Floor, newBound, zone) == false)
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

	public IReadOnlyList<ZoneArea> GetZonesForBuilding(uint runtimeBuildingId)
	{
		if (runtimeBuildingId == 0)
			return EmptyZones;

		if (zonesByBuildingId.TryGetValue(runtimeBuildingId, out List<ZoneArea> buildingZones) == false)
			return EmptyZones;

		return buildingZones;
	}

	public bool TryGetZonesForBuilding(uint runtimeBuildingId, out IReadOnlyList<ZoneArea> result)
	{
		result = GetZonesForBuilding(runtimeBuildingId);
		return result.Count > 0;
	}

	public int GetZoneCountForBuilding(uint runtimeBuildingId)
	{
		return GetZonesForBuilding(runtimeBuildingId).Count;
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
				RuntimeBuildingId = zone.RuntimeBuildingId,
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
			if (BuildingManager == null || BuildingManager.TryGetBuilding(zoneData.RuntimeBuildingId, out Building ownerBuilding) == false)
			{
				Debug.LogWarning($"[Save] Skipping zone restore {zoneData.Name}: missing building {zoneData.RuntimeBuildingId}.");
				continue;
			}

			AddZone(ownerBuilding, zoneData.Name, zoneData.Type, new RectInt(zoneData.Bounds.X, zoneData.Bounds.Y, zoneData.Bounds.Width, zoneData.Bounds.Height), zoneData.Floor);
		}
	}

	public void ResetRuntimeState()
	{
		registeredZones.Clear();
		RebuildZoneLookup();
	}

	private static bool ContainsRect(in RectInt outer, in RectInt inner)
	{
		return inner.xMin >= outer.xMin
			&& inner.yMin >= outer.yMin
			&& inner.xMax <= outer.xMax
			&& inner.yMax <= outer.yMax;
	}
}
