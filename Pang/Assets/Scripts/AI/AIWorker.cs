using System;
using System.Collections.Generic;
using Assets.Scripts.AI.BT;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

[System.Flags]
public enum WorkerAbility
{
	None = 0,
	CarryBox = 1 << 0,
	PickingStoring = 1 << 1,
	Packing = 1 << 2,
	Labeling = 1 << 3,
	CargoHandling = 1 << 4,
	// ...
}

public enum WorkActionType
{
	PickItem,
	PutItem,
	PickBox,
	PutBox,
	PackItem,
	MoveBox,
	HandleMistake
}

public enum WorkerStatusAction
{
	None = 0,

	// 상시
	WaitingForItems,
	WaitingForTargetBuilding,
	HandlingMistake,
	Collapse,

	// 선택
	Idle,
	MovingTo,
	Resting,
	Charging,
	Working,
}

public enum WorkerStatusTarget
{
	None = 0,

	Box,
	Rocket,
	Shelf,
	CargoPort,
	BoxPool,
	PackingStation,
	LaunchStation,
	Charger,
	WorkTarget
}

public struct WorkerStatusInfo
{
	public WorkerStatusAction Action;
	public WorkerStatusTarget Target;

	public WorkerStatusInfo(WorkerStatusAction action, WorkerStatusTarget target)
	{
		Action = action;
		Target = target;
	}

	public static WorkerStatusInfo None => new(WorkerStatusAction.None, WorkerStatusTarget.None);
}

[System.Serializable]
public abstract partial class AIWorker : MonoBehaviour, IGridPlaceable, IGridPlacementEffect
{
	// worker identity
	[SerializeField] private string workerFirstName;
	[SerializeField] private string workerLastName;
	[SerializeField] private uint workerID;

	// worker ability def
	[SerializeField] private WorkerType workerType;
	[SerializeField] private WorkerAbility abilities;
	[SerializeField] private int monthlyCost;

	// base stat
	[SerializeField] private float baseMoveSpeedMultiplier = 1.0f;
	[SerializeField] private float minimumMoveSpeedMultiplier = 0.5f;
	[SerializeField] private float baseWorkSpeedMultiplier = 1.0f;
	[SerializeField] private float minimumWorkSpeedMultiplier = 0.5f;
	
	// task and bt
	[SerializeField] private int tick = 0;
	[SerializeField] private WorkerTask currentTask = null;
	[SerializeField] private WorkerTask.TaskType workerMainTaskType = WorkerTask.TaskType.Undefined;

	[Header("Visual")]
	[SerializeField] private Transform visualRoot;

	private FindRoute routeFinder;
	private BehaviorTree behaviorTree;
	private readonly BlackBoard localBlackBoard = new();
	private WorkerStatusInfo workerState = WorkerStatusInfo.None;
	private GameObject currentVisualInstance;
	private WorkerVisualDefinition currentVisualDefinition;
	private CarryBoxAbility carryingAbility;

	private int3 position;
	private FacingDirection facingDirection;

	private IInteractionPoint currentWorkingPoint = null;
	private bool isRegistered = false;

	// event
	public event System.Action<WorkerStatusAction> OnActionChanged;

	// worker identity
	public string Name
	{
		get
		{
			string displayName = $"{workerFirstName} {workerLastName}".Trim();
			return string.IsNullOrWhiteSpace(displayName) ? name : displayName;
		}
	}
	public uint WorkerID => workerID;

	// worker ability
	public WorkerType WorkerType => workerType;
	public WorkerAbility Ability => abilities;
	public int MonthlyCost => monthlyCost;

	// stat
	public float BaseMoveSpeedMultiplier => baseMoveSpeedMultiplier;
	public float MinimumMoveSpeedMultiplier => minimumMoveSpeedMultiplier;
	public float BaseWorkSpeedMultiplier => baseWorkSpeedMultiplier;
	public float MinimumWorkSpeedMultiplier => minimumWorkSpeedMultiplier;

