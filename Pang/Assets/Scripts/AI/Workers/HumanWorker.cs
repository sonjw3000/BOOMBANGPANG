using UnityEngine;

public class HumanWorker : AIWorker
{
	private const float CargoCapsuleHandlingFactor = 0.6f;

	private static WorkPolicyService WorkPolicy => GameContext.Instance.WMSys.WorkPolicyService;
	private float experience;

	[SerializeField] private float fatigue;
	[SerializeField] private float unsafeExposure;
	[SerializeField] private uint incidentRandomState;
	[SerializeField, Range(0.0f, 1.0f)] private float incidentRoll;
	[SerializeField] private bool incidentStateInitialized;
	[SerializeField] private int incidentCount;

	private HumanWorkHandlingResult pendingWorkHandling;
	private bool hasPendingWorkHandling;
	private HumanIncidentPayload pendingIncident;

	public float Fatigue => fatigue;
	public float UnsafeExposure => unsafeExposure;
	public float IncidentRoll => incidentRoll;
	public uint IncidentRandomState => incidentRandomState;
	public bool HasIncidentState => incidentStateInitialized;
	public int IncidentCount => Mathf.Max(0, incidentCount);
	public bool HasPendingIncident => pendingIncident != null;
	public override bool HasPendingBlockingIncident => HasPendingIncident;
	[SerializeField] private float fatigueIncreasePerTask = 2.0f;
	[SerializeField] private bool isSuitRemoved;

	public bool IsSuitRemoved => isSuitRemoved;

	protected override IBaseNode BuildWorkerBaseNode()
	{
		SelectorNode root = new();
		root.Add(BuildHumanIncidentNode());
		root.Add(BuildRecoveryNode(WorkerStatusTarget.RestFacility, InteractionKind.Rest));
		return root;
	}

	public override float GetWorkSpeedMultiplier()
		// => Mathf.Lerp(BaseWorkSpeedMultiplier, MinimumWorkSpeedMultiplier, fatigue / 100.0f);
	{
		float workerMultiplier = Mathf.Lerp(
			BaseWorkSpeedMultiplier,
			MinimumWorkSpeedMultiplier,
			fatigue / 100.0f);
		if (isSuitRemoved == false || GameContext.HasInstance == false)
			return workerMultiplier;

		return workerMultiplier * GameContext.Instance.OxygenSvc.GetSuitlessWorkSpeedMultiplier(this);
	}

	public override float GetMoveSpeedMultiplier()
		=> Mathf.Lerp(BaseMoveSpeedMultiplier, MinimumMoveSpeedMultiplier, fatigue / 100.0f);

	public override void OnTaskCompleted()
	{
		base.OnTaskCompleted();
		experience += 10.0f;
	}

	public override void TickVitals(float deltaTime) { }

	public override void AddFatigue(float amount)
		=> fatigue = Mathf.Clamp(fatigue + Mathf.Max(0.0f, amount), 0.0f, 100.0f);

	public override float GetFatigue() => fatigue;
	public override bool NeedsRecovery() => fatigue >= WorkPolicy.WorkerRestFatigueThreshold;
	public override bool IsRecoveryComplete() => fatigue <= WorkPolicy.WorkerRestTargetFatigue;

	internal bool ShouldRequestRecoveryBeforeTask(WorkerTask task)
	{
		if (task == null ||
			GameContext.HasInstance == false ||
			GameContext.Instance.WMSys?.WorkPolicyService == null)
		{
			return false;
		}

		WorkPolicyService policy = GameContext.Instance.WMSys.WorkPolicyService;
		float requiredReserve = CalculateTaskFatigueReserve(task, policy);
		return requiredReserve > 0.0f &&
			fatigue + requiredReserve >= policy.WorkerRestFatigueThreshold;
	}

