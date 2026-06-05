using System;

[Serializable]
public enum RocketLandingSeverity
{
	Soft,
	Hard,
}

[Serializable]
public struct RocketLandingOutcome
{
	public RocketLandingSeverity Severity;
	public int OverriddenTargetCount;
	public int OverriddenRocketCount;

	public bool HasOverride => OverriddenTargetCount > 0;
	public bool HasRocketOverride => OverriddenRocketCount > 0;

	public RocketLandingOutcome(RocketLandingSeverity severity, int overriddenTargetCount, int overriddenRocketCount)
	{
		Severity = severity;
		OverriddenTargetCount = overriddenTargetCount;
		OverriddenRocketCount = overriddenRocketCount;
	}
}