	// grid
	public int3 GridPosition => position;
	public FacingDirection Direction => facingDirection;

	// task
	public WorkerTask CurrentTask => currentTask;
	public WorkerTask.TaskType TaskType => workerMainTaskType;
	public IInteractionPoint CurrentWorkingBuilding => currentWorkingPoint;
	public bool IsAssignedToPackingStation => currentWorkingPoint is PackingStation;
	public CarryBoxAbility CarryingAbility
	{
		get
		{
			if (carryingAbility == null)
				TryGetComponent(out carryingAbility);

			return carryingAbility;
		}
	}

	// worker show stat
	public WorkerStatusInfo WorkerState => workerState;
	public WorkerStatusTarget BuildingTarget => WorkerStatusTarget.None;

	static private WorkerManager WorkerMgr => GameContext.Instance.WorkerMgr;
	static private GridService GridService => GameContext.Instance.GridService;
	static private ZoneManager ZoneManager => GameContext.Instance.ZoneMgr;

	// worker show stat setting
	public void SetWorkerAction(WorkerStatusAction action) 
	{
		if (workerState.Action == action) return;
		workerState.Action = action;
		OnActionChanged?.Invoke(action);
	}
	public void SetWorkerTarget(WorkerStatusTarget target) => workerState.Target = target;

	// should build BT here
	private void BuildBehaviorTree()
	{
		SelectorNode root = new();

		IBaseNode workerBaseNode = BuildWorkerBaseNode();
		ActionNode performTask = new(DoWork);
		WaitNode wait = new(1.0f);

		if (workerBaseNode != null)
			root.Add(workerBaseNode);
		root.Add(performTask);
		root.Add(wait);

		behaviorTree = new BehaviorTree(root);
	}


	public void SetWorkerID(uint id) => workerID = id;

	public bool HasAbility(WorkerAbility req) => (Ability & req) == req;

	public void ApplyArchetype(WorkerArchetype archetype)
	{
		if (archetype == null)
		{
			Debug.LogError($"Worker archetype is missing on {name}");
			return;
		}

		workerFirstName = archetype.WorkerNameDefinition.WorkerFirstName;
		workerLastName = archetype.WorkerNameDefinition.WorkerLastName;
		gameObject.name = Name;

		workerType = archetype.AbilityDefinition.workerType;
		abilities = archetype.AbilityDefinition.abilities;
		monthlyCost = archetype.AbilityDefinition.monthlyCost;

		baseMoveSpeedMultiplier = archetype.WorkerBaseStat.baseMoveSpeedMultiplier;
		minimumMoveSpeedMultiplier = archetype.WorkerBaseStat.minimumMoveSpeedMultiplier;
		baseWorkSpeedMultiplier = archetype.WorkerBaseStat.baseWorkSpeedMultiplier;
		minimumWorkSpeedMultiplier = archetype.WorkerBaseStat.minimumWorkSpeedMultiplier;

		ApplyVisual(archetype.WorkerVisualDefinition);
		archetype.SetupWorker(this);
	}

	private void ApplyVisual(WorkerVisualDefinition visualDefinition)
	{
		if (currentVisualInstance != null)
		{
			Destroy(currentVisualInstance);
			currentVisualInstance = null;
		}

		currentVisualDefinition = visualDefinition;

		if (visualDefinition == null || visualDefinition.Prefab == null)
			return;

		Transform targetRoot = visualRoot != null ? visualRoot : transform;
		currentVisualInstance = Instantiate(visualDefinition.Prefab, targetRoot);
		currentVisualInstance.transform.localPosition = Vector3.zero;
		currentVisualInstance.transform.localRotation = Quaternion.identity;
		currentVisualInstance.transform.localScale = Vector3.one;

		// Keep presentation under VisualRoot so animation/presenter components can be added later
		// without mixing visual-only hierarchy concerns into gameplay/root components.
	}

