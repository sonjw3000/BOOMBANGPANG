public enum StoringPlacingPolicyType
{
	BelowAverageFilledNearest,
	Nearest,
}

public static class StoringPlacingPolicyFactory
{
	public static IPlacingPolicy Create(StoringPlacingPolicyType type)
	{
		switch (type)
		{
			case StoringPlacingPolicyType.Nearest:
				return new NearestPlacingPolicy();

			case StoringPlacingPolicyType.BelowAverageFilledNearest:
			default:
				return new BelowAverageFilledNearestPlacingPolicy();
		}
	}
}
