using UnityEngine;

public abstract partial class AIWorker
{
	public void InitializeForSaveLoad(bool preserveWorkerId = false)
	{
		if (isRegistered)
			return;

		routeFinder = transform.GetComponent<FindRoute>();

		if (routeFinder == null)
		{
			Debug.Log($"FindRoute가 null이다 해당 객체가 프리뷰가 아니라면 큰일이다, 이름: {this.name}");
			return;
		}

		WorkerMgr.RegisterWorker(this, preserveWorkerId);
		isRegistered = true;

		routeFinder.SetAIMaster(this);
		routeFinder.RestoreTravelledCells(restoredCarriedMovementCells);
		restoredCarriedMovementCells = 0;
		BuildBehaviorTree();
	}

	public WorkerSaveData CaptureState()
	{
		WorkerSaveData data = new()
		{
			WorkerId = workerID,
			PrimaryBuildingId = primaryBuildingId,
			FirstName = workerFirstName,
			LastName = workerLastName,
			WorkerKind = WorkerKind,
			HumanType = HumanType,
			RobotType = RobotType,
			Abilities = abilities,
			MonthlyCost = monthlyCost,
			HiredAtElapsedWeek = HiredAtElapsedWeek,
			ItemDamageIncidentCount = itemDamageIncidentCount,
			VisualId = currentVisualDefinition != null ? currentVisualDefinition.VisualId : string.Empty,
			BaseMoveSpeedMultiplier = baseMoveSpeedMultiplier,
			MinimumMoveSpeedMultiplier = minimumMoveSpeedMultiplier,
			BaseWorkSpeedMultiplier = baseWorkSpeedMultiplier,
			MinimumWorkSpeedMultiplier = minimumWorkSpeedMultiplier,
			SafeHandlingWeightKg = SafeHandlingWeightKg,
			MainTaskType = workerMainTaskType,
			AssignedTaskTypes = new System.Collections.Generic.List<WorkerTask.TaskType>(workerAssignedTaskTypes),
			HasPendingAssignment = hasPendingAssignment,
			PendingPrimaryBuildingId = pendingPrimaryBuildingId,
			PendingAssignedTaskTypes = new System.Collections.Generic.List<WorkerTask.TaskType>(pendingAssignedTaskTypes),
			StatusAction = workerState.Action,
			StatusTarget = workerState.Target,
			OperationalState = operationalState,
			ControlMode = ControlMode,
			ReturningPlayerContainer = IsReturningPlayerContainer,
			NavigationRescueOverride = navigationRescueOverride,
			HasNavigationRescueGoal = hasNavigationRescueGoal,
			NavigationRescueGoal = new Int3SaveData(navigationRescueGoal.x, navigationRescueGoal.y, navigationRescueGoal.z),
			CarriedMovementCells = routeFinder != null ? routeFinder.TravelledCellsSinceLastConsume : 0,
			CarryingBox = null,
		};

		var carryBoxAbility = CarryingAbility;
		if (carryBoxAbility != null && carryBoxAbility.CarryingBox != null)
		{
			data.CarryingBox = new BoxReferenceSaveData
			{
				BoxType = carryBoxAbility.CarryingBox.Type,
				BoxId = carryBoxAbility.CarryingBox.BoxId,
			};
		}

		CaptureSubclassState(data);
		return data;
	}

