using System;
using System.Collections.Generic;

public static class ResearchIds
{
	public const string InventoryDigitization = "inventory_digitization";
	public const string WorkflowPolicyManagement = "workflow_policy_management";
	public const string WorkflowPolicyOptimization = "workflow_policy_optimization";
	public const string QualityControl = "quality_control";
	public const string TemperatureMonitoring = "temperature_monitoring";
	public const string ThermalOperations = "thermal_operations";
	public const string IndoorWorkProtocols = "indoor_work_protocols";
	public const string RoboticWorkforce = "robotic_workforce";
	public const string NavigationNetwork = "navigation_network";
	public const string TrafficControl = "traffic_control";
	public const string HumanRecognition = "human_recognition";
}

public enum ResearchState
{
	Locked,
	Available,
	Queued,
	InProgress,
	Completed,
}

public enum ResearchStartFailureReason
{
	None,
	ServiceUnavailable,
	UnknownResearch,
	AlreadyResearched,
	AlreadyQueued,
	ResearchInProgress,
	MissingPrerequisite,
	InsufficientFunds,
	NotQueued,
	InvalidQueuePosition,
	InvalidQueueOrder,
}

public sealed partial class ResearchService
{
	private readonly HashSet<string> researchedIds = new(StringComparer.Ordinal);
	private readonly List<string> queuedResearchIds = new();

	private ResearchCatalog catalog;
	private EconomyService economyService;
	private GameTime gameTime;
	private string activeResearchId;
	private int remainingWeeks;
	private bool weekEventBound;
	private bool moneyEventBound;
	private bool isStartingResearch;
	private bool isRestoringState;

	public event Action<string> OnResearchCompleted;
	public event Action OnResearchStateChanged;

	public ResearchCatalog Catalog => catalog;
	public IReadOnlyList<ResearchDefinition> Definitions => catalog != null
		? catalog.Definitions
		: Array.Empty<ResearchDefinition>();
	public IReadOnlyCollection<string> ResearchedIds => researchedIds;
	public IReadOnlyList<string> QueuedResearchIds => queuedResearchIds;
	public int QueuedResearchCount => queuedResearchIds.Count;
	public string ActiveResearchId => activeResearchId;
	public int RemainingWeeks => remainingWeeks;
	public bool IsResearching => string.IsNullOrWhiteSpace(activeResearchId) == false;
	public ResearchDefinition ActiveResearch =>
		catalog != null && catalog.TryGet(activeResearchId, out ResearchDefinition definition)
			? definition
			: null;

	public void Initialize(ResearchCatalog researchCatalog, EconomyService economy, GameTime time)
	{
		UnbindWeekEvent();
		UnbindMoneyEvent();
		catalog = researchCatalog;
		economyService = economy;
		gameTime = time;
		BindMoneyEvent();

		if (IsResearching)
			BindWeekEvent();
		else if (TryStartNextQueuedResearch())
			OnResearchStateChanged?.Invoke();
	}

	public void Unbind()
	{
		UnbindWeekEvent();
		UnbindMoneyEvent();
	}

	public bool IsResearched(string researchId)
	{
		return string.IsNullOrWhiteSpace(researchId) == false &&
			researchedIds.Contains(researchId);
	}

	public ResearchState GetState(string researchId)
	{
		if (IsResearched(researchId))
			return ResearchState.Completed;

		if (string.Equals(activeResearchId, researchId, StringComparison.Ordinal))
			return ResearchState.InProgress;

		if (GetQueueIndex(researchId) >= 0)
			return ResearchState.Queued;

		return CanEnqueueResearch(researchId, out _)
			? ResearchState.Available
			: ResearchState.Locked;
	}

	public int GetQueueIndex(string researchId)
	{
		if (string.IsNullOrWhiteSpace(researchId))
			return -1;

		for (int i = 0; i < queuedResearchIds.Count; ++i)
		{
			if (string.Equals(queuedResearchIds[i], researchId, StringComparison.Ordinal))
				return i;
		}

		return -1;
	}

	public bool CanEnqueueResearch(string researchId, out ResearchStartFailureReason reason)
	{
		if (catalog == null || economyService == null || gameTime == null)
		{
			reason = ResearchStartFailureReason.ServiceUnavailable;
			return false;
		}

		if (catalog.TryGet(researchId, out ResearchDefinition definition) == false)
		{
			reason = ResearchStartFailureReason.UnknownResearch;
			return false;
		}

		if (IsResearched(researchId))
		{
			reason = ResearchStartFailureReason.AlreadyResearched;
			return false;
		}

		if (string.Equals(activeResearchId, researchId, StringComparison.Ordinal))
		{
			reason = ResearchStartFailureReason.ResearchInProgress;
			return false;
		}

		if (GetQueueIndex(researchId) >= 0)
		{
			reason = ResearchStartFailureReason.AlreadyQueued;
			return false;
		}

		HashSet<string> plannedResearchIds = BuildPlannedResearchSet();
		if (ArePrerequisitesSatisfied(definition, plannedResearchIds) == false)
		{
			reason = ResearchStartFailureReason.MissingPrerequisite;
			return false;
		}

		reason = ResearchStartFailureReason.None;
		return true;
	}

