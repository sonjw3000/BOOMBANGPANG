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
	private const PlacingPolicyType DefaultPlacingPolicyType = PlacingPolicyType.Nearest;

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
	[SerializeField] [Range(10, 100)] private int maximumDamageAmount = 50;
	[SerializeField] private uint unloadingDestinationBuildingId = 0;
	[SerializeField] [Range(1f, 100f)] private float storingBoxFillLimitPercent = 80.0f;
	[SerializeField] private CollectingPolicyType defaultStoringCollectingPolicyType = DefaultCollectingPolicyType;
	[SerializeField] private PlacingPolicyType defaultStoringPlacingPolicyType = DefaultPlacingPolicyType;
	[SerializeField] private bool inboundQualityControlEnabled;
	[SerializeField] [Range(0f, 100f)] private float minimumInboundFreshnessPercent = QualityControlPolicy.DefaultMinimumFreshnessPercent;
	[SerializeField] [Range(0f, 100f)] private float maximumInboundDamagePercent = QualityControlPolicy.DefaultMaximumDamagePercent;

	private StoringPlanner storingPlanner;
	private readonly Dictionary<CapsuleBuffer, LabelingTask> labelingTasksByBuffer = new();
	private readonly HashSet<uint> storingScheduleBuildingIds = new();
	private readonly List<uint> buildingIdScratch = new();
	// Keep the exact event publishers so teardown is independent of GameContext ordering.
	private RocketService boundRocketService;
	private AreaManager boundAreaManager;
	private BuildingManager boundBuildingManager;
	private CapsuleDockService boundCapsuleDockService;
	private CapsuleRelocateCoordinator boundCapsuleRelocateCoordinator;
	private ItemTransferTaskScheduler boundItemTransferTaskScheduler;
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
	public float StoringBoxFillLimitPercent => storingBoxFillLimitPercent;
	public int HardLandingChange => hardLandingChange;
	public int DamageRate => damageRate;
	public int MaximumDamageAmount => maximumDamageAmount;
	public uint UnloadingDestinationBuildingId => unloadingDestinationBuildingId;
	public bool InboundQualityControlEnabled => inboundQualityControlEnabled && IsResearchCompleted(ResearchIds.QualityControl);
	public float MinimumInboundFreshnessPercent => minimumInboundFreshnessPercent;
	public float MaximumInboundDamagePercent => maximumInboundDamagePercent;

	public QualityInspectionResult InspectInboundQuality(ItemStack stack)
	{
		return QualityControlPolicy.Inspect(
			stack,
			minimumInboundFreshnessPercent,
			maximumInboundDamagePercent);
	}

	public bool TrySetInboundQualityControlEnabled(bool enabled)
	{
		if (IsResearchCompleted(ResearchIds.QualityControl) == false)
			return false;

		inboundQualityControlEnabled = enabled;
		ReevaluateLabelingWork();
		return true;
	}

	public bool TrySetInboundQualityThresholds(float minimumFreshnessPercent, float maximumDamagePercent)
	{
		if (IsResearchCompleted(ResearchIds.QualityControl) == false)
			return false;

		minimumInboundFreshnessPercent = Mathf.Clamp(minimumFreshnessPercent, 0.0f, 100.0f);
		maximumInboundDamagePercent = Mathf.Clamp(maximumDamagePercent, 0.0f, 100.0f);
		return true;
	}

	private void ReevaluateLabelingWork()
	{
		CapsuleBufferService bufferService = CapsuleBufferService;
		if (bufferService == null)
			return;

		foreach (CapsuleBuffer buffer in bufferService.GetBuffers())
			ReevaluateBufferWork(buffer);
	}

	internal bool HasLabelingWork(CapsuleBuffer buffer)
	{
		if (buffer == null)
			return false;

		for (int i = 0; i < buffer.Stacks.Count; ++i)
		{
			ItemStack stack = buffer.Stacks[i];
			if (stack != null &&
				stack.Quantity > 0 &&
				stack.Status == ItemStatus.None &&
				stack.HasQuality(ItemQuality.Waste) == false)
			{
				return true;
			}
		}

		return false;
	}

	internal bool IsLabelingTargetReady(uint buildingId, CapsuleBuffer buffer)
	{
		if (buildingId == 0 || GameContext.HasInstance == false ||
			GameContext.Instance.BuildingMgr == null ||
			GameContext.Instance.BuildingMgr.TryGetBuilding(buildingId, out Building registeredBuilding) == false ||
			registeredBuilding == null ||
			buffer?.DockedCapsule == null ||
			buffer.DockedCapsule.RouteKind != CargoRouteKind.Standard ||
			buffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.Inside ||
			buffer.IsCapsuleEmpty() ||
			HasLabelingWork(buffer) == false)
		{
			return false;
		}

		CapsuleBufferService bufferService = CapsuleBufferService;
		if (bufferService == null ||
			bufferService.TryGetRegisteredBuildingId(buffer, out uint registeredBuildingId) == false ||
			registeredBuildingId != buildingId ||
			bufferService.IsRuleMatchedBuffer(buffer, buffer.DockedCapsule, evaluateLaunchReadiness: false) == false ||
			IsBufferRuleConfiguredForStage(buffer, CargoProcessStage.Unlabeled) == false)
		{
			return false;
		}

		CapsuleRelocateCoordinator coordinator = GameContext.Instance.ExistingCapsuleRelocateCoordinator;
		return coordinator == null ||
			(coordinator.IsPlayerClaimed(buffer) == false &&
			 coordinator.IsReserved(buffer) == false &&
			 coordinator.IsRelocationSourceActive(buffer) == false &&
			 coordinator.IsRelocationTargetActive(buffer) == false);
	}

	internal bool CanRequestLabelingTask(uint buildingId, CapsuleBuffer buffer)
	{
		return TaskMgr != null &&
			IsLabelingTargetReady(buildingId, buffer) &&
			HasOwnedLabelingTask(buffer) == false &&
			TaskMgr.HasManagedTaskFacilityDependency(buffer) == false;
	}

	internal bool TryRequestLabelingTask(uint buildingId, CapsuleBuffer buffer)
	{
		return CanRequestLabelingTask(buildingId, buffer) &&
			TaskMgr.EnqueueTaskBuildRequest(new LabelingTaskBuildRequest(buildingId, buffer));
	}

	internal bool RegisterLabelingTask(LabelingTask task)
	{
		CapsuleBuffer buffer = task?.TargetBuffer;
		if (buffer == null || task.BuildingId == 0 ||
			IsLabelingTargetReady(task.BuildingId, buffer) == false ||
			HasOwnedLabelingTask(buffer))
		{
			return false;
		}

		labelingTasksByBuffer[buffer] = task;
		return true;
	}

	private bool HasOwnedLabelingTask(CapsuleBuffer buffer)
	{
		if (buffer == null || labelingTasksByBuffer.TryGetValue(buffer, out LabelingTask task) == false)
			return false;

		if (task != null &&
			task.CurrentStatus != WorkerTask.Status.Completed &&
			task.CurrentStatus != WorkerTask.Status.Invalidated)
		{
			return true;
		}

		labelingTasksByBuffer.Remove(buffer);
		return false;
	}

	private bool IsBufferRuleConfiguredForStage(CapsuleBuffer buffer, CargoProcessStage stage)
	{
		FacilityRuleManager ruleManager = GameContext.HasInstance ? GameContext.Instance.FacilityRuleMgr : null;
		return buffer != null &&
			ruleManager != null &&
			buffer.FacilityRulePresetId != FacilityRuleManager.NoRulePresetId &&
			ruleManager.TryGetPreset(buffer.FacilityRulePresetId, out FacilityRulePreset preset) &&
			preset?.Rule?.RequiredCargoProcessStage == stage;
	}

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

	public bool CanUseStoringCollectingPolicy(CollectingPolicyType policyType)
	{
		if (IsResearchCompleted(ResearchIds.WorkflowPolicyManagement) == false)
			return false;

		return policyType == CollectingPolicyType.Nearest ||
			(policyType == CollectingPolicyType.LargestQuantityNearest &&
			 IsResearchCompleted(ResearchIds.WorkflowPolicyOptimization));
	}

	public bool TrySetStoringCollectingPolicy(CollectingPolicyType policyType)
	{
		if (CanUseStoringCollectingPolicy(policyType) == false)
			return false;

		SetStoringCollectingPolicy(policyType);
		return true;
	}

	public void SetStoringPlacingPolicy(PlacingPolicyType policyType)
	{
		defaultStoringPlacingPolicyType = policyType;
		if (storingPlanner == null)
			return;

		storingPlanner.SetPlacingPolicy(policyType);
	}

	public bool CanUseStoringPlacingPolicy(PlacingPolicyType policyType)
	{
		if (IsResearchCompleted(ResearchIds.WorkflowPolicyManagement) == false)
			return false;

		return policyType == PlacingPolicyType.Nearest ||
			(policyType == PlacingPolicyType.BelowAverageFilledNearest &&
			 IsResearchCompleted(ResearchIds.WorkflowPolicyOptimization));
	}

	public bool TrySetStoringPlacingPolicy(PlacingPolicyType policyType)
	{
		if (CanUseStoringPlacingPolicy(policyType) == false)
			return false;

		SetStoringPlacingPolicy(policyType);
		return true;
	}

	public bool TrySetStoringBoxFillLimitPercent(float value)
	{
		if (IsResearchCompleted(ResearchIds.WorkflowPolicyOptimization) == false)
			return false;

		SetStoringBoxFillLimitPercent(value);
		return true;
	}

	private void SetStoringBoxFillLimitPercent(float value)
	{
		storingBoxFillLimitPercent = Mathf.Clamp(value, 1.0f, 100.0f);
		storingPlanner?.SetBoxFillLimitPercent(storingBoxFillLimitPercent);
	}

	private static bool IsResearchCompleted(string researchId)
	{
		return GameContext.HasInstance &&
			GameContext.Instance.ResearchService?.IsResearched(researchId) == true;
	}

	public void OnTaskCompleted(WorkerTask task)
	{
		if (task is LabelingTask labelingTask)
			OnLabelingTaskEnded(labelingTask);

		switch (task.Type)
		{
			case Unloading:
			case IB:
				break;
			case Storing:
				break;
		}
	}

	public void OnTaskInvalidated(WorkerTask task)
	{
		if (task is LabelingTask labelingTask)
			OnLabelingTaskEnded(labelingTask);

		if (task is not CapsuleRelocationTask relocationTask || GameContext.HasInstance == false)
			return;

		GameContext.Instance.CapsuleRelocateCoordinator?.ReleaseReservation(
			relocationTask.SourceDock,
			relocationTask.TargetDock);
	}

	public void OnFacilityInvalidating(IFacility facility, in FacilityInvalidationContext context)
	{
		if (facility is not CapsuleDock dock || GameContext.HasInstance == false)
			return;

		GameContext.Instance.CapsuleRelocateCoordinator?.CancelPendingRequests(dock);
	}

	public void OnInboundRocketLanded(Rocket rocket)
	{
		if (rocket == null)
			return;

		TryEnqueueUnloadingTask(rocket);
	}

	private void Awake()
	{
		EnsurePlanner();
	}

	private void OnEnable()
	{
		EnsurePlanner();
		bool didBind = BindEvents();
		ReevaluateLabelingWork();
		if (didBind)
			RetryActiveRocketUnloadingTasks();
	}

	private void Start()
	{
		EnsurePlanner();
		bool didBind = BindEvents();
		ReevaluateLabelingWork();
		if (didBind)
			RetryActiveRocketUnloadingTasks();
	}

	private void OnDisable()
	{
		UnbindEvents();
	}

	private void HandleAreaChanged(Area area)
	{
		if (area != null && area.Type == AreaType.RocketLanding)
			RetryActiveRocketUnloadingTasks();
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

	private void EnsurePlanner()
	{
		if (storingPlanner == null)
			RebuildPlanner();
	}

	private bool BindEvents()
	{
		bool didBind = false;
		if (boundRocketService == null)
		{
			boundRocketService = RocketService;
			if (boundRocketService != null)
			{
				boundRocketService.InboundRocketLanded += OnInboundRocketLanded;
				didBind = true;
			}
		}

		if (boundAreaManager == null)
		{
			boundAreaManager = AreaManager;
			if (boundAreaManager != null)
			{
				boundAreaManager.OnAreaChanged += HandleAreaChanged;
				boundAreaManager.OnAreaRemoved += HandleAreaChanged;
				didBind = true;
			}
		}

		if (boundBuildingManager == null && GameContext.HasInstance)
		{
			boundBuildingManager = GameContext.Instance.BuildingMgr;
			if (boundBuildingManager != null)
			{
				boundBuildingManager.OnBuildingsChanged += HandleBuildingsChanged;
				didBind = true;
			}
		}

		if (boundCapsuleDockService == null && GameContext.HasInstance)
		{
			boundCapsuleDockService = GameContext.Instance.CapsuleDockSvc;
			if (boundCapsuleDockService != null)
			{
				boundCapsuleDockService.OnCapsuleUndocked += HandleCapsuleUndocked;
				didBind = true;
			}
		}

		if (boundCapsuleRelocateCoordinator == null && GameContext.HasInstance)
		{
			boundCapsuleRelocateCoordinator = GameContext.Instance.CapsuleRelocateCoordinator;
			if (boundCapsuleRelocateCoordinator != null)
			{
				boundCapsuleRelocateCoordinator.OnRuleRoutingEvaluated += HandleRuleRoutingEvaluated;
				didBind = true;
			}
		}

		if (boundItemTransferTaskScheduler == null && GameContext.HasInstance)
			boundItemTransferTaskScheduler = GameContext.Instance.ItemTransferTaskScheduler;

		SyncBuildingTaskProducers();

		return didBind;
	}

	private void UnbindEvents()
	{
		if (boundRocketService != null)
			boundRocketService.InboundRocketLanded -= OnInboundRocketLanded;

		if (boundAreaManager != null)
		{
			boundAreaManager.OnAreaChanged -= HandleAreaChanged;
			boundAreaManager.OnAreaRemoved -= HandleAreaChanged;
		}

		if (boundBuildingManager != null)
			boundBuildingManager.OnBuildingsChanged -= HandleBuildingsChanged;

		if (boundCapsuleDockService != null)
			boundCapsuleDockService.OnCapsuleUndocked -= HandleCapsuleUndocked;

		if (boundCapsuleRelocateCoordinator != null)
			boundCapsuleRelocateCoordinator.OnRuleRoutingEvaluated -= HandleRuleRoutingEvaluated;

		UnregisterStoringTaskProducers();

		boundRocketService = null;
		boundAreaManager = null;
		boundBuildingManager = null;
		boundCapsuleDockService = null;
		boundCapsuleRelocateCoordinator = null;
		boundItemTransferTaskScheduler = null;
	}

	private void HandleBuildingsChanged()
	{
		SyncBuildingTaskProducers();
		ReevaluateLabelingWork();
	}

	private void HandleCapsuleUndocked(uint buildingId, CapsuleDock dock)
	{
		if (dock is not CapsuleBuffer buffer)
			return;

		InvalidateOwnedLabelingTask(buffer, TaskInvalidationReason.SourceUnavailable);
		EvaluateStoringWork(buildingId);
	}

	private void HandleRuleRoutingEvaluated(
		uint buildingId,
		CapsuleBuffer buffer,
		bool _)
	{
		if (buffer == null)
			return;

		if (IsLabelingTargetReady(buildingId, buffer))
			TryRequestLabelingTask(buildingId, buffer);
		else
			InvalidateOwnedLabelingTask(buffer, TaskInvalidationReason.RuleChanged);

		EvaluateStoringWork(buildingId);
	}

	private void ReevaluateBufferWork(CapsuleBuffer buffer)
	{
		if (buffer == null || CapsuleBufferService == null ||
			CapsuleBufferService.TryGetRegisteredBuildingId(buffer, out uint buildingId) == false)
		{
			return;
		}

		if (IsLabelingTargetReady(buildingId, buffer))
			TryRequestLabelingTask(buildingId, buffer);
		else
			InvalidateOwnedLabelingTask(buffer, TaskInvalidationReason.RuleChanged);

		EvaluateStoringWork(buildingId);
	}

	private void OnLabelingTaskEnded(LabelingTask task)
	{
		CapsuleBuffer buffer = task?.TargetBuffer;
		if (buffer == null)
			return;

		if (labelingTasksByBuffer.TryGetValue(buffer, out LabelingTask owner) && ReferenceEquals(owner, task))
			labelingTasksByBuffer.Remove(buffer);

		if (CapsuleBufferService != null &&
			CapsuleBufferService.TryGetRegisteredBuildingId(buffer, out uint buildingId))
		{
			TryRequestLabelingTask(buildingId, buffer);
		}
	}

	private void InvalidateOwnedLabelingTask(
		CapsuleBuffer buffer,
		TaskInvalidationReason reason)
	{
		if (buffer == null || labelingTasksByBuffer.TryGetValue(buffer, out LabelingTask task) == false)
			return;

		if (task == null ||
			task.CurrentStatus == WorkerTask.Status.Completed ||
			task.CurrentStatus == WorkerTask.Status.Invalidated)
		{
			labelingTasksByBuffer.Remove(buffer);
			return;
		}

		if (task.IsTaskEnd)
			return;

		if (TaskMgr?.IsManagingTask(task) == true)
			TaskMgr.InvalidateTask(task, reason);
		else
			labelingTasksByBuffer.Remove(buffer);
	}

	private void SyncBuildingTaskProducers()
	{
		BuildingManager buildingManager = boundBuildingManager ??
			(GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null);
		ItemTransferTaskScheduler scheduler = boundItemTransferTaskScheduler ??
			(GameContext.HasInstance ? GameContext.Instance.ItemTransferTaskScheduler : null);
		if (buildingManager == null || scheduler == null)
			return;

		buildingIdScratch.Clear();
		IReadOnlyList<Building> buildings = buildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			uint buildingId = buildings[i]?.RuntimeBuildingId ?? 0;
			if (buildingId == 0)
				continue;

			buildingIdScratch.Add(buildingId);
			scheduler.Register(
				buildingId,
				ItemTransferScheduleMode.Storing,
				WorkerTask.TaskType.Storing,
				TryBuildStoringItemTransferTask);
			storingScheduleBuildingIds.Add(buildingId);
			EvaluateStoringWork(buildingId);
		}

		uint[] registeredIds = new uint[storingScheduleBuildingIds.Count];
		storingScheduleBuildingIds.CopyTo(registeredIds);
		for (int i = 0; i < registeredIds.Length; ++i)
		{
			uint buildingId = registeredIds[i];
			if (buildingIdScratch.Contains(buildingId))
				continue;

			scheduler.Unregister(buildingId, ItemTransferScheduleMode.Storing);
			storingScheduleBuildingIds.Remove(buildingId);
		}
	}

	private void UnregisterStoringTaskProducers()
	{
		ItemTransferTaskScheduler scheduler = boundItemTransferTaskScheduler ??
			(GameContext.HasInstance ? GameContext.Instance.ItemTransferTaskScheduler : null);
		if (scheduler != null)
		{
			foreach (uint buildingId in storingScheduleBuildingIds)
				scheduler.Unregister(buildingId, ItemTransferScheduleMode.Storing);
		}

		storingScheduleBuildingIds.Clear();
	}

	private ItemTransferScheduleResult TryBuildStoringItemTransferTask(
		ItemTransferScheduleRequest request,
		out WorkerTask task)
	{
		task = null;
		if (request.Worker == null || request.Worker.CanAcceptGeneralTask(request.TaskType) == false)
			return ItemTransferScheduleResult.WorkerRejected;

		if (storingPlanner == null ||
			storingPlanner.HasPendingCollectWork(request.BuildingId) == false ||
			storingPlanner.BuildItemTransferTask(
				request.Worker,
				request.BuildingId,
				out ItemTransferTask itemTransferTask) == false)
		{
			return ItemTransferScheduleResult.NoWork;
		}

		task = itemTransferTask;
		return ItemTransferScheduleResult.Scheduled;
	}

	private void EvaluateStoringWork(uint buildingId)
	{
		if (buildingId == 0 || storingPlanner == null)
			return;

		ItemTransferTaskScheduler scheduler = boundItemTransferTaskScheduler ??
			(GameContext.HasInstance ? GameContext.Instance.ItemTransferTaskScheduler : null);
		if (scheduler == null)
			return;
		if (storingPlanner.HasPendingCollectWork(buildingId))
			scheduler.MarkDirty(buildingId, ItemTransferScheduleMode.Storing);
		else
			scheduler.ClearDirty(buildingId, ItemTransferScheduleMode.Storing);
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
		if (rocket == null || TaskMgr == null || TaskMgr.HasFacilityDependency(rocket))
			return;

		uint destinationBuildingId = ResolveUnloadingDestinationBuildingId(rocket);
		if (destinationBuildingId == 0)
		{
			if (GameContext.HasInstance)
				GameContext.Instance.CapsuleRelocateCoordinator.CancelPendingRequests(rocket);
			return;
		}

		CapsuleLogisticsState requiredState = rocket.RefreshPayloadLogisticsState();

		GameContext.Instance.CapsuleRelocateCoordinator.RequestSend(new CapsuleRelocateSendRequest(
			rocket,
			CapsuleDockState.InboundSource,
			requiredState,
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

	public void RetryActiveRocketUnloadingTasks()
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
