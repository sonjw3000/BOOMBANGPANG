using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

public enum AreaType
{
	WorkerSpawn = 0,
	RocketLanding = 1,
}

public partial class AreaManager : MonoBehaviour
{
	[FormerlySerializedAs("registeredZones")]
	[SerializeField] private List<Area> registeredAreas = new();
	[FormerlySerializedAs("zoneColors")]
	[SerializedDictionary("AreaType", "OverlayColor")]
	[SerializeField] private SerializedDictionary<AreaType, Color> areaColors = new();

	private readonly Dictionary<int, Dictionary<AreaType, List<Area>>> areas = new();

	public IReadOnlyList<Area> RegisteredAreas => registeredAreas;

	private GridService GridService
	{
		get
		{
			if (GameContext.HasInstance)
				return GameContext.Instance.GridService;

			return FindFirstObjectByType<GridService>();
		}
	}

	public event Action<Area> OnAreaAdded;
	public event Action<Area> OnAreaRemoved;
	public event Action<Area> OnAreaChanged;
	public event Action OnAreasRebuilt;

	private void Awake()
	{
		RebuildAreaLookup();
	}

#if UNITY_EDITOR
	private void OnValidate()
	{
		RebuildAreaLookup();
	}
#endif

	public void RebuildAreaLookup()
	{
		areas.Clear();
		for (int i = 0; i < registeredAreas.Count; ++i)
			RegisterArea(registeredAreas[i]);

		OnAreasRebuilt?.Invoke();
	}

	public bool CanPlaceArea(int floor, in RectInt bounds, Area ignore = null)
	{
		if (bounds.width <= 0 || bounds.height <= 0 || GridService == null)
			return false;

		for (int z = bounds.yMin; z < bounds.yMax; ++z)
		{
			for (int x = bounds.xMin; x < bounds.xMax; ++x)
			{
				GridCell cell = GridService.GetCell(x, floor, z);
				if (cell == null || cell.BuildingId != 0)
					return false;
			}
		}

		return HasOverlap(floor, bounds, ignore) == false;
	}

	public Area AddArea(string name, AreaType type, in RectInt bounds, int floor)
	{
		if (CanPlaceArea(floor, bounds) == false)
		{
			Debug.LogWarning($"Cannot add {type} area {name}: bounds {bounds} are invalid or overlap another area.");
			return null;
		}

		Area area = new(name, type, bounds, floor);
		registeredAreas.Add(area);
		RegisterArea(area);
		OnAreaAdded?.Invoke(area);
		return area;
	}

	public Area AddArea(AreaType type, in RectInt bounds, int floor)
	{
		return AddArea(BuildDefaultAreaName(type), type, bounds, floor);
	}

	public bool RemoveArea(Area area)
	{
		if (area == null || registeredAreas.Remove(area) == false)
			return false;

		if (areas.TryGetValue(area.Floor, out Dictionary<AreaType, List<Area>> floorAreas)
			&& floorAreas.TryGetValue(area.Type, out List<Area> typedAreas))
		{
			typedAreas.Remove(area);
			if (typedAreas.Count == 0)
				floorAreas.Remove(area.Type);
			if (floorAreas.Count == 0)
				areas.Remove(area.Floor);
		}

		OnAreaRemoved?.Invoke(area);
		return true;
	}

	public bool ResizeArea(Area area, in RectInt newBounds)
	{
		if (ContainsArea(area) == false || CanPlaceArea(area.Floor, newBounds, area) == false)
			return false;

		area.Resize(newBounds);
		OnAreaChanged?.Invoke(area);
		return true;
	}

	public bool RenameArea(Area area, string newName)
	{
		if (ContainsArea(area) == false || string.IsNullOrWhiteSpace(newName))
			return false;

		area.Rename(newName.Trim());
		OnAreaChanged?.Invoke(area);
		return true;
	}

	public bool TrySetDestinationBuilding(Area area, uint buildingId)
	{
		if (ContainsArea(area) == false || area.Type != AreaType.RocketLanding)
			return false;

		if (buildingId != 0 &&
			(GameContext.HasInstance == false || GameContext.Instance.BuildingMgr == null ||
			GameContext.Instance.BuildingMgr.TryGetBuilding(buildingId, out _) == false))
			return false;

		if (area.DestinationBuildingId == buildingId)
			return true;

		area.SetDestinationBuildingId(buildingId);
		OnAreaChanged?.Invoke(area);
		return true;
	}

	public bool TryGetAreas(out IReadOnlyList<Area> result, int floor, AreaType type)
	{
		result = null;
		if (areas.TryGetValue(floor, out Dictionary<AreaType, List<Area>> floorAreas) == false
			|| floorAreas.TryGetValue(type, out List<Area> typedAreas) == false
			|| typedAreas.Count == 0)
		{
			return false;
		}

		result = typedAreas;
		return true;
	}

	public bool TryGetAreaAt(in int3 position, out Area result)
	{
		result = null;
		if (areas.TryGetValue(position.y, out Dictionary<AreaType, List<Area>> floorAreas) == false)
			return false;

		foreach (List<Area> typedAreas in floorAreas.Values)
		{
			for (int i = 0; i < typedAreas.Count; ++i)
			{
				Area area = typedAreas[i];
				if (area != null && area.Contains(position))
				{
					result = area;
					return true;
				}
			}
		}

		return false;
	}

	public Color GetAreaColor(AreaType type)
	{
		if (areaColors.TryGetValue(type, out Color color))
			return color;

		return type == AreaType.WorkerSpawn
			? new Color(0.32f, 0.74f, 0.98f, 1f)
			: new Color(1f, 0.63f, 0.2f, 1f);
	}

	private void RegisterArea(Area area)
	{
		if (area == null)
			return;

		if (areas.TryGetValue(area.Floor, out Dictionary<AreaType, List<Area>> floorAreas) == false)
		{
			floorAreas = new Dictionary<AreaType, List<Area>>();
			areas[area.Floor] = floorAreas;
		}

		if (floorAreas.TryGetValue(area.Type, out List<Area> typedAreas) == false)
		{
			typedAreas = new List<Area>();
			floorAreas[area.Type] = typedAreas;
		}

		if (typedAreas.Contains(area) == false)
			typedAreas.Add(area);
	}

	private bool ContainsArea(Area area)
	{
		return area != null && registeredAreas.Contains(area);
	}

	private bool HasOverlap(int floor, in RectInt bounds, Area ignore)
	{
		if (areas.TryGetValue(floor, out Dictionary<AreaType, List<Area>> floorAreas) == false)
			return false;

		foreach (List<Area> typedAreas in floorAreas.Values)
		{
			for (int i = 0; i < typedAreas.Count; ++i)
			{
				Area other = typedAreas[i];
				if (other != ignore && other != null && bounds.Overlaps(other.Bounds))
					return true;
			}
		}

		return false;
	}

	private string BuildDefaultAreaName(AreaType type)
	{
		string baseName = type == AreaType.WorkerSpawn ? "Worker Spawn Area" : "Rocket Landing Area";
		string candidate = baseName;
		int suffix = 1;
		while (registeredAreas.Exists(area => area != null && area.DisplayName == candidate))
		{
			suffix += 1;
			candidate = $"{baseName} {suffix}";
		}

		return candidate;
	}
}
