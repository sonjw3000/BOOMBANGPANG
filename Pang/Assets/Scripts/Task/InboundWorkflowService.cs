using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static WorkerTask.TaskType;

// inbound 작업 흐름을 관리
// 깨차

// rocket 착륙
// payload unload
// labeling
// storing

public partial class InboundWorkflowService : MonoBehaviour, IBoundService
{
	private const CollectingPolicyType DefaultCollectingPolicyType = CollectingPolicyType.Nearest;
	private const PlacingPolicyType DefaultPlacingPolicyType = PlacingPolicyType.BelowAverageFilledNearest;

	[SerializeField] private InboundRequestService requestService;
	[SerializeField] private ZoneManager zoneManager;
	[SerializeField] private ZoneType landingZoneType = ZoneType.RocketLanding;
	[SerializeField] private int landingZoneFloor = 0;
	[SerializeField] private int randomSearchCountPerZone = 12;
	[SerializeField] private float inboundRocketSpawnInterval = 10.0f;
	[SerializeField] [Range(0, 100)] private int hardLandingChange = 30;
	[SerializeField] [Range(0, 100)] private int damageRate = 30;
	[SerializeField] [Range(10, 100)] private int damagePercent = 50;
	[SerializeField] private uint unloadingDestinationBuildingId = 0;
	[SerializeField] private int maxStoreTasksPerUpdate = 64;
	[SerializeField] [Range(1f, 100f)] private float storingBoxFillLimitPercent = 80.0f;
	[SerializeField] private CollectingPolicyType defaultStoringCollectingPolicyType = DefaultCollectingPolicyType;
	[SerializeField] private PlacingPolicyType defaultStoringPlacingPolicyType = DefaultPlacingPolicyType;

	private StoringPlanner storingPlanner;
	private float timeSinceLastInboundRocketSpawn = 0.0f;
	public InboundRequestService RequestService => requestService;
	public StoringPlanner StoringPlanner => storingPlanner;
	private TaskManager TaskMgr => GameContext.Instance.TaskMgr;
	private ItemDatabase ItemDB => GameContext.Instance.ItemDB;
	private BoxManager BoxMgr => GameContext.Instance.BoxMgr;
	private CargoPortService CargoPortService => GameContext.HasInstance ? GameContext.Instance.CargoPortSvc : null;
	private CapsuleBufferService CapsuleBufferService => GameContext.HasInstance ? GameContext.Instance.CapsuleBufferSvc : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private RocketService RocketService => GameContext.Instance.RocketSvc;
	private DeliveryService DeliveryService => GameContext.Instance.DeliveryService;
	private ZoneManager ZoneManager
	{
		get
		{
			if (zoneManager == null && GameContext.HasInstance)
				zoneManager = GameContext.Instance.ZoneMgr;

			return zoneManager;
		}
	}
	public CollectingPolicyType StoringCollectingPolicyType => storingPlanner != null ? storingPlanner.CollectingPolicyType : defaultStoringCollectingPolicyType;
	public PlacingPolicyType StoringPlacingPolicyType => storingPlanner != null ? storingPlanner.PlacingPolicyType : defaultStoringPlacingPolicyType;
	public int HardLandingChange => hardLandingChange;
	public int DamageRate => damageRate;
	public int DamagePercent => damagePercent;
	public uint UnloadingDestinationBuildingId => unloadingDestinationBuildingId;

	public bool TryGetUnloadingDestinationBuilding(out Building building)
	{
		building = null;
		return unloadingDestinationBuildingId != 0
			&& GameContext.HasInstance
			&& GameContext.Instance.BuildingMgr != null
			&& GameContext.Instance.BuildingMgr.TryGetBuilding(unloadingDestinationBuildingId, out building)
			&& building != null;
	}

	public void SetUnloadingDestinationBuilding(Building building)
	{
		SetUnloadingDestinationBuilding(building != null ? building.RuntimeBuildingId : 0);
	}

	public void SetUnloadingDestinationBuilding(uint buildingId)
	{
		unloadingDestinationBuildingId = buildingId;
	}

	public void ClearUnloadingDestinationBuilding()
	{
		unloadingDestinationBuildingId = 0;
	}

	public void SetStoringCollectingPolicy(CollectingPolicyType policyType)
	{
		defaultStoringCollectingPolicyType = policyType;
		if (storingPlanner == null)
			return;

		storingPlanner.SetCollectingPolicy(policyType);
	}

	public void SetStoringPlacingPolicy(PlacingPolicyType policyType)
	{
		defaultStoringPlacingPolicyType = policyType;
		if (storingPlanner == null)
			return;

		storingPlanner.SetPlacingPolicy(policyType);
	}

