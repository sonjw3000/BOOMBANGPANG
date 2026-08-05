using System;
using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public interface IWorkerUIProvider
{
	AIWorker Target { get; }
	string Name { get; }
	string Subtitle { get; }
	string WorkerTypeLabel { get; }
	string ResourceDisplay { get; }
	string MoveSpeedDisplay { get; }
	string WorkSpeedDisplay { get; }
	string MainTaskTypeDisplay { get; }
	string AbilityDisplay { get; }
	string MonthlyCostDisplay { get; }
	string PositionDisplay { get; }
	string DestinationDisplay { get; }
	string ActionDisplay { get; }
	string TargetDisplay { get; }
	string CurrentTaskButtonLabel { get; }
	string CurrentTaskSummary { get; }
	bool HasAssignedTask { get; }
	bool HasCarriedBox { get; }
	string CarriedBoxFillDisplay { get; }
	ItemContainerDisplayInfo GetCarriedBoxDisplay();
}

public abstract class WorkerUIProviderBase<TWorker> : UIProvider<TWorker>, IWorkerUIProvider, ISelectionInspectorProvider
	where TWorker : AIWorker
{
	protected abstract string ResourceLabel { get; }
	protected abstract float ResourceValue { get; }
	protected virtual IWearable Wearable => null;
	protected virtual string ExtraProfileLabel => null;
	protected virtual string ExtraProfileDisplay => null;
	protected virtual string DebugProfileLabel => null;
	protected virtual string DebugProfileDisplay => null;
	private bool ShowDebugProfile =>
		GameContext.HasInstance &&
		(GameContext.Instance.GameCheat || Debug.isDebugBuild) &&
		string.IsNullOrWhiteSpace(DebugProfileLabel) == false;

	public override string Name => currentTarget != null ? currentTarget.Name : "Unknown Worker";
	public override string Subtitle => currentTarget != null ? GetWorkerTypeLabel(currentTarget) : "Unknown Worker";
	public override Sprite Icon => null;
	AIWorker IWorkerUIProvider.Target => currentTarget;

	public string WorkerTypeLabel => currentTarget != null ? GetWorkerTypeLabel(currentTarget) : "Unknown";
	public string ResourceDisplay => $"{ResourceValue:0.0}%";
	public string WearDisplay => Wearable != null ? $"{Wearable.Wear * 100.0f:0.0}%" : "0.0%";
	public string WearEfficiencyDisplay => Wearable != null ? $"{Wearable.WearEfficiency * 100.0f:0.0}%" : "100.0%";
	public string MoveSpeedDisplay => currentTarget != null ? $"x{currentTarget.GetMoveSpeedMultiplier():0.00}" : "x0.00";
	public string WorkSpeedDisplay => currentTarget != null ? $"x{currentTarget.GetWorkSpeedMultiplier():0.00}" : "x0.00";
	public string MainTaskTypeDisplay => currentTarget != null ? BuildTaskTypeDisplay(currentTarget) : "None";
	public string AbilityDisplay => currentTarget != null ? BuildAbilityDisplay(currentTarget.Ability) : "None";
	public string MonthlyCostDisplay => currentTarget != null ? currentTarget.MonthlyCost.ToString() : "0";
	public string PositionDisplay => currentTarget != null ? currentTarget.GridPosition.ToString() : "(0,0,0)";
	public string ActionDisplay => currentTarget != null ? currentTarget.WorkerState.Action.ToString() : "None";
	public string TargetDisplay => currentTarget != null ? currentTarget.WorkerState.Target.ToString() : "None";
	public string CurrentTaskButtonLabel => currentTarget?.CurrentTask != null ? currentTarget.CurrentTask.GetType().Name : "None";
	public string CurrentTaskSummary => currentTarget?.CurrentTask != null ? currentTarget.CurrentTask.GetStatusSummary() : "No assigned task.";
	public bool HasAssignedTask => currentTarget?.CurrentTask != null;
	public bool HasCarriedBox => currentTarget?.CarryingAbility?.CarryingBox != null;
	public string ControlDisplay => currentTarget == null
		? "Unavailable"
		: currentTarget.IsPlayerOverride
			? currentTarget.IsManualNavigation
				? $"Player Override · Manual Navigation · {currentTarget.PlayerOverridePhase}"
				: $"Player Override · {currentTarget.PlayerOverridePhase}"
			: "Automatic";
	public float CarriedBoxFillPercent
	{
		get
		{
			BoxBase box = currentTarget?.CarryingAbility?.CarryingBox;
			if (box == null || box.MaxSize <= 0.0f)
				return 0.0f;

			return (box.TotalSize / box.MaxSize) * 100.0f;
		}
	}
	public string CarriedBoxFillDisplay => $"{CarriedBoxFillPercent:0.0}%";

	public string DestinationDisplay
	{
		get
		{
			if (currentTarget == null || currentTarget.TryGetCurrentDestination(out var name, out var position) == false)
				return "None";

			return $"{name} ({position.x}, {position.y}, {position.z})";
		}
	}

	public ItemContainerDisplayInfo GetCarriedBoxDisplay() => new()
	{
		ContainerName = currentTarget?.CarryingAbility?.CarryingBox != null
			? $"{currentTarget.CarryingAbility.CarryingBox.Type} Box #{currentTarget.CarryingAbility.CarryingBox.BoxId}"
			: "None",
		HasContainer = currentTarget?.CarryingAbility?.CarryingBox != null,
		Container = currentTarget?.CarryingAbility?.CarryingBox,
		Items = ItemContainerDisplayUtility.BuildItemRows(currentTarget?.CarryingAbility?.CarryingBox),
		ManifestItems = ItemContainerDisplayUtility.BuildManifestRows(currentTarget?.CarryingAbility?.CarryingBox),
	};

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock(ResourceLabel, ResourceDisplay));
		if (Wearable != null)
			infoBlocks.Add(new KeyValueBlock("Wear", WearDisplay));
		if (string.IsNullOrWhiteSpace(ExtraProfileLabel) == false)
			infoBlocks.Add(new KeyValueBlock(ExtraProfileLabel, ExtraProfileDisplay));
		if (ShowDebugProfile)
			infoBlocks.Add(new KeyValueBlock(DebugProfileLabel, DebugProfileDisplay));
		infoBlocks.Add(new KeyValueBlock("MoveSpeed", MoveSpeedDisplay));
		infoBlocks.Add(new KeyValueBlock("Position", PositionDisplay));
		infoBlocks.Add(new KeyValueBlock("MainTaskType", MainTaskTypeDisplay));
		infoBlocks.Add(new KeyValueBlock("Action", ActionDisplay));
		infoBlocks.Add(new KeyValueBlock("Target", TargetDisplay));
	}

	public override void DeleteObject()
	{
		if (currentTarget == null || GameContext.HasInstance == false)
			return;

		if (GameContext.Instance.WorkerMgr.TryRemoveWorker(currentTarget) == false)
			Debug.LogWarning($"Worker {currentTarget.Name} cannot be removed in state {currentTarget.OperationalState}.");
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 6)
			return;

		int index = 0;
		(infoBlocks[index++] as KeyValueBlock)?.UpdateValue(ResourceDisplay);
		if (Wearable != null)
			(infoBlocks[index++] as KeyValueBlock)?.UpdateValue(WearDisplay);
		if (string.IsNullOrWhiteSpace(ExtraProfileLabel) == false)
			(infoBlocks[index++] as KeyValueBlock)?.UpdateValue(ExtraProfileDisplay);
		if (ShowDebugProfile)
			(infoBlocks[index++] as KeyValueBlock)?.UpdateValue(DebugProfileDisplay);
		(infoBlocks[index++] as KeyValueBlock)?.UpdateValue(MoveSpeedDisplay);
		(infoBlocks[index++] as KeyValueBlock)?.UpdateValue(PositionDisplay);
		(infoBlocks[index++] as KeyValueBlock)?.UpdateValue(MainTaskTypeDisplay);
		(infoBlocks[index++] as KeyValueBlock)?.UpdateValue(ActionDisplay);
		(infoBlocks[index] as KeyValueBlock)?.UpdateValue(TargetDisplay);
	}

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddTab("Profile", GetProfileVersion, BuildProfilePanel);
		model.AddTab("Activity", GetActivityVersion, BuildActivityPanel);
		model.AddTab("Assignment", GetAssignmentVersion, BuildAssignmentPanel);
		model.AddTab("Carry", GetCarryVersion, BuildCarryPanel);
		model.AddOverview(ResourceLabel, () => ResourceDisplay);
		if (Wearable != null)
			model.AddOverview("Wear", () => WearDisplay);
		if (string.IsNullOrWhiteSpace(ExtraProfileLabel) == false)
			model.AddOverview(ExtraProfileLabel, () => ExtraProfileDisplay);
		if (ShowDebugProfile)
			model.AddOverview(DebugProfileLabel, () => DebugProfileDisplay);
		model.AddOverview("Main Task", () => MainTaskTypeDisplay);
		model.AddOverview("Action", () => ActionDisplay);
		model.AddOverview("Control", () => ControlDisplay);
		model.AddAction("Take Control", TakePlayerControl, CanTakePlayerControl);
		model.AddAction("Interact", RequestInteractionWindow, CanRequestInteractionWindow);
		model.AddAction("Release Control", ReleasePlayerControl, CanReleasePlayerControl);
		model.AddAction("Remove", DeleteObject, isDangerous: true);
	}

	private bool CanTakePlayerControl()
	{
		return currentTarget != null &&
			currentTarget.IsPlayerOverride == false &&
			GameContext.HasInstance &&
			GameContext.Instance.PlayerOverrideSvc != null;
	}

	private bool CanRequestInteractionWindow()
	{
		return currentTarget != null &&
			currentTarget.IsPlayerOverride &&
			currentTarget.IsNavigationRescueOverride == false &&
			currentTarget.PlayerOverridePhase == PlayerOverridePhase.AwaitingCommand &&
			GameContext.HasInstance &&
			GameContext.Instance.PlayerOverrideSvc != null;
	}

	private bool CanReleasePlayerControl()
	{
		return currentTarget != null &&
			currentTarget.IsPlayerOverride &&
			GameContext.HasInstance &&
			GameContext.Instance.PlayerOverrideSvc != null;
	}

	private void TakePlayerControl()
	{
		if (CanTakePlayerControl() == false)
			return;

		if (GameContext.Instance.PlayerOverrideSvc.TryTakeControl(currentTarget, out string reason) == false &&
			string.IsNullOrWhiteSpace(reason) == false)
		{
			Debug.LogWarning($"[WorkerUIProvider] Unable to take control of {currentTarget.Name}: {reason}");
			GameContext.Instance.HudEventManager?.Publish(HudEventType.Warning, reason, currentTarget);
		}
	}

	private void RequestInteractionWindow()
	{
		if (CanRequestInteractionWindow())
			GameContext.Instance.PlayerOverrideSvc.RequestInteractionWindow(currentTarget);
	}

	private void ReleasePlayerControl()
	{
		if (CanReleasePlayerControl() == false)
			return;

		if (GameContext.Instance.PlayerOverrideSvc.TryReleaseControl(currentTarget, out string reason) == false &&
			string.IsNullOrWhiteSpace(reason) == false)
		{
			Debug.LogWarning($"[WorkerUIProvider] Unable to release control of {currentTarget.Name}: {reason}");
			GameContext.Instance.HudEventManager?.Publish(HudEventType.Warning, reason, currentTarget);
		}
	}

	private int GetProfileVersion()
	{
		int baseVersion = HashCode.Combine(
			ResourceDisplay,
			WearDisplay,
			WearEfficiencyDisplay,
			MoveSpeedDisplay,
			WorkSpeedDisplay,
			MainTaskTypeDisplay,
			AbilityDisplay,
			MonthlyCostDisplay);
		return HashCode.Combine(
			baseVersion,
			ExtraProfileDisplay,
			ShowDebugProfile ? DebugProfileDisplay : string.Empty);
	}

	private int GetActivityVersion()
	{
		return HashCode.Combine(PositionDisplay, DestinationDisplay, ActionDisplay, TargetDisplay, CurrentTaskButtonLabel, CurrentTaskSummary);
	}

	private int GetCarryVersion()
	{
		BoxBase box = currentTarget?.CarryingAbility?.CarryingBox;
		unchecked
		{
			return SelectionDetailContentUtility.GetItemContainerVersion(box) * 31 + (box != null ? (int)box.BoxId : 0);
		}
	}

	private int GetAssignmentVersion()
	{
		AIWorker worker = currentTarget;
		if (worker == null)
			return 0;

		unchecked
		{
			int version = worker.CurrentTask != null ? 1 : 0;
			version = version * 31 + (int)worker.PrimaryBuildingId;
			for (int i = 0; i < worker.AssignedTaskTypes.Count; ++i)
				version = version * 31 + (int)worker.AssignedTaskTypes[i];

			version = version * 31 + (worker.HasPendingAssignment ? 1 : 0);
			version = version * 31 + (int)worker.PendingPrimaryBuildingId;
			for (int i = 0; i < worker.PendingAssignedTaskTypes.Count; ++i)
				version = version * 31 + (int)worker.PendingAssignedTaskTypes[i];

			BuildingManager buildingManager = GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
			version = version * 31 + (buildingManager?.RegisteredBuildings.Count ?? 0);
			return version;
		}
	}

	private SelectionDetailPanelModel BuildProfilePanel()
	{
		SelectionDetailPanelModel panel = new() { Title = "PROFILE", Summary = WorkerTypeLabel };
		AddDetailValue(panel, ResourceLabel, ResourceDisplay);
		if (Wearable != null)
		{
			AddDetailValue(panel, "Wear", WearDisplay);
			AddDetailValue(panel, "Wear Efficiency", WearEfficiencyDisplay);
		}
		if (string.IsNullOrWhiteSpace(ExtraProfileLabel) == false)
			AddDetailValue(panel, ExtraProfileLabel, ExtraProfileDisplay);
		if (ShowDebugProfile)
			AddDetailValue(panel, DebugProfileLabel, DebugProfileDisplay);
		AddDetailValue(panel, "Move Speed", MoveSpeedDisplay);
		AddDetailValue(panel, "Work Speed", WorkSpeedDisplay);
		AddDetailValue(panel, "Main Task", MainTaskTypeDisplay);
		AddDetailValue(panel, "Abilities", AbilityDisplay.Replace('\n', ',').Replace(",", ", "));
		AddDetailValue(panel, "Monthly Cost", MonthlyCostDisplay);
		return panel;
	}

	private SelectionDetailPanelModel BuildActivityPanel()
	{
		SelectionDetailPanelModel panel = new() { Title = "ACTIVITY", Summary = CurrentTaskButtonLabel };
		AddDetailValue(panel, "Task", CurrentTaskSummary);
		AddDetailValue(panel, "Action", ActionDisplay);
		AddDetailValue(panel, "Target", TargetDisplay);
		AddDetailValue(panel, "Destination", DestinationDisplay);
		AddDetailValue(panel, "Position", PositionDisplay);
		return panel;
	}

	private SelectionDetailPanelModel BuildCarryPanel()
	{
		BoxBase box = currentTarget?.CarryingAbility?.CarryingBox;
		return SelectionDetailContentUtility.BuildItemContainerPanel(
			"CARRY",
			box != null ? $"{GetCarriedBoxDisplay().ContainerName} · Filled {CarriedBoxFillDisplay}" : "Not carrying anything.",
			GetCarriedBoxDisplay());
	}

	private SelectionDetailPanelModel BuildAssignmentPanel()
	{
		AIWorker worker = currentTarget;
		if (worker == null)
			return new SelectionDetailPanelModel { Title = "ASSIGNMENT", Summary = "Worker unavailable." };

		bool working = worker.CurrentTask != null;
		bool editingPending = worker.HasPendingAssignment;
		uint editingBuildingId = editingPending ? worker.PendingPrimaryBuildingId : worker.PrimaryBuildingId;
		IReadOnlyList<WorkerTask.TaskType> editingTaskTypes =
			editingPending ? worker.PendingAssignedTaskTypes : worker.AssignedTaskTypes;

		List<uint> buildingIds = new();
		List<string> buildingChoices = new();
		int buildingIndex = BuildBuildingChoices(editingBuildingId, buildingIds, buildingChoices, out BuildingType? buildingType, out bool buildingValid);

		SelectionDetailPanelModel panel = new()
		{
			Title = "ASSIGNMENT",
			Summary = editingPending
				? "Scheduled assignment"
				: working ? "Current assignment · locked while working" : "Current assignment",
		};

		if (editingPending)
		{
			AddDetailValue(panel, "Current Workplace", GetBuildingDisplayName(worker.PrimaryBuildingId));
			AddDetailValue(panel, "Current Tasks", BuildTaskTypeDisplay(worker.AssignedTaskTypes));
		}

		SelectionDetailEditorModel editor = new()
		{
			Message = GetAssignmentMessage(working, editingPending, buildingValid),
			DropdownLabel = editingPending ? "Scheduled Workplace" : "Workplace",
			DropdownChoices = buildingChoices,
			DropdownIndex = buildingIndex,
			DropdownEnabled = working == false || editingPending,
			ToggleLabel = editingPending ? "Scheduled Tasks" : "Assigned Tasks",
		};
		panel.Editor = editor;

		if (working && editingPending == false)
		{
			editor.PrimaryActionLabel = "Schedule Change";
			editor.PrimaryAction = () =>
			{
				if (GameContext.HasInstance)
					GameContext.Instance.WorkerMgr.TryScheduleWorkerAssignment(
						worker,
						worker.PrimaryBuildingId,
						worker.AssignedTaskTypes);
			};
		}
		else if (editingPending)
		{
			editor.SecondaryActionLabel = "Cancel Scheduled Change";
			editor.SecondaryAction = () =>
			{
				if (GameContext.HasInstance)
					GameContext.Instance.WorkerMgr.CancelPendingWorkerAssignment(worker);
			};
		}

		editor.DropdownChanged = index =>
		{
			if (index < 0 || index >= buildingIds.Count || GameContext.HasInstance == false)
				return;

			uint buildingId = buildingIds[index];
			if (editingPending)
				GameContext.Instance.WorkerMgr.TryScheduleWorkerAssignment(worker, buildingId, Array.Empty<WorkerTask.TaskType>());
			else
				GameContext.Instance.WorkerMgr.TrySetWorkerAssignment(worker, buildingId, Array.Empty<WorkerTask.TaskType>());
		};

		List<WorkerTask.TaskType> assignableTaskTypes = new();
		if (buildingValid)
			WorkerTaskAssignmentPolicy.GetAssignableTaskTypes(worker, buildingType, assignableTaskTypes);
		for (int i = 0; i < assignableTaskTypes.Count; ++i)
		{
			WorkerTask.TaskType taskType = assignableTaskTypes[i];
			if (taskType == WorkerTask.TaskType.Undefined)
				continue;

			SelectionDetailToggleModel toggle = new()
			{
				Label = GetTaskTypeDisplayName(taskType),
				Value = ContainsTaskType(editingTaskTypes, taskType),
				Enabled = working == false || editingPending,
			};
			toggle.Changed = assigned =>
			{
				if (GameContext.HasInstance == false)
					return;

				IReadOnlyList<WorkerTask.TaskType> currentTaskTypes =
					editingPending ? worker.PendingAssignedTaskTypes : worker.AssignedTaskTypes;
				uint currentBuildingId =
					editingPending ? worker.PendingPrimaryBuildingId : worker.PrimaryBuildingId;
				List<WorkerTask.TaskType> nextTaskTypes = new(currentTaskTypes);
				if (assigned && nextTaskTypes.Contains(taskType) == false)
					nextTaskTypes.Add(taskType);
				else if (assigned == false)
					nextTaskTypes.Remove(taskType);

				if (editingPending)
					GameContext.Instance.WorkerMgr.TryScheduleWorkerAssignment(worker, currentBuildingId, nextTaskTypes);
				else
					GameContext.Instance.WorkerMgr.TrySetWorkerAssignment(worker, currentBuildingId, nextTaskTypes);
			};
			editor.Toggles.Add(toggle);
		}

		return panel;
	}

	private static string GetAssignmentMessage(bool working, bool editingPending, bool buildingValid)
	{
		if (buildingValid == false)
			return "Selected workplace no longer exists. Choose a valid workplace.";
		if (editingPending)
			return working
				? "Applies after the current task ends."
				: "Ready to apply when the assignment becomes valid.";
		if (working)
			return "Current assignment cannot be edited while working.";
		return "Changes apply immediately.";
	}

	private static int BuildBuildingChoices(
		uint selectedBuildingId,
		List<uint> buildingIds,
		List<string> choices,
		out BuildingType? selectedBuildingType,
		out bool selectedBuildingValid)
	{
		buildingIds.Add(0);
		choices.Add("None (Outdoor)");
		selectedBuildingType = null;
		selectedBuildingValid = selectedBuildingId == 0;
		int selectedIndex = 0;

		BuildingManager manager = GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
		if (manager != null)
		{
			foreach (Building building in manager.RegisteredBuildings)
			{
				if (building == null)
					continue;

				buildingIds.Add(building.RuntimeBuildingId);
				choices.Add($"{building.DisplayName} · {building.Type}");
				if (building.RuntimeBuildingId == selectedBuildingId)
				{
					selectedIndex = buildingIds.Count - 1;
					selectedBuildingType = building.Type;
					selectedBuildingValid = true;
				}
			}
		}

		if (selectedBuildingId != 0 && selectedBuildingValid == false)
		{
			buildingIds.Add(selectedBuildingId);
			choices.Add($"Missing Building #{selectedBuildingId}");
			selectedIndex = buildingIds.Count - 1;
		}

		return selectedIndex;
	}

	private static string GetBuildingDisplayName(uint buildingId)
	{
		if (buildingId == 0)
			return "None (Outdoor)";

		return GameContext.HasInstance &&
			GameContext.Instance.BuildingMgr != null &&
			GameContext.Instance.BuildingMgr.TryGetBuilding(buildingId, out Building building) &&
			building != null
				? $"{building.DisplayName} · {building.Type}"
				: $"Missing Building #{buildingId}";
	}

	private static bool ContainsTaskType(IReadOnlyList<WorkerTask.TaskType> taskTypes, WorkerTask.TaskType taskType)
	{
		if (taskTypes == null)
			return false;

		for (int i = 0; i < taskTypes.Count; ++i)
		{
			if (taskTypes[i] == taskType)
				return true;
		}
		return false;
	}

	private static void AddDetailValue(SelectionDetailPanelModel panel, string label, string value)
	{
		panel.Rows.Add(new SelectionDetailRow { Primary = label, Secondary = value });
	}

	public static string GetWorkerTypeLabel(AIWorker worker)
	{
		if (worker == null)
			return "Unknown";

		if (worker.WorkerKind == WorkerKind.Robot)
		{
			return worker.RobotType switch
			{
				RobotType.Transfer => "Robot / Transfer",
				_ => $"Robot / {worker.RobotType}",
			};
		}

		return worker.HumanType switch
		{
			HumanType.FullTime => "Human / FullTime",
			HumanType.PartTime => "Human / PartTime",
			HumanType.Illegal => "Human / Illegal",
			_ => $"Human / {worker.HumanType}",
		};
	}

	private static string BuildAbilityDisplay(WorkerAbility ability)
	{
		if (ability == WorkerAbility.None)
			return "None";

		StringBuilder builder = new();
		foreach (WorkerAbility flag in Enum.GetValues(typeof(WorkerAbility)))
		{
			if (flag == WorkerAbility.None || ability.HasFlag(flag) == false)
				continue;

			if (builder.Length > 0)
				builder.Append('\n');

			builder.Append(flag);
		}

		return builder.Length > 0 ? builder.ToString() : "None";
	}

	private static string BuildTaskTypeDisplay(AIWorker worker)
	{
		return worker == null ? "Undefined" : BuildTaskTypeDisplay(worker.AssignedTaskTypes);
	}

	private static string BuildTaskTypeDisplay(IReadOnlyList<WorkerTask.TaskType> taskTypes)
	{
		if (taskTypes == null || taskTypes.Count == 0)
			return "Undefined";

		if (taskTypes.Count == 1)
			return GetTaskTypeDisplayName(taskTypes[0]);

		StringBuilder builder = new();
		for (int i = 0; i < taskTypes.Count; ++i)
		{
			if (i > 0)
				builder.Append(", ");

			builder.Append(GetTaskTypeDisplayName(taskTypes[i]));
		}

		return builder.ToString();
	}

	private static string GetTaskTypeDisplayName(WorkerTask.TaskType taskType)
	{
		return taskType switch
		{
			WorkerTask.TaskType.IB => "CapsuleRelocation (Inbound)",
			WorkerTask.TaskType.CapsuleClear => "CapsuleRelocation (Clear)",
			WorkerTask.TaskType.CapsuleSupply => "CapsuleRelocation (Supply)",
			WorkerTask.TaskType.OB => "CapsuleRelocation (Outbound)",
			WorkerTask.TaskType.LaunchSort => "Launch Sort",
			WorkerTask.TaskType.WasteCollection => "Waste Collection",
			_ => taskType.ToString(),
		};
	}
}
