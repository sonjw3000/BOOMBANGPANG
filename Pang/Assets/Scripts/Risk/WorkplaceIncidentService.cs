using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class WorkplaceIncidentDecision
{
	public static bool ShouldBreakNoAccidentRecord(
		bool claimProcessed,
		WorkerServiceProviderKind providerKind)
		=> claimProcessed || providerKind == WorkerServiceProviderKind.ExternalVendor;

	public static bool ShouldApplyUnprocessedClaimPenalty(bool claimProcessed, HumanType humanType)
		=> claimProcessed == false && humanType != HumanType.Illegal;
}

public sealed class WorkplaceIncidentService : MonoBehaviour
{
	[SerializeField, Min(0f)] private float unprocessedClaimReputationPenalty = 1f;

	private readonly List<WorkerIncidentCase> incidents = new();
	private readonly Dictionary<int, WorkerIncidentCase> incidentsById = new();
	private readonly Dictionary<uint, WorkerIncidentCase> currentIncidentByWorker = new();
	private readonly Dictionary<uint, bool> claimProcessingByWorker = new();
	private readonly Dictionary<uint, WorkerIncidentContext> pendingIncidentContextByWorker = new();

	private WorkerManager workerManager;
	private MedicalService medicalService;
	private RobotFixService robotFixService;
	private VendorService vendorService;
	private EconomyService economyService;
	private int nextIncidentId = 1;
	private bool isAccidentFree = true;
	private bool isBound;

	public IReadOnlyList<WorkerIncidentCase> Incidents => incidents;
	public bool IsAccidentFree => isAccidentFree;

	public event Action<WorkerIncidentCase> OnIncidentCreated;
	public event Action<WorkerIncidentCase> OnIncidentResolved;
	public event Action<bool> OnAccidentFreeChanged;

	public void Initialize(
		WorkerManager workers,
		MedicalService medical,
		RobotFixService robotFix,
		VendorService vendors,
		EconomyService economy)
	{
		Unbind();
		workerManager = workers;
		medicalService = medical;
		robotFixService = robotFix;
		vendorService = vendors;
		economyService = economy;

		if (workerManager == null || medicalService == null || robotFixService == null || vendorService == null)
			return;

		workerManager.OnWorkerOperationalStateChanged += HandleWorkerOperationalStateChanged;
		medicalService.OnHandoffCompleted += HandleMedicalHandoffCompleted;
		medicalService.OnServiceAvailabilityChanged += RetryPendingIncidents;
		robotFixService.OnHandoffCompleted += HandleRobotFixHandoffCompleted;
		robotFixService.OnServiceAvailabilityChanged += RetryPendingIncidents;
		vendorService.OnVendorsChanged += RetryPendingIncidents;
		isBound = true;
	}

	public void Unbind()
	{
		if (isBound == false)
			return;

		if (workerManager != null)
			workerManager.OnWorkerOperationalStateChanged -= HandleWorkerOperationalStateChanged;
		if (medicalService != null)
		{
			medicalService.OnHandoffCompleted -= HandleMedicalHandoffCompleted;
			medicalService.OnServiceAvailabilityChanged -= RetryPendingIncidents;
		}
		if (robotFixService != null)
		{
			robotFixService.OnHandoffCompleted -= HandleRobotFixHandoffCompleted;
			robotFixService.OnServiceAvailabilityChanged -= RetryPendingIncidents;
		}
		if (vendorService != null)
			vendorService.OnVendorsChanged -= RetryPendingIncidents;

		isBound = false;
	}

	public void SetClaimProcessing(uint workerId, bool shouldProcess)
	{
		claimProcessingByWorker[workerId] = shouldProcess;
	}

	public bool ShouldProcessClaim(uint workerId)
	{
		return claimProcessingByWorker.TryGetValue(workerId, out bool shouldProcess)
			? shouldProcess
			: true;
	}

	public bool TryPrepareRobotCollision(
		RobotWorker robot,
		HumanWorker human,
		in int3 position,
		ulong simulationTick)
	{
		if (robot == null || human == null ||
			currentIncidentByWorker.ContainsKey(human.WorkerID) ||
			pendingIncidentContextByWorker.ContainsKey(human.WorkerID))
		{
			return false;
		}

		pendingIncidentContextByWorker[human.WorkerID] = new WorkerIncidentContext(
			WorkerIncidentCause.RobotCollision,
			robot.WorkerID,
			human.WorkerID,
			position,
			simulationTick);
		return true;
	}

	public WorkerIncidentCase CompletePreparedRobotCollision(HumanWorker human)
	{
		if (human == null)
			return null;

		if (currentIncidentByWorker.TryGetValue(human.WorkerID, out WorkerIncidentCase current))
			return current;

		if (human.IsOperational ||
			pendingIncidentContextByWorker.Remove(human.WorkerID, out WorkerIncidentContext context) == false)
		{
			return null;
		}

		return CreateIncident(human, human.OperationalState, in context);
	}

