using System;
using System.Collections.Generic;
using Assets.Scripts.AI.BT;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

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
	PickItem = 0,
	PutItem = 1,
	PickBox = 2,
	PutBox = 3,
	PackItem = 4,
	MoveBox = 5,
	HandleMistake = 6,
	LabelItem = 7,
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
	UsingAirlock,
	Resting,
	Charging,
	Working,
	TrafficBlock,
	Knockout,
	Death,
	Malfunction,
}

public enum WorkerOperationalState
{
	Active,
	Knockout,
	Death,
	Malfunction,
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
	WorkTarget,
	Airlock,
	CapsuleBuffer,
	PowerPort,
	PowerHub,
	RefrigerationUnit,
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
public abstract partial class AIWorker : MonoBehaviour, IGridPlaceable, IGridPlacementEffect, IHealth
{
	// worker identity
	[SerializeField] private string workerFirstName;
	[SerializeField] private string workerLastName;
	[SerializeField] private uint workerID;

	// worker ability def
	[FormerlySerializedAs("workerType")]
	[SerializeField] private LegacyWorkerIdentityType legacyWorkerIdentityType;
	[SerializeField] private WorkerKind workerKind = WorkerKind.Human;
	[SerializeField] private HumanType humanType = HumanType.FullTime;
	[SerializeField] private RobotType robotType = RobotType.Transfer;
	[SerializeField] private bool identityInitialized;
	[SerializeField] private WorkerAbility abilities;
	[SerializeField] private int monthlyCost;
	[SerializeField] private int hiredAtElapsedWeek = -1;
	[SerializeField] private int itemDamageIncidentCount;
	[SerializeField] private HealthState health = new();
	[SerializeField] private WorkerOperationalState operationalState = WorkerOperationalState.Active;

	// base stat
	[SerializeField] private float baseMoveSpeedMultiplier = 1.0f;
	[SerializeField] private float minimumMoveSpeedMultiplier = 0.5f;
	[SerializeField] private float baseWorkSpeedMultiplier = 1.0f;
	[SerializeField] private float minimumWorkSpeedMultiplier = 0.5f;
	
	// task and bt
	[SerializeField] private int tick = 0;
	[SerializeField] private WorkerTask currentTask = null;
	[SerializeField] private WorkerTask.TaskType workerMainTaskType = WorkerTask.TaskType.Undefined;
	[SerializeField] private List<WorkerTask.TaskType> workerAssignedTaskTypes = new();
	[SerializeField] private uint primaryBuildingId = 0;

	[Header("Visual")]
	[SerializeField] private Transform visualRoot;

	private FindRoute routeFinder;
	private BehaviorTree behaviorTree;
	private readonly BlackBoard localBlackBoard = new();
	private WorkerStatusInfo workerState = WorkerStatusInfo.None;
	private WorkerStatusAction preTrafficAction = WorkerStatusAction.None;
	private GameObject currentVisualInstance;
	private WorkerVisualDefinition currentVisualDefinition;
	private CarryBoxAbility carryingAbility;
	private Transform currentVisualCarrySlot;
	private Transform currentVisualStatusSlot;
	private Transform defaultCarrySlot;

	private int3 position;
	private FacingDirection facingDirection;

	private IInteractionPoint currentWorkingPoint = null;
	private bool isRegistered = false;
	private bool isTrafficBlocked = false;

