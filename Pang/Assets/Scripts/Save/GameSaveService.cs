using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

public sealed class GameSaveService : MonoBehaviour
{
	[SerializeField] private string saveFileName = "savegame_";
	[SerializeField] private string saveFileNameDef = "savegame.json";
	private readonly string fileExt = ".json";
	[SerializeField] private bool enableDebugHotkeys = true;

	private readonly Dictionary<GameObject, int> placeableIds = new();
	private readonly Dictionary<OrderLine, int> orderLineIds = new();
	private int nextPlaceableId = 1;
	private int nextOrderLineId = 1;

	public static string SaveDirectoryPath => Application.persistentDataPath;

	private string SavePath => Path.Combine(Application.persistentDataPath, saveFileNameDef);

	private string SavePathPerSlot(int slot) => Path.Combine(Application.persistentDataPath, saveFileName + slot + fileExt);

	private GameContext Ctx => GameContext.Instance;

	private void Update()
	{
		if (enableDebugHotkeys == false || GameContext.HasInstance == false)
			return;

		// save
		if (Input.GetKeyDown(KeyCode.F5))
			SaveGame(SavePath);

		if (Input.GetKeyDown(KeyCode.F6))
			SaveGame(SavePathPerSlot(1));

		if (Input.GetKeyDown(KeyCode.F7))
			SaveGame(SavePathPerSlot(2));

		if (Input.GetKeyDown(KeyCode.F8))
			SaveGame(SavePathPerSlot(3));

		// load
		if (Input.GetKeyDown(KeyCode.F9))
			LoadGame(SavePath);

		if (Input.GetKeyDown(KeyCode.F10))
			LoadGame(SavePathPerSlot(1));

		if (Input.GetKeyDown(KeyCode.F11))
			LoadGame(SavePathPerSlot(2));

		if (Input.GetKeyDown(KeyCode.F12))
			LoadGame(SavePathPerSlot(3));
	}

	public void SaveGame(string savePath)
	{
		GameSaveData data = Capture();
		string json = JsonUtility.ToJson(data, true);
		File.WriteAllText(savePath, json);
		Debug.Log($"[Save] Game saved to {savePath}");
	}

	public bool LoadGame(string savePath)
	{
		if (TryReadSaveData(savePath, out GameSaveData data) == false)
			return false;

		Restore(data);
		Debug.Log($"[Save] Game loaded from {savePath}");
		return true;
	}

	public static IEnumerable<string> EnumerateJsonSaveFiles()
	{
		if (Directory.Exists(SaveDirectoryPath) == false)
			yield break;

		foreach (string filePath in Directory.EnumerateFiles(SaveDirectoryPath, "*.json", SearchOption.TopDirectoryOnly))
			yield return filePath;
	}

	public static bool TryReadSaveData(string savePath, out GameSaveData data)
	{
		data = null;

		if (File.Exists(savePath) == false)
		{
			Debug.LogWarning($"[Save] No save file at {savePath}");
			return false;
		}

		try
		{
			string json = File.ReadAllText(savePath);
			data = JsonUtility.FromJson<GameSaveData>(json);
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[Save] Failed to read save file at {savePath}: {ex.Message}");
			return false;
		}

		if (data == null)
		{
			Debug.LogWarning($"[Save] Failed to parse save file at {savePath}");
			return false;
		}

		return true;
	}

