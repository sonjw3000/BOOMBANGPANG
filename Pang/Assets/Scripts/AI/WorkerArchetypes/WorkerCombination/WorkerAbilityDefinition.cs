using UnityEngine;
using UnityEngine.Serialization;

public enum WorkerKind
{
	Human,
	Robot,
	None,
}

public enum HumanType
{
	FullTime,
	PartTime,
	Illegal,
}

public enum RobotType
{
	Transfer,
}

public enum WorkerPolicyType
{
	HumanFullTime = 0,
	HumanPartTime = 1,
	HumanIllegal = 2,
	RobotTransfer = 3,
}

public enum LegacyWorkerIdentityType
{
	FullTime = 0,
	PartTime = 1,
	Illegal = 2,
	Robot = 3,
}

[System.Serializable]
public struct WorkerAbilityDefinition
{
	[FormerlySerializedAs("workerType")]
	[SerializeField] private LegacyWorkerIdentityType legacyWorkerIdentityType;
	[SerializeField] private WorkerKind workerKind;
	[SerializeField] private HumanType humanType;
	[SerializeField] private RobotType robotType;
	[SerializeField] private bool identityInitialized;

	public WorkerAbility abilities;
	public int monthlyCost;
	public int installCost;

	public WorkerKind WorkerKind
	{
		get
		{
			EnsureIdentityInitialized();
			return workerKind;
		}
	}

	public HumanType HumanType
	{
		get
		{
			EnsureIdentityInitialized();
			return humanType;
		}
	}

	public RobotType RobotType
	{
		get
		{
			EnsureIdentityInitialized();
			return robotType;
		}
	}

	public WorkerPolicyType PolicyType
	{
		get
		{
			EnsureIdentityInitialized();
			return workerKind == WorkerKind.Robot
				? WorkerPolicyType.RobotTransfer
				: humanType switch
				{
					HumanType.PartTime => WorkerPolicyType.HumanPartTime,
					HumanType.Illegal => WorkerPolicyType.HumanIllegal,
					_ => WorkerPolicyType.HumanFullTime,
				};
		}
	}

	public void SetHumanIdentity(HumanType humanType)
	{
		workerKind = WorkerKind.Human;
		this.humanType = humanType;
		robotType = RobotType.Transfer;
		legacyWorkerIdentityType = humanType switch
		{
			HumanType.PartTime => LegacyWorkerIdentityType.PartTime,
			HumanType.Illegal => LegacyWorkerIdentityType.Illegal,
			_ => LegacyWorkerIdentityType.FullTime,
		};
		identityInitialized = true;
	}

	public void SetRobotIdentity(RobotType robotType)
	{
		workerKind = WorkerKind.Robot;
		humanType = HumanType.FullTime;
		this.robotType = robotType;
		legacyWorkerIdentityType = LegacyWorkerIdentityType.Robot;
		identityInitialized = true;
	}

	public void EnsureIdentityInitialized()
	{
		if (identityInitialized)
			return;

		switch (legacyWorkerIdentityType)
		{
			case LegacyWorkerIdentityType.PartTime:
				SetHumanIdentity(HumanType.PartTime);
				break;

			case LegacyWorkerIdentityType.Illegal:
				SetHumanIdentity(HumanType.Illegal);
				break;

			case LegacyWorkerIdentityType.Robot:
				SetRobotIdentity(RobotType.Transfer);
				break;

			case LegacyWorkerIdentityType.FullTime:
			default:
				SetHumanIdentity(HumanType.FullTime);
				break;
		}
	}
}