	// event
	public event System.Action<WorkerStatusAction> OnActionChanged;
	public event System.Action<AIWorker, WorkerStatusAction, WorkerStatusAction> OnStatusChanged;
	public event System.Action<AIWorker, WorkerTask.TaskType, WorkerTask.TaskType> OnTaskTypeChanged;
	public event System.Action<AIWorker, bool> OnTrafficBlockChanged;
	public event System.Action<AIWorker, WorkerOperationalState, WorkerOperationalState> OnOperationalStateChanged;

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
	public WorkerKind WorkerKind
	{
		get
		{
			EnsureIdentityInitialized();
			return workerKind;
		}
	}
	public HumanType HumanType
	{
		get
		{
			EnsureIdentityInitialized();
			return humanType;
		}
	}
	public RobotType RobotType
	{
		get
		{
			EnsureIdentityInitialized();
			return robotType;
		}
	}
	public WorkerPolicyType WorkerPolicyType
	{
		get
		{
			EnsureIdentityInitialized();

			if (workerKind == WorkerKind.Robot)
				return WorkerPolicyType.RobotTransfer;

			return humanType switch
			{
				HumanType.PartTime => WorkerPolicyType.HumanPartTime,
				HumanType.Illegal => WorkerPolicyType.HumanIllegal,
				_ => WorkerPolicyType.HumanFullTime,
			};
		}
	}
	public WorkerAbility Ability => abilities;
	public int MonthlyCost => monthlyCost;
	public int HiredAtElapsedWeek => Mathf.Max(0, hiredAtElapsedWeek);
	public int ItemDamageIncidentCount => itemDamageIncidentCount;
	public float Health => health.Health;
	public float MaxHealth => health.MaxHealth;
	public WorkerOperationalState OperationalState => operationalState;
	public bool IsOperational => operationalState == WorkerOperationalState.Active;

	public float ApplyDamage(float amount)
	{
		float applied = health.ApplyDamage(amount);
		if (applied > 0.0f && health.Health <= 0.0f)
		{
			EnterIncapacitatedState(WorkerKind == WorkerKind.Robot
				? WorkerOperationalState.Malfunction
				: WorkerOperationalState.Death);
		}

		return applied;
	}
	public void RestoreHealth(float value) => health.RestoreHealth(value);

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
	public IReadOnlyList<WorkerTask.TaskType> AssignedTaskTypes => workerAssignedTaskTypes;
	public uint PrimaryBuildingId => primaryBuildingId;
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
	public WorkerStatusAction EffectiveStatusAction => isTrafficBlocked ? preTrafficAction : workerState.Action;
	public WorkerStatusTarget BuildingTarget => WorkerStatusTarget.None;
	public bool IsTrafficBlocked => isTrafficBlocked;
	public FindRoute RouteFinder => routeFinder;
	public Transform CarrySlot => ResolveCarrySlot();
	public Transform StatusSlot => currentVisualStatusSlot;

	static private WorkerManager WorkerMgr => GameContext.Instance.WorkerMgr;
	static private GridService GridService => GameContext.Instance.GridService;
	// worker show stat setting
	public void SetWorkerAction(WorkerStatusAction action) 
	{
		if (isTrafficBlocked)
		{
			if (preTrafficAction == action)
				return;

			WorkerStatusAction previousAction = preTrafficAction;
			preTrafficAction = action;
			OnStatusChanged?.Invoke(this, previousAction, action);
			return;
		}

		if (workerState.Action == action)
			return;

		WorkerStatusAction oldAction = workerState.Action;
		workerState.Action = action;
		preTrafficAction = action;
		OnActionChanged?.Invoke(action);
		OnStatusChanged?.Invoke(this, oldAction, action);
	}
	public void SetWorkerTarget(WorkerStatusTarget target) => workerState.Target = target;

	public void BeginTrafficBlock()
	{
		if (isTrafficBlocked)
			return;

		isTrafficBlocked = true;
		preTrafficAction = workerState.Action;
		workerState.Action = WorkerStatusAction.TrafficBlock;
		OnActionChanged?.Invoke(workerState.Action);
		OnTrafficBlockChanged?.Invoke(this, true);
	}

	public void EndTrafficBlock()
	{
		if (isTrafficBlocked == false)
			return;

		isTrafficBlocked = false;
		workerState.Action = preTrafficAction;
		OnActionChanged?.Invoke(workerState.Action);
		OnTrafficBlockChanged?.Invoke(this, false);
	}