	private GameSaveData Capture()
	{
		placeableIds.Clear();
		orderLineIds.Clear();
		nextPlaceableId = 1;
		nextOrderLineId = 1;

		GameSaveData data = new();
		data.SavedAtUtc = DateTime.UtcNow.ToString("O");
		data.Policy.WorkSpeed = Ctx.WMSys.WorkPolicyService.CaptureState();
		data.Policy.WorkApproach = Ctx.IBWorkflowSvc.CapturePolicyState();
		data.Policy.OutboundWorkApproach = Ctx.OBWorkflowSvc.CapturePolicyState();
		data.Time = Ctx.GameTime.CaptureState();
		data.Economy = Ctx.EconomyService.CaptureState();
		data.ItemLedger = Ctx.WMSys.ItemLedger.CaptureState();
		data.Buildings = Ctx.BuildingMgr.CaptureState();
		data.BuildingFootprints = Ctx.BuildingFootprintService.CaptureState();
		data.Zones = Ctx.ZoneMgr.CaptureState();
		data.FacilityRules = Ctx.FacilityRuleMgr.CaptureState();
		data.Grid = Ctx.GridService.CaptureState();
		data.Contracts = Ctx.ContractMgr.CaptureState();
		data.Orders = Ctx.OrderMgr.CaptureState(RegisterOrderLine);
		data.OutboundPickingManifests = Ctx.OBWorkflowSvc.CapturePickingManifestState(RegisterOrderLine);
		data.DeliveryQueue = Ctx.DeliveryService.CaptureState();
		data.OrderDelivery = Ctx.OrderDelivery.CaptureState();
		data.RocketService = Ctx.RocketSvc.CaptureState();
		data.BoxRegistry = Ctx.BoxMgr.CaptureSaveData(RegisterOrderLine);
		data.WorkerManager.NextWorkerId = Ctx.WorkerMgr.NextWorkerId;
		data.WorkJobCounters.NextPickingJobId = PickingPlanner.GetNextJobId();
		data.WorkJobCounters.NextStoringJobId = StoringPlanner.GetNextJobId();

		foreach (var entry in Ctx.GridService.GetPlacedObjectsSnapshot().OrderBy(e => e.Value.center.x).ThenBy(e => e.Value.center.y).ThenBy(e => e.Value.center.z))
		{
			data.Placeables.Add(CapturePlaceable(entry.Key, entry.Value));
		}

		data.Tasks.AddRange(CaptureTasks(Ctx.TaskMgr.TaskQueue, false));
		data.Tasks.AddRange(CaptureTasks(Ctx.TaskMgr.TaskOnProgress, true));
		return data;
	}

