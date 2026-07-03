using System;
using System.Collections.Generic;

namespace Assets.Scripts.Save
{
	namespace JsonData
	{
		[System.Serializable]
		public class Placeable
		{
			public int x, y, z;
			public string placeableID;
			public FacingDirection facingDirection;
		}

		[System.Serializable]
		public class PlaceableData
		{
			public List<Placeable> placeables;
		}

		[System.Serializable]
		public class GridMapData
		{
			public int X, Y, Z;
			public int[] Tiles;
		}

		[System.Serializable]
		public class BasicData
		{
			public int SaveCount;
			public string Version;
		}

		[System.Serializable]
		public class SaveData
		{
			public BasicData BasicData;
			public GridMapData MapData;
			public PlaceableData PlaceableData;
		}
	}
}


[Serializable]
public sealed class GameSaveData
{
	public int Version = 2;
	public string SavedAtUtc;

	public PolicyStateSaveData Policy = new();
	public TimeSaveData Time = new();
	public EconomySaveData Economy = new();
	public ItemLedgerSaveData ItemLedger = new();
	public BuildingManagerSaveData Buildings = new();
	public BuildingFootprintServiceSaveData BuildingFootprints = new();
	public ZoneManagerSaveData Zones = new();
	public GridMapSaveData Grid = new();
	public ContractServiceSaveData Contracts = new();
	public OrderManagerSaveData Orders = new();
	public OutboundPickingManifestSaveData OutboundPickingManifests = new();
	public DeliveryQueueSaveData DeliveryQueue = new();
	public OrderDeliverySaveData OrderDelivery = new();
	[UnityEngine.Serialization.FormerlySerializedAs("RocketManager")]
	public RocketServiceSaveData RocketService = new();
	public BoxRegistrySaveData BoxRegistry = new();
	public WorkerManagerSaveData WorkerManager = new();
	public WorkJobCounterSaveData WorkJobCounters = new();
	public List<PlaceableSaveData> Placeables = new();
	public List<TaskSaveData> Tasks = new();
}

[Serializable]
public sealed class PolicyStateSaveData
{
	public WorkPolicyRuntimeSaveData WorkSpeed = new();
	public InboundWorkflowPolicySaveData WorkApproach = new();
	public OutboundWorkflowPolicySaveData OutboundWorkApproach = new();
}

[Serializable]
public sealed class WorkPolicyRuntimeSaveData
{
	public List<WorkerPolicyTypeFloatSaveData> MoveSpeedMultipliers = new();
	public List<WorkerPolicyTypeFloatSaveData> WorkSpeedMultipliers = new();
}

[Serializable]
public sealed class WorkerPolicyTypeFloatSaveData
{
	public WorkerPolicyType WorkerPolicyType;
	public float Value = 1.0f;
}

[Serializable]
public sealed class InboundWorkflowPolicySaveData
{
	public CollectingPolicyType StoringCollectingPolicy = CollectingPolicyType.Nearest;
	public PlacingPolicyType StoringPlacingPolicy = PlacingPolicyType.BelowAverageFilledNearest;
	public uint UnloadingDestinationBuildingId;
}

[Serializable]
public sealed class OutboundWorkflowPolicySaveData
{
	public CollectingPolicyType PickingCollectingPolicy = CollectingPolicyType.Nearest;
}

[Serializable]
public sealed class OutboundPickingManifestSaveData
{
	public List<PickingManifestSaveData> Manifests = new();
}

[Serializable]
public sealed class PickingManifestSaveData
{
	public uint BoxId;
	public List<PickingManifestLineSaveData> Lines = new();
}

[Serializable]
public sealed class PickingManifestLineSaveData
{
	public int OrderLineId = -1;
	public uint ItemId;
	public int PickedQuantity;
	public int PackedQuantity;
	public PackageOutboundStage OutboundStage = PackageOutboundStage.None;
}

[Serializable]
public sealed class TimeSaveData
{
	public float TimeElapsed;
	public int ElapsedWeek;
	public int ElapsedMonth;
	public float TimeScale;
}

[Serializable]
public sealed class EconomySaveData
{
	public int Money;
	public float Reputation;
}

