using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.UI
{
	public enum WorkerTaskHandleGroup
	{
		Undefined,
		CargoHandle,
		ItemHandle,
	}

	public class WorkerItemView : MonoBehaviour
	{
		[SerializeField] private Image thumbnail;
		[SerializeField] private TMP_Text nameText;
		[SerializeField] private TMP_Text taskText;
		[SerializeField] private TMP_Text statusText;
		[SerializeField] private TMP_Text fatigueText;

		// Right side toggle/tab placeholder
		[SerializeField] private GameObject managementTab;
		[SerializeField] private TMPro.TMP_Dropdown handleGroupDropdown;
		[SerializeField] private TMPro.TMP_Dropdown typeDropdown;
		[SerializeField] private TMPro.TMP_Dropdown primaryBuildingDropdown;

		private AIWorker currentWorker;
		private WorkerTaskHandleGroup selectedHandleGroup = WorkerTaskHandleGroup.Undefined;
		private readonly System.Collections.Generic.List<WorkerTaskHandleGroup> validHandleGroups = new();
		private readonly System.Collections.Generic.List<WorkerTask.TaskType> validTypes = new();
		private readonly System.Collections.Generic.List<uint> validBuildingIds = new();

		private void Awake()
		{
			if (handleGroupDropdown != null)
				handleGroupDropdown.onValueChanged.AddListener(OnHandleGroupDropdownValueChanged);

			if (typeDropdown != null)
				typeDropdown.onValueChanged.AddListener(OnTaskDropdownValueChanged);

			if (primaryBuildingDropdown != null)
				primaryBuildingDropdown.onValueChanged.AddListener(OnPrimaryBuildingDropdownValueChanged);
		}

		public void Setup(AIWorker worker)
		{
			if (worker == null)
				return;

			bool workerChanged = currentWorker != worker;
			currentWorker = worker;
			if (workerChanged)
				selectedHandleGroup = GetHandleGroup(worker.TaskType);

			nameText.text = $"{worker.Name} (ID: {worker.WorkerID})";
			taskText.text = $"Task: {worker.TaskType}";

			var state = worker.WorkerState;
			statusText.text = $"{state.Action} {state.Target}";

			if (worker is HumanWorker human)
			{
				fatigueText.text = $"Fatigue: {human.Fatigue:F1}%";
				fatigueText.color = GetFatigueColor(human.Fatigue);
			}
			else if (worker is RobotWorker robot)
			{
				fatigueText.text = $"Battery: {robot.BatteryLevel:F1}%";
				fatigueText.color = GetBatteryColor(robot.BatteryLevel);
			}
			else
			{
				fatigueText.text = "";
			}

			EnsureManagementLayout();
			ConfigureHandleGroupDropdown(worker);
			ConfigureTaskDropdown(worker);
			ConfigurePrimaryBuildingDropdown(worker);
		}

		private void ConfigureHandleGroupDropdown(AIWorker worker)
		{
			if (handleGroupDropdown == null)
				return;

			handleGroupDropdown.onValueChanged.RemoveListener(OnHandleGroupDropdownValueChanged);
			handleGroupDropdown.ClearOptions();
			validHandleGroups.Clear();

			WorkerTaskHandleGroup currentGroup = selectedHandleGroup;
			int selectedIndex = 0;
			var options = new System.Collections.Generic.List<string>();

			AddHandleGroupOption(WorkerTaskHandleGroup.Undefined, "Undefined", currentGroup, options, ref selectedIndex);
			AddHandleGroupOption(WorkerTaskHandleGroup.CargoHandle, "Cargo Handle", currentGroup, options, ref selectedIndex);
			AddHandleGroupOption(WorkerTaskHandleGroup.ItemHandle, "Item Handle", currentGroup, options, ref selectedIndex);

			handleGroupDropdown.interactable = true;
			handleGroupDropdown.AddOptions(options);
			handleGroupDropdown.value = selectedIndex;
			handleGroupDropdown.RefreshShownValue();
			handleGroupDropdown.onValueChanged.AddListener(OnHandleGroupDropdownValueChanged);
		}

		private void AddHandleGroupOption(
			WorkerTaskHandleGroup group,
			string label,
			WorkerTaskHandleGroup currentGroup,
			System.Collections.Generic.List<string> options,
			ref int selectedIndex)
		{
			if (group == currentGroup)
				selectedIndex = validHandleGroups.Count;

			validHandleGroups.Add(group);
			options.Add(label);
		}

		private void ConfigureTaskDropdown(AIWorker worker)
		{
			if (typeDropdown == null)
				return;

			typeDropdown.onValueChanged.RemoveListener(OnTaskDropdownValueChanged);
			typeDropdown.ClearOptions();
			validTypes.Clear();

			int selectedIndex = 0;
			var options = new System.Collections.Generic.List<string>();
			var assignableTypes = new System.Collections.Generic.List<WorkerTask.TaskType>();
			WorkerTaskAssignmentPolicy.GetAssignableTaskTypes(worker, assignableTypes);
			WorkerTaskHandleGroup selectedGroup = GetSelectedHandleGroup();
			bool currentTaskAvailable = WorkerManager.CanChangeType(worker, worker.TaskType);

			if (currentTaskAvailable == false || IsTaskTypeVisibleInHandleGroup(worker.TaskType, selectedGroup) == false)
			{
				validTypes.Add(worker.TaskType);
				options.Add($"{worker.TaskType} (Current Unavailable)");
			}

			for (int i = 0; i < assignableTypes.Count; ++i)
			{
				WorkerTask.TaskType type = assignableTypes[i];
				if (IsTaskTypeVisibleInHandleGroup(type, selectedGroup) == false)
					continue;

				if (validTypes.Contains(type))
					continue;

				if (type == worker.TaskType)
					selectedIndex = validTypes.Count;

				validTypes.Add(type);
				options.Add(type.ToString());
			}

			if (options.Count > 0)
			{
				typeDropdown.interactable = true;
				typeDropdown.AddOptions(options);
				typeDropdown.value = selectedIndex;
				typeDropdown.RefreshShownValue();
			}
			else
			{
				typeDropdown.AddOptions(new System.Collections.Generic.List<string> { "No Task Available" });
				typeDropdown.interactable = false;
				typeDropdown.RefreshShownValue();
			}

			typeDropdown.onValueChanged.AddListener(OnTaskDropdownValueChanged);
		}

		private void ConfigurePrimaryBuildingDropdown(AIWorker worker)
		{
			if (primaryBuildingDropdown == null)
				return;

			primaryBuildingDropdown.onValueChanged.RemoveListener(OnPrimaryBuildingDropdownValueChanged);
			primaryBuildingDropdown.ClearOptions();
			validBuildingIds.Clear();

			int selectedIndex = 0;
			var options = new System.Collections.Generic.List<string>
			{
				"Bld: None (Outdoor)"
			};
			validBuildingIds.Add(0);

			if (worker.PrimaryBuildingId == 0)
				selectedIndex = 0;

			var buildingMgr = GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
			if (buildingMgr != null)
			{
				for (int i = 0; i < buildingMgr.RegisteredBuildings.Count; ++i)
				{
					Building building = buildingMgr.RegisteredBuildings[i];
					if (building == null)
						continue;

					if (building.RuntimeBuildingId == worker.PrimaryBuildingId)
						selectedIndex = validBuildingIds.Count;

					validBuildingIds.Add(building.RuntimeBuildingId);
					options.Add($"Bld: {building.DisplayName}");
				}
			}

			primaryBuildingDropdown.interactable = options.Count > 0;
			primaryBuildingDropdown.AddOptions(options);
			primaryBuildingDropdown.value = Mathf.Clamp(selectedIndex, 0, options.Count - 1);
			primaryBuildingDropdown.RefreshShownValue();
			primaryBuildingDropdown.onValueChanged.AddListener(OnPrimaryBuildingDropdownValueChanged);
		}

		private void EnsureManagementLayout()
		{
			if (managementTab == null || typeDropdown == null)
				return;

			if (managementTab.TryGetComponent(out LayoutElement managementLayout))
			{
				managementLayout.preferredWidth = 180f;
				managementLayout.minWidth = 180f;
				managementLayout.preferredHeight = 120f;
				managementLayout.minHeight = 120f;
			}

			ConfigureDropdownRect(handleGroupDropdown != null ? handleGroupDropdown.GetComponent<RectTransform>() : null, 0f, -4f);
			ConfigureDropdownRect(typeDropdown.GetComponent<RectTransform>(), 0f, -42f);
			ConfigureDropdownRect(primaryBuildingDropdown != null ? primaryBuildingDropdown.GetComponent<RectTransform>() : null, 0f, -80f);
		}

		private static void ConfigureDropdownRect(RectTransform rect, float left, float top)
		{
			if (rect == null)
				return;

			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = new Vector2(0f, top);
			rect.sizeDelta = new Vector2(left - 16f, 34f);
		}

		private void OnHandleGroupDropdownValueChanged(int index)
		{
			if (currentWorker == null || index < 0 || index >= validHandleGroups.Count)
				return;

			selectedHandleGroup = validHandleGroups[index];
			ConfigureTaskDropdown(currentWorker);
		}

		private void OnTaskDropdownValueChanged(int index)
		{
			if (currentWorker == null || index < 0 || index >= validTypes.Count)
				return;

			WorkerTask.TaskType newType = validTypes[index];
			if (newType == currentWorker.TaskType)
				return;

			GameContext.Instance.WorkerMgr.ChangeWorkerTaskType(currentWorker, newType);
			taskText.text = $"Task: {currentWorker.TaskType}";
			if (newType != WorkerTask.TaskType.Undefined)
				selectedHandleGroup = GetHandleGroup(newType);
		}

		private void OnPrimaryBuildingDropdownValueChanged(int index)
		{
			if (currentWorker == null || index < 0 || index >= validBuildingIds.Count)
				return;

			currentWorker.SetPrimaryBuildingId(validBuildingIds[index]);
			ConfigureTaskDropdown(currentWorker);
		}

		private WorkerTaskHandleGroup GetSelectedHandleGroup()
		{
			if (handleGroupDropdown == null || handleGroupDropdown.value < 0 || handleGroupDropdown.value >= validHandleGroups.Count)
				return currentWorker != null ? GetHandleGroup(currentWorker.TaskType) : WorkerTaskHandleGroup.Undefined;

			return selectedHandleGroup;
		}

		private static WorkerTaskHandleGroup GetHandleGroup(WorkerTask.TaskType taskType)
		{
			return taskType switch
			{
				WorkerTask.TaskType.IB or
				WorkerTask.TaskType.OB or
				WorkerTask.TaskType.CargoTransfer or
				WorkerTask.TaskType.Loading or
				WorkerTask.TaskType.Unloading => WorkerTaskHandleGroup.CargoHandle,

				WorkerTask.TaskType.Picking or
				WorkerTask.TaskType.Storing or
				WorkerTask.TaskType.Water or
				WorkerTask.TaskType.Packing or
				WorkerTask.TaskType.Labeling => WorkerTaskHandleGroup.ItemHandle,

				_ => WorkerTaskHandleGroup.Undefined,
			};
		}

		private static bool IsTaskTypeVisibleInHandleGroup(WorkerTask.TaskType taskType, WorkerTaskHandleGroup group)
		{
			if (taskType == WorkerTask.TaskType.Undefined)
				return true;

			return GetHandleGroup(taskType) == group;
		}

		private Color GetFatigueColor(float fatigue)
		{
			if (fatigue < 50) return Color.green;
			if (fatigue < 80) return new Color(1f, 0.5f, 0f); // Orange
			return Color.red;
		}

		private Color GetBatteryColor(float battery)
		{
			if (battery > 50) return Color.green;
			if (battery > 20) return new Color(1f, 0.5f, 0f); // Orange
			return Color.red;
		}
	}
}