	private void Restore(GameSaveData data)
	{
		Dictionary<int, GameObject> restoredPlaceables = new();
		Dictionary<int, OrderLine> restoredOrderLines = new();
		Dictionary<uint, AIWorker> workersById = new();

		Ctx.TaskMgr.ResetRuntimeState();
		Ctx.OrderDelivery.ResetRuntimeState();
		Ctx.ContractMgr.ResetRuntimeState();
		Ctx.OrderMgr.ResetRuntimeState();
		Ctx.DeliveryService.ResetRuntimeState();
		Ctx.IBWorkflowSvc.ResetRuntimeState();
		Ctx.OBWorkflowSvc.ResetRuntimeState();
		Ctx.WorkerMgr.ResetRuntimeState();
		Ctx.ZoneMgr.ResetRuntimeState();
		Ctx.FacilityRuleMgr.ResetRuntimeState();
		Ctx.BuildingMgr.ResetRuntimeState();
		Ctx.AirlockSvc.ResetRuntimeState();
		Ctx.FacilityMgr.ResetRuntimeState();
		Ctx.StorageService.ResetRuntimeState();
		Ctx.BuildingFootprintService.ResetRuntimeState();
		Ctx.WMSys.WorkPolicyService.ResetRuntimeState();
		Ctx.WMSys.ItemLedger.ResetRuntimeState();
		Ctx.BoxMgr.DestroyAllBoxes();
		Ctx.BoxMgr.ResetRuntimeState();
		Ctx.GridService.ResetRuntimeState();
		Ctx.RocketSvc.ResetRuntimeState();
		Ctx.OBWorkflowSvc.PackingStationService.ResetRuntimeState();

		Ctx.GridService.RestoreState(data.Grid);
		Ctx.BuildingFootprintService.RestoreState(data.Buildings, data.BuildingFootprints);
		Ctx.ZoneMgr.RestoreState(data.Zones);
		Ctx.FacilityRuleMgr.RestoreState(data.FacilityRules);
		Ctx.WMSys.WorkPolicyService.RestoreState(data.Policy != null ? data.Policy.WorkSpeed : null);
		Ctx.IBWorkflowSvc.RestorePolicyState(data.Policy != null ? data.Policy.WorkApproach : null);
		Ctx.OBWorkflowSvc.RestorePolicyState(data.Policy != null ? data.Policy.OutboundWorkApproach : null);
		Ctx.GameTime.RestoreState(data.Time);
		Ctx.EconomyService.RestoreState(data.Economy);
		Ctx.ContractMgr.RestoreState(data.Contracts);
		Ctx.OrderMgr.RestoreState(data.Orders, Ctx.ContractMgr, restoredOrderLines);
		Ctx.BoxMgr.RestoreSaveData(data.BoxRegistry, restoredOrderLines);
		Ctx.OBWorkflowSvc.RestorePickingManifestState(data.OutboundPickingManifests, restoredOrderLines);

		foreach (PlaceableSaveData placeableData in data.Placeables.Where(p => p.IsWorker == false))
			InstantiatePlaceable(placeableData, restoredPlaceables, workersById, restoredOrderLines);

		foreach (PlaceableSaveData placeableData in data.Placeables.Where(p => p.IsWorker))
			InstantiatePlaceable(placeableData, restoredPlaceables, workersById, restoredOrderLines);

		Ctx.BuildingMgr.RestoreBuildingLinks(data.Buildings);

		Ctx.WMSys.ItemLedger.RestoreState(data.ItemLedger);
		Ctx.DeliveryService.RestoreState(data.DeliveryQueue, Ctx.ItemDB);
		Ctx.OrderDelivery.RestoreState(data.OrderDelivery);
		Ctx.RocketSvc.RestoreState(data.RocketService);
		Ctx.WorkerMgr.SetNextWorkerId(data.WorkerManager.NextWorkerId);
		PickingPlanner.SetNextJobId(data.WorkJobCounters.NextPickingJobId);
		StoringPlanner.SetNextJobId(data.WorkJobCounters.NextStoringJobId);

		foreach (PlaceableSaveData placeableData in data.Placeables)
		{
			if (placeableData.PackingStation == null)
				continue;

			if (restoredPlaceables.TryGetValue(placeableData.SaveId, out var placeableObj) == false)
				continue;

			if (placeableObj.TryGetComponent<PackingStation>(out var packingStation) == false)
				continue;

			packingStation.RestoreWorkerBindings(workersById, placeableData.PackingStation);
		}

		foreach (TaskSaveData taskData in data.Tasks)
		{
			WorkerTask task = CreateTask(taskData, restoredPlaceables, restoredOrderLines);
			if (task == null)
				continue;

			if (taskData.IsInProgress)
			{
				if (workersById.TryGetValue(taskData.AssignedWorkerId, out var worker) == false)
				{
					Debug.LogWarning($"[Save] Missing worker {taskData.AssignedWorkerId} for in-progress task {taskData.TaskType}");
					continue;
				}

				worker.SetTask(task);
				Ctx.TaskMgr.AddRestoredInProgressTask(task);
			}
			else
			{
				Ctx.TaskMgr.EnqueueTask(task);
			}
		}

		Ctx.WorkerMgr.RebuildWorkerStatusCaches();
		Ctx.FacilityRuleMgr.RebuildAppliedFacilityLookup();
		Ctx.FacilityRuleOverlay?.RefreshOverlay();
	}