[Serializable]
public sealed class ItemLedgerSaveData
{
	public List<ItemQuantitySaveData> Totals = new();
	public List<ItemQuantitySaveData> Reserved = new();
	public List<uint> OrderableItems = new();
}

[Serializable]
public sealed class BuildingManagerSaveData
{
	public List<BuildingSaveData> Buildings = new();
}

[Serializable]
public sealed class BuildingSaveData
{
	public uint RuntimeBuildingId;
	public string Name;
	public BuildingType Type;
	public BuildingState State;
	public BuildingWorkScope WorkScope;
	public bool OverrideCapsuleThreshold;
	public float CapsuleThresholdPercent;
	public List<uint> OutputBuildingIds = new();
}

[Serializable]
public sealed class BuildingFootprintServiceSaveData
{
	public List<BuildingFootprintSaveData> Footprints = new();
}

[Serializable]
public sealed class BuildingFootprintSaveData
{
	public uint RuntimeBuildingId;
	public int Floor;
	public RectIntSaveData Bounds = new();
}

[Serializable]
public sealed class ZoneManagerSaveData
{
	public List<ZoneSaveData> Zones = new();
}

[Serializable]
public sealed class ZoneSaveData
{
	public string Name;
	public ZoneType Type;
	public uint RuntimeBuildingId;
	public int Floor;
	public RectIntSaveData Bounds = new();
	public ZoneRuleSaveData Rule = new();
}

[Serializable]
public sealed class ZoneRuleSaveData
{
	public int Priority;
	public ZoneItemRuleSaveData ItemRule = new();
	public ZoneWorkerRuleSaveData WorkerRule = new();
}

[Serializable]
public sealed class ZoneItemRuleSaveData
{
	public ItemTag RequiredItemTags = ItemTag.None;
	public ItemTag ForbiddenItemTags = ItemTag.None;
	public List<uint> WhiteListItemIds = new();
	public List<uint> BlackListItemIds = new();
}

[Serializable]
public sealed class ZoneWorkerRuleSaveData
{
	public WorkerKind RequiredWorkerKind = WorkerKind.None;
	public List<HumanType> RequiredHumanTypes = new();
	public List<HumanType> ForbiddenHumanTypes = new();
	public List<RobotType> RequiredRobotTypes = new();
	public List<RobotType> ForbiddenRobotTypes = new();
	public WorkerAbility RequiredWorkerAbility = WorkerAbility.None;
}

[Serializable]
public sealed class GridMapSaveData
{
	public Int3SaveData MapSize = new();
	public int[] Tiles;
}

[Serializable]
public sealed class ContractServiceSaveData
{
	public List<ContractRuntimeSaveData> ActiveContracts = new();
}

[Serializable]
public sealed class ContractRuntimeSaveData
{
	public uint ContractId;
	public Assets.Scripts.Contract.ContractType Type;
	public int RemainingDuration;
	public int DeliveryDelta;
	public bool AutoRenewal;
}

[Serializable]
public sealed class OrderManagerSaveData
{
	public int NextOrderId;
	public List<OrderSaveData> Orders = new();
}

[Serializable]
public sealed class OrderSaveData
{
	public int OrderId;
	public OrderTotalStatus Status;
	public List<OrderLineSaveData> Lines = new();
}

[Serializable]
public sealed class OrderLineSaveData
{
	public int LineId;
	public uint ItemId;
	public int Quantity;
	public OrderStatus Status;
	public uint SourceContractId;
	public int StartWeek;
	public int DueWeek;
	public int BaseReward;
	public int DelayPenalty;
	public float ReputationChange;
	public int PickingAllocatedQuantity;
	public int PickingCompletedQuantity;
	public int PackagingCompletedQuantity;
	public int WaitingForShippingQuantity;
	public int ShippingQuantity;
	public int InDeliveryQuantity;
	public int CompletedQuantity;
}

[Serializable]
public sealed class DeliveryQueueSaveData
{
	public List<DeliveryRequestSaveData> Requests = new();
}

[Serializable]
public sealed class DeliveryRequestSaveData
{
	public uint ContractId;
	public uint ItemId;
	public int Quantity;
}

