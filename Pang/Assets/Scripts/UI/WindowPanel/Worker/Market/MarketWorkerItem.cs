using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
	public class MarketWorkerItem : MonoBehaviour
	{
		[SerializeField] private TMP_Text nameText;
		[SerializeField] private TMP_Text abilityText;
		[SerializeField] private TMP_Text wageText;
		[SerializeField] private TMP_Text riskText;
		[SerializeField] private Button hireButton;

		private WorkerArchetype currentArchetype;

		public void Setup(WorkerArchetype archetype)
		{
			currentArchetype = archetype;
			nameText.text = archetype.workerName;
			abilityText.text = $"Ability: {archetype.abilities}";
			wageText.text = $"Wage: {archetype.monthlyCost}/month"; // Changed to monthly as per current SO
			riskText.text = "Risk: Low"; // Defaulting as requested

			hireButton.onClick.RemoveAllListeners();
			hireButton.onClick.AddListener(Hire);
		}

		private void Hire()
		{
			var workerSpawnMgr = GameContext.Instance.WorkerSpawnMgr;
			if (workerSpawnMgr == null)
			{
				Debug.LogWarning("WorkerSpawnManager is not available.");
				return;
			}

			if (workerSpawnMgr.TrySpawnWorker(currentArchetype, this, out var spawnedWorker) == false)
			{
				Debug.LogWarning($"Failed to hire {currentArchetype.workerName}");
				return;
			}

			Debug.Log($"Hired {spawnedWorker.Name}!");
		}
	}
}
