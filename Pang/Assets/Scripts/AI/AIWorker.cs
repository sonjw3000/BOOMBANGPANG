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

	private FindRoute routeFinder;
	private BehaviorTree behaviorTree;
	private readonly BlackBoard localBlackBoard = new();
	private WorkerStatusInfo workerState = WorkerStatusInfo.None;

	private int3 position;
	private FacingDirection facingDirection;

	private IInteractionPoint currentWorkingPoint = null;
	private bool isRegistered = false;

	// event
	public event System.Action<WorkerStatusAction> OnActionChanged;

	// worker identity
	public string Name => $"{workerFirstName} {workerLastName}";
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

	// worker show stat
	public WorkerStatusInfo WorkerState => workerState;
	public WorkerStatusTarget BuildingTarget => WorkerStatusTarget.None;

	static private WorkerManager WorkerMgr => GameContext.Instance.WorkerMgr;

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

		workerType = archetype.AbilityDefinition.workerType;
		abilities = archetype.AbilityDefinition.abilities;
		monthlyCost = archetype.AbilityDefinition.monthlyCost;

		baseMoveSpeedMultiplier = archetype.WorkerBaseStat.baseMoveSpeedMultiplier;
		minimumMoveSpeedMultiplier = archetype.WorkerBaseStat.minimumMoveSpeedMultiplier;
		baseWorkSpeedMultiplier = archetype.WorkerBaseStat.baseWorkSpeedMultiplier;
		minimumWorkSpeedMultiplier = archetype.WorkerBaseStat.minimumWorkSpeedMultiplier;

		archetype.SetupWorker(this);
	}

	private void Start()
	{
		routeFinder = transform.GetComponent<FindRoute>();

		if (routeFinder == null)
		{
			Debug.Log($"FindRoute가 null이다 해당 객체가 프리뷰가 아니라면 큰일이다, 이름: {this.name}");

			return;
		}

		// register AI's BT to AI Manager
		WorkerMgr.RegisterWorker(this);
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
		gameObject.TryGetComponent<CarryBoxAbility>(out var component);
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
		gameObject.TryGetComponent<CarryBoxAbility>(out var component);
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

	public virtual float GetWorkSpeedMultiplier() { return 1.0f; }
	public virtual float GetMoveSpeedMultiplier() { return 1.0f; }
	public virtual void OnTaskCompleted() { }
	public virtual void TickVitals(float deltaTime) { }

	public abstract void AddFatigue(float fatigue);
	public abstract float GetFatigue();

	// decreased chance by researches or some pieces of equipment
	public virtual float GetIncidentMitigationMultiplier() { return 1.0f; }
}
