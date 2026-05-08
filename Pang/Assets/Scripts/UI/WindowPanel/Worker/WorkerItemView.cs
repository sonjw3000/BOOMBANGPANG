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

		public void Setup(AIWorker worker)
		{
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

			// Thumbnail placeholder
			if (thumbnail != null)
			{
				// thumbnail.sprite = ...
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
