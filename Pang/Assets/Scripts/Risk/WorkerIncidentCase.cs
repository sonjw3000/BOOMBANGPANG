using System;
using Unity.Mathematics;

public enum WorkerIncidentCause
{
	Unknown,
	RobotCollision,
}

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
	public WorkerIncidentCause Cause;
	public uint WorkerId;
	public uint InstigatorWorkerId;
	public uint VictimWorkerId;
	public int PositionX;
	public int PositionY;
	public int PositionZ;
	public ulong OccurredAtSimulationTick;
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

public readonly struct WorkerIncidentContext
{
	public readonly WorkerIncidentCause Cause;
	public readonly uint InstigatorWorkerId;
	public readonly uint VictimWorkerId;
	public readonly int3 Position;
	public readonly ulong OccurredAtSimulationTick;

	public WorkerIncidentContext(
		WorkerIncidentCause cause,
		uint instigatorWorkerId,
		uint victimWorkerId,
		in int3 position,
		ulong occurredAtSimulationTick)
	{
		Cause = cause;
		InstigatorWorkerId = instigatorWorkerId;
		VictimWorkerId = victimWorkerId;
		Position = position;
		OccurredAtSimulationTick = occurredAtSimulationTick;
	}
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
