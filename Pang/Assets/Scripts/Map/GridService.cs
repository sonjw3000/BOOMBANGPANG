using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Assets.Scripts.Save.JsonData;

public enum PlacementResult
{
	Success,

	// failed
	BlockedByDynamicObstacle,
	BlockedByStaticObstacle,
	GameObjectMismatch,
	TriedToMoveOutOfBound,
}

public class PlacementResultPayload
{
	public readonly PlacementResult result;
	public readonly GameObject disturbedBy;
	public PlacementResultPayload(PlacementResult result, GameObject disturbedBy = null)
	{
		this.result = result;
		this.disturbedBy = disturbedBy;
	}
}

public partial class GridService : MonoBehaviour
{
	private const int OutdoorRegionId = 0;
	private const int BoundaryRegionId = 1;
	private const int IndoorRegionId = 2;

	[SerializeField] private GameObject placeableParent;
	[SerializeField] private GameObject gridParent;
	[SerializeField] private Material gridBoundaryMaterial;
	[SerializeField] private Color[] gridBoundaryColor;

	private Texture2D gridBoundaryTexture;
	private GameObject gridBoundaryQuad;
	private short[] gridBoundary;

	private readonly GridMap gridMap = new();
	private static readonly int3[] SpaceRegionDirections =
	{
		new(1, 0, 0),
		new(-1, 0, 0),
		new(0, 0, 1),
		new(0, 0, -1),
	};


	public GridCell[,,] Map => gridMap.Map;
	public int3 MapSize => gridMap.MapSize;

	public bool IsReady { get; private set; }

	public GridCell GetCell(in int3 pos)
	{
		if (gridMap.IsInBound(pos) == false)
			return null;
		return gridMap.Map[pos.x, pos.y, pos.z];
	}

	public GridCell GetCell(int x, int y, int z)
	{
		if (gridMap.IsInBound(x, y, z) == false)
			return null;
		return gridMap.Map[x, y, z];
	}

	public bool TrySetTemperature(in int3 pos, float temperatureCelsius)
	{
		GridCell cell = GetCell(pos);
		if (cell == null)
			return false;

		float previous = cell.TemperatureCelsius;
		if (cell.SetTemperature(temperatureCelsius) == false)
			return false;

		OnCellTemperatureChanged?.Invoke(pos, previous, cell.TemperatureCelsius);
		return true;
	}

	public bool TryAdjustTemperature(in int3 pos, float deltaCelsius)
	{
		GridCell cell = GetCell(pos);
		if (cell == null || float.IsNaN(deltaCelsius) || float.IsInfinity(deltaCelsius) || deltaCelsius == 0.0f)
			return false;

		return TrySetTemperature(pos, cell.TemperatureCelsius + deltaCelsius);
	}

	public bool TrySetFireIntensity(in int3 pos, float intensity) =>
		TrySetHazardLevel(pos, GridHazardType.Fire, intensity);

	public bool TryAdjustFireIntensity(in int3 pos, float delta) =>
		TryAdjustHazardLevel(pos, GridHazardType.Fire, delta);

	public bool TrySetContaminationLevel(in int3 pos, float level) =>
		TrySetHazardLevel(pos, GridHazardType.Contamination, level);

	public bool TryAdjustContaminationLevel(in int3 pos, float delta) =>
		TryAdjustHazardLevel(pos, GridHazardType.Contamination, delta);

	public bool TrySetCorrosiveLevel(in int3 pos, float level) =>
		TrySetHazardLevel(pos, GridHazardType.Corrosive, level);

	public bool TryAdjustCorrosiveLevel(in int3 pos, float delta) =>
		TryAdjustHazardLevel(pos, GridHazardType.Corrosive, delta);

	public bool TrySetRadiationLevel(in int3 pos, float level) =>
		TrySetHazardLevel(pos, GridHazardType.Radiation, level);

	public bool TryAdjustRadiationLevel(in int3 pos, float delta) =>
		TryAdjustHazardLevel(pos, GridHazardType.Radiation, delta);

	public bool TrySetHazardLevel(in int3 pos, GridHazardType hazardType, float level)
	{
		GridCell cell = GetCell(pos);
		if (cell == null)
			return false;

		float previous = cell.GetHazardLevel(hazardType);
		if (cell.SetHazardLevel(hazardType, level) == false)
			return false;

		OnCellHazardChanged?.Invoke(pos, hazardType, previous, cell.GetHazardLevel(hazardType));
		return true;
	}