[Serializable]
public sealed class OrderDeliverySaveData
{
	public List<DeliveryProgressSaveData> Progresses = new();
}

[Serializable]
public sealed class DeliveryProgressSaveData
{
	public BoxReferenceSaveData Box;
	public float TimeRemain;
}

[Serializable]
public sealed class RocketServiceSaveData
{
	public float TimeSinceLastSpawn;
	public List<RocketSaveData> ActiveRockets = new();
}

[Serializable]
public sealed class BoxRegistrySaveData
{
	public List<BoxIdCounterSaveData> NextBoxIds = new();
	public List<BoxSaveData> Boxes = new();
}

[Serializable]
public sealed class BoxIdCounterSaveData
{
	public BoxType BoxType;
	public uint NextBoxId = 1;
}

[Serializable]
public sealed class BoxReferenceSaveData
{
	public BoxType BoxType = BoxType.None;
	public uint BoxId = 0;
}

[Serializable]
public sealed class WorkerManagerSaveData
{
	public uint NextWorkerId;
}

[Serializable]
public sealed class WorkJobCounterSaveData
{
	public int NextPickingJobId;
	public int NextStoringJobId;
}

[Serializable]
public sealed class PlaceableSaveData
{
	public int SaveId;
	public string PlaceableId;
	public FacingDirection FacingDirection;
	public Int3SaveData GridPosition = new();
	public bool IsWorker;

	public WorkerSaveData Worker;
	public ShelfContainerSaveData Shelf;
	public CargoPortSaveData CargoPort;
	public BoxPoolSaveData BoxPool;
	public CapsuleBufferSaveData CapsuleBuffer;
	public PackingStationSaveData PackingStation;
	public RocketSaveData Rocket;
	public LaunchStationSaveData LaunchStation;
}

[Serializable]
public sealed class WorkerSaveData
{
	public uint WorkerId;
	public uint PrimaryBuildingId;
	public string FirstName;
	public string LastName;
	public string VisualId;
	public WorkerKind WorkerKind;
	public HumanType HumanType;
	public RobotType RobotType;
	public WorkerAbility Abilities;
	public int MonthlyCost;
	public float BaseMoveSpeedMultiplier;
	public float MinimumMoveSpeedMultiplier;
	public float BaseWorkSpeedMultiplier;
	public float MinimumWorkSpeedMultiplier;
	public WorkerTask.TaskType MainTaskType;
	public List<WorkerTask.TaskType> AssignedTaskTypes = new();
	public WorkerStatusAction StatusAction;
	public WorkerStatusTarget StatusTarget;
	public float Fatigue;
	public float Experience;
	public float BatteryLevel;
	public float BatteryEfficiency;
	public BoxReferenceSaveData CarryingBox;
}

[Serializable]
public sealed class ShelfContainerSaveData
{
	public List<ItemStackSaveData> Stacks = new();
	public List<ItemQuantitySaveData> ReservedPick = new();
}

[Serializable]
public sealed class CargoPortSaveData
{
	public bool InputReady;
}

[Serializable]
public sealed class BoxPoolSaveData
{
	public List<BoxReferenceSaveData> Boxes = new();
}

[Serializable]
public sealed class CapsuleBufferSaveData
{
	public BoxReferenceSaveData Box;
	public CapsuleDockState DockState;
}

[Serializable]
public sealed class PackingStationSaveData
{
	public List<ItemStackSaveData> PackedItems = new();
	public BoxWithOrderSaveData WaitingBox;
	public BoxWithOrderSaveData CurrentBox;
	public BoxWithOrderSaveData EndBox;
	public int CurrentWorkerId = -1;
	public int IncomingWorkerId = -1;
	public bool IncomingRequestSuspended;
}

[Serializable]
public sealed class RocketSaveData
{
	public Rocket.RocketState State;
	public Int3SaveData LandingPoint = new();
	public float FallingSpeed;
	public float LaunchSpeed;
	public float LaunchHeight;
	public Vector3SaveData WorldPosition = new();
	public Vector3SaveData ForwardVector = new();
	public BoxReferenceSaveData DockedCapsule;
}

