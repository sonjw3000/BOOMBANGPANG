using System.Collections.Generic;

public enum RobotNavigationDependency
{
	HubOrchestrated = 0,
	OnboardCompute = 1,
	FullyAutonomous = 2,
}

public enum RobotNavigationWaitReason
{
	None = 0,
	Coverage = 1,
	OrchestrationCapacity = 2,
}

public readonly struct NavigationTransitionReservation
{
	internal readonly int Id;

	internal NavigationTransitionReservation(int id)
	{
		Id = id;
	}

	public bool RequiresCommit => Id != 0;
}

public static class RobotNavigationAllocationMath
{
	public static Dictionary<uint, int> SplitCompute(int requiredCompute, IReadOnlyList<uint> orderedHubIds)
	{
		Dictionary<uint, int> shares = new();
		if (requiredCompute <= 0 || orderedHubIds == null || orderedHubIds.Count == 0)
			return shares;

		int baseShare = requiredCompute / orderedHubIds.Count;
		int remainder = requiredCompute % orderedHubIds.Count;
		for (int i = 0; i < orderedHubIds.Count; ++i)
		{
			int share = baseShare + (i < remainder ? 1 : 0);
			if (orderedHubIds[i] != 0 && share > 0)
				shares[orderedHubIds[i]] = share;
		}
		return shares;
	}

	public static Dictionary<uint, int> PositiveDelta(
		IReadOnlyDictionary<uint, int> current,
		IReadOnlyDictionary<uint, int> target)
	{
		Dictionary<uint, int> result = new();
		if (target == null)
			return result;

		foreach (KeyValuePair<uint, int> entry in target)
		{
			int currentValue = current != null && current.TryGetValue(entry.Key, out int value) ? value : 0;
			int increase = entry.Value - currentValue;
			if (increase > 0)
				result[entry.Key] = increase;
		}
		return result;
	}

	public static bool FitsCapacity(int capacity, int assigned, int reserved, int increase)
	{
		return capacity >= 0 && assigned >= 0 && reserved >= 0 && increase >= 0 &&
			(long)assigned + reserved + increase <= capacity;
	}
}
