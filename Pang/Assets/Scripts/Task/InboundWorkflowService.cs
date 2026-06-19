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

public class InboundWorkflowService : MonoBehaviour, IBoundService
{
	private const CollectingPolicyType DefaultCollectingPolicyType = CollectingPolicyType.Nearest;
	private const PlacingPolicyType DefaultPlacingPolicyType = PlacingPolicyType.BelowAverageFilledNearest;

	[SerializeField] private CargoPortService cargoPortService;
	[SerializeField] private InboundRequestService requestService;
	[SerializeField] private ZoneManager zoneManager;
	[SerializeField] private ZoneType landingZoneType = ZoneType.RocketLanding;
	[SerializeField] private int landingZoneFloor = 0;
	[SerializeField] private int randomSearchCountPerZone = 12;
	[SerializeField] private float inboundRocketSpawnInterval = 10.0f;
	[SerializeField] private uint unloadingDestinationBuildingId = 0;
	[SerializeField] private int maxStoreTasksPerUpdate = 64;
	[SerializeField] [Range(1f, 100f)] private float storingBoxFillLimitPercent = 80.0f;
	[SerializeField] private CollectingPolicyType defaultStoringCollectingPolicyType = DefaultCollectingPolicyType;
	[SerializeField] private PlacingPolicyType defaultStoringPlacingPolicyType = DefaultPlacingPolicyType;

	private StoringPlanner storingPlanner;
	private float timeSinceLastInboundRocketSpawn = 0.0f;

	public CargoPortService CargoPortService => cargoPortService;
	public InboundRequestService RequestService => requestService;
	public StoringPlanner StoringPlanner => storingPlanner;
	private TaskManager TaskMgr => GameContext.Instance.TaskMgr;
	private ItemDatabase ItemDB => GameContext.Instance.ItemDB;
	private BoxPoolService BoxPoolService => GameContext.Instance.WMSys.BoxPoolService;
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

	public InboundWorkflowPolicySaveData CapturePolicyState()
	{
		return new InboundWorkflowPolicySaveData
		{
			StoringCollectingPolicy = StoringCollectingPolicyType,
			StoringPlacingPolicy = StoringPlacingPolicyType,
			UnloadingDestinationBuildingId = unloadingDestinationBuildingId,
		};
	}

	public void RestorePolicyState(InboundWorkflowPolicySaveData data)
	{
		CollectingPolicyType collectingPolicyType = data != null ? data.StoringCollectingPolicy : DefaultCollectingPolicyType;
		PlacingPolicyType policyType = data != null ? data.StoringPlacingPolicy : DefaultPlacingPolicyType;
		unloadingDestinationBuildingId = data != null ? data.UnloadingDestinationBuildingId : 0;
		SetStoringCollectingPolicy(collectingPolicyType);
		SetStoringPlacingPolicy(policyType);
	}

	public void OnTaskCompleted(WorkerTask task)
	{
		switch (task.Type)
		{
			case Unloading:
				break;
			case Storing:
				break;
		}
	}

	public void OnInboundRocketLanded(Rocket rocket)
	{
		if (rocket == null)
			return;

		CargoPort targetPort = ResolveUnloadingDestinationPort(rocket);
		UnloadingTask task = new(rocket, targetPort);
		TaskMgr.EnqueueTask(task);
	}

	private void Awake()
	{
		RebuildPlanner();
	}

	private void Start()
	{
		if (RocketService != null)
			RocketService.InboundRocketLanded += OnInboundRocketLanded;
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

	public void ResetRuntimeState()
	{
		timeSinceLastInboundRocketSpawn = 0.0f;
		requestService?.ResetRuntimeState();
		RebuildPlanner();
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
		if (storingPlanner == null || storingPlanner.HasPendingCollectWork() == false)
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
			cargoPortService,
			requestService,
			defaultStoringCollectingPolicyType,
			defaultStoringPlacingPolicyType);
	}

	private int GetCurrentStoringTaskCount()
	{
		return TaskMgr.TaskQueue[Storing].Count + TaskMgr.TaskOnProgress[Storing].Count;
	}

	private int GetDesiredStoringTaskCount()
	{
		float effectiveBoxCapacity = GetEffectiveStoringBoxCapacity();
		if (effectiveBoxCapacity <= 0.0f)
			return 0;

		float totalOutstandingSize = requestService != null ? requestService.GetOutstandingTotalSize(ItemDB) : 0.0f;
		if (totalOutstandingSize <= 0.0f)
			return 0;

		return Mathf.CeilToInt(totalOutstandingSize / effectiveBoxCapacity);
	}

	private float GetEffectiveStoringBoxCapacity()
	{
		float toteCapacity = BoxPoolService != null ? BoxPoolService.ToteCapacity : 0.0f;
		if (toteCapacity <= 0.0f)
			return 0.0f;

		float fillRatio = Mathf.Clamp01(storingBoxFillLimitPercent / 100.0f);
		return toteCapacity * fillRatio;
	}

	private CargoPort ResolveUnloadingDestinationPort(Rocket rocket)
	{
		if (rocket == null || cargoPortService == null)
			return null;

		if (unloadingDestinationBuildingId != 0)
		{
			TryResolveConfiguredUnloadingDestinationPort(rocket.GridPosition, out CargoPort configuredTarget);
			return configuredTarget;
		}

		return cargoPortService.FindClosestAvailablePort(
			rocket.GridPosition,
			InteractionKind.Put,
			predicate: candidate => candidate is InboundCargoPort);
	}

	private bool TryResolveConfiguredUnloadingDestinationPort(in int3 from, out CargoPort targetPort)
	{
		targetPort = null;
		if (unloadingDestinationBuildingId == 0 || cargoPortService == null)
			return false;

		List<CargoPort> ports = new();
		if (cargoPortService.TryQueryPorts(unloadingDestinationBuildingId, ports, port => port != null && port is InboundCargoPort) == false)
			return false;

		int bestScore = int.MaxValue;
		for (int i = 0; i < ports.Count; ++i)
		{
			CargoPort port = ports[i];
			if (port == null || port.IsInteractionAvailable(InteractionKind.Put) == false)
				continue;

			if (InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				port,
				InteractionKind.Put,
				from,
				GameContext.Instance.GridService,
				out _,
				out int score) == false)
			{
				continue;
			}

			if (score >= bestScore)
				continue;

			bestScore = score;
			targetPort = port;
		}

		return targetPort != null;
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
