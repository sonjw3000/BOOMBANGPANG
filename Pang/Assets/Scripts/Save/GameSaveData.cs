using System;
using System.Collections.Generic;

[Serializable]
public sealed class GameSaveData
{
	public int Version = 1;
	public string SavedAtUtc;

	public TimeSaveData Time = new();
	public EconomySaveData Economy = new();
	public ItemLedgerSaveData ItemLedger = new();
	public ZoneManagerSaveData Zones = new();
	public GridMapSaveData Grid = new();
	public ContractServiceSaveData Contracts = new();
	public OrderManagerSaveData Orders = new();
	public DeliveryQueueSaveData DeliveryQueue = new();
	public OrderDeliverySaveData OrderDelivery = new();
	public RocketManagerSaveData RocketManager = new();
	public BoxRegistrySaveData BoxRegistry = new();
	public WorkerManagerSaveData WorkerManager = new();
	public WorkJobCounterSaveData WorkJobCounters = new();
	public List<PlaceableSaveData> Placeables = new();
	public List<TaskSaveData> Tasks = new();
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
public sealed class ZoneManagerSaveData
{
	public List<ZoneSaveData> Zones = new();
}

[Serializable]
public sealed class ZoneSaveData
{
	public string Name;
	public ZoneType Type;
	public int Floor;
	public RectIntSaveData Bounds = new();
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
	public uint BoxId;
	public float TimeRemain;
}

[Serializable]
public sealed class RocketManagerSaveData
{
	public float TimeSinceLastSpawn;
}

[Serializable]
public sealed class BoxRegistrySaveData
{
	public uint NextBoxId = 1;
	public List<BoxSaveData> Boxes = new();
	public List<uint> InactivePoolBoxIds = new();
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
	public PackingStationSaveData PackingStation;
	public RocketSaveData Rocket;
	public LaunchStationSaveData LaunchStation;
}

[Serializable]
public sealed class WorkerSaveData
{
	public uint WorkerId;
	public string FirstName;
	public string LastName;
	public string VisualId;
	public WorkerType WorkerType;
	public WorkerAbility Abilities;
	public int MonthlyCost;
	public float BaseMoveSpeedMultiplier;
	public float MinimumMoveSpeedMultiplier;
	public float BaseWorkSpeedMultiplier;
	public float MinimumWorkSpeedMultiplier;
	public WorkerTask.TaskType MainTaskType;
	public WorkerStatusAction StatusAction;
	public WorkerStatusTarget StatusTarget;
	public float Fatigue;
	public float Experience;
	public float BatteryLevel;
	public float BatteryEfficiency;
	public uint CarryingBoxId;
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
	public List<uint> BoxIds = new();
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
}

[Serializable]
public sealed class LaunchStationSaveData
{
	public List<uint> CargoQueueBoxIds = new();
	public uint LoadedCargoBoxId = 0;
	public bool ReadyToLaunch;
}

[Serializable]
public sealed class BoxSaveData
{
	public uint BoxId;
	public BoxType BoxType;
	public string ConcreteType;
	public List<ItemStackSaveData> Stacks = new();
}

[Serializable]
public sealed class BoxWithOrderSaveData
{
	public uint BoxId = 0;
	public WorkJobSaveData Job;
}

[Serializable]
public sealed class ItemStackSaveData
{
	public uint ItemId;
	public int Quantity;
	public bool IsPackage;
	public int RelatedOrderLineId = -1;
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
	public PackingTaskSaveData Packing;
	public WaterTaskSaveData Water;
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
	public bool IsLoadEnd;
}

[Serializable]
public sealed class PickingTaskSaveData
{
	public WorkJobSaveData Job;
	public bool IsTaskEnd;
}

[Serializable]
public sealed class StoringTaskSaveData
{
	public WorkJobSaveData Job;
	public StoringTask.Phase CurrentPhase;
	public bool IsJobEnd;
	public WorkLineSaveData PlacingLine;
}

[Serializable]
public sealed class PackingTaskSaveData
{
	public int TargetStationId;
	public bool IsTaskEnd;
}

[Serializable]
public sealed class WaterTaskSaveData
{
	public TransferContextSaveData From;
	public TransferContextSaveData To;
	public bool WorkPhase;
}

[Serializable]
public sealed class TransferContextSaveData
{
	public int TargetPlaceableId;
	public TransferObjectType TransferType;
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