	public bool TryEnqueueResearch(string researchId, out ResearchStartFailureReason reason)
	{
		if (CanEnqueueResearch(researchId, out reason) == false)
			return false;

		queuedResearchIds.Add(researchId);
		TryStartNextQueuedResearch();
		OnResearchStateChanged?.Invoke();
		return true;
	}

	public bool TryRemoveQueuedResearch(string researchId, out ResearchStartFailureReason reason)
	{
		int queueIndex = GetQueueIndex(researchId);
		if (queueIndex < 0)
		{
			reason = ResearchStartFailureReason.NotQueued;
			return false;
		}

		List<string> candidateQueue = new(queuedResearchIds);
		candidateQueue.RemoveAt(queueIndex);
		if (ValidateQueueOrder(candidateQueue) == false)
		{
			reason = ResearchStartFailureReason.InvalidQueueOrder;
			return false;
		}

		queuedResearchIds.RemoveAt(queueIndex);
		TryStartNextQueuedResearch();
		OnResearchStateChanged?.Invoke();
		reason = ResearchStartFailureReason.None;
		return true;
	}

	public bool TryMoveQueuedResearch(
		string researchId,
		int targetIndex,
		out ResearchStartFailureReason reason)
	{
		int currentIndex = GetQueueIndex(researchId);
		if (currentIndex < 0)
		{
			reason = ResearchStartFailureReason.NotQueued;
			return false;
		}

		if (targetIndex < 0 || targetIndex >= queuedResearchIds.Count || targetIndex == currentIndex)
		{
			reason = ResearchStartFailureReason.InvalidQueuePosition;
			return false;
		}

		List<string> candidateQueue = new(queuedResearchIds);
		candidateQueue.RemoveAt(currentIndex);
		candidateQueue.Insert(targetIndex, researchId);
		if (ValidateQueueOrder(candidateQueue) == false)
		{
			reason = ResearchStartFailureReason.InvalidQueueOrder;
			return false;
		}

		queuedResearchIds.Clear();
		queuedResearchIds.AddRange(candidateQueue);
		TryStartNextQueuedResearch();
		OnResearchStateChanged?.Invoke();
		reason = ResearchStartFailureReason.None;
		return true;
	}

	public bool TryGetQueueBlockReason(out ResearchStartFailureReason reason)
	{
		reason = ResearchStartFailureReason.None;
		if (IsResearching || queuedResearchIds.Count == 0)
			return false;

		return CanStartResearch(queuedResearchIds[0], out reason) == false;
	}

	public bool CanStartResearch(string researchId, out ResearchStartFailureReason reason)
	{
		if (catalog == null || economyService == null || gameTime == null)
		{
			reason = ResearchStartFailureReason.ServiceUnavailable;
			return false;
		}

		if (catalog.TryGet(researchId, out ResearchDefinition definition) == false)
		{
			reason = ResearchStartFailureReason.UnknownResearch;
			return false;
		}

		if (IsResearched(researchId))
		{
			reason = ResearchStartFailureReason.AlreadyResearched;
			return false;
		}

		if (IsResearching)
		{
			reason = ResearchStartFailureReason.ResearchInProgress;
			return false;
		}

		int queueIndex = GetQueueIndex(researchId);
		if ((queueIndex > 0) || (queueIndex < 0 && queuedResearchIds.Count > 0))
		{
			reason = ResearchStartFailureReason.InvalidQueuePosition;
			return false;
		}

		if (ArePrerequisitesSatisfied(definition, researchedIds) == false)
		{
			reason = ResearchStartFailureReason.MissingPrerequisite;
			return false;
		}

		if (economyService.CanAfford(definition.Cost) == false)
		{
			reason = ResearchStartFailureReason.InsufficientFunds;
			return false;
		}

		reason = ResearchStartFailureReason.None;
		return true;
	}

	public bool TryStartResearch(string researchId, out ResearchStartFailureReason reason)
	{
		if (CanStartResearch(researchId, out reason) == false)
			return false;

		if (TryActivateResearch(researchId) == false)
		{
			reason = ResearchStartFailureReason.UnknownResearch;
			return false;
		}

		OnResearchStateChanged?.Invoke();
		return true;
	}