	public void CancelPreparedRobotCollision(HumanWorker human)
	{
		if (human != null)
			pendingIncidentContextByWorker.Remove(human.WorkerID);
	}

	public WorkplaceIncidentSaveData CaptureState()
	{
		WorkplaceIncidentSaveData data = new()
		{
			NextIncidentId = nextIncidentId,
			IsAccidentFree = isAccidentFree,
		};

		for (int i = 0; i < incidents.Count; ++i)
			data.Incidents.Add(CloneIncident(incidents[i]));

		foreach (KeyValuePair<uint, bool> entry in claimProcessingByWorker)
		{
			data.ClaimSettings.Add(new WorkerClaimSettingSaveData
			{
				WorkerId = entry.Key,
				ShouldProcess = entry.Value,
			});
		}

		return data;
	}

	public void RestoreState(WorkplaceIncidentSaveData data)
	{
		ResetRuntimeState();
		if (data == null)
			return;

		nextIncidentId = Mathf.Max(1, data.NextIncidentId);
		isAccidentFree = data.IsAccidentFree;

		if (data.ClaimSettings != null)
		{
			for (int i = 0; i < data.ClaimSettings.Count; ++i)
			{
				WorkerClaimSettingSaveData setting = data.ClaimSettings[i];
				claimProcessingByWorker[setting.WorkerId] = setting.ShouldProcess;
			}
		}

		if (data.Incidents != null)
		{
			for (int i = 0; i < data.Incidents.Count; ++i)
			{
				WorkerIncidentCase incident = CloneIncident(data.Incidents[i]);
				if (incident == null)
					continue;

				incidents.Add(incident);
				incidentsById[incident.IncidentId] = incident;
				currentIncidentByWorker[incident.WorkerId] = incident;
			}
		}

		RetryPendingIncidents();
	}

	public void ResetRuntimeState()
	{
		incidents.Clear();
		incidentsById.Clear();
		currentIncidentByWorker.Clear();
		claimProcessingByWorker.Clear();
		pendingIncidentContextByWorker.Clear();
		nextIncidentId = 1;
		isAccidentFree = true;
	}

	private void HandleWorkerOperationalStateChanged(
		AIWorker worker,
		WorkerOperationalState previousState,
		WorkerOperationalState nextState)
	{
		if (worker == null || nextState == WorkerOperationalState.Active)
			return;

		if (currentIncidentByWorker.TryGetValue(worker.WorkerID, out WorkerIncidentCase current))
		{
			current.OperationalState = nextState;
			return;
		}

		WorkerIncidentContext context = default;
		if (pendingIncidentContextByWorker.TryGetValue(worker.WorkerID, out context))
			pendingIncidentContextByWorker.Remove(worker.WorkerID);

		CreateIncident(worker, nextState, in context);
	}

	private WorkerIncidentCase CreateIncident(
		AIWorker worker,
		WorkerOperationalState operationalState,
		in WorkerIncidentContext context)
	{
		WorkerIncidentCase incident = new()
		{
			IncidentId = nextIncidentId++,
			Cause = context.Cause,
			WorkerId = worker.WorkerID,
			InstigatorWorkerId = context.InstigatorWorkerId,
			VictimWorkerId = context.VictimWorkerId,
			PositionX = context.Position.x,
			PositionY = context.Position.y,
			PositionZ = context.Position.z,
			OccurredAtSimulationTick = context.OccurredAtSimulationTick,
			WorkerKind = worker.WorkerKind,
			HumanType = worker.HumanType,
			OperationalState = operationalState,
			ResponseKind = worker.WorkerKind == WorkerKind.Human
				? WorkerIncidentResponseKind.Medical
				: WorkerIncidentResponseKind.RobotFix,
			State = WorkerIncidentCaseState.AwaitingService,
			ProviderKind = WorkerServiceProviderKind.None,
			ClaimDecision = worker.WorkerKind == WorkerKind.Human
				? OccupationalClaimDecision.Pending
				: OccupationalClaimDecision.DoNotProcess,
			ClaimOutcome = worker.WorkerKind == WorkerKind.Human
				? OccupationalClaimOutcome.Pending
				: OccupationalClaimOutcome.NotApplicable,
		};

		incidents.Add(incident);
		incidentsById[incident.IncidentId] = incident;
		currentIncidentByWorker[incident.WorkerId] = incident;
		OnIncidentCreated?.Invoke(incident);
		TryRequestService(incident, worker);
		return incident;
	}

	private void RetryPendingIncidents()
	{
		if (workerManager == null)
			return;

		for (int i = 0; i < incidents.Count; ++i)
		{
			WorkerIncidentCase incident = incidents[i];
			if (incident == null || incident.State != WorkerIncidentCaseState.AwaitingService)
				continue;

			AIWorker worker = FindWorker(incident.WorkerId);
			if (worker != null)
				TryRequestService(incident, worker);
		}
	}