	// should build BT here
	private void BuildBehaviorTree()
	{
		SelectorNode root = new();
		root.Add(new ActionNode(HoldIncapacitatedState));

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

		archetype.AbilityDefinition.EnsureIdentityInitialized();
		if (archetype.AbilityDefinition.WorkerKind == WorkerKind.Robot)
			SetRobotIdentity(archetype.AbilityDefinition.RobotType);
		else
			SetHumanIdentity(archetype.AbilityDefinition.HumanType);
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
		currentVisualCarrySlot = null;
		currentVisualStatusSlot = null;

		if (visualDefinition == null || visualDefinition.Prefab == null)
			return;

		Transform targetRoot = visualRoot != null ? visualRoot : transform;
		currentVisualInstance = Instantiate(visualDefinition.Prefab, targetRoot);
		currentVisualInstance.transform.localPosition = Vector3.zero;
		currentVisualInstance.transform.localRotation = Quaternion.identity;
		currentVisualInstance.transform.localScale = Vector3.one;
		currentVisualCarrySlot = FindVisualSlot(currentVisualInstance.transform, "CarrySlot");
		currentVisualStatusSlot = FindVisualSlot(currentVisualInstance.transform, "StatusSlot");

		// Keep presentation under VisualRoot so animation/presenter components can be added later
		// without mixing visual-only hierarchy concerns into gameplay/root components.
	}

	private Transform ResolveCarrySlot()
	{
		if (currentVisualCarrySlot != null)
			return currentVisualCarrySlot;

		if (defaultCarrySlot == null)
			defaultCarrySlot = transform.Find("SlotRoot");

		return defaultCarrySlot;
	}

	private static Transform FindVisualSlot(Transform root, string slotName)
	{
		if (root == null)
			return null;

		foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
		{
			if (child.name == slotName)
				return child;
		}

		return null;
	}

	private void Start()
	{
		EnsureEmploymentInitialized();
		InitializeForSaveLoad();
		if (currentTask == null && IsOperational)
		{
			WorkerMgr.AddIdleWorker(this);
		}
		else
		{
			WorkerMgr.RemoveIdleWorker(this);
		}
	}

	public void MarkHired(int elapsedWeek)
	{
		hiredAtElapsedWeek = Mathf.Max(0, elapsedWeek);
		itemDamageIncidentCount = 0;
	}

	public void ReportItemDamageIncident()
	{
		if (itemDamageIncidentCount < int.MaxValue)
			++itemDamageIncidentCount;
	}

	public int GetEmploymentWeekCount(int currentElapsedWeek)
	{
		return Mathf.Max(1, currentElapsedWeek - HiredAtElapsedWeek + 1);
	}

	public float GetAverageItemDamageIncidentsPerWeek(int currentElapsedWeek)
	{
		return itemDamageIncidentCount / (float)GetEmploymentWeekCount(currentElapsedWeek);
	}

	private void EnsureEmploymentInitialized()
	{
		if (hiredAtElapsedWeek >= 0)
			return;

		int currentWeek = GameContext.HasInstance && GameContext.Instance.GameTime != null
			? GameContext.Instance.GameTime.WeeksPassed
			: 0;
		hiredAtElapsedWeek = Mathf.Max(0, currentWeek);
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
		if (workerMainTaskType == taskType)
			return;

		WorkerTask.TaskType previousTaskType = workerMainTaskType;
		workerMainTaskType = taskType;
		workerAssignedTaskTypes.Clear();
		AddAssignedTaskType(taskType);
		workerMainTaskType = workerAssignedTaskTypes.Count > 0 ? workerAssignedTaskTypes[0] : WorkerTask.TaskType.Undefined;
		OnTaskTypeChanged?.Invoke(this, previousTaskType, taskType);
	}

	public bool IsAssignedToTaskType(WorkerTask.TaskType taskType)
	{
		return workerAssignedTaskTypes.Contains(taskType);
	}

	public void EnsureAssignedTaskTypesInitialized()
	{
		if (workerAssignedTaskTypes.Count <= 0 && workerMainTaskType != WorkerTask.TaskType.Undefined)
			workerAssignedTaskTypes.Add(workerMainTaskType);

		NormalizeAssignedTaskTypes();
	}

