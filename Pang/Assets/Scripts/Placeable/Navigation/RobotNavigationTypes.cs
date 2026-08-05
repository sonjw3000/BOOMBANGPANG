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
