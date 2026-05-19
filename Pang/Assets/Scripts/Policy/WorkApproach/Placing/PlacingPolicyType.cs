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
			case PlacingPolicyType.Nearest:
				return new NearestPlacingPolicy();

			case PlacingPolicyType.BelowAverageFilledNearest:
			default:
				return new BelowAverageFilledNearestPlacingPolicy();
		}
	}
}
