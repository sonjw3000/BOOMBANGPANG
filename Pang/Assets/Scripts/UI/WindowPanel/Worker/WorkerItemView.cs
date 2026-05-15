using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.UI
{
	public class WorkerItemView : MonoBehaviour
	{
		[SerializeField] private Image thumbnail;
		[SerializeField] private TMP_Text nameText;
		[SerializeField] private TMP_Text taskText;
		[SerializeField] private TMP_Text statusText;
		[SerializeField] private TMP_Text fatigueText;

		// Right side toggle/tab placeholder
		[SerializeField] private GameObject managementTab;
		[SerializeField] private TMPro.TMP_Dropdown typeDropdown;

		private AIWorker currentWorker;
		private System.Collections.Generic.List<WorkerTask.TaskType> validTypes = new System.Collections.Generic.List<WorkerTask.TaskType>();

		private void Awake()
		{
			if (typeDropdown != null)
			{
				typeDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
			}
		}

		public void Setup(AIWorker worker)
		{
			currentWorker = worker;
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

			// Dropdown setup
			if (typeDropdown != null)
			{
				typeDropdown.onValueChanged.RemoveAllListeners();
				typeDropdown.ClearOptions();
				validTypes.Clear();

				int selectedIndex = 0;
				var options = new System.Collections.Generic.List<string>();

				foreach (WorkerTask.TaskType type in System.Enum.GetValues(typeof(WorkerTask.TaskType)))
				{
					// Hide only internal emergency handling from the management UI.
					if (type == WorkerTask.TaskType.HandleMistake)
						continue;

					if (WorkerManager.CanChangeType(worker, type))
					{
						if (type == worker.TaskType) selectedIndex = validTypes.Count;
						validTypes.Add(type);
						options.Add(type.ToString());
					}
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
				
				typeDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
			}
			}

		private void OnDropdownValueChanged(int index)
		{
			if (currentWorker == null || index < 0 || index >= validTypes.Count) return;

			var newType = validTypes[index];
			if (newType != currentWorker.TaskType)
			{
				GameContext.Instance.WorkerMgr.ChangeWorkerTaskType(currentWorker, newType);
				taskText.text = $"Task: {currentWorker.TaskType}";
			}
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
