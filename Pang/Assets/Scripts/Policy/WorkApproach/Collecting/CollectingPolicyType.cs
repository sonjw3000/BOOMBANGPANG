public enum CollectingPolicyType
{
	Nearest,
	LargestQuantityNearest,
}

public static class CollectingPolicyFactory
{
	public static ICollectingPolicy<TRequestLine> Create<TRequestLine>(CollectingPolicyType type)
	{
		switch (type)
		{
			case CollectingPolicyType.LargestQuantityNearest:
				return new LargestQuantityNearestCollectingPolicy<TRequestLine>();

			case CollectingPolicyType.Nearest:
			default:
				return new NearestCollectingPolicy<TRequestLine>();
		}
	}
}