	private void Start()
	{
		InitializeForSaveLoad();
	}

	public void InitializeForSaveLoad(bool preserveWorkerId = false)
	{
		if (isRegistered)
			return;

		routeFinder = transform.GetComponent<FindRoute>();

		if (routeFinder == null)
		{
			Debug.Log($"FindRoute가 null이다 해당 객체가 프리뷰가 아니라면 큰일이다, 이름: {this.name}");

			return;
		}

		// register AI's BT to AI Manager
		WorkerMgr.RegisterWorker(this, preserveWorkerId);
		isRegistered = true;

		routeFinder.SetAIMaster(this);
		BuildBehaviorTree();
	}

	private void OnDestroy()
	{
		// unregister AI
		if (isRegistered == false || GameContext.HasInstance == false)
			return;

		WorkerMgr.UnregisterWorker(this);
	}

	public bool TryAttachBox(BoxBase box)
	{
		var component = CarryingAbility;
		if (component == null)
		{
			Debug.LogError("No CarryBox Ability!!!!!!");
			return false;
		}

		//Debug.Log("Attached!");

		return component.PutBox(box);
	}

	public bool TryDetachBox(out BoxBase box)
	{
		var component = CarryingAbility;
		if (component == null)
		{
			Debug.LogError("No CarryBox Ability!!!!!!");
			box = null;
			return false;
		}

		return component.GetBox(out box);
	}

	public bool RunBT(BlackBoard GlobalBlackboard)
	{
		BTContext btx;
		btx.DeltaTime = Time.deltaTime;
		btx.LocalBlackBoard = localBlackBoard;
		btx.GlobalBlackBoard = GlobalBlackboard;
		btx.Worker = this;
		btx.Tick = tick++;

		behaviorTree?.RunBT(btx);

		return true;
	}

	public void ChangeWorkerType(WorkerTask.TaskType taskType)
	{
		workerMainTaskType = taskType;
	}

	public void SetTask(WorkerTask task)
	{
		if (GameContext.HasInstance && task != null)
			WorkerMgr.RemoveIdleWorker(this);

		task?.SetAIWorker(this);
		currentTask = task;
	}

	// for findroute only
	public void SetPosition(in int3 position)
	{
		this.position = position;
	}

	public void SetDirection(FacingDirection direction)
	{
		facingDirection = direction;
	}

	public void OnPositionSet(in int3 position, FacingDirection direction)
	{
		facingDirection = direction;
		enabled = true;
		SetPosition(position);
	}

	public void OnRemoved()
	{
		//int3 previousNode = routeFinder.PreviousNode;
		//int3 nextNode = routeFinder.NextNode;

		//Cell[,,] map = GameContext.Instance.MapResources.mapRef;

		//if (previousNode.x >= 0 && previousNode.y >= 0 && previousNode.z >= 0)
		//{
		//	Cell prevCell = map[previousNode.x, previousNode.y, previousNode.z];
		//	prevCell.type = prevCell.previousType;
		//}
		//if (nextNode.x >= 0 && nextNode.y >= 0 && nextNode.z >= 0)
		//{
		//	Cell nextCell = map[nextNode.x, nextNode.y, nextNode.z];
		//	nextCell.type = nextCell.previousType;
		//}
	}

	public void OnDestroyedBy(in DestroyContext ctx)
	{
		// 당장 생각나는거만 적음
		// 들고 있던 태스크에 대해서 실패했다고 뭔가 해줘야하고
		// 또 뭐냐 산재처리 해줘야하는데 이건 아직 미구현이니까 투두리스트로 남겨야하고
		// 뭐 기타등등 해줘야하는데
		// 폭발도 하는게 좀 간지나긴 하는데 폭발은 로케트쪽에서 해주는게 나으려나
		// 
	}

	public void OnWorkingPointSet(IInteractionPoint workingPoint)
	{
		currentWorkingPoint = workingPoint;
	}