	public void SetAssignedTaskTypes(IEnumerable<WorkerTask.TaskType> taskTypes)
	{
		WorkerTask.TaskType previousTaskType = workerMainTaskType;
		workerAssignedTaskTypes.Clear();

		if (taskTypes != null)
		{
			foreach (WorkerTask.TaskType taskType in taskTypes)
			{
				AddAssignedTaskType(taskType);
			}
		}

		workerMainTaskType = workerAssignedTaskTypes.Count > 0 ? workerAssignedTaskTypes[0] : WorkerTask.TaskType.Undefined;
		if (previousTaskType != workerMainTaskType)
			OnTaskTypeChanged?.Invoke(this, previousTaskType, workerMainTaskType);
	}

	private void NormalizeAssignedTaskTypes()
	{
		if (workerAssignedTaskTypes.Count <= 0)
			return;

		List<WorkerTask.TaskType> assigned = new(workerAssignedTaskTypes);
		workerAssignedTaskTypes.Clear();
		for (int i = 0; i < assigned.Count; ++i)
			AddAssignedTaskType(assigned[i]);

		workerMainTaskType = workerAssignedTaskTypes.Count > 0 ? workerAssignedTaskTypes[0] : WorkerTask.TaskType.Undefined;
	}

	private void AddAssignedTaskType(WorkerTask.TaskType taskType)
	{
		if (taskType == WorkerTask.TaskType.Undefined)
			return;

		if (workerAssignedTaskTypes.Contains(taskType) == false)
			workerAssignedTaskTypes.Add(taskType);
	}

	public void SetPrimaryBuildingId(uint buildingId)
	{
		primaryBuildingId = buildingId;
	}

	public void SetHumanIdentity(HumanType humanType)
	{
		workerKind = WorkerKind.Human;
		this.humanType = humanType;
		robotType = RobotType.Transfer;
		legacyWorkerIdentityType = humanType switch
		{
			HumanType.PartTime => LegacyWorkerIdentityType.PartTime,
			HumanType.Illegal => LegacyWorkerIdentityType.Illegal,
			_ => LegacyWorkerIdentityType.FullTime,
		};
		identityInitialized = true;
	}

	public void SetRobotIdentity(RobotType robotType)
	{
		workerKind = WorkerKind.Robot;
		humanType = HumanType.FullTime;
		this.robotType = robotType;
		legacyWorkerIdentityType = LegacyWorkerIdentityType.Robot;
		identityInitialized = true;
	}

	public bool SetTask(WorkerTask task)
	{
		if (task != null && IsOperational == false)
			return false;

		if (GameContext.HasInstance && task != null)
			WorkerMgr.RemoveIdleWorker(this);
		else if (IsOperational)
		{
			WorkerMgr.AddIdleWorker(this);
		}

		if (task != null && task.SetAIWorker(this) == false)
		{
			if (GameContext.HasInstance && IsOperational)
				WorkerMgr.AddIdleWorker(this);
			return false;
		}

		currentTask = task;
		BuildBehaviorTree();
		return true;
	}

	internal void ClearTask(WorkerTask expectedTask, bool becomeIdle)
	{
		if (expectedTask != null && currentTask != expectedTask)
			return;

		currentTask = null;
		routeFinder?.CancelCurrentRoute();
		localBlackBoard.Clear();
		BuildBehaviorTree();
		if (GameContext.HasInstance)
		{
			if (becomeIdle && IsOperational)
				WorkerMgr.AddIdleWorker(this);
			else
				WorkerMgr.RemoveIdleWorker(this);
		}
	}

	public bool EnterIncapacitatedState(WorkerOperationalState state)
	{
		if (state == WorkerOperationalState.Active || operationalState == state)
			return false;

		WorkerOperationalState previousState = operationalState;
		operationalState = state;
		routeFinder?.CancelCurrentRoute();
		localBlackBoard.Clear();

		if (GameContext.HasInstance)
		{
			WorkerMgr.RemoveIdleWorker(this);
			GameContext.Instance.TaskMgr.ReturnTask(this);
		}

		if (isTrafficBlocked)
			EndTrafficBlock();

		SetWorkerTarget(WorkerStatusTarget.None);
		SetWorkerAction(GetIncapacitatedStatusAction(state));
		BuildBehaviorTree();
		enabled = true;
		OnOperationalStateChanged?.Invoke(this, previousState, state);
		return true;
	}

