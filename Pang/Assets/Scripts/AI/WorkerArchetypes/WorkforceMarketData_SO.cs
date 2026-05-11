using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Worker/MarketData")]
public class WorkforceMarketData_SO : ScriptableObject
{
	// full, part, illegal, transport robot, etc
	[SerializeField] private string workforceMarketName = string.Empty;
	[SerializeField] private bool randomCombination = false;

	[SerializeField] private List<WorkerNameDefinition> workerNames;
	[SerializeField] private List<WorkerVisualDefinition> workerVisuals;
	[SerializeField] private List<WorkerAbilityDefinition> workerAbilities;
	[SerializeField] private List<WorkerBaseStatDefinition> workerBaseStats;

	public string WorkForceMarketName => workforceMarketName;

	private void OnValidate()
	{
		if (randomCombination)
		{
			int cnt = workerNames.Count;
			if (workerAbilities.Count != cnt || workerBaseStats.Count != cnt)
			{
				Debug.LogError("Worker Archetypes Count Mismatch");
			}
		}
	}

	public IEnumerable<WorkerArchetype> EnumerateArchetypes(int page, int count)
	{
		if (randomCombination)
		{
			for (int i = 0; i < count; ++i)
			{
				int firstNameIdx = Random.Range(0, workerNames.Count);
				int lastNameIdx = Random.Range(0, workerNames.Count);
				int visualIdx = Random.Range(0, workerVisuals.Count);
				int abilityIdx = Random.Range(0, workerAbilities.Count);
				int statIdx = Random.Range(0, workerBaseStats.Count);

				WorkerNameDefinition name = new();
				name.WorkerFirstName = workerNames[firstNameIdx].WorkerFirstName;
				name.WorkerLastName = workerNames[lastNameIdx].WorkerLastName;

				yield return new(name, workerVisuals[visualIdx], workerAbilities[abilityIdx], workerBaseStats[statIdx]);
			}
			
		}
		else
		{
			for (int i = page * count; i < workerNames.Count && i < page * count + count; ++i)
			{
				yield return new(workerNames[i], workerVisuals[i], workerAbilities[i], workerBaseStats[i]);
			}
		}

		yield break;
	}
}
