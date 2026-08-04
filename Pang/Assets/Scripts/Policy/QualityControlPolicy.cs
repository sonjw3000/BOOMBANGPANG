using UnityEngine;

public readonly struct QualityInspectionResult
{
	public readonly bool Accepted;
	public readonly bool FailedFreshness;
	public readonly bool FailedDamage;
	public readonly bool WasWaste;

	public QualityInspectionResult(
		bool accepted,
		bool failedFreshness,
		bool failedDamage,
		bool wasWaste)
	{
		Accepted = accepted;
		FailedFreshness = failedFreshness;
		FailedDamage = failedDamage;
		WasWaste = wasWaste;
	}
}

public static class QualityControlPolicy
{
	public const float DefaultMinimumFreshnessPercent = 50.0f;
	public const float DefaultMaximumDamagePercent = 50.0f;

	public static QualityInspectionResult Inspect(
		ItemStack stack,
		float minimumFreshnessPercent,
		float maximumDamagePercent)
	{
		if (stack == null || stack.Quantity <= 0)
			return new QualityInspectionResult(false, false, false, false);

		bool usesFreshness = UsesFreshness(stack.ItemID);
		bool failedFreshness = usesFreshness &&
			stack.FreshnessPercent < Mathf.Clamp(minimumFreshnessPercent, 0.0f, 100.0f);
		bool failedDamage = stack.DamagePercent > Mathf.Clamp(maximumDamagePercent, 0.0f, 100.0f);
		bool wasWaste = stack.HasQuality(ItemQuality.Waste);
		return new QualityInspectionResult(
			failedFreshness == false && failedDamage == false && wasWaste == false,
			failedFreshness,
			failedDamage,
			wasWaste);
	}

	private static bool UsesFreshness(uint itemId)
	{
		return itemId != 0 &&
			GameContext.HasInstance &&
			GameContext.Instance.ItemDB != null &&
			GameContext.Instance.ItemDB.GetItemData(itemId, out ItemDefinition definition) &&
			definition != null &&
			definition.UsesFreshness;
	}
}
