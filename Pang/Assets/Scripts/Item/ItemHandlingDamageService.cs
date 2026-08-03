using UnityEngine;

public class ItemHandlingDamageService : MonoBehaviour
{
	[Header("Robot Handling Risk")]
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

		// Human handling damage is owned by HumanIncidentService so that its
		// fixed worker roll and saved RNG state cannot be bypassed by reloading.
		return 0.0f;
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
