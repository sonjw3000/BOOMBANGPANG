using System;

public enum WorkerIncidentResponseKind
{
	Medical,
	RobotFix,
}

public enum WorkerIncidentCaseState
{
	AwaitingService,
	ServiceRequested,
	HandedOver,
	Resolved,
}

public enum WorkerServiceProviderKind
{
	None,
	Internal,
	ExternalVendor,
}

public enum OccupationalClaimDecision
{
	Pending,
	Process,
	DoNotProcess,
}

public enum OccupationalClaimOutcome
{
	NotApplicable,
	Pending,
	Processed,
	NotProcessed,
}

[Serializable]
public sealed class WorkerIncidentCase
{
	public int IncidentId;
	public uint WorkerId;
	public WorkerKind WorkerKind;
	public HumanType HumanType;
	public WorkerOperationalState OperationalState;
	public WorkerIncidentResponseKind ResponseKind;
	public WorkerIncidentCaseState State;
	public WorkerServiceProviderKind ProviderKind;
	public uint ProviderId;
	public OccupationalClaimDecision ClaimDecision;
	public OccupationalClaimOutcome ClaimOutcome;
	public bool BrokeNoAccidentRecord;
	public bool AppliedReputationPenalty;
}

public readonly struct WorkerServiceHandoff
{
	public readonly int IncidentId;
	public readonly WorkerServiceProviderKind ProviderKind;
	public readonly uint ProviderId;

	public WorkerServiceHandoff(int incidentId, WorkerServiceProviderKind providerKind, uint providerId)
	{
		IncidentId = incidentId;
		ProviderKind = providerKind;
		ProviderId = providerId;
	}
}