	private PlaceableSaveData CapturePlaceable(GameObject obj, PlacementContext ctx)
	{
		PlaceableSaveData data = new();
		data.SaveId = RegisterPlaceable(obj);
		data.PlaceableId = ctx.placeableDefinition != null ? ctx.placeableDefinition.placeableID : string.Empty;
		data.FacingDirection = ctx.facingDirection;
		data.GridPosition = ToSave(ctx.center);
		if (obj.TryGetComponent<IFacility>(out var facility))
			data.FacilityRulePresetId = facility.FacilityRulePresetId;

		if (obj.TryGetComponent<AIWorker>(out var worker))
		{
			data.IsWorker = true;
			data.Worker = worker.CaptureState();
		}

		if (obj.TryGetComponent<ShelfBase>(out var shelf))
		{
			data.Shelf = shelf.CaptureState(RegisterOrderLine);
		}

		if (obj.TryGetComponent<CargoPort>(out var cargoPort))
		{
			data.CargoPort = cargoPort.CaptureState(GetPlaceableIdOrDefault);
		}

		if (obj.TryGetComponent<BoxPool>(out var boxPool))
		{
			data.BoxPool = boxPool.CaptureState();
		}

		if (obj.TryGetComponent<CapsuleBuffer>(out var capsuleBuffer))
		{
			data.CapsuleBuffer = capsuleBuffer.CaptureState();
		}

		if (obj.TryGetComponent<PackingStation>(out var packingStation))
		{
			data.PackingStation = packingStation.CaptureState(RegisterOrderLine, GetPlaceableIdOrDefault);
		}

		if (obj.TryGetComponent<Rocket>(out var rocket))
		{
			data.Rocket = rocket.CaptureState();
		}

		if (obj.TryGetComponent<LaunchStation>(out var launchStation))
		{
			data.LaunchStation = launchStation.CaptureState();
		}

		return data;
	}

	private IEnumerable<TaskSaveData> CaptureTasks(IReadOnlyDictionary<WorkerTask.TaskType, LinkedList<WorkerTask>> source, bool isInProgress)
	{
		foreach (var list in source.Values)
		{
			foreach (WorkerTask task in list)
			{
				TaskSaveData taskData = new();
				taskData.TaskType = task.Type;
				taskData.IsInProgress = isInProgress;
				taskData.AssignedWorkerId = task.OccupyWorker != null ? task.OccupyWorker.WorkerID : 0;

				switch (task)
				{
					case UnloadingTask unloading:
						taskData.Unloading = unloading.CaptureState(GetPlaceableIdOrDefault);
						break;
					case LoadingTask loading:
						taskData.Loading = loading.CaptureState(GetPlaceableIdOrDefault);
						break;
					case PickingTask picking:
						taskData.Picking = picking.CaptureState(GetPlaceableIdOrDefault, RegisterOrderLine);
						break;
					case StoringTask storing:
						taskData.Storing = storing.CaptureState(GetPlaceableIdOrDefault, RegisterOrderLine);
						break;
					case CapsuleRelocationTask capsuleRelocation:
						taskData.CapsuleTransfer = capsuleRelocation.CaptureState(GetPlaceableIdOrDefault);
						break;
					case CargoTransferTask cargoTransfer:
						taskData.CargoTransfer = cargoTransfer.CaptureState(GetPlaceableIdOrDefault);
						break;
					case PackingTask packing:
						taskData.Packing = packing.CaptureState(GetPlaceableIdOrDefault);
						break;
				}

				yield return taskData;
			}
		}
	}