	public void RestoreState(WorkerSaveData data)
	{
		if (data == null)
			return;

		workerFirstName = data.FirstName;
		workerLastName = data.LastName;
		workerID = data.WorkerId;
		primaryBuildingId = data.PrimaryBuildingId;
		if (data.WorkerKind == WorkerKind.Robot)
			SetRobotIdentity(data.RobotType);
		else
			SetHumanIdentity(data.HumanType);
		abilities = data.WorkerKind != WorkerKind.Robot && (int)data.Abilities == -1
			? WorkerAbility.CarryBox |
				WorkerAbility.PickingStoring |
				WorkerAbility.Packing |
				WorkerAbility.Labeling |
				WorkerAbility.CargoHandling
			: data.Abilities;
		monthlyCost = data.MonthlyCost;
		hiredAtElapsedWeek = Mathf.Max(0, data.HiredAtElapsedWeek);
		itemDamageIncidentCount = Mathf.Max(0, data.ItemDamageIncidentCount);
		baseMoveSpeedMultiplier = data.BaseMoveSpeedMultiplier;
		minimumMoveSpeedMultiplier = data.MinimumMoveSpeedMultiplier;
		baseWorkSpeedMultiplier = data.BaseWorkSpeedMultiplier;
		minimumWorkSpeedMultiplier = data.MinimumWorkSpeedMultiplier;
		safeHandlingWeightKg = data.SafeHandlingWeightKg > 0.0f ? data.SafeHandlingWeightKg : 20.0f;
		workerMainTaskType = data.MainTaskType;
		workerAssignedTaskTypes.Clear();
		if (data.AssignedTaskTypes != null && data.AssignedTaskTypes.Count > 0)
		{
			for (int i = 0; i < data.AssignedTaskTypes.Count; ++i)
			{
				WorkerTask.TaskType taskType = data.AssignedTaskTypes[i];
				AddRestoredAssignedTaskType(taskType);
			}
		}
		else if (workerMainTaskType != WorkerTask.TaskType.Undefined)
		{
			AddRestoredAssignedTaskType(workerMainTaskType);
		}

		workerMainTaskType = workerAssignedTaskTypes.Count > 0 ? workerAssignedTaskTypes[0] : WorkerTask.TaskType.Undefined;
		hasPendingAssignment = data.HasPendingAssignment;
		pendingPrimaryBuildingId = data.PendingPrimaryBuildingId;
		pendingAssignedTaskTypes.Clear();
		if (hasPendingAssignment && data.PendingAssignedTaskTypes != null)
		{
			for (int i = 0; i < data.PendingAssignedTaskTypes.Count; ++i)
			{
				WorkerTask.TaskType taskType = data.PendingAssignedTaskTypes[i];
				if (taskType != WorkerTask.TaskType.Undefined &&
					pendingAssignedTaskTypes.Contains(taskType) == false)
				{
					pendingAssignedTaskTypes.Add(taskType);
				}
			}
		}
		if (hasPendingAssignment == false)
			pendingPrimaryBuildingId = 0;
		workerState = new WorkerStatusInfo(data.StatusAction, data.StatusTarget);
		if (workerState.Action == WorkerStatusAction.Resting ||
			workerState.Action == WorkerStatusAction.Charging)
		{
			workerState = new WorkerStatusInfo(WorkerStatusAction.Idle, WorkerStatusTarget.None);
		}
		operationalState = data.OperationalState;
		RestorePlayerOverrideState(data.ControlMode, data.NavigationRescueOverride, data.HasNavigationRescueGoal,
			new Unity.Mathematics.int3(data.NavigationRescueGoal.X, data.NavigationRescueGoal.Y, data.NavigationRescueGoal.Z));
		returningPlayerContainer = data.ReturningPlayerContainer && data.ControlMode == WorkerControlMode.Automatic;
		restoredCarriedMovementCells = Mathf.Max(0, data.CarriedMovementCells);
		preTrafficAction = workerState.Action;
		isTrafficBlocked = false;
		tick = 0;

		if (string.IsNullOrWhiteSpace(data.VisualId) == false)
			ApplyVisual(GameContext.Instance.WorkerVisualCatalog?.FindById(data.VisualId));
		ApplyOperationalVisualState();

		EnsureAbilitiesConfigured();
		RestoreSubclassState(data);

		if (data.CarryingBox != null && GameContext.Instance.BoxMgr.TryGetBox(data.CarryingBox.BoxType, data.CarryingBox.BoxId, out var box))
			TryAttachBox(box);
	}

	private void AddRestoredAssignedTaskType(WorkerTask.TaskType taskType)
	{
		if (taskType == WorkerTask.TaskType.Undefined)
			return;

		if (workerAssignedTaskTypes.Contains(taskType) == false)
			workerAssignedTaskTypes.Add(taskType);
	}

	protected virtual void CaptureSubclassState(WorkerSaveData data) { }
	protected virtual void RestoreSubclassState(WorkerSaveData data) { }

	internal bool RestoreTaskFromSave(WorkerTask task)
	{
		if (task == null || IsOperational == false || IsRecovering)
			return false;
		if (IsPlayerOverride && navigationRescueOverride == false)
			return false;
		if (task.SetAIWorker(this) == false)
			return false;

		currentTask = task;
		if (navigationRescueOverride)
		{
			navigationRescueTask = task;
			navigationRescueBox = CarryingAbility?.CarryingBox;
		}
		else if (CanUseAutomaticNavigation(out RobotNavigationWaitReason reason) == false)
		{
			BeginNavigationWait(reason);
		}

		if (GameContext.HasInstance)
			WorkerMgr.RemoveIdleWorker(this);
		BuildBehaviorTree();
		enabled = true;
		return true;
	}

	internal void FinalizeNavigationRestoreFromSave()
	{
		if (navigationRescueOverride && navigationRescueTask == null)
		{
			navigationRescueOverride = false;
			navigationRescueBox = null;
			hasNavigationRescueGoal = false;
		}
	}
}