	private static WorkerStatusAction GetIncapacitatedStatusAction(WorkerOperationalState state)
	{
		return state switch
		{
			WorkerOperationalState.Knockout => WorkerStatusAction.Knockout,
			WorkerOperationalState.Death => WorkerStatusAction.Death,
			WorkerOperationalState.Malfunction => WorkerStatusAction.Malfunction,
			_ => WorkerStatusAction.Idle,
		};
	}

	private static IBaseNode.NodeState HoldIncapacitatedState(in BTContext ctx)
	{
		if (ctx.Worker == null || ctx.Worker.IsOperational)
			return IBaseNode.NodeState.Failure;

		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.None);
		ctx.Worker.SetWorkerAction(GetIncapacitatedStatusAction(ctx.Worker.OperationalState));
		return IBaseNode.NodeState.Running;
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

	private void EnsureIdentityInitialized()
	{
		if (identityInitialized)
			return;

		switch (legacyWorkerIdentityType)
		{
			case LegacyWorkerIdentityType.PartTime:
				SetHumanIdentity(HumanType.PartTime);
				break;

			case LegacyWorkerIdentityType.Illegal:
				SetHumanIdentity(HumanType.Illegal);
				break;

			case LegacyWorkerIdentityType.Robot:
				SetRobotIdentity(RobotType.Transfer);
				break;

			case LegacyWorkerIdentityType.FullTime:
			default:
				SetHumanIdentity(HumanType.FullTime);
				break;
		}
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
		if (IsOperational == false || currentTask != null || IsAssignedToTaskType(taskType) == false)
			return false;

		if (IsAssignedToPackingStation)
			return false;

		return true;
	}

	public bool CanAcceptGeneralTask(WorkerTask task)
	{
		return task != null && CanAcceptGeneralTask(task.Type);
	}

	public bool CanAcceptPreferredTask(WorkerTask task)
	{
		if (IsOperational == false || currentTask != null || task == null || IsAssignedToTaskType(task.Type) == false)
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

	public bool TryGetCurrentDestination(out string destinationName, out int3 destinationPosition)
	{
		destinationName = string.Empty;
		destinationPosition = default;

		if (localBlackBoard.TryGetTargetBuilding(out var targetPlaceable) && targetPlaceable is Component targetComponent)
		{
			destinationName = targetComponent.name;
			if (routeFinder != null && routeFinder.HasActiveGoal)
			{
				destinationPosition = routeFinder.CurrentGoalPosition;
				return true;
			}
		}

		if (routeFinder == null || routeFinder.HasActiveGoal == false)
			return false;

		destinationPosition = routeFinder.CurrentGoalPosition;
		destinationName = workerState.Target switch
		{
			WorkerStatusTarget.Charger => "Charger",
			WorkerStatusTarget.Airlock => "Airlock",
			_ when workerState.Action == WorkerStatusAction.Resting || workerState.Action == WorkerStatusAction.Charging => "Recovery Point",
			_ => workerState.Target != WorkerStatusTarget.None ? workerState.Target.ToString() : "Destination",
		};

		return true;
	}

	public virtual float GetWorkSpeedMultiplier() { return 1.0f; }
	public virtual float GetMoveSpeedMultiplier() { return 1.0f; }
	public virtual void OnTaskCompleted() { }
	public virtual void TickVitals(float deltaTime) { }
	public abstract bool NeedsRecovery();
	public abstract bool IsRecoveryComplete();
	public abstract void TickRecovery(float deltaTime);
	public abstract void AddFatigue(float fatigue);
	public abstract float GetFatigue();

	// decreased chance by researches or some pieces of equipment
	public virtual float GetIncidentMitigationMultiplier() { return 1.0f; }


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