	public bool TryAdjustHazardLevel(in int3 pos, GridHazardType hazardType, float delta)
	{
		GridCell cell = GetCell(pos);
		if (cell == null || float.IsNaN(delta) || float.IsInfinity(delta) || delta == 0.0f)
			return false;

		return TrySetHazardLevel(pos, hazardType, cell.GetHazardLevel(hazardType) + delta);
	}

	public bool IsPassable(in int3 pos)
	{
		if (gridMap.IsInBound(pos) == false)
			return false;
		return gridMap.Map[pos.x, pos.y, pos.z].IsPassable;
	}

	public bool IsBlocked(in int3 pos)
	{
		if (gridMap.IsInBound(pos) == false)
			return true;
		return gridMap.Map[pos.x, pos.y, pos.z].IsBlocked;
	}

	public bool IsIndoor(in int3 pos) => GetRegionId(pos) == IndoorRegionId;
	public bool IsOutdoor(in int3 pos) => GetRegionId(pos) == OutdoorRegionId;
	public int GetRegionId(in int3 pos)
	{
		if (gridMap.IsInBound(pos) == false)
			return OutdoorRegionId;

		return gridMap.Map[pos.x, pos.y, pos.z].RegionId;
	}

	public bool IsSameRegion(in int3 from, in int3 to)
	{
		return GetRegionId(from) == GetRegionId(to);
	}

	public IEnumerable<KeyValuePair<GameObject, PlacementContext>> GetPlacedObjectsSnapshot() => placedObjects;


	private readonly Dictionary<GameObject, PlacementContext> placedObjects = new();

	public event System.Action<PlacementContext> OnPlaceableInstalled;
	public event System.Action<int3, float, float> OnCellTemperatureChanged;
	public event System.Action<int3, GridHazardType, float, float> OnCellHazardChanged;
	public event System.Action OnSpaceRegionsChanged;

	public bool IsPlacedObject(GameObject targetObj) => targetObj != null && placedObjects.ContainsKey(targetObj);

	private EconomyService Economy => GameContext.Instance.EconomyService;
	private BuildingManager BuildingManager => GameContext.Instance.BuildingMgr;
	private FacilityManager FacilityManager => GameContext.Instance.FacilityMgr;
	private WorkerSpawnManager WorkerSpawnMgr => GameContext.Instance.WorkerSpawnMgr;

	public void OnGameStart()
	{
		GameObject tileFloor = GameObject.CreatePrimitive(PrimitiveType.Quad);

		tileFloor.transform.localScale = new Vector3Int(gridMap.MapSize.x, gridMap.MapSize.z, gridMap.MapSize.y);
		tileFloor.transform.Rotate(90, 0, 0);
		tileFloor.transform.position = new Vector3(gridMap.MapSize.x / 2 - 0.5f, 0, gridMap.MapSize.z / 2 - 0.5f);

		tileFloor.transform.parent = gridParent.transform;

		int3 mapSize = MapSize;
		gridBoundary ??= new short[mapSize.x * mapSize.z];

		if (gridBoundaryQuad != null)
			Destroy(gridBoundaryQuad);

		// boundary Quad
		gridBoundaryQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
		gridBoundaryQuad.transform.localScale = new Vector3Int(gridMap.MapSize.x, gridMap.MapSize.z, gridMap.MapSize.y);
		gridBoundaryQuad.transform.Rotate(90, 0, 0);
		gridBoundaryQuad.transform.position = new Vector3(gridMap.MapSize.x / 2 - 0.5f, 0, gridMap.MapSize.z / 2 - 0.5f);
		gridBoundaryQuad.name = "GridBoundaryQuad";
		gridBoundaryQuad.transform.SetParent(gridParent.transform);

		var collider = gridBoundaryQuad.GetComponent<Collider>();
		if (collider != null)
			Destroy(collider);

		if (gridBoundaryTexture != null)
			Destroy(gridBoundaryTexture);

		gridBoundaryTexture = new(mapSize.x, MapSize.z, TextureFormat.R16, false, true);
		gridBoundaryTexture.filterMode = FilterMode.Point;
		gridBoundaryTexture.wrapMode = TextureWrapMode.Clamp;

		gridBoundaryMaterial.SetTexture("_GridTex", gridBoundaryTexture);
		gridBoundaryMaterial.SetColorArray("_GridColors", gridBoundaryColor);

		var renderer = gridBoundaryQuad.GetComponent<MeshRenderer>();
		List<Material> mats = new();
		mats.Add(gridBoundaryMaterial);
		renderer.SetMaterials(mats);
		gridBoundaryQuad.SetActive(false);

		IsReady = true;
		RecalculateSpaceRegions();
	}