	public void OnTaskCompleted(WorkerTask task)
	{
		switch (task.Type)
		{
			case Unloading:
			case IB:
				break;
			case Storing:
				break;
		}
	}

	public void OnInboundRocketLanded(Rocket rocket)
	{
		if (rocket == null)
			return;

		TryEnqueueUnloadingTask(rocket);
	}

	private void Awake()
	{
		RebuildPlanner();
	}

	private void Start()
	{
		if (RocketService != null)
			RocketService.InboundRocketLanded += OnInboundRocketLanded;

		TryEnqueueActiveRocketUnloadingTasks();
	}

	private void OnEnable()
	{
	}

	private void OnDisable()
	{
	}

	private void OnDestroy()
	{
		if (RocketService != null)
			RocketService.InboundRocketLanded -= OnInboundRocketLanded;
	}

	private void Update()
	{
		CheckInboundRocketSpawn();
		CheckStoreTaskAvailable();
	}

	private void CheckInboundRocketSpawn()
	{
		timeSinceLastInboundRocketSpawn += Time.deltaTime;
		if (timeSinceLastInboundRocketSpawn < inboundRocketSpawnInterval)
			return;

		if (DeliveryService.TryPeek(out var _) == false)
			return;

		if (TryGetLandingPoint(out var landingPoint) == false)
			return;

		if (RocketService != null && RocketService.TrySpawnInboundRocket(landingPoint))
			timeSinceLastInboundRocketSpawn = 0.0f;
	}

	private void CheckStoreTaskAvailable()
	{
		if (storingPlanner == null)
			return;

		BuildingManager buildingManager = BuildingManager;
		if (buildingManager == null || buildingManager.RegisteredBuildings.Count <= 0)
		{
			CheckGlobalStoreTaskAvailable();
			return;
		}

		int remainingBuilds = maxStoreTasksPerUpdate;
		for (int i = 0; i < buildingManager.RegisteredBuildings.Count && remainingBuilds > 0; ++i)
		{
			Building building = buildingManager.RegisteredBuildings[i];
			uint buildingId = building != null ? building.RuntimeBuildingId : 0;
			if (buildingId == 0 || storingPlanner.HasPendingCollectWork(buildingId) == false)
				continue;

			int desiredTaskCount = GetDesiredStoringTaskCount(buildingId);
			int currentTaskCount = GetCurrentStoringTaskCount(buildingId);
			if (desiredTaskCount <= currentTaskCount)
				continue;

			int tasksToBuild = Mathf.Min(remainingBuilds, Mathf.Max(0, desiredTaskCount - currentTaskCount));
			for (int taskIndex = 0; taskIndex < tasksToBuild; ++taskIndex)
			{
				if (storingPlanner.BuildStoreTask(buildingId, out var task) == false)
					break;

				if (task != null)
				{
					TaskMgr.EnqueueTask(task);
					remainingBuilds -= 1;
				}
			}
		}
	}

	private void CheckGlobalStoreTaskAvailable()
	{
		if (storingPlanner.HasPendingCollectWork() == false)
			return;

		int desiredTaskCount = GetDesiredStoringTaskCount();
		int currentTaskCount = GetCurrentStoringTaskCount();
		if (desiredTaskCount <= currentTaskCount)
			return;

		int tasksToBuild = Mathf.Min(maxStoreTasksPerUpdate, Mathf.Max(0, desiredTaskCount - currentTaskCount));
		for (int i = 0; i < tasksToBuild; ++i)
		{
			if (storingPlanner.BuildStoreTask(out var task) == false)
				break;

			if (task != null)
				TaskMgr.EnqueueTask(task);
		}
	}

	private void RebuildPlanner()
	{
		storingPlanner = new StoringPlanner(
			CapsuleBufferService,
			defaultStoringCollectingPolicyType,
			defaultStoringPlacingPolicyType);
	}

	private int GetCurrentStoringTaskCount()
	{
		return TaskMgr.TaskQueue[Storing].Count + TaskMgr.TaskOnProgress[Storing].Count;
	}

	private int GetCurrentStoringTaskCount(uint buildingId)
	{
		if (buildingId == 0)
			return GetCurrentStoringTaskCount();

		return CountStoringTasks(TaskMgr.TaskQueue[Storing], buildingId) + CountStoringTasks(TaskMgr.TaskOnProgress[Storing], buildingId);
	}

	private static int CountStoringTasks(IEnumerable<WorkerTask> tasks, uint buildingId)
	{
		if (tasks == null)
			return 0;

		int count = 0;
		foreach (WorkerTask task in tasks)
		{
			if (task is StoringTask storingTask && storingTask.BuildingId == buildingId)
				count += 1;
		}

		return count;
	}