	private void TryRequestService(WorkerIncidentCase incident, AIWorker worker)
	{
		if (incident == null || worker == null || incident.State != WorkerIncidentCaseState.AwaitingService)
			return;
		if (incident.ResponseKind == WorkerIncidentResponseKind.Medical && medicalService == null)
			return;
		if (incident.ResponseKind == WorkerIncidentResponseKind.RobotFix && robotFixService == null)
			return;

		incident.State = WorkerIncidentCaseState.ServiceRequested;
		bool accepted = incident.ResponseKind == WorkerIncidentResponseKind.Medical
			? medicalService.RequestCare(incident.IncidentId, worker)
			: robotFixService.RequestRepair(incident.IncidentId, worker);

		if (accepted == false && incident.State == WorkerIncidentCaseState.ServiceRequested)
			incident.State = WorkerIncidentCaseState.AwaitingService;
	}

	private void HandleMedicalHandoffCompleted(WorkerServiceHandoff handoff)
	{
		if (incidentsById.TryGetValue(handoff.IncidentId, out WorkerIncidentCase incident) == false ||
			incident.State == WorkerIncidentCaseState.Resolved)
			return;

		incident.ProviderKind = handoff.ProviderKind;
		incident.ProviderId = handoff.ProviderId;
		incident.State = WorkerIncidentCaseState.HandedOver;

		bool claimProcessed = ShouldProcessClaim(incident.WorkerId);
		incident.ClaimDecision = claimProcessed
			? OccupationalClaimDecision.Process
			: OccupationalClaimDecision.DoNotProcess;
		incident.ClaimOutcome = claimProcessed
			? OccupationalClaimOutcome.Processed
			: OccupationalClaimOutcome.NotProcessed;

		if (WorkplaceIncidentDecision.ShouldBreakNoAccidentRecord(claimProcessed, handoff.ProviderKind))
			BreakNoAccidentRecord(incident);

		if (WorkplaceIncidentDecision.ShouldApplyUnprocessedClaimPenalty(claimProcessed, incident.HumanType))
			ApplyUnprocessedClaimPenalty(incident);

		ResolveIncident(incident);
	}

	private void HandleRobotFixHandoffCompleted(WorkerServiceHandoff handoff)
	{
		if (incidentsById.TryGetValue(handoff.IncidentId, out WorkerIncidentCase incident) == false ||
			incident.State == WorkerIncidentCaseState.Resolved)
			return;

		incident.ProviderKind = handoff.ProviderKind;
		incident.ProviderId = handoff.ProviderId;
		incident.ClaimDecision = OccupationalClaimDecision.DoNotProcess;
		incident.ClaimOutcome = OccupationalClaimOutcome.NotApplicable;
		ResolveIncident(incident);
	}

	private void BreakNoAccidentRecord(WorkerIncidentCase incident)
	{
		incident.BrokeNoAccidentRecord = true;
		if (isAccidentFree == false)
			return;

		isAccidentFree = false;
		OnAccidentFreeChanged?.Invoke(false);
	}

	private void ApplyUnprocessedClaimPenalty(WorkerIncidentCase incident)
	{
		incident.AppliedReputationPenalty = true;
		if (unprocessedClaimReputationPenalty <= 0f || economyService == null)
			return;

		economyService.ApplyTransaction(new EconomyTransaction
		{
			moneyDelta = 0,
			reputationDelta = -unprocessedClaimReputationPenalty,
			reason = EconomyTransaction.Reason.OccupationalClaimNotProcessed,
		});
	}

	private void ResolveIncident(WorkerIncidentCase incident)
	{
		incident.State = WorkerIncidentCaseState.Resolved;
		OnIncidentResolved?.Invoke(incident);
	}

	private AIWorker FindWorker(uint workerId)
	{
		IReadOnlyList<AIWorker> workers = workerManager.Workers;
		for (int i = 0; i < workers.Count; ++i)
		{
			if (workers[i] != null && workers[i].WorkerID == workerId)
				return workers[i];
		}

		return null;
	}

	private static WorkerIncidentCase CloneIncident(WorkerIncidentCase source)
	{
		if (source == null)
			return null;

		return new WorkerIncidentCase
		{
			IncidentId = source.IncidentId,
			Cause = source.Cause,
			WorkerId = source.WorkerId,
			InstigatorWorkerId = source.InstigatorWorkerId,
			VictimWorkerId = source.VictimWorkerId,
			PositionX = source.PositionX,
			PositionY = source.PositionY,
			PositionZ = source.PositionZ,
			OccurredAtSimulationTick = source.OccurredAtSimulationTick,
			WorkerKind = source.WorkerKind,
			HumanType = source.HumanType,
			OperationalState = source.OperationalState,
			ResponseKind = source.ResponseKind,
			State = source.State,
			ProviderKind = source.ProviderKind,
			ProviderId = source.ProviderId,
			ClaimDecision = source.ClaimDecision,
			ClaimOutcome = source.ClaimOutcome,
			BrokeNoAccidentRecord = source.BrokeNoAccidentRecord,
			AppliedReputationPenalty = source.AppliedReputationPenalty,
		};
	}
}