	public void BuildDefaultMap()
	{
		gridMap.SetMapSize(new int3(100, 1, 100));

		GridCell[,,] newMap = new GridCell[MapSize.x, MapSize.y, MapSize.z];
		for (int x = 0; x < MapSize.x; ++x)
		{
			for (int y = 0; y < MapSize.y; ++y)
			{
				for (int z = 0; z < MapSize.z; ++z)
				{
					newMap[x, y, z] = new GridCell(0);
				}
			}
		}

		gridMap.SetMap(newMap);
	}

	public bool OnCheckInstallable(in PlacementContext ctx, List<int3> possibleCell, List<int3> blocked)
	{
		if (ctx.placeableDefinition == null || ctx.placeableDefinition.gridFootprint == null)
		{
			Debug.LogWarning("No placeable or Footprint!!");
			return false;
		}

		return EvaluatePlacement(in ctx, possibleCell, blocked);
	}

	public void GetOverrideTargets(in PlacementContext ctx, List<GameObject> targetsBuffer)
	{
		if (targetsBuffer == null)
			return;

		targetsBuffer.Clear();

		if (ctx.placeableDefinition == null || ctx.placeableDefinition.gridFootprint == null)
			return;

		CollectOverrideTargets(ctx, out var targets);
		foreach (GameObject target in targets)
		{
			if (target != null)
				targetsBuffer.Add(target);
		}
	}

	// gridPlaceable이 install이 되었을 때
	public bool OnInstall(in PlacementContext ctx)
	{
		if (ctx.placeableDefinition == null || ctx.placeableDefinition.gridFootprint == null)
		{
			Debug.LogWarning("No placeable or Footprint!!");
			return false;
		}

		if (EvaluatePlacement(in ctx, null, null) == false)
		{
			ShowPlacementErrorIfNeeded(ctx, "Cannot place here");
			return false;
		}

		if (ctx.placementEvent == PlacementEvent.Normal && Economy.CanAfford(ctx.placeableDefinition.Cost) == false)
		{
			Debug.Log("Can't afford that money!");
			ShowPlacementErrorIfNeeded(ctx, "Not enough money");
			return false;
		}


		GameObject obj = ctx.placedObj;

		if (obj == null)
			obj = Instantiate(ctx.placeableDefinition.prefab, placeableParent.transform);

		NormalizePlacedObjectParent(obj);

		if (obj == null)
		{
			Debug.LogError("Failed to instantiate placeable prefab.");
			return false;
		}

		CollectOverrideTargets(ctx, out var overrideTargets);
		NotifyAndRemoveOverrideTargets(overrideTargets, ctx.placeableDefinition, obj);

		IInteractionPoint interactable = obj.GetComponent<IInteractionPoint>();
		interactable?.ClearInteractionPoints();


		GridFootprint footprint = ctx.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;

		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				var footprintCell = footprint.Get(x, z);
				if (IsEmptyFootprintCell(footprintCell))
					continue;

				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, ctx.facingDirection);
				int3 target = ctx.center + rotatedOffset;

				//Debug.Log($"target: {target} / offset: {offset}");

				if (gridMap.IsInBound(target) == false)
					return false;

				Map[target.x, target.y, target.z].Set(footprintCell, obj);