	public bool CanAcceptGeneralTask(WorkerTask.TaskType taskType)
	{
		if (currentTask != null || TaskType != taskType)
			return false;

		if (IsAssignedToPackingStation)
			return false;

		if (NeedsRecovery() && TryFindRecoveryPoint(out _))
			return false;

		return true;
	}

	public bool CanAcceptGeneralTask(WorkerTask task)
	{
		return task != null && CanAcceptGeneralTask(task.Type);
	}

	public bool CanAcceptPreferredTask(WorkerTask task)
	{
		if (currentTask != null || task == null || TaskType != task.Type)
			return false;

		if (task is PackingTask packingTask)
		{
			PackingStation targetStation = packingTask.TargetStation;
			if (targetStation == null)
				return false;

			return targetStation.CurrentPackingWorker == this;
		}

		return CanAcceptGeneralTask(task);
	}

	public void UpdatePackingRecoveryState()
	{
		if (CurrentWorkingBuilding is PackingStation station)
			station.SetIncomingRequestSuspended(NeedsRecovery());
	}

	public bool CanLeaveAssignedStationForRecovery()
	{
		if (CurrentWorkingBuilding is not PackingStation station)
			return true;

		return station.CanAssignedWorkerLeaveForRecovery();
	}

	public bool TryCanBeginRecovery(out int3 recoveryPoint)
	{
		recoveryPoint = default;

		if (NeedsRecovery() == false)
			return false;

		if (CanLeaveAssignedStationForRecovery() == false)
			return false;

		return TryFindRecoveryPoint(out recoveryPoint);
	}

	public void BeginRecovery()
	{
		if (CurrentWorkingBuilding is PackingStation station)
		{
			station.CurrentPackingWorker = null;
			station.RefreshWaitingState();
		}
	}

	public bool TryFindRecoveryPoint(out int3 recoveryPoint)
	{
		recoveryPoint = default;

		if (ZoneManager == null)
			return false;

		if (ZoneManager.TryGetZones(out var zones, GridPosition.y, GetRecoveryZoneType()) == false)
			return false;

		int startIndex = UnityEngine.Random.Range(0, zones.Count);
		for (int i = 0; i < zones.Count; ++i)
		{
			var zone = zones[(startIndex + i) % zones.Count];
			if (TryFindPointInZone(zone, out recoveryPoint))
				return true;
		}

		return false;
	}

	private bool TryFindPointInZone(ZoneArea zone, out int3 recoveryPoint)
	{
		for (int i = 0; i < 8; ++i)
		{
			zone.GetRandomPoint(out var candidate);
			if (IsRecoveryPointAvailable(candidate))
			{
				recoveryPoint = candidate;
				return true;
			}
		}

		for (int z = zone.Bounds.yMin; z < zone.Bounds.yMax; ++z)
		{
			for (int x = zone.Bounds.xMin; x < zone.Bounds.xMax; ++x)
			{
				var candidate = new int3(x, zone.Floor, z);
				if (IsRecoveryPointAvailable(candidate))
				{
					recoveryPoint = candidate;
					return true;
				}
			}
		}

		recoveryPoint = default;
		return false;
	}

	private bool IsRecoveryPointAvailable(in int3 candidate)
	{
		var cell = GridService?.GetCell(candidate);
		if (cell == null || cell.IsBlocked)
			return false;

		return cell.CanPlaceObject || candidate.Equals(GridPosition);
	}

	public virtual float GetWorkSpeedMultiplier() { return 1.0f; }
	public virtual float GetMoveSpeedMultiplier() { return 1.0f; }
	public virtual void OnTaskCompleted() { }
	public virtual void TickVitals(float deltaTime) { }
	public abstract bool NeedsRecovery();
	public abstract bool IsRecoveryComplete();
	public abstract void TickRecovery(float deltaTime);
	public abstract WorkerStatusAction GetRecoveryAction();
	public abstract ZoneType GetRecoveryZoneType();