	private int GetDesiredStoringTaskCount()
	{
		return GetDesiredStoringTaskCount(0);
	}

	private int GetDesiredStoringTaskCount(uint buildingId)
	{
		float effectiveBoxCapacity = GetEffectiveStoringBoxCapacity();
		if (effectiveBoxCapacity <= 0.0f)
			return 0;

		float totalOutstandingSize = storingPlanner != null ? storingPlanner.GetCollectOutstandingTotalSize(buildingId, ItemDB) : 0.0f;
		if (totalOutstandingSize <= 0.0f)
			return 0;

		return Mathf.CeilToInt(totalOutstandingSize / effectiveBoxCapacity);
	}

	private float GetEffectiveStoringBoxCapacity()
	{
		float toteCapacity = BoxMgr != null ? BoxMgr.ToteCapacity : 0.0f;
		if (toteCapacity <= 0.0f)
			return 0.0f;

		float fillRatio = Mathf.Clamp01(storingBoxFillLimitPercent / 100.0f);
		return toteCapacity * fillRatio;
	}

	internal CargoPort ResolveUnloadingDestinationPort(Rocket rocket, uint requestedBuildingId = 0)
	{
		if (rocket == null || CargoPortService == null)
			return null;

		ZoneFilter zoneFilter = ZoneFilter.ForContainer(rocket.DockedCapsule);

		return CargoPortService.FindClosestAvailablePort(
			rocket.GridPosition,
			InteractionKind.Put,
			requestedBuildingId,
			zoneFilter,
			predicate: candidate => candidate is InboundCargoPort);
	}

	private void TryEnqueueUnloadingTask(Rocket rocket)
	{
		if (rocket == null || TaskMgr == null)
			return;

		rocket.DockedCapsule?.SetLogisticsState(CapsuleLogisticsState.IB);

		GameContext.Instance.CapsuleRelocateCoordinator.RequestSend(new CapsuleRelocateSendRequest(
			rocket,
			CapsuleDockState.InboundSource,
			CapsuleLogisticsState.IB,
			CapsuleDockState.IBStandby,
			CapsuleRelocateScope.LinkedBuilding,
			0,
			unloadingDestinationBuildingId,
			EnqueueRocketUnloadTask));
	}

	private bool EnqueueRocketUnloadTask(CapsuleRelocateMatch match)
	{
		if (TaskMgr == null || match.SourceDock == null || match.TargetDock == null)
			return false;

		CapsuleRelocationTask task = new(
			Unloading,
			match.SourceDock,
			match.TargetDock,
			0,
			CapsuleRelocationReason.SourceMustClear);
		TaskMgr.EnqueueTask(task);
		return true;
	}

	private void TryEnqueueActiveRocketUnloadingTasks()
	{
		if (RocketService == null)
			return;

		IReadOnlyList<Rocket> rockets = RocketService.Rockets;
		for (int i = 0; i < rockets.Count; ++i)
		{
			Rocket rocket = rockets[i];
			if (rocket != null && rocket.State == Rocket.RocketState.OnPad)
				TryEnqueueUnloadingTask(rocket);
		}
	}

	private bool TryGetLandingPoint(out int3 landingPoint)
	{
		landingPoint = default;
		if (ZoneManager == null || RocketService == null)
			return false;

		if (ZoneManager.TryGetZones(out var zones, landingZoneFloor, landingZoneType) == false)
			return false;

		int startIndex = UnityEngine.Random.Range(0, zones.Count);
		for (int i = 0; i < zones.Count; ++i)
		{
			ZoneArea zone = zones[(startIndex + i) % zones.Count];
			if (TryFindLandingPoint(zone, out landingPoint))
				return true;
		}

		return false;
	}

	private bool TryFindLandingPoint(ZoneArea zone, out int3 landingPoint)
	{
		for (int i = 0; i < Mathf.Max(1, randomSearchCountPerZone); ++i)
		{
			zone.GetRandomPoint(out int3 candidatePoint);
			if (RocketService.CanLandAt(candidatePoint))
			{
				landingPoint = candidatePoint;
				return true;
			}
		}

		for (int z = zone.Bounds.yMin; z < zone.Bounds.yMax; ++z)
		{
			for (int x = zone.Bounds.xMin; x < zone.Bounds.xMax; ++x)
			{
				int3 candidatePoint = new(x, zone.Floor, z);
				if (RocketService.CanLandAt(candidatePoint))
				{
					landingPoint = candidatePoint;
					return true;
				}
			}
		}

		landingPoint = default;
		return false;
	}
}