	private float CalculateTaskFatigueReserve(WorkerTask task, WorkPolicyService policy)
	{
		return task.Type switch
		{
			WorkerTask.TaskType.Unloading or
			WorkerTask.TaskType.IB or
			WorkerTask.TaskType.CapsuleClear or
			WorkerTask.TaskType.CapsuleSupply or
			WorkerTask.TaskType.OB or
			WorkerTask.TaskType.Loading or
			WorkerTask.TaskType.CargoTransfer => CalculateBoxTransferFatigueReserve(task, policy),
			WorkerTask.TaskType.Storing or
			WorkerTask.TaskType.Picking or
			WorkerTask.TaskType.LaunchSort or
			WorkerTask.TaskType.WasteCollection =>
				policy.GetWorkFatigue(this, WorkActionType.PickItem) +
				policy.GetWorkFatigue(this, WorkActionType.PutItem),
			WorkerTask.TaskType.PackingInput =>
				policy.GetWorkFatigue(this, WorkActionType.PickItem) +
				policy.GetWorkFatigue(this, WorkActionType.PutBox),
			WorkerTask.TaskType.PackingOutput =>
				policy.GetWorkFatigue(this, WorkActionType.PickBox) +
				policy.GetWorkFatigue(this, WorkActionType.MoveBox) +
				policy.GetWorkFatigue(this, WorkActionType.PutItem),
			WorkerTask.TaskType.Packing =>
				policy.GetWorkFatigue(this, WorkActionType.PackItem) +
				policy.GetWorkFatigue(this, WorkActionType.MoveBox),
			WorkerTask.TaskType.Labeling =>
				policy.GetWorkFatigue(this, WorkActionType.LabelItem),
			_ => 0.0f,
		};
	}

	private float CalculateBoxTransferFatigueReserve(WorkerTask task, WorkPolicyService policy)
	{
		float pickFatigue = policy.GetWorkFatigue(this, WorkActionType.PickBox);
		float putFatigue = policy.GetWorkFatigue(this, WorkActionType.PutBox);
		if (task is CapsuleRelocationTask relocationTask &&
			relocationTask.SourceDock?.DockedCapsule is CargoCapsule capsule)
		{
			HumanWorkHandlingResult handling = BuildCapsuleHandlingEstimate(capsule);
			HumanIncidentService incidentService = GameContext.Instance.HumanIncident;
			if (incidentService != null)
			{
				pickFatigue = incidentService.CalculateActionFatigue(this, pickFatigue, in handling);
				putFatigue = incidentService.CalculateActionFatigue(this, putFatigue, in handling);
			}
		}

		return pickFatigue +
			policy.GetWorkFatigue(this, WorkActionType.MoveBox) +
			putFatigue;
	}

	public override void TickRecovery(float recoveryPerSecond, float deltaTime)
	{
		float elapsed = Mathf.Max(0.0f, deltaTime);
		fatigue = Mathf.Max(0.0f, fatigue - Mathf.Max(0.0f, recoveryPerSecond) * elapsed);
		if (GameContext.HasInstance && GameContext.Instance.HumanIncident != null)
		{
			unsafeExposure = Mathf.Max(
				0.0f,
				unsafeExposure - GameContext.Instance.HumanIncident.GetExposureRecoveryPerSecond() * elapsed);
		}
	}

	public override WorkerStatusAction GetRecoveryAction() => WorkerStatusAction.Resting;

	private static HumanWorkHandlingResult BuildCapsuleHandlingEstimate(CargoCapsule capsule)
	{
		float totalWeight = 0.0f;
		int totalQuantity = 0;
		ItemDatabase itemDatabase = GameContext.Instance.ItemDB;
		if (capsule != null && itemDatabase != null)
		{
			for (int i = 0; i < capsule.Stacks.Count; ++i)
			{
				ItemStack stack = capsule.Stacks[i];
				if (stack == null || stack.Quantity <= 0 ||
					itemDatabase.GetItemData(stack.ItemID, out ItemDefinition item) == false)
				{
					continue;
				}

				totalWeight += item.Weight * stack.Quantity;
				totalQuantity += stack.Quantity;
			}
		}

		return new HumanWorkHandlingResult(
			0,
			totalQuantity,
			totalWeight * CargoCapsuleHandlingFactor,
			ItemTag.None,
			capsule);
	}

	public void EnsureIncidentState(uint globalSeed)
	{
		if (incidentStateInitialized)
			return;

		incidentRandomState = HumanIncidentService.BuildWorkerSeed(globalSeed, WorkerID);
		incidentRoll = HumanIncidentService.NextUnitFloat(ref incidentRandomState);
		incidentStateInitialized = true;
	}

	public void ResetIncidentState(uint globalSeed)
	{
		unsafeExposure = 0.0f;
		incidentRandomState = 0;
		incidentRoll = 0.0f;
		incidentStateInitialized = false;
		incidentCount = 0;
		pendingIncident = null;
		ClearPendingWorkHandling();
		EnsureIncidentState(globalSeed);
	}

