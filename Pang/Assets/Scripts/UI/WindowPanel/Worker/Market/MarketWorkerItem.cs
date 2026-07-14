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

		public WorkerArchetype CurrentArchetype = new();

		public void Setup()
		{
			nameText.text = $"{CurrentArchetype.WorkerNameDefinition.WorkerFirstName} {CurrentArchetype.WorkerNameDefinition.WorkerLastName}";
			abilityText.text = $"Ability: {CurrentArchetype.AbilityDefinition.abilities}";
			wageText.text = $"Wage: {CurrentArchetype.AbilityDefinition.monthlyCost}/month"; // Changed to monthly as per current SO
			riskText.text = "Risk: Low"; // Defaulting as requested

			hireButton.interactable = true;
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

			if (workerSpawnMgr.TryHireWorker(CurrentArchetype, this, out var spawnedWorker) == false)
			{
				Debug.LogWarning($"Failed to hire {CurrentArchetype.WorkerNameDefinition.WorkerFirstName} {CurrentArchetype.WorkerNameDefinition.WorkerLastName}");
				return;
			}

			CurrentArchetype = new();
			hireButton.interactable = false;

			Debug.Log($"Hired {spawnedWorker.Name}!");
		}
	}
}