	public abstract void AddFatigue(float fatigue);
	public abstract float GetFatigue();

	// decreased chance by researches or some pieces of equipment
	public virtual float GetIncidentMitigationMultiplier() { return 1.0f; }

	public WorkerSaveData CaptureState(Func<BoxBase, uint> registerBox)
	{
		WorkerSaveData data = new()
		{
			WorkerId = workerID,
			FirstName = workerFirstName,
			LastName = workerLastName,
			WorkerType = workerType,
			Abilities = abilities,
			MonthlyCost = monthlyCost,
			VisualId = currentVisualDefinition != null ? currentVisualDefinition.VisualId : string.Empty,
			BaseMoveSpeedMultiplier = baseMoveSpeedMultiplier,
			MinimumMoveSpeedMultiplier = minimumMoveSpeedMultiplier,
			BaseWorkSpeedMultiplier = baseWorkSpeedMultiplier,
			MinimumWorkSpeedMultiplier = minimumWorkSpeedMultiplier,
			MainTaskType = workerMainTaskType,
			StatusAction = workerState.Action,
			StatusTarget = workerState.Target,
			CarryingBoxId = 0,
		};

		var carryBoxAbility = CarryingAbility;
		if (carryBoxAbility != null &&
			carryBoxAbility.CarryingBox != null &&
			registerBox != null)
		{
			data.CarryingBoxId = registerBox(carryBoxAbility.CarryingBox);
		}

		CaptureSubclassState(data);
		return data;
	}

	public void RestoreState(WorkerSaveData data, Dictionary<uint, BoxBase> restoredBoxes)
	{
		if (data == null)
			return;

		workerFirstName = data.FirstName;
		workerLastName = data.LastName;
		workerID = data.WorkerId;
		workerType = data.WorkerType;
		abilities = data.Abilities;
		monthlyCost = data.MonthlyCost;
		baseMoveSpeedMultiplier = data.BaseMoveSpeedMultiplier;
		minimumMoveSpeedMultiplier = data.MinimumMoveSpeedMultiplier;
		baseWorkSpeedMultiplier = data.BaseWorkSpeedMultiplier;
		minimumWorkSpeedMultiplier = data.MinimumWorkSpeedMultiplier;
		workerMainTaskType = data.MainTaskType;
		workerState = new WorkerStatusInfo(data.StatusAction, data.StatusTarget);
		tick = 0;

		if (string.IsNullOrWhiteSpace(data.VisualId) == false)
			ApplyVisual(GameContext.Instance.WorkerVisualCatalog?.FindById(data.VisualId));

		EnsureAbilitiesConfigured();
		RestoreSubclassState(data);

		if (data.CarryingBoxId > 0 && restoredBoxes.TryGetValue(data.CarryingBoxId, out var box))
			TryAttachBox(box);
	}

	protected virtual void CaptureSubclassState(WorkerSaveData data) { }
	protected virtual void RestoreSubclassState(WorkerSaveData data) { }

	private void EnsureAbilitiesConfigured()
	{
		if (abilities.HasFlag(WorkerAbility.CargoHandling) && GetComponent<CargoHandlingAbility>() == null)
			gameObject.AddComponent<CargoHandlingAbility>();
		if (abilities.HasFlag(WorkerAbility.CarryBox))
		{
			if (CarryingAbility == null)
				carryingAbility = gameObject.AddComponent<CarryBoxAbility>();
		}
		if (abilities.HasFlag(WorkerAbility.Labeling) && GetComponent<LabelingAbility>() == null)
			gameObject.AddComponent<LabelingAbility>();
		if (abilities.HasFlag(WorkerAbility.Packing) && GetComponent<PackageAbility>() == null)
			gameObject.AddComponent<PackageAbility>();
		if (abilities.HasFlag(WorkerAbility.PickingStoring) && GetComponent<PickStoreAbility>() == null)
			gameObject.AddComponent<PickStoreAbility>();
	}
}