	public float AddUnsafeExposure(float amount, float maximum)
	{
		float previous = unsafeExposure;
		unsafeExposure = Mathf.Clamp(
			unsafeExposure + Mathf.Max(0.0f, amount),
			0.0f,
			Mathf.Max(0.0f, maximum));
		return unsafeExposure - previous;
	}

	public void ReduceUnsafeExposure(float remainingRatio)
		=> unsafeExposure *= Mathf.Clamp01(remainingRatio);

	public uint BeginNextIncidentCycle()
	{
		uint eventSeed = HumanIncidentService.NextUInt(ref incidentRandomState);
		incidentRoll = HumanIncidentService.NextUnitFloat(ref incidentRandomState);
		if (incidentCount < int.MaxValue)
			++incidentCount;
		return eventSeed;
	}

	public void SetPendingIncident(HumanIncidentPayload payload)
		=> pendingIncident = payload;

	public bool TryGetPendingIncident(out HumanIncidentPayload payload)
	{
		payload = pendingIncident;
		return payload != null;
	}

	public void ClearPendingIncident()
		=> pendingIncident = null;

	public override void ClearPendingWorkHandling()
	{
		pendingWorkHandling = default;
		hasPendingWorkHandling = false;
	}

	public override void ReportItemHandling(uint itemId, int quantity, IItemContainer destination)
	{
		if (quantity <= 0 || GameContext.HasInstance == false ||
			GameContext.Instance.ItemDB.GetItemData(itemId, out ItemDefinition item) == false)
		{
			return;
		}

		float weight = item.Weight * quantity;
		if (hasPendingWorkHandling)
		{
			uint combinedItemId = pendingWorkHandling.ItemId == itemId ? itemId : 0;
			pendingWorkHandling = new HumanWorkHandlingResult(
				combinedItemId,
				pendingWorkHandling.Quantity + quantity,
				pendingWorkHandling.HandlingWeightKg + weight,
				pendingWorkHandling.ItemTags | item.Tag,
				destination ?? pendingWorkHandling.Destination);
		}
		else
		{
			pendingWorkHandling = new HumanWorkHandlingResult(itemId, quantity, weight, item.Tag, destination);
			hasPendingWorkHandling = true;
		}
	}

	public override void ReportBoxHandling(BoxBase box, float handlingFactor = 1.0f)
	{
		if (box == null || GameContext.HasInstance == false)
			return;

		if (box is CargoCapsule)
			handlingFactor *= CargoCapsuleHandlingFactor;

		float totalWeight = 0.0f;
		int totalQuantity = 0;
		ItemTag tags = ItemTag.None;
		uint singleItemId = 0;
		bool hasItemId = false;
		bool hasMixedItemIds = false;
		for (int i = 0; i < box.Stacks.Count; ++i)
		{
			ItemStack stack = box.Stacks[i];
			if (stack == null || stack.Quantity <= 0 ||
				GameContext.Instance.ItemDB.GetItemData(stack.ItemID, out ItemDefinition item) == false)
			{
				continue;
			}

			totalWeight += item.Weight * stack.Quantity;
			totalQuantity += stack.Quantity;
			tags |= item.Tag;
			if (hasItemId == false)
			{
				singleItemId = stack.ItemID;
				hasItemId = true;
			}
			else if (singleItemId != stack.ItemID)
			{
				hasMixedItemIds = true;
			}
		}

		pendingWorkHandling = new HumanWorkHandlingResult(
			hasMixedItemIds ? 0 : singleItemId,
			totalQuantity,
			totalWeight * Mathf.Max(0.0f, handlingFactor),
			tags,
			box);
		hasPendingWorkHandling = true;
	}

	public override bool TryConsumePendingWorkHandling(out HumanWorkHandlingResult result)
	{
		result = pendingWorkHandling;
		bool hadResult = hasPendingWorkHandling;
		ClearPendingWorkHandling();
		return hadResult;
	}

