public enum PlacingPolicyType
{
	BelowAverageFilledNearest,
	Nearest,
}

public static class PlacingPolicyFactory
{
	public static IPlacingPolicy Create(PlacingPolicyType type)
	{
		switch (type)
		{
			case PlacingPolicyType.BelowAverageFilledNearest:
				return new BelowAverageFilledNearestPlacingPolicy();

			case PlacingPolicyType.Nearest:
			default:
				return new NearestPlacingPolicy();
		}
	}
}
