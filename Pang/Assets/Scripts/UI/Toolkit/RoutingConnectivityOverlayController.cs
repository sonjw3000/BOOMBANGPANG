using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace UniverseLogistics.UI.Toolkit
{
	public enum RoutingConnectionType
	{
		LandingToBuilding = 0,
		BuildingToBuilding = 1,
	}

	public readonly struct RoutingConnectionKey : IEquatable<RoutingConnectionKey>
	{
		public RoutingConnectionType Type { get; }
		public Area SourceArea { get; }
		public uint SourceBuildingId { get; }
		public uint TargetBuildingId { get; }

		private RoutingConnectionKey(RoutingConnectionType type, Area sourceArea, uint sourceBuildingId, uint targetBuildingId)
		{
			Type = type;
			SourceArea = sourceArea;
			SourceBuildingId = sourceBuildingId;
			TargetBuildingId = targetBuildingId;
		}

		public static RoutingConnectionKey ForLanding(Area area, uint targetBuildingId) =>
			new(RoutingConnectionType.LandingToBuilding, area, 0, targetBuildingId);

		public static RoutingConnectionKey ForBuildings(uint sourceBuildingId, uint targetBuildingId) =>
			new(RoutingConnectionType.BuildingToBuilding, null, sourceBuildingId, targetBuildingId);

		public bool Equals(RoutingConnectionKey other) =>
			Type == other.Type && ReferenceEquals(SourceArea, other.SourceArea) &&
			SourceBuildingId == other.SourceBuildingId && TargetBuildingId == other.TargetBuildingId;

		public override bool Equals(object obj) => obj is RoutingConnectionKey other && Equals(other);

		public override int GetHashCode() => HashCode.Combine((int)Type, SourceArea, SourceBuildingId, TargetBuildingId);
	}

	public sealed class CachedRoutingPath
	{
		private readonly List<int3> cells;

		public RoutingConnectionKey Key { get; }
		public IReadOnlyList<int3> Cells => cells;
		public bool HasPath => cells.Count > 0;

		public CachedRoutingPath(RoutingConnectionKey key, IReadOnlyList<int3> sourceCells)
		{
			Key = key;
			cells = sourceCells != null ? new List<int3>(sourceCells) : new List<int3>();
		}
	}

	public sealed class RoutingConnectivityOverlayController : MonoBehaviour
	{
		private readonly struct RouteRequest
		{
			public RoutingConnectionKey Key { get; }
			public int3 Start { get; }
			public int3 End { get; }

			public RouteRequest(RoutingConnectionKey key, in int3 start, in int3 end)
			{
				Key = key;
				Start = start;
				End = end;
			}
		}

		[SerializeField] private GameObject overlayQuadPrefab;
		[SerializeField] private int initialPoolSize = 128;
		[SerializeField] private float overlayHeight = 0.026f;
		[SerializeField] private Color landingRouteColor = new(1f, 0.53f, 0.14f, 0.34f);
		[SerializeField] private Color buildingRouteColor = new(0.2f, 0.82f, 0.43f, 0.36f);

		private readonly Dictionary<RoutingConnectionKey, CachedRoutingPath> cachedPaths = new();
		private readonly Dictionary<int3, RoutingConnectionType> visibleCells = new();
		private readonly List<GameObject> activeQuads = new();
		private GameObject overlayRoot;
		private GameObjectPool quadPool;
		private bool isVisible;
		private int refreshGeneration;
		private int pendingPathCount;

		public IReadOnlyDictionary<RoutingConnectionKey, CachedRoutingPath> CachedPaths => cachedPaths;
		public bool IsVisible => isVisible;
		public int ConnectionCount => cachedPaths.Count;
		public int PendingPathCount => pendingPathCount;
		public int ValidPathCount
		{
			get
			{
				int count = 0;
				foreach (CachedRoutingPath path in cachedPaths.Values)
					if (path.HasPath) count += 1;
				return count;
			}
		}

		public event Action PathsChanged;

		private PathFindingService PathFinding => GameContext.HasInstance ? GameContext.Instance.PathFinding : null;
		private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
		private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
		private BuildingFootprintService FootprintService => GameContext.HasInstance ? GameContext.Instance.BuildingFootprintService : null;
		private AreaManager AreaManager => GameContext.HasInstance ? GameContext.Instance.AreaMgr : null;

		public void Configure(GameObject targetOverlayQuadPrefab)
		{
			overlayQuadPrefab = targetOverlayQuadPrefab;
		}

		private void Awake()
		{
			overlayRoot = new GameObject("RoutingConnectivityOverlayRoot");
			Transform parent = GameContext.HasInstance ? GameContext.Instance.transform : transform;
			overlayRoot.transform.SetParent(parent, false);
			overlayRoot.hideFlags = HideFlags.HideInHierarchy;
			overlayRoot.SetActive(false);
			quadPool = new GameObjectPool(Mathf.Max(0, initialPoolSize), CreateQuad);
		}

		private void OnDisable()
		{
			SetVisible(false);
		}

		private void OnDestroy()
		{
			refreshGeneration += 1;
			if (overlayRoot != null) Destroy(overlayRoot);
		}

		public void SetVisible(bool visible)
		{
			if (isVisible == visible)
			{
				if (visible) RefreshConnections();
				return;
			}

			isVisible = visible;
			if (overlayRoot != null) overlayRoot.SetActive(visible);
			if (visible)
				RefreshConnections();
			else
				HideVisuals();
		}

		public void RefreshConnections()
		{
			refreshGeneration += 1;
			int generation = refreshGeneration;
			pendingPathCount = 0;
			cachedPaths.Clear();
			HideVisuals();

			List<RouteRequest> requests = BuildRouteRequests();
			for (int i = 0; i < requests.Count; ++i)
			{
				RouteRequest request = requests[i];
				cachedPaths[request.Key] = new CachedRoutingPath(request.Key, null);
				pendingPathCount += 1;
				if (PathFinding == null || PathFinding.RequestPreviewRoute(request.Start, request.End,
					cells => HandlePathCompleted(generation, request.Key, cells),
					position => CanTraverseBlockedCell(request.Key, position)) == false)
				{
					pendingPathCount -= 1;
				}
			}

			RebuildVisuals();
			PathsChanged?.Invoke();
		}

		private void HandlePathCompleted(int generation, RoutingConnectionKey key, IReadOnlyList<int3> cells)
		{
			if (generation != refreshGeneration) return;
			cachedPaths[key] = new CachedRoutingPath(key, cells);
			pendingPathCount = Mathf.Max(0, pendingPathCount - 1);
			RebuildVisuals();
			PathsChanged?.Invoke();
		}

		private List<RouteRequest> BuildRouteRequests()
		{
			List<RouteRequest> requests = new();
			if (BuildingManager == null || FootprintService == null || GridService == null) return requests;

			if (AreaManager != null)
			{
				IReadOnlyList<Area> areas = AreaManager.RegisteredAreas;
				for (int i = 0; i < areas.Count; ++i)
				{
					Area area = areas[i];
					if (area == null || area.Type != AreaType.RocketLanding || area.DestinationBuildingId == 0 ||
						BuildingManager.TryGetBuilding(area.DestinationBuildingId, out Building destination) == false || destination == null ||
						TryGetAreaPoint(area, out int3 areaPoint) == false ||
						TryGetBuildingPoint(destination, out int3 landingDestination) == false || areaPoint.y != landingDestination.y)
						continue;

					requests.Add(new RouteRequest(
						RoutingConnectionKey.ForLanding(area, destination.RuntimeBuildingId),
						areaPoint,
						landingDestination));
				}
			}

			IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
			for (int i = 0; i < buildings.Count; ++i)
			{
				Building source = buildings[i];
				if (source == null || TryGetBuildingPoint(source, out int3 sourcePoint) == false) continue;
				foreach (uint targetBuildingId in source.OutputBuildingIds)
				{
					if (BuildingManager.TryGetBuilding(targetBuildingId, out Building target) == false || target == null ||
						TryGetBuildingPoint(target, out int3 targetPoint) == false || sourcePoint.y != targetPoint.y)
						continue;

					requests.Add(new RouteRequest(
						RoutingConnectionKey.ForBuildings(source.RuntimeBuildingId, targetBuildingId),
						sourcePoint,
						targetPoint));
				}
			}

			return requests;
		}

		private bool CanTraverseBlockedCell(RoutingConnectionKey key, int3 position)
		{
			GridCell cell = GridService?.GetCell(position);
			if (cell == null) return false;

			if (cell.BuildingId == key.TargetBuildingId ||
				(key.SourceBuildingId != 0 && cell.BuildingId == key.SourceBuildingId))
				return true;

			Area sourceArea = key.SourceArea;
			return sourceArea != null && position.y == sourceArea.Floor &&
				sourceArea.Bounds.Contains(new Vector2Int(position.x, position.z));
		}

		private bool TryGetBuildingPoint(Building building, out int3 point)
		{
			point = default;
			if (building == null || FootprintService.TryGetFootprint(building.RuntimeBuildingId, out BuildingFootprintRecord footprint) == false ||
				footprint == null)
				return false;

			point = new int3(footprint.Center.x, footprint.Floor, footprint.Center.y);
			if (GridService.IsBlocked(point) == false) return true;
			return TryFindNearestPassablePoint(footprint.Bounds, footprint.Floor, point, building.RuntimeBuildingId, out point);
		}

		private bool TryGetAreaPoint(Area area, out int3 point)
		{
			point = default;
			if (area == null) return false;
			RectInt bounds = area.Bounds;
			point = new int3(bounds.xMin + (bounds.width - 1) / 2, area.Floor, bounds.yMin + (bounds.height - 1) / 2);
			if (GridService.IsBlocked(point) == false) return true;
			return TryFindNearestPassablePoint(bounds, area.Floor, point, 0, out point);
		}

		private bool TryFindNearestPassablePoint(RectInt bounds, int floor, in int3 origin, uint requiredBuildingId, out int3 point)
		{
			point = default;
			int bestDistance = int.MaxValue;
			bool found = false;
			for (int z = bounds.yMin; z < bounds.yMax; ++z)
			{
				for (int x = bounds.xMin; x < bounds.xMax; ++x)
				{
					int3 candidate = new(x, floor, z);
					GridCell cell = GridService.GetCell(candidate);
					if (cell == null || GridService.IsBlocked(candidate) ||
						(requiredBuildingId != 0 && cell.BuildingId != requiredBuildingId))
						continue;

					int distance = math.abs(candidate.x - origin.x) + math.abs(candidate.z - origin.z);
					if (distance >= bestDistance) continue;
					bestDistance = distance;
					point = candidate;
					found = true;
				}
			}
			return found;
		}

		private void RebuildVisuals()
		{
			HideVisuals();
			if (isVisible == false || quadPool == null || GridService == null) return;

			visibleCells.Clear();
			foreach (CachedRoutingPath path in cachedPaths.Values)
			{
				IReadOnlyList<int3> cells = path.Cells;
				for (int i = 0; i < cells.Count; ++i)
				{
					int3 cellPosition = cells[i];
					GridCell cell = GridService.GetCell(cellPosition);
					if (cell == null || cell.BuildingId != 0) continue;
					if (visibleCells.TryGetValue(cellPosition, out RoutingConnectionType existing) &&
						existing == RoutingConnectionType.BuildingToBuilding)
						continue;

					visibleCells[cellPosition] = path.Key.Type;
				}
			}

			foreach (KeyValuePair<int3, RoutingConnectionType> entry in visibleCells)
			{
				GameObject quad = quadPool.Get();
				if (quad == null) continue;
				int3 position = entry.Key;
				quad.transform.position = new Vector3(position.x, position.y + overlayHeight, position.z);
				quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
				quad.transform.localScale = Vector3.one;
				MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
				if (renderer != null)
					renderer.material.color = entry.Value == RoutingConnectionType.BuildingToBuilding
						? buildingRouteColor
						: landingRouteColor;
				activeQuads.Add(quad);
			}
		}

		private GameObject CreateQuad()
		{
			if (overlayQuadPrefab == null) return null;
			GameObject quad = Instantiate(overlayQuadPrefab, overlayRoot.transform);
			quad.name = "RoutingConnectivityCell";
			return quad;
		}

		private void HideVisuals()
		{
			if (quadPool != null)
			{
				for (int i = 0; i < activeQuads.Count; ++i) quadPool.Release(activeQuads[i]);
			}
			activeQuads.Clear();
			visibleCells.Clear();
		}
	}
}