	public override void ApplyCarriedMovementFatigue(int travelledCells)
	{
		if (travelledCells <= 0 || CarryingAbility?.CarryingBox == null || GameContext.HasInstance == false)
			return;

		float weight = 0.0f;
		BoxBase box = CarryingAbility.CarryingBox;
		for (int i = 0; i < box.Stacks.Count; ++i)
		{
			ItemStack stack = box.Stacks[i];
			if (stack != null)
				weight += GameContext.Instance.ItemDB.GetItemWeight(stack.ItemID) * stack.Quantity;
		}

		if (weight <= 0.0f)
			return;

		float loadRatio = weight / SafeHandlingWeightKg;
		AddFatigue(0.05f * Mathf.Max(1.0f, loadRatio) * travelledCells);
	}

	internal void PrepareForAirlockTransit(AirlockDirection direction)
	{
		if (direction == AirlockDirection.InsideToOutside)
			isSuitRemoved = false;
	}

	internal void ReconcileSuitStateFromCurrentLocation()
	{
		isSuitRemoved = CanRemoveSuitAtCurrentLocation();
	}

	internal void ForceSuitOn() => isSuitRemoved = false;

	private bool CanRemoveSuitAtCurrentLocation()
	{
		if (GameContext.HasInstance == false)
			return false;

		GridService gridService = GameContext.Instance.GridService;
		BuildingManager buildingManager = GameContext.Instance.BuildingMgr;
		GridCell cell = gridService?.GetCell(GridPosition);
		return cell != null &&
			cell.IsIndoor &&
			buildingManager != null &&
			buildingManager.TryGetBuilding(cell.BuildingId, out Building building) &&
			building != null &&
			building.IsSuitRemovalPolicyActive;
	}

	protected override void CaptureSubclassState(WorkerSaveData data)
	{
		data.Fatigue = fatigue;
		data.Experience = experience;
		data.HasHumanIncidentState = incidentStateInitialized;
		data.UnsafeExposure = unsafeExposure;
		data.IncidentRandomState = incidentRandomState;
		data.IncidentRoll = incidentRoll;
		data.HumanIncidentCount = incidentCount;
		data.HasPendingHumanIncident = pendingIncident != null;
		if (pendingIncident != null)
		{
			data.PendingHumanIncidentType = pendingIncident.Type;
			data.PendingHumanIncidentResponse = pendingIncident.ResponseType;
			data.PendingHumanIncidentCause = pendingIncident.Cause;
			data.PendingHumanIncidentRiskScore = pendingIncident.RiskScore;
			data.PendingHumanIncidentChance = pendingIncident.Chance;
			data.PendingHumanIncidentExposureGain = pendingIncident.ExposureGain;
			data.PendingHumanIncidentHealthDamage = pendingIncident.HealthDamage;
		}
		data.IsSuitRemoved = isSuitRemoved;
	}

	protected override void RestoreSubclassState(WorkerSaveData data)
	{
		fatigue = Mathf.Clamp(data.Fatigue, 0.0f, 100.0f);
		experience = data.Experience;
		incidentStateInitialized = data.HasHumanIncidentState && data.IncidentRandomState != 0;
		float maximumExposure = GameContext.HasInstance && GameContext.Instance.HumanIncident != null
			? GameContext.Instance.HumanIncident.GetMaximumUnsafeExposure()
			: 40.0f;
		unsafeExposure = Mathf.Clamp(data.UnsafeExposure, 0.0f, Mathf.Max(0.0f, maximumExposure));
		incidentRandomState = incidentStateInitialized ? data.IncidentRandomState : 0;
		incidentRoll = incidentStateInitialized ? Mathf.Clamp01(data.IncidentRoll) : 0.0f;
		incidentCount = Mathf.Max(0, data.HumanIncidentCount);
		pendingIncident = data.HasPendingHumanIncident &&
			data.PendingHumanIncidentResponse != HumanIncidentResponseType.None
			? new HumanIncidentPayload(
				data.PendingHumanIncidentType,
				data.PendingHumanIncidentResponse,
				data.PendingHumanIncidentCause,
				Mathf.Max(0.0f, data.PendingHumanIncidentRiskScore),
				Mathf.Clamp01(data.PendingHumanIncidentChance),
				Mathf.Max(0.0f, data.PendingHumanIncidentExposureGain),
				Mathf.Max(0.0f, data.PendingHumanIncidentHealthDamage))
			: null;
		isSuitRemoved = data.IsSuitRemoved && CanRemoveSuitAtCurrentLocation();
	}
}
