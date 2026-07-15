using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static WorkerTask.TaskType;
using UnityEngine.Serialization;

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
	[FormerlySerializedAs("zoneManager")]
	[SerializeField] private AreaManager areaManager;
	[FormerlySerializedAs("landingZoneFloor")]
	[SerializeField] private int landingAreaFloor = 0;
	[FormerlySerializedAs("randomSearchCountPerZone")]
	[SerializeField] private int randomSearchCountPerArea = 12;
	[SerializeField] private float inboundRocketSpawnInterval = 10.0f;
	[SerializeField] [Range(0, 100)] private int hardLandingChange = 30;
	[SerializeField] [Range(0, 100)] private int damageRate = 30;
	[SerializeField] [Range(10, 100)] private int damagePercent = 50;
	[SerializeField] private uint unloadingDestinationBuildingId = 0;
	[SerializeField] [Range(1f, 100f)] private float storingBoxFillLimitPercent = 80.0f;
	[SerializeField] private CollectingPolicyType defaultStoringCollectingPolicyType = DefaultCollectingPolicyType;
	[SerializeField] private PlacingPolicyType defaultStoringPlacingPolicyType = DefaultPlacingPolicyType;

	private StoringPlanner storingPlanner;
	private float timeSinceLastInboundRocketSpawn = 0.0f;
	public InboundRequestService RequestService => requestService;
	public StoringPlanner StoringPlanner => storingPlanner;
	private TaskManager TaskMgr => GameContext.HasInstance ? GameContext.Instance.TaskMgr : null;
	private CargoPortService CargoPortService => GameContext.HasInstance ? GameContext.Instance.CargoPortSvc : null;
	private CapsuleBufferService CapsuleBufferService => GameContext.HasInstance ? GameContext.Instance.CapsuleBufferSvc : null;
	private RocketService RocketService => GameContext.HasInstance ? GameContext.Instance.RocketSvc : null;
	private DeliveryService DeliveryService => GameContext.HasInstance ? GameContext.Instance.DeliveryService : null;
	private AreaManager AreaManager
	{
		get
		{
			if (areaManager == null && GameContext.HasInstance)
				areaManager = GameContext.Instance.AreaMgr;

			return areaManager;
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
		if (AreaManager != null)
			AreaManager.OnAreaChanged += HandleAreaChanged;

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
		if (AreaManager != null)
			AreaManager.OnAreaChanged -= HandleAreaChanged;
	}

	private void HandleAreaChanged(Area area)
	{
		if (area != null && area.Type == AreaType.RocketLanding)
			TryEnqueueActiveRocketUnloadingTasks();
	}

	private void Update()
	{
		CheckInboundRocketSpawn();
	}

	private void CheckInboundRocketSpawn()
	{
		DeliveryService deliveryService = DeliveryService;
		RocketService rocketService = RocketService;
		if (deliveryService == null || rocketService == null)
			return;

		timeSinceLastInboundRocketSpawn += Time.deltaTime;
		if (timeSinceLastInboundRocketSpawn < inboundRocketSpawnInterval)
			return;

		if (deliveryService.TryPeek(out var _) == false)
			return;

		if (TryGetLandingPoint(rocketService, out var landingPoint) == false)
			return;

		if (rocketService.TrySpawnInboundRocket(landingPoint))
			timeSinceLastInboundRocketSpawn = 0.0f;
	}

	private void RebuildPlanner()
	{
		storingPlanner = new StoringPlanner(
			CapsuleBufferService,
			defaultStoringCollectingPolicyType,
			defaultStoringPlacingPolicyType,
			storingBoxFillLimitPercent);
	}

	internal CargoPort ResolveUnloadingDestinationPort(Rocket rocket, uint requestedBuildingId = 0)
	{
		if (rocket == null || CargoPortService == null)
			return null;

		FacilityFilter facilityFilter = FacilityFilter.ForContainer(rocket.DockedCapsule);
		uint destinationBuildingId = requestedBuildingId != 0
			? requestedBuildingId
			: ResolveUnloadingDestinationBuildingId(rocket);
		if (destinationBuildingId == 0)
			return null;

		return CargoPortService.FindClosestAvailablePort(
			rocket.GridPosition,
			InteractionKind.Put,
			destinationBuildingId,
			facilityFilter,
			predicate: candidate => candidate is InboundCargoPort);
	}

	private void TryEnqueueUnloadingTask(Rocket rocket)
	{
		if (rocket == null || TaskMgr == null)
			return;

		uint destinationBuildingId = ResolveUnloadingDestinationBuildingId(rocket);
		if (destinationBuildingId == 0)
			return;

		rocket.DockedCapsule?.SetLogisticsState(CapsuleLogisticsState.IB);

		GameContext.Instance.CapsuleRelocateCoordinator.RequestSend(new CapsuleRelocateSendRequest(
			rocket,
			CapsuleDockState.InboundSource,
			CapsuleLogisticsState.IB,
			CapsuleDockState.IBStandby,
			CapsuleRelocateScope.LinkedBuilding,
			0,
			destinationBuildingId,
			EnqueueRocketUnloadTask));
	}

	private uint ResolveUnloadingDestinationBuildingId(Rocket rocket)
	{
		if (rocket != null && AreaManager != null &&
			AreaManager.TryGetAreaAt(rocket.LandingPos, out Area landingArea) &&
			landingArea != null && landingArea.Type == AreaType.RocketLanding && landingArea.DestinationBuildingId != 0)
		{
			return landingArea.DestinationBuildingId;
		}

		return 0;
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
		RocketService rocketService = RocketService;
		if (rocketService == null)
			return;

		IReadOnlyList<Rocket> rockets = rocketService.Rockets;
		for (int i = 0; i < rockets.Count; ++i)
		{
			Rocket rocket = rockets[i];
			if (rocket != null && rocket.State == Rocket.RocketState.OnPad)
				TryEnqueueUnloadingTask(rocket);
		}
	}

	private bool TryGetLandingPoint(RocketService rocketService, out int3 landingPoint)
	{
		landingPoint = default;
		if (AreaManager == null || rocketService == null)
			return false;

		if (AreaManager.TryGetAreas(out var areas, landingAreaFloor, AreaType.RocketLanding) == false)
			return false;

		int startIndex = UnityEngine.Random.Range(0, areas.Count);
		for (int i = 0; i < areas.Count; ++i)
		{
			Area area = areas[(startIndex + i) % areas.Count];
			if (TryFindLandingPoint(rocketService, area, out landingPoint))
				return true;
		}

		return false;
	}

	private bool TryFindLandingPoint(RocketService rocketService, Area area, out int3 landingPoint)
	{
		for (int i = 0; i < Mathf.Max(1, randomSearchCountPerArea); ++i)
		{
			area.GetRandomPoint(out int3 candidatePoint);
			if (rocketService.CanLandAt(candidatePoint))
			{
				landingPoint = candidatePoint;
				return true;
			}
		}

		for (int z = area.Bounds.yMin; z < area.Bounds.yMax; ++z)
		{
			for (int x = area.Bounds.xMin; x < area.Bounds.xMax; ++x)
			{
				int3 candidatePoint = new(x, area.Floor, z);
				if (rocketService.CanLandAt(candidatePoint))
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