[Serializable]
public sealed class LaunchStationSaveData
{
	public List<BoxReferenceSaveData> CargoQueueBoxes = new();
	public BoxReferenceSaveData LoadedCargoBox;
	public bool ReadyToLaunch;
}

[Serializable]
public sealed class BoxSaveData
{
	public uint BoxId;
	public BoxType BoxType;
	public string ConcreteType;
	public CapsuleLogisticsState CapsuleLogisticsState;
	public List<ItemStackSaveData> Stacks = new();
}

[Serializable]
public sealed class BoxWithOrderSaveData
{
	public BoxReferenceSaveData Box;
	public WorkJobSaveData Job;
}

[Serializable]
public sealed class ItemStackSaveData
{
	public uint ItemId;
	public int Quantity;
	public byte Freshness = 100;
	public byte Damage;
	public ItemStatus Status = ItemStatus.None;
	public PackageOutboundStage OutboundStage = PackageOutboundStage.None;
}

[Serializable]
public sealed class ItemQuantitySaveData
{
	public uint ItemId;
	public int Quantity;
}

[Serializable]
public sealed class TaskSaveData
{
	public WorkerTask.TaskType TaskType;
	public bool IsInProgress;
	public uint AssignedWorkerId;

	public UnloadingTaskSaveData Unloading;
	public LoadingTaskSaveData Loading;
	public PickingTaskSaveData Picking;
	public StoringTaskSaveData Storing;
	public CapsuleTransferTaskSaveData CapsuleTransfer;
	public CargoTransferTaskSaveData CargoTransfer;
	public PackingTaskSaveData Packing;
}

[Serializable]
public sealed class UnloadingTaskSaveData
{
	public int TargetRocketId;
	public int CargoPortId;
	public bool IsUnloadEnd;
}

[Serializable]
public sealed class LoadingTaskSaveData
{
	public int TargetPortId;
	public int TargetStationId;
	public bool IsLoadEnd;
}

[Serializable]
public sealed class PickingTaskSaveData
{
	public WorkJobSaveData Job;
		public uint BuildingId;
		public bool IsPickingPhaseEnd;
		public bool IsTaskEnd;
		public WorkLineSaveData CurrentPlaceLine;
		public int PlacingLineIndex;
	}

[Serializable]
public sealed class StoringTaskSaveData
{
	public uint BuildingId;
	public WorkJobSaveData Job;
	public StoringTask.Phase CurrentPhase;
	public bool IsJobEnd;
	public WorkLineSaveData PlacingLine;
}

[Serializable]
public sealed class CapsuleTransferTaskSaveData
{
	public bool HasTaskType;
	public WorkerTask.TaskType TaskType;
	public bool IsInbound;
	public uint BuildingId;
	public int SourcePlaceableId = -1;
	public int TargetPlaceableId = -1;
}

[Serializable]
public sealed class CargoTransferTaskSaveData
{
	public int SourcePortId = -1;
	public int TargetPortId = -1;
}

[Serializable]
public sealed class PackingTaskSaveData
{
	public int TargetStationId;
	public bool IsTaskEnd;
}

[Serializable]
public sealed class WorkJobSaveData
{
	public int JobId;
	public WorkOp WorkType;
	public int CurrentLineIndex;
	public List<WorkLineSaveData> Lines = new();
}

[Serializable]
public sealed class WorkLineSaveData
{
	public WorkLineAction Action;
	public int SourcePlaceableId;
	public uint ItemId;
	public int Quantity;
	public int CompleteQuantity;
	public int RelatedOrderLineId;
}

[Serializable]
public struct Int3SaveData
{
	public int X;
	public int Y;
	public int Z;

	public Int3SaveData(int x, int y, int z)
	{
		X = x;
		Y = y;
		Z = z;
	}
}

[Serializable]
public struct RectIntSaveData
{
	public int X;
	public int Y;
	public int Width;
	public int Height;

	public RectIntSaveData(int x, int y, int width, int height)
	{
		X = x;
		Y = y;
		Width = width;
		Height = height;
	}
}

[Serializable]
public struct Vector3SaveData
{
	public float X;
	public float Y;
	public float Z;

	public Vector3SaveData(float x, float y, float z)
	{
		X = x;
		Y = y;
		Z = z;
	}
}