	private void InstantiatePlaceable(
		PlaceableSaveData save,
		Dictionary<int, GameObject> restoredPlaceables,
		Dictionary<uint, AIWorker> workersById,
		Dictionary<int, OrderLine> restoredOrderLines)
	{
		PlaceableDefinition definition = Ctx.PlaceableCatalog.FindById(save.PlaceableId);
		if (definition == null || definition.prefab == null)
		{
			Debug.LogWarning($"[Save] Missing placeable definition {save.PlaceableId}");
			return;
		}

		GameObject obj = Instantiate(definition.prefab);
		PlacementContext context = new(FromSave(save.GridPosition), save.FacingDirection, definition, PlacementEvent.Load, obj);
		if (Ctx.GridService.OnInstall(context) == false)
		{
			Destroy(obj);
			Debug.LogWarning($"[Save] Failed to restore placeable {save.PlaceableId}");
			return;
		}

		restoredPlaceables[save.SaveId] = obj;

		if (obj.TryGetComponent<IFacility>(out var facility))
		{
			if (save.FacilityRulePresetId == FacilityRuleManager.NoRulePresetId)
			{
				facility.SetFacilityRulePresetId(FacilityRuleManager.NoRulePresetId);
			}
			else if (Ctx.FacilityRuleMgr.ApplyPreset(facility, save.FacilityRulePresetId) == false)
			{
				facility.SetFacilityRulePresetId(FacilityRuleManager.NoRulePresetId);
				Debug.LogWarning($"[Save] Missing facility rule preset {save.FacilityRulePresetId} for {save.PlaceableId}.");
			}
		}

		if (obj.TryGetComponent<ShelfBase>(out var shelf) && save.Shelf != null)
		{
			shelf.RestoreState(save.Shelf, restoredOrderLines);
		}

		if (obj.TryGetComponent<CargoPort>(out var cargoPort) && save.CargoPort != null)
		{
			cargoPort.RestoreState(save.CargoPort);
		}

		if (obj.TryGetComponent<BoxPool>(out var boxPool) && save.BoxPool != null)
		{
			boxPool.RestoreState(save.BoxPool);
		}

		if (obj.TryGetComponent<CapsuleBuffer>(out var capsuleBuffer) && save.CapsuleBuffer != null)
		{
			capsuleBuffer.RestoreState(save.CapsuleBuffer);
		}

		if (obj.TryGetComponent<PackingStation>(out var packingStation) && save.PackingStation != null)
		{
			packingStation.RestoreState(save.PackingStation, restoredOrderLines, restoredPlaceables);
			packingStation.InitializeForSaveLoad();
		}

		if (obj.TryGetComponent<Rocket>(out var rocket) && save.Rocket != null)
		{
			rocket.RestoreState(save.Rocket);
		}

		if (obj.TryGetComponent<LaunchStation>(out var launchStation) && save.LaunchStation != null)
		{
			launchStation.RestoreState(save.LaunchStation);
			launchStation.InitializeForSaveLoad();
		}

		if (obj.TryGetComponent<AIWorker>(out var worker) && save.Worker != null)
		{
			worker.RestoreState(save.Worker);
			worker.InitializeForSaveLoad(preserveWorkerId: true);
			workersById[worker.WorkerID] = worker;
		}
	}

	private WorkerTask CreateTask(TaskSaveData taskData, Dictionary<int, GameObject> restoredPlaceables, Dictionary<int, OrderLine> restoredOrderLines)
	{
		switch (taskData.TaskType)
		{
			case WorkerTask.TaskType.Unloading:
				return taskData.CapsuleTransfer?.Restore(restoredPlaceables) ?? taskData.Unloading?.Restore(restoredPlaceables);
			case WorkerTask.TaskType.IB:
			case WorkerTask.TaskType.CapsuleClear:
			case WorkerTask.TaskType.CapsuleSupply:
			case WorkerTask.TaskType.OB:
				return taskData.CapsuleTransfer?.Restore(restoredPlaceables);
			case WorkerTask.TaskType.CargoTransfer:
				return taskData.CapsuleTransfer?.Restore(restoredPlaceables) ?? taskData.CargoTransfer?.Restore(restoredPlaceables);
			case WorkerTask.TaskType.Loading:
				return taskData.Loading?.Restore(restoredPlaceables);
			case WorkerTask.TaskType.Picking:
				return taskData.Picking?.Restore(restoredPlaceables, restoredOrderLines);
			case WorkerTask.TaskType.Storing:
				return taskData.CapsuleTransfer?.Restore(restoredPlaceables) ?? taskData.Storing?.Restore(restoredPlaceables, restoredOrderLines);
			case WorkerTask.TaskType.Packing:
				return taskData.Packing?.Restore(restoredPlaceables);
			case WorkerTask.TaskType.Labeling:
				return null;
			default:
				return null;
		}
	}

