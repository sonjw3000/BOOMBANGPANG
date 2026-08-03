using UnityEngine;

public class HumanWorker : AIWorker
{
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

	public float Fatigue => fatigue;
	public bool IsSuitRemoved => isSuitRemoved;

	protected override IBaseNode BuildWorkerBaseNode()
	{
		SelectorNode root = new();
		root.Add(BuildHumanIncidentNode());
		root.Add(BuildRecoveryNode(WorkerStatusTarget.RestFacility, InteractionKind.Rest));
		return root;
	}

	public override float GetWorkSpeedMultiplier()
		=> Mathf.Lerp(BaseWorkSpeedMultiplier, MinimumWorkSpeedMultiplier, fatigue / 100.0f);
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
