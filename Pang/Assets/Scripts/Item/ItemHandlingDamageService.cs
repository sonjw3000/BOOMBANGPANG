using UnityEngine;

public class ItemHandlingDamageService : MonoBehaviour
{
	[Header("Worker Handling Risk")]
	[SerializeField, Range(0.0f, 1.0f)] private float humanBaseDamageChance = 0.01f;
	[SerializeField, Range(0.0f, 1.0f)] private float humanFatigueDamageChance = 0.09f;
	[SerializeField, Range(0.0f, 1.0f)] private float robotBaseDamageChance = 0.01f;

	[Header("Damage")]
	[SerializeField, Range(1, 100)] private int minimumDamageIncrease = 5;
	[SerializeField, Range(1, 100)] private int maximumDamageIncrease = 15;

	public bool TryRollDamage(
		AIWorker worker,
		ItemStack stack,
		IItemContainer destination,
		out byte damageIncrease)
	{
		damageIncrease = 0;
		if (worker == null || stack == null || stack.Quantity <= 0 || destination == null)
			return false;

		float chance = Mathf.Clamp01(
			CalculateWorkerRisk(worker) +
			CalculateWorkerItemRisk(worker, stack) +
			CalculateItemDestinationRisk(stack, destination));
		if (chance <= 0.0f || Random.value >= chance)
			return false;

		int minimum = Mathf.Clamp(minimumDamageIncrease, 1, 100);
		int maximum = Mathf.Clamp(maximumDamageIncrease, minimum, 100);
		damageIncrease = (byte)Random.Range(minimum, maximum + 1);
		return damageIncrease > 0;
	}

	private float CalculateWorkerRisk(AIWorker worker)
	{
		if (worker.WorkerKind == WorkerKind.Robot)
			return robotBaseDamageChance;

		float fatigue = Mathf.Clamp01(worker.GetFatigue() / 100.0f);
		return humanBaseDamageChance + humanFatigueDamageChance * fatigue;
	}

	private static float CalculateWorkerItemRisk(AIWorker worker, ItemStack stack)
	{
		// Reserved for worker proficiency with specific item types or tags.
		return 0.0f;
	}

	private static float CalculateItemDestinationRisk(ItemStack stack, IItemContainer destination)
	{
		// Reserved for compatibility between an item and its destination container.
		return 0.0f;
	}
}