	private int RegisterPlaceable(GameObject obj)
	{
		if (obj == null)
			return -1;

		if (placeableIds.TryGetValue(obj, out int id))
			return id;

		id = nextPlaceableId++;
		placeableIds[obj] = id;
		return id;
	}

	private int RegisterOrderLine(OrderLine line)
	{
		if (line == null)
			return -1;

		if (orderLineIds.TryGetValue(line, out int id))
			return id;

		id = nextOrderLineId++;
		orderLineIds[line] = id;
		return id;
	}

	private int GetPlaceableIdOrDefault(GameObject obj)
	{
		return obj == null ? -1 : RegisterPlaceable(obj);
	}

	private static Int3SaveData ToSave(int3 value) => new(value.x, value.y, value.z);
	private static int3 FromSave(Int3SaveData value) => new(value.X, value.Y, value.Z);

	private sealed class WorkLineRestoreBuilder
	{
	}
}

public static class TaskSaveDataExtensions
{
	public static UnloadingTask Restore(this UnloadingTaskSaveData data, Dictionary<int, GameObject> placeables)
	{
		if (data == null ||
			placeables.TryGetValue(data.TargetRocketId, out var rocketObj) == false ||
			rocketObj.TryGetComponent<Rocket>(out var rocket) == false ||
			placeables.TryGetValue(data.CargoPortId, out var portObj) == false ||
			portObj.TryGetComponent<CargoPort>(out var cargoPort) == false)
		{
			return null;
		}

		UnloadingTask task = new(rocket, cargoPort);
		task.RestoreState(cargoPort, data.IsUnloadEnd);
		return task;
	}

	public static LoadingTask Restore(this LoadingTaskSaveData data, Dictionary<int, GameObject> placeables)
	{
		if (data == null ||
			placeables.TryGetValue(data.TargetPortId, out var portObj) == false ||
			portObj.TryGetComponent<CargoPort>(out var cargoPort) == false ||
			placeables.TryGetValue(data.TargetStationId, out var stationObj) == false ||
			stationObj.TryGetComponent<LaunchStation>(out var targetStation) == false)
		{
			return null;
		}

		LoadingTask task = new(cargoPort, targetStation);
		task.RestoreState(data.IsLoadEnd);
		return task;
	}

	public static PickingTask Restore(this PickingTaskSaveData data, Dictionary<int, GameObject> placeables, Dictionary<int, OrderLine> orderLines)
	{
		if (data?.Job == null)
			return null;

		WorkJob job = data.Job.Restore(placeables, orderLines);
		if (job == null)
			return null;

		PickingTask task = new(job, data.BuildingId);
		WorkLine currentPlaceLine = data.CurrentPlaceLine != null ? data.CurrentPlaceLine.Restore(placeables, orderLines) : null;
		task.RestoreState(data.BuildingId, data.IsPickingPhaseEnd, data.IsTaskEnd, currentPlaceLine, data.PlacingLineIndex);
		return task;
	}

	public static StoringTask Restore(this StoringTaskSaveData data, Dictionary<int, GameObject> placeables, Dictionary<int, OrderLine> orderLines)
	{
		if (data?.Job == null)
			return null;

		WorkJob job = data.Job.Restore(placeables, orderLines);
		if (job == null)
			return null;

		StoringTask task = new(job, data.BuildingId);
		WorkLine placingLine = data.PlacingLine != null ? data.PlacingLine.Restore(placeables, orderLines) : null;
		task.RestoreState(data.CurrentPhase, data.IsJobEnd, placingLine);
		return task;
	}