				if (footprintCell.flags.HasFlag(GridFlags.Interaction))
				{
					// if interaction cell, add grid interaction to IGridPlaceable
					if (interactable == null)
					{
						Debug.LogError("The instantiated object does not have an IGridPlaceable component.");
						Destroy(obj);
						return false;
					}

					interactable.AddInteractionPoint(footprintCell.interactionKind, in target);
				}
			}
		}

		obj.transform.position += new Vector3(
			ctx.center.x,
			ctx.center.y,
			ctx.center.z
		);
		obj.transform.rotation = GetRotation(ctx.facingDirection);

		placedObjects[obj] = ctx;

		var gridPlaceable = obj.GetComponent<IGridPlaceable>();
		if (gridPlaceable != null)
		{
			gridPlaceable.OnPositionSet(ctx.center, ctx.facingDirection);
		}

		if (obj.TryGetComponent<IFacility>(out var facility))
		{
			uint owningBuildingId = ResolveOwningBuildingId(in ctx);
			if (owningBuildingId != 0)
				BuildingManager.TryRegisterFacility(owningBuildingId, facility);

			FacilityManager?.RegisterFacility(owningBuildingId, facility);
		}

		if (ctx.placeableDefinition.gridFootprint.IsNeedToRefresh)
			RecalculateSpaceRegions();

		OnPlaceableInstalled?.Invoke(ctx);

		return true;
	}

	private void ShowPlacementErrorIfNeeded(in PlacementContext ctx, string message)
	{
		if (ctx.placementEvent != PlacementEvent.Normal || ctx.placedObj != null || string.IsNullOrWhiteSpace(message))
			return;

		GameContext.Instance.FloatingTextManager?.ShowScreen(
			FloatingTextPreset.Error,
			message,
			Input.mousePosition);
	}

	private void NormalizePlacedObjectParent(GameObject obj)
	{
		if (obj == null)
			return;

		Transform desiredParent = placeableParent != null ? placeableParent.transform : null;
		if (obj.TryGetComponent<AIWorker>(out _) &&
			WorkerSpawnMgr != null &&
			WorkerSpawnMgr.SpawnedWorkerRoot != null)
		{
			desiredParent = WorkerSpawnMgr.SpawnedWorkerRoot;
		}

		if (desiredParent != null && obj.transform.parent != desiredParent)
			obj.transform.SetParent(desiredParent, true);
	}

	public bool OnRemove(GameObject targetObj)
	{
		return OnRemove(targetObj, true);
	}

	public bool OnRemove(GameObject targetObj, bool destroyObject)
	{
		if (placedObjects.TryGetValue(targetObj, out var context) == false)
		{
			Debug.Log("cant get");
			return false;
		}

		IFacility facility = null;
		uint owningBuildingId = 0;
		if (targetObj.TryGetComponent<IFacility>(out facility))
			owningBuildingId = ResolveOwningBuildingId(in context);

		GridFootprint footprint = context.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;
		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				var footprintCell = footprint.Get(x, z);
				if (IsEmptyFootprintCell(footprintCell))
					continue;

				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, context.facingDirection);
				int3 target = context.center + rotatedOffset;
				if (gridMap.IsInBound(target) == false)
					return false;
				// clear to cell
				Map[target.x, target.y, target.z].Remove(footprintCell, targetObj);
			}
		}

		placedObjects.Remove(targetObj);
		if (targetObj.TryGetComponent<IGridPlacementEffect>(out var placementEffect))
			placementEffect.OnRemoved();

		if (targetObj.TryGetComponent<IInteractionPoint>(out var interactable))
			interactable.ClearInteractionPoints();

		if (facility != null && owningBuildingId != 0)
			BuildingManager.TryUnregisterFacility(owningBuildingId, facility);

		if (facility != null)
			FacilityManager?.UnregisterFacility(owningBuildingId, facility);

		if (destroyObject)
			Destroy(targetObj);

		if (context.placeableDefinition.gridFootprint.IsNeedToRefresh)
			RecalculateSpaceRegions();

		return true;
	}

	public bool TryReserve(FindRoute findRoute, in int3 pos)
	{
		if (IsBlocked(pos))
			return false;

		return GetCell(pos).TryReserve(findRoute);
	}

	public bool CanRelocateWorkerForTransit(AIWorker worker, in int3 newCenter)
	{
		if (worker == null || placedObjects.TryGetValue(worker.gameObject, out PlacementContext context) == false)
			return false;

		return CanRelocatePlacedObject(worker.gameObject, context, newCenter);
	}

	public bool TryUnreserve(FindRoute findRoute, in int3 pos)
	{
		return gridMap.Map[pos.x, pos.y, pos.z].TryUnreserve(findRoute);
	}

	public bool TryRelocateWorkerForTransit(AIWorker worker, in int3 newCenter, FacingDirection direction)
	{
		if (worker == null || placedObjects.TryGetValue(worker.gameObject, out PlacementContext context) == false)
			return false;

		if (CanRelocatePlacedObject(worker.gameObject, context, newCenter) == false)
			return false;

		FindRoute route = worker.RouteFinder;
		int3 previousCenter = context.center;
		GridFootprint footprint = context.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;

		if (route != null)
			TryUnreserve(route, previousCenter);

		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				FootprintCell footprintCell = footprint.Get(x, z);
				if (IsEmptyFootprintCell(footprintCell))
					continue;

				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, context.facingDirection);
				int3 target = previousCenter + rotatedOffset;
				if (gridMap.IsInBound(target) == false)
					return false;

				Map[target.x, target.y, target.z].Remove(footprintCell, worker.gameObject);
			}
		}

		context.center = newCenter;
		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				FootprintCell footprintCell = footprint.Get(x, z);
				if (IsEmptyFootprintCell(footprintCell))
					continue;

				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, context.facingDirection);
				int3 target = newCenter + rotatedOffset;
				if (gridMap.IsInBound(target) == false)
					return false;

				Map[target.x, target.y, target.z].Set(footprintCell, worker.gameObject);
			}
		}

		worker.transform.position = new Vector3(newCenter.x, newCenter.y, newCenter.z);
		worker.transform.rotation = GetRotation(direction);
		worker.SetPosition(newCenter);
		worker.SetDirection(direction);

		if (route != null && TryReserve(route, newCenter) == false)
		{
			Debug.LogWarning($"[GridService] Failed to reserve relocated worker cell at {newCenter}.");
			return false;
		}

		if (context.placeableDefinition.gridFootprint.IsNeedToRefresh)
			RecalculateSpaceRegions();

		return true;
	}

	public bool RegisterPlannedPath(FindRoute findRoute, in int3 pos)
	{
		GridCell cell = GetCell(pos);
		if (cell == null)
			return false;

		return cell.RegisterPlannedRoute(findRoute);
	}

	public bool UnregisterPlannedPath(FindRoute findRoute, in int3 pos)
	{
		GridCell cell = GetCell(pos);
		if (cell == null)
			return false;

		return cell.UnregisterPlannedRoute(findRoute);
	}

	public int GetPlannedPathCongestionCost(in int3 pos, FindRoute requester, int activePathCost, int stalePathCost)
	{
		GridCell cell = GetCell(pos);
		if (cell == null || cell.PlannedPathCount == 0)
			return 0;

		int totalCost = 0;
		foreach (var plannedRoute in cell.PlannedRoutes)
		{
			if (plannedRoute == requester)
				continue;

			if (plannedRoute == null || plannedRoute.HasPlannedPath == false)
			{
				totalCost += stalePathCost;
				continue;
			}

			totalCost += activePathCost;
		}

		return totalCost;
	}

	public PlacementResult TryMove(FindRoute findRoute, in int3 from, in int3 to)
	{
		var fromGridCell = GetCell(from);
		//var obj = gridMap.GetObjectOnGrid(from);
		var toGridCell = GetCell(to);

		if (IsBlocked(to))
			return PlacementResult.BlockedByStaticObstacle;

		if (fromGridCell.ReservedRoute != findRoute)
		{
			// reserve로 바꾸었기 때문에 해당 내용은 일어나선 안된다
			Debug.LogWarning("Cant Hit Here!! Need Check" + $", from: {fromGridCell.ReservedRoute}, to: {findRoute}");
			return PlacementResult.GameObjectMismatch;
		}

		if (toGridCell.ReservedRoute != findRoute)
		{
			// reserve로 바꾸었기 때문에 해당 내용은 일어나선 안된다
			Debug.LogWarning("Cant Hit Here!! Need Check");
			return PlacementResult.BlockedByDynamicObstacle;
		}

		PlacementContext context = placedObjects[findRoute.gameObject];
		GridFootprint footprint = context.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;
		
		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				var footprintCell = footprint.Get(x, z);
				if (IsEmptyFootprintCell(footprintCell))
					continue;

				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, context.facingDirection);
				int3 target = context.center + rotatedOffset;
				if (gridMap.IsInBound(target) == false)
					return PlacementResult.TriedToMoveOutOfBound;
				// clear to cell
				Map[target.x, target.y, target.z].Remove(footprintCell, findRoute.gameObject);
			}
		}

		context.center = to;
		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				var footprintCell = footprint.Get(x, z);
				if (IsEmptyFootprintCell(footprintCell))
					continue;

				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, context.facingDirection);
				int3 target = context.center + rotatedOffset;
				if (gridMap.IsInBound(target) == false)
					return PlacementResult.TriedToMoveOutOfBound;
				Map[target.x, target.y, target.z].Set(footprintCell, findRoute.gameObject);
			}
		}

		if (context.placeableDefinition.gridFootprint.IsNeedToRefresh)
			RecalculateSpaceRegions();

		return PlacementResult.Success;
	}

	public FindRoute GetReservedFindRoute(in int3 pos)
	{
		return gridMap.Map[pos.x, pos.y, pos.z].ReservedRoute;
	}

	// force moving
	private void OnForceMove(GameObject targetObj, PlacementContext ctx, in int3 newCenter)
	{
		// todo
		// 여기서 떨어진 애들은 파괴되어야함
		var footprint = ctx.placeableDefinition.gridFootprint;

		Vector2Int pivot = footprint.Pivot;

		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				var footprintCell = footprint.Get(x, z);
				if (IsEmptyFootprintCell(footprintCell))
					continue;

				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 target = newCenter + offset;

				// set to cell
				Map[target.x, target.y, target.z].Set(footprintCell, targetObj);
			}
		}
		if (targetObj.TryGetComponent<IGridPlaceable>(out var gridPlaceable))
		{
			gridPlaceable.OnPositionSet(newCenter, ctx.facingDirection);
		}
		placedObjects[targetObj] = ctx;

		if (ctx.placeableDefinition.gridFootprint.IsNeedToRefresh)
			RecalculateSpaceRegions();
	}

	public GameObject GetObjectOnGrid(in int3 pos)
	{
		return gridMap.GetObjectOnGrid(pos);
	}

	public void SetGridBoundaryVisible(bool visible)
	{
		if (gridBoundaryQuad == null)
			return;

		gridBoundaryQuad.SetActive(visible);
	}

	private static int3 RotateOffset(int3 offset, FacingDirection direction)
	{
		return direction switch
		{
			FacingDirection.North => offset,
			FacingDirection.East => new int3(offset.z, 0, -offset.x),
			FacingDirection.South => new int3(-offset.x, 0, -offset.z),
			FacingDirection.West => new int3(-offset.z, 0, offset.x),
			_ => offset
		};
	}

	private static Quaternion GetRotation(FacingDirection direction)
	{
		return direction switch
		{
			FacingDirection.North => Quaternion.identity,
			FacingDirection.East => Quaternion.Euler(0f, 90f, 0f),
			FacingDirection.South => Quaternion.Euler(0f, 180f, 0f),
			FacingDirection.West => Quaternion.Euler(0f, 270f, 0f),
			_ => Quaternion.identity
		};
	}

	private static bool IsEmptyFootprintCell(in FootprintCell footprintCell)
	{
		return footprintCell.flags == GridFlags.None;
	}

	private static bool CanOverride(in FootprintCell footprintCell, GridCell targetCell)
	{
		if (targetCell == null || targetCell.OccupancyObjectOnGrid == null)
			return false;

		GridOccupancyCategory targetCategory = targetCell.OccupancyCategory;
		if (targetCategory == GridOccupancyCategory.None)
			return false;

		return (footprintCell.overrideTargets & targetCategory) != 0;
	}

	private bool CanRelocatePlacedObject(GameObject placedObject, PlacementContext context, in int3 newCenter)
	{
		if (placedObject == null || context == null || context.placeableDefinition == null || context.placeableDefinition.gridFootprint == null)
			return false;

		GridFootprint footprint = context.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;

		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				FootprintCell footprintCell = footprint.Get(x, z);
				if (IsEmptyFootprintCell(footprintCell))
					continue;

				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, context.facingDirection);
				int3 target = newCenter + rotatedOffset;
				GridCell targetCell = GetCell(target);
				if (targetCell == null)
					return false;

				if (targetCell.CanPlaceObject || targetCell.OccupancyObjectOnGrid == placedObject)
					continue;

				return false;
			}
		}

		return true;
	}

	private void CollectOverrideTargets(in PlacementContext ctx, out HashSet<GameObject> targets)
	{
		targets = new HashSet<GameObject>();

		GridFootprint footprint = ctx.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;

		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				FootprintCell footprintCell = footprint.Get(x, z);
				if (IsEmptyFootprintCell(footprintCell))
					continue;

				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, ctx.facingDirection);
				int3 target = ctx.center + rotatedOffset;
				GridCell targetCell = GetCell(target);
				if (CanOverride(footprintCell, targetCell) == false)
					continue;

				targets.Add(targetCell.OccupancyObjectOnGrid);
			}
		}
	}

	private void NotifyAndRemoveOverrideTargets(HashSet<GameObject> targets, PlaceableDefinition overridingDefinition, GameObject overridingObject)
	{
		if (targets == null || targets.Count == 0)
			return;

		foreach (GameObject target in targets)
		{
			if (target == null)
				continue;

			string overridingName = overridingDefinition != null ? overridingDefinition.name : "UnknownPlaceable";
			string overridingObjectName = overridingObject != null ? overridingObject.name : "PendingInstance";
			string targetName = target.name;
			string targetCategory = "Unknown";
			PlaceableDefinition targetDefinition = null;

			foreach (var entry in placedObjects)
			{
				if (entry.Key != target)
					continue;

				targetDefinition = entry.Value.placeableDefinition;
				targetCategory = targetDefinition != null
					? targetDefinition.name
					: targetCategory;
				break;
			}

			Debug.Log($"[GridService] Override triggered: target={targetName}, targetDef={targetCategory}, overriddenBy={overridingName}, overridingObject={overridingObjectName}");

			DestroyContext ctx = DestroyContext.ForOverride(overridingDefinition, overridingObject);
			if (target.TryGetComponent<IFacility>(out var facility) &&
				FacilityManager != null &&
				FacilityManager.DestroyFacility(facility, in ctx))
			{
				continue;
			}

			if (target.TryGetComponent<IGridPlaceable>(out var gridPlaceable))
				gridPlaceable.OnDestroyedBy(in ctx);

			OnRemove(target);
		}
	}

	private bool EvaluatePlacement(in PlacementContext ctx, List<int3> possibleCell, List<int3> blocked)
	{
		bool installable = true;
		GridFootprint footprint = ctx.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;
		bool hasResolvedBuildingId = false;
		uint resolvedBuildingId = 0;

		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				FootprintCell footprintCell = footprint.Get(x, z);
				if (IsEmptyFootprintCell(footprintCell))
					continue;

				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, ctx.facingDirection);
				int3 target = ctx.center + rotatedOffset;

				if (gridMap.IsInBound(target) == false)
				{
					installable = false;
					continue;
				}

				GridCell targetCell = Map[target.x, target.y, target.z];
				bool canPlace = targetCell.CanPlaceObject || CanOverride(footprintCell, targetCell);
				bool meetsEnvironment = DoesFootprintCellMeetPlacementRequirement(
					footprintCell,
					targetCell,
					ref hasResolvedBuildingId,
					ref resolvedBuildingId);

				if (canPlace && meetsEnvironment)
				{
					possibleCell?.Add(target);
					continue;
				}

				blocked?.Add(target);
				installable = false;
			}
		}

		return installable;
	}

	private bool DoesFootprintCellMeetPlacementRequirement(
		in FootprintCell footprintCell,
		GridCell targetCell,
		ref bool hasResolvedBuildingId,
		ref uint resolvedBuildingId)
	{
		if (targetCell == null)
			return false;

		uint targetBuildingId = targetCell.BuildingId;
		if (DoesCellEnvironmentRequirementMatch(targetBuildingId, footprintCell.environmentRequirement) == false)
			return false;

		if ((footprintCell.flags & GridFlags.Interaction) != 0)
			return true;

		if (footprintCell.environmentRequirement != FootprintCellEnvironmentRequirement.Any)
			return true;

		if (hasResolvedBuildingId == false)
		{
			hasResolvedBuildingId = true;
			resolvedBuildingId = targetBuildingId;
			return true;
		}

		return resolvedBuildingId == targetBuildingId;
	}

	private static bool DoesCellEnvironmentRequirementMatch(uint buildingId, FootprintCellEnvironmentRequirement requirement)
	{
		return requirement switch
		{
			FootprintCellEnvironmentRequirement.Any => true,
			FootprintCellEnvironmentRequirement.Indoor => buildingId != 0,
			FootprintCellEnvironmentRequirement.Outdoor => buildingId == 0,
			_ => true,
		};
	}

	private uint ResolveOwningBuildingId(in PlacementContext ctx)
	{
		GridFootprint footprint = ctx.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;

		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				FootprintCell footprintCell = footprint.Get(x, z);
				if (IsEmptyFootprintCell(footprintCell) || (footprintCell.flags & GridFlags.Interaction) != 0)
					continue;

				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, ctx.facingDirection);
				int3 target = ctx.center + rotatedOffset;
				GridCell targetCell = GetCell(target);
				if (targetCell != null)
					return targetCell.BuildingId;
			}
		}

		return 0;
	}

	private void RecalculateSpaceRegions()
	{
		if (Map == null)
			return;

		int3 size = MapSize;
		if (size.x <= 0 || size.y <= 0 || size.z <= 0)
			return;

		bool[,,] visited = new bool[size.x, size.y, size.z];
		Queue<int3> queue = new();

		for (int x = 0; x < size.x; ++x)
		{
			for (int y = 0; y < size.y; ++y)
			{
				for (int z = 0; z < size.z; ++z)
				{
					GridCell cell = Map[x, y, z];
					cell.SetRegionId(cell.SealsSpace ? BoundaryRegionId : OutdoorRegionId);
				}
			}
		}

		EnqueueOutdoorBoundaryCells(size, visited, queue);
		FloodFill(queue, visited, OutdoorRegionId);

		for (int x = 0; x < size.x; ++x)
		{
			for (int y = 0; y < size.y; ++y)
			{
				for (int z = 0; z < size.z; ++z)
				{
					int3 pos = new(x, y, z);
					if (visited[x, y, z] || IsSpaceRegionBlocked(pos))
						continue;

					queue.Enqueue(pos);
					visited[x, y, z] = true;
					FloodFill(queue, visited, IndoorRegionId);
				}
			}
		}

		for (int x = 0; x < size.x; ++x)
			for (int z = 0; z < size.z; ++z)
				gridBoundary[z * MapSize.x + x] = (short)(Map[x, 0, z].RegionId);

		gridBoundaryTexture.SetPixelData<short>(gridBoundary, 0, 0);
		gridBoundaryTexture.Apply(false, false);
		OnSpaceRegionsChanged?.Invoke();
	}

	private void EnqueueOutdoorBoundaryCells(int3 size, bool[,,] visited, Queue<int3> queue)
	{
		for (int y = 0; y < size.y; ++y)
		{
			for (int x = 0; x < size.x; ++x)
			{
				TryEnqueueOutdoorCell(new int3(x, y, 0), visited, queue);
				TryEnqueueOutdoorCell(new int3(x, y, size.z - 1), visited, queue);
			}

			for (int z = 0; z < size.z; ++z)
			{
				TryEnqueueOutdoorCell(new int3(0, y, z), visited, queue);
				TryEnqueueOutdoorCell(new int3(size.x - 1, y, z), visited, queue);
			}
		}
	}

	private void TryEnqueueOutdoorCell(in int3 pos, bool[,,] visited, Queue<int3> queue)
	{
		if (gridMap.IsInBound(pos) == false || visited[pos.x, pos.y, pos.z] || IsSpaceRegionBlocked(pos))
			return;

		visited[pos.x, pos.y, pos.z] = true;
		queue.Enqueue(pos);
	}

	private void FloodFill(Queue<int3> queue, bool[,,] visited, int regionId)
	{
		while (queue.Count > 0)
		{
			int3 current = queue.Dequeue();
			Map[current.x, current.y, current.z].SetRegionId(regionId);

			for (int i = 0; i < SpaceRegionDirections.Length; ++i)
			{
				int3 next = current + SpaceRegionDirections[i];
				if (gridMap.IsInBound(next) == false || visited[next.x, next.y, next.z] || IsSpaceRegionBlocked(next))
					continue;

				visited[next.x, next.y, next.z] = true;
				queue.Enqueue(next);
			}
		}
	}

	private bool IsSpaceRegionBlocked(in int3 pos)
	{
		GridCell cell = GetCell(pos);
		return cell == null || cell.SealsSpace;
	}


}
