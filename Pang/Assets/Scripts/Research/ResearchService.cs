using System;
using System.Collections.Generic;

public static class ResearchIds
{
	public const string InventoryDigitization = "inventory_digitization";
	public const string WorkflowPolicyManagement = "workflow_policy_management";
	public const string WorkflowPolicyOptimization = "workflow_policy_optimization";
	public const string ThermalOperations = "thermal_operations";
}

public enum ResearchState
{
	Locked,
	Available,
	InProgress,
	Completed,
}

public enum ResearchStartFailureReason
{
	None,
	ServiceUnavailable,
	UnknownResearch,
	AlreadyResearched,
	ResearchInProgress,
	MissingPrerequisite,
	InsufficientFunds,
}

public sealed partial class ResearchService
{
	private readonly HashSet<string> researchedIds = new(StringComparer.Ordinal);

	private ResearchCatalog catalog;
	private EconomyService economyService;
	private GameTime gameTime;
	private string activeResearchId;
	private int remainingWeeks;
	private bool weekEventBound;

	public event Action<string> OnResearchCompleted;
	public event Action OnResearchStateChanged;

	public ResearchCatalog Catalog => catalog;
	public IReadOnlyList<ResearchDefinition> Definitions => catalog != null
		? catalog.Definitions
		: Array.Empty<ResearchDefinition>();
	public IReadOnlyCollection<string> ResearchedIds => researchedIds;
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
		catalog = researchCatalog;
		economyService = economy;
		gameTime = time;

		if (IsResearching)
			BindWeekEvent();
	}

	public void Unbind()
	{
		UnbindWeekEvent();
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

		return CanStartResearch(researchId, out _) ? ResearchState.Available : ResearchState.Locked;
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

		foreach (string prerequisiteUid in definition.PrerequisiteUids)
		{
			if (IsResearched(prerequisiteUid) == false)
			{
				reason = ResearchStartFailureReason.MissingPrerequisite;
				return false;
			}
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

		ResearchDefinition definition = catalog.TryGet(researchId, out ResearchDefinition found)
			? found
			: null;
		if (definition == null)
		{
			reason = ResearchStartFailureReason.UnknownResearch;
			return false;
		}

		economyService.ApplyTransaction(new EconomyTransaction
		{
			moneyDelta = -definition.Cost,
			reputationDelta = 0,
			reason = EconomyTransaction.Reason.ResearchInvestment,
		});

		activeResearchId = researchId;
		remainingWeeks = definition.DurationWeeks;
		BindWeekEvent();
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

		OnResearchCompleted?.Invoke(researchId);
		OnResearchStateChanged?.Invoke();
		return true;
	}

	public void ResetRuntimeState()
	{
		UnbindWeekEvent();
		researchedIds.Clear();
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
}