	public static WorkerTask Restore(this CapsuleTransferTaskSaveData data, Dictionary<int, GameObject> placeables)
	{
		if (data == null ||
			placeables.TryGetValue(data.SourcePlaceableId, out var sourceObj) == false ||
			placeables.TryGetValue(data.TargetPlaceableId, out var targetObj) == false)
		{
			return null;
		}

		if (sourceObj.TryGetComponent<CapsuleDock>(out var sourceDock) == false ||
			targetObj.TryGetComponent<CapsuleDock>(out var targetDock) == false)
		{
			return null;
		}

		WorkerTask.TaskType taskType = data.HasTaskType
			? data.TaskType
			: data.IsInbound ? WorkerTask.TaskType.IB : WorkerTask.TaskType.OB;
		CapsuleRelocationReason reason = taskType switch
		{
			WorkerTask.TaskType.Unloading => CapsuleRelocationReason.SourceMustClear,
			WorkerTask.TaskType.IB => CapsuleRelocationReason.SourceMustClear,
			WorkerTask.TaskType.OB => CapsuleRelocationReason.DestinationNeedsCapsule,
			WorkerTask.TaskType.CapsuleClear => CapsuleRelocationReason.StateMismatch,
			WorkerTask.TaskType.CapsuleSupply => CapsuleRelocationReason.DestinationNeedsCapsule,
			WorkerTask.TaskType.CargoTransfer => CapsuleRelocationReason.SourceMustClear,
			_ => CapsuleRelocationReason.StateMismatch,
		};
		return new CapsuleRelocationTask(taskType, sourceDock, targetDock, data.BuildingId, reason);
	}

	public static PackingTask Restore(this PackingTaskSaveData data, Dictionary<int, GameObject> placeables)
	{
		if (data == null || placeables.TryGetValue(data.TargetStationId, out var stationObj) == false || stationObj.TryGetComponent<PackingStation>(out var station) == false)
			return null;

		PackingTask task = new(station);
		task.RestoreState(data.IsTaskEnd);
		return task;
	}

	public static CargoTransferTask Restore(this CargoTransferTaskSaveData data, Dictionary<int, GameObject> placeables)
	{
		if (data == null ||
			placeables.TryGetValue(data.SourcePortId, out var sourceObj) == false ||
			sourceObj.TryGetComponent<OutboundCargoPort>(out var sourcePort) == false ||
			data.TargetPortId < 0 ||
			placeables.TryGetValue(data.TargetPortId, out var targetObj) == false ||
			targetObj.TryGetComponent<InboundCargoPort>(out var targetPort) == false)
		{
			return null;
		}

		return new CargoTransferTask(sourcePort, targetPort);
	}

	public static WorkJob Restore(this WorkJobSaveData data, Dictionary<int, GameObject> placeables, Dictionary<int, OrderLine> orderLines)
	{
		if (data == null)
			return null;

		List<WorkLine> lines = new(data.Lines.Count);
		foreach (WorkLineSaveData lineData in data.Lines)
		{
			WorkLine line = lineData.Restore(placeables, orderLines);
			if (line == null)
				return null;

			lines.Add(line);
		}

		WorkJob job = new(data.JobId, lines, data.WorkType);
		job.RestoreState(data.CurrentLineIndex, data.WorkType);
		return job;
	}

	public static WorkLine Restore(this WorkLineSaveData data, Dictionary<int, GameObject> placeables, Dictionary<int, OrderLine> orderLines)
	{
		if (data == null || placeables.TryGetValue(data.SourcePlaceableId, out var sourceObj) == false)
			return null;

		if (TryResolveWorkLineTarget(sourceObj, out IItemContainer container, out IGridPlaceable target) == false)
			return null;

		orderLines.TryGetValue(data.RelatedOrderLineId, out var relatedOrderLine);
		WorkLine line = new(data.Action, container, target, data.ItemId, data.Quantity, relatedOrderLine);
		line.CompleteQuantity = data.CompleteQuantity;
		return line;
	}

	private static bool TryResolveWorkLineTarget(GameObject sourceObj, out IItemContainer container, out IGridPlaceable target)
	{
		container = null;
		target = null;
		if (sourceObj == null)
			return false;

		Component[] components = sourceObj.GetComponents<Component>();
		for (int i = 0; i < components.Length; ++i)
		{
			if (components[i] is IItemContainer itemContainer && components[i] is IGridPlaceable placeable)
			{
				container = itemContainer;
				target = placeable;
				return true;
			}
		}

		return false;
	}
}
