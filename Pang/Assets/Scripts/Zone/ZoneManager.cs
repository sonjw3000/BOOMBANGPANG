using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using Unity.Mathematics;
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

	public static ZoneType ToSpawnZoneType(this WorkerKind workerKind)
	{
		return workerKind == WorkerKind.Robot ? ZoneType.RobotSpawn : ZoneType.HumanSpawn;
	}
}

public partial class ZoneManager : MonoBehaviour
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
			PopulateFacilitiesForZone(zone);
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

		if (BuildingFootprintService == null ||
			BuildingFootprintService.TryGetFootprint(ownerBuilding.RuntimeBuildingId, out BuildingFootprintRecord footprint) == false ||
			footprint == null)
			return false;

		if (footprint.Floor != floor || ContainsRect(footprint.Bounds, bound) == false)
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

	public bool CanPlaceGlobalZone(ZoneType type, int floor, in RectInt bound, ZoneArea ignore = null)
	{
		if (type != ZoneType.RocketLanding)
			return false;

		if (bound.width <= 0 || bound.height <= 0 || GridService == null)
			return false;

		for (int z = bound.yMin; z < bound.yMax; ++z)
		{
			for (int x = bound.xMin; x < bound.xMax; ++x)
			{
				GridCell cell = GridService.GetCell(x, floor, z);
				if (cell == null || cell.BuildingId != 0)
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
		PopulateFacilitiesForZone(res);
		OnZoneAdded?.Invoke(res);

		return res;
	}

	public ZoneArea AddZone(Building ownerBuilding, ZoneType type, in RectInt bound, int floor)
	{
		return AddZone(ownerBuilding, BuildDefaultZoneName(type), type, bound, floor);
	}

	public ZoneArea AddGlobalZone(string name, ZoneType type, in RectInt bound, int floor)
	{
		if (CanPlaceGlobalZone(type, floor, bound) == false)
		{
			Debug.Log($"Global zone {name} is overlapped or invalid.");
			return null;
		}

		ZoneArea res = new(name, type, bound, floor, 0);
		registeredZones.Add(res);
		RegisterZone(res);
		PopulateFacilitiesForZone(res);
		OnZoneAdded?.Invoke(res);
		return res;
	}

	public ZoneArea AddGlobalZone(ZoneType type, in RectInt bound, int floor)
	{
		return AddGlobalZone(BuildDefaultZoneName(type), type, bound, floor);
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
		zone.ClearFacilities();

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
		PopulateFacilitiesForZone(zone);
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

	public bool SetZoneRule(ZoneArea zone, ZoneRule rule)
	{
		if (CheckHavingZone(zone) == false)
			return false;

		zone.SetRule(rule);
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

	public bool TryRegisterFacility(IFacility facility)
	{
		if (facility == null)
			return false;

		TryUnregisterFacility(facility);

		if (TryGetZoneAt(facility.GridPosition, out ZoneArea zone) == false || zone == null)
			return false;

		if (zone.RegisterFacility(facility) == false)
			return false;

		OnZoneChanged?.Invoke(zone);
		return true;
	}

	public bool TryUnregisterFacility(IFacility facility)
	{
		if (facility == null)
			return false;

		bool removed = false;
		for (int i = 0; i < registeredZones.Count; ++i)
		{
			ZoneArea zone = registeredZones[i];
			if (zone == null || zone.UnregisterFacility(facility) == false)
				continue;

			removed = true;
			OnZoneChanged?.Invoke(zone);
		}

		return removed;
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

	private void PopulateFacilitiesForZone(ZoneArea zone)
	{
		if (zone == null)
			return;

		zone.ClearFacilities();
		if (GridService == null)
			return;

		HashSet<IFacility> uniqueFacilities = new();
		RectInt bounds = zone.Bounds;

		for (int z = bounds.yMin; z < bounds.yMax; ++z)
		{
			for (int x = bounds.xMin; x < bounds.xMax; ++x)
			{
				int3 pos = new(x, zone.Floor, z);
				GameObject obj = GridService.GetObjectOnGrid(pos);
				if (obj == null || obj.TryGetComponent<IFacility>(out IFacility facility) == false)
					continue;

				if (facility.GridPosition.Equals(pos) == false)
					continue;

				if (uniqueFacilities.Add(facility))
					zone.RegisterFacility(facility);
			}
		}
	}

	private static bool ContainsRect(in RectInt outer, in RectInt inner)
	{
		return inner.xMin >= outer.xMin
			&& inner.yMin >= outer.yMin
			&& inner.xMax <= outer.xMax
			&& inner.yMax <= outer.yMax;
	}
}