	public bool TryCompleteResearch(string researchId)
	{
		if (catalog == null ||
			catalog.TryGet(researchId, out _) == false ||
			researchedIds.Add(researchId) == false)
			return false;

		if (string.Equals(activeResearchId, researchId, StringComparison.Ordinal))
		{
			activeResearchId = null;
			remainingWeeks = 0;
			UnbindWeekEvent();
		}
		else
		{
			int queueIndex = GetQueueIndex(researchId);
			if (queueIndex >= 0)
				queuedResearchIds.RemoveAt(queueIndex);
		}

		OnResearchCompleted?.Invoke(researchId);
		TryStartNextQueuedResearch();
		OnResearchStateChanged?.Invoke();
		return true;
	}

	public void ResetRuntimeState()
	{
		UnbindWeekEvent();
		researchedIds.Clear();
		queuedResearchIds.Clear();
		activeResearchId = null;
		remainingWeeks = 0;
		OnResearchStateChanged?.Invoke();
	}

	private void OnWeekPassed()
	{
		if (IsResearching == false)
		{
			UnbindWeekEvent();
			return;
		}

		remainingWeeks = Math.Max(remainingWeeks - 1, 0);
		if (remainingWeeks > 0)
		{
			OnResearchStateChanged?.Invoke();
			return;
		}

		TryCompleteResearch(activeResearchId);
	}

	private bool TryStartNextQueuedResearch()
	{
		if (isRestoringState || isStartingResearch || IsResearching || queuedResearchIds.Count == 0)
			return false;

		string researchId = queuedResearchIds[0];
		return CanStartResearch(researchId, out _) && TryActivateResearch(researchId);
	}

	private bool TryActivateResearch(string researchId)
	{
		if (catalog == null || catalog.TryGet(researchId, out ResearchDefinition definition) == false)
			return false;

		isStartingResearch = true;
		try
		{
			int queueIndex = GetQueueIndex(researchId);
			if (queueIndex >= 0)
				queuedResearchIds.RemoveAt(queueIndex);

			activeResearchId = researchId;
			remainingWeeks = definition.DurationWeeks;
			BindWeekEvent();

			economyService.ApplyTransaction(new EconomyTransaction
			{
				moneyDelta = -definition.Cost,
				reputationDelta = 0,
				reason = EconomyTransaction.Reason.ResearchInvestment,
			});
		}
		finally
		{
			isStartingResearch = false;
		}

		return true;
	}

	private HashSet<string> BuildPlannedResearchSet()
	{
		HashSet<string> plannedResearchIds = new(researchedIds, StringComparer.Ordinal);
		if (string.IsNullOrWhiteSpace(activeResearchId) == false)
			plannedResearchIds.Add(activeResearchId);

		for (int i = 0; i < queuedResearchIds.Count; ++i)
			plannedResearchIds.Add(queuedResearchIds[i]);

		return plannedResearchIds;
	}

	private bool ValidateQueueOrder(IReadOnlyList<string> researchIds)
	{
		HashSet<string> plannedResearchIds = new(researchedIds, StringComparer.Ordinal);
		if (string.IsNullOrWhiteSpace(activeResearchId) == false)
			plannedResearchIds.Add(activeResearchId);

		for (int i = 0; i < researchIds.Count; ++i)
		{
			string researchId = researchIds[i];
			if (catalog == null ||
				catalog.TryGet(researchId, out ResearchDefinition definition) == false ||
				plannedResearchIds.Contains(researchId) ||
				ArePrerequisitesSatisfied(definition, plannedResearchIds) == false)
			{
				return false;
			}

			plannedResearchIds.Add(researchId);
		}

		return true;
	}

	private static bool ArePrerequisitesSatisfied(
		ResearchDefinition definition,
		ISet<string> availableResearchIds)
	{
		foreach (string prerequisiteUid in definition.PrerequisiteUids)
		{
			if (availableResearchIds.Contains(prerequisiteUid) == false)
				return false;
		}

		return true;
	}

	private void BindWeekEvent()
	{
		if (weekEventBound || gameTime == null)
			return;

		gameTime.OnWeekPassed += OnWeekPassed;
		weekEventBound = true;
	}

	private void UnbindWeekEvent()
	{
		if (weekEventBound && gameTime != null)
			gameTime.OnWeekPassed -= OnWeekPassed;

		weekEventBound = false;
	}

	private void BindMoneyEvent()
	{
		if (moneyEventBound || economyService == null)
			return;

		economyService.OnMoneyChanged += OnMoneyChanged;
		moneyEventBound = true;
	}

	private void UnbindMoneyEvent()
	{
		if (moneyEventBound && economyService != null)
			economyService.OnMoneyChanged -= OnMoneyChanged;

		moneyEventBound = false;
	}

	private void OnMoneyChanged(int _)
	{
		if (isRestoringState || isStartingResearch)
			return;

		if (TryStartNextQueuedResearch())
			OnResearchStateChanged?.Invoke();
	}
}
