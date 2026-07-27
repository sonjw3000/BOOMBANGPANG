using System;
using System.Collections.Generic;
using Assets.Scripts.Contract.ItemContract;
using Assets.Scripts.UI;
using UnityEngine;

public sealed class ScenarioObjectiveService : MonoBehaviour
{
	[SerializeField] private bool scenarioEnabled = true;
	[SerializeField] private ScenarioDefinition activeScenario;
	[SerializeField] private string clearNoticeTitle = "Game Clear";
	[SerializeField] private string clearNoticeMessage = "Scenario complete.";

	private EconomyService economyService;
	private ResearchService researchService;
	private OrderManager orderManager;
	private EventNoticeService eventNoticeService;
	private int currentObjectiveIndex;
	private int currentSettledOrderCount;
	private bool isCleared;
	private bool isRestoring;
	private bool started;

	public event Action OnObjectiveChanged;
	public event Action OnGoalCleared;

	public ScenarioDefinition ActiveScenario => activeScenario;
	public ScenarioObjectiveDefinition CurrentObjective => GetObjective(currentObjectiveIndex);
	public int CurrentSettledOrderCount => currentSettledOrderCount;
	public bool IsCleared => isCleared;
	public bool IsEnabled => scenarioEnabled && activeScenario != null;

	private void OnEnable()
	{
		SubscribeServices();
	}

	private void Start()
	{
		started = true;
		SubscribeServices();
		if (ValidateScenario() == false)
			return;

		if (isCleared == false)
		{
			ShowCurrentObjectiveNotice();
			EvaluateCurrentObjective();
		}
	}

	private void OnDisable()
	{
		UnsubscribeServices();
	}

	public ScenarioObjectiveSaveData CaptureState()
	{
		return new ScenarioObjectiveSaveData
		{
			ScenarioId = activeScenario != null ? activeScenario.ScenarioId : string.Empty,
			CurrentObjectiveId = CurrentObjective != null ? CurrentObjective.ObjectiveId : string.Empty,
			CurrentSettledOrderCount = currentSettledOrderCount,
			IsCleared = isCleared,
		};
	}

	public void ResetRuntimeState()
	{
		isRestoring = true;
		currentObjectiveIndex = 0;
		currentSettledOrderCount = 0;
		isCleared = false;
	}

	public void RestoreState(ScenarioObjectiveSaveData data)
	{
		currentObjectiveIndex = 0;
		currentSettledOrderCount = 0;
		isCleared = false;

		if (activeScenario == null || scenarioEnabled == false)
		{
			isRestoring = false;
			OnObjectiveChanged?.Invoke();
			return;
		}

		if (data != null &&
			string.IsNullOrWhiteSpace(data.ScenarioId) == false &&
			data.ScenarioId == activeScenario.ScenarioId)
		{
			currentSettledOrderCount = Mathf.Max(0, data.CurrentSettledOrderCount);
			isCleared = data.IsCleared;
			currentObjectiveIndex = isCleared
				? activeScenario.Objectives.Count
				: FindObjectiveIndex(data.CurrentObjectiveId);
		}

		isRestoring = false;
		OnObjectiveChanged?.Invoke();
		EvaluateCurrentObjective(showNotice: false);
	}

	public string GetProgressText()
	{
		if (isCleared)
			return "Complete";

		ScenarioObjectiveDefinition objective = CurrentObjective;
		if (objective == null)
			return string.Empty;

		List<string> parts = new();
		if (objective.HasOrderRequirement)
		{
			int current = Mathf.Min(currentSettledOrderCount, objective.RequiredSettledOrderCount);
			parts.Add($"{current} / {objective.RequiredSettledOrderCount} orders");
		}

		IReadOnlyList<string> researchUids = objective.RequiredResearchUids;
		if (researchUids != null && researchUids.Count > 0)
		{
			int researchedCount = 0;
			for (int i = 0; i < researchUids.Count; ++i)
			{
				if (researchService?.IsResearched(researchUids[i]) == true)
					++researchedCount;
			}
			parts.Add($"{researchedCount} / {researchUids.Count} research");
		}

		if (objective.RequireMinimumReputation)
		{
			float reputation = economyService != null ? economyService.Reputation : 0.0f;
			parts.Add($"{reputation:F1} / {objective.MinimumReputation:F1} reputation");
		}

		return string.Join("  |  ", parts);
	}

	private void SubscribeServices()
	{
		if (GameContext.HasInstance == false)
			return;

		EconomyService nextEconomy = GameContext.Instance.EconomyService;
		if (economyService != nextEconomy)
		{
			if (economyService != null)
				economyService.OnReputationChanged -= OnReputationChanged;
			economyService = nextEconomy;
			if (economyService != null)
				economyService.OnReputationChanged += OnReputationChanged;
		}

		ResearchService nextResearch = GameContext.Instance.ResearchService;
		if (researchService != nextResearch)
		{
			if (researchService != null)
				researchService.OnResearchCompleted -= OnResearchCompleted;
			researchService = nextResearch;
			if (researchService != null)
				researchService.OnResearchCompleted += OnResearchCompleted;
		}

		OrderManager nextOrderManager = GameContext.Instance.OrderMgr;
		if (orderManager != nextOrderManager)
		{
			if (orderManager != null)
				orderManager.OnOrderSettled -= OnOrderSettled;
			orderManager = nextOrderManager;
			if (orderManager != null)
				orderManager.OnOrderSettled += OnOrderSettled;
		}
	}

	private void UnsubscribeServices()
	{
		if (economyService != null)
			economyService.OnReputationChanged -= OnReputationChanged;
		if (researchService != null)
			researchService.OnResearchCompleted -= OnResearchCompleted;
		if (orderManager != null)
			orderManager.OnOrderSettled -= OnOrderSettled;

		economyService = null;
		researchService = null;
		orderManager = null;
	}

	private void OnReputationChanged(float _)
	{
		OnObjectiveChanged?.Invoke();
		EvaluateCurrentObjective();
	}

	private void OnResearchCompleted(string _)
	{
		OnObjectiveChanged?.Invoke();
		EvaluateCurrentObjective();
	}

	private void OnOrderSettled(Order order)
	{
		if (CanProcessEvents() == false)
			return;

		ScenarioObjectiveDefinition objective = CurrentObjective;
		if (objective == null ||
			objective.HasOrderRequirement == false ||
			MatchesContract(order, objective.TargetContract) == false ||
			(objective.RequireOnTime && IsOrderOnTime(order) == false))
		{
			return;
		}

		++currentSettledOrderCount;
		OnObjectiveChanged?.Invoke();
		EvaluateCurrentObjective();
	}

	private void EvaluateCurrentObjective(bool showNotice = true)
	{
		if (CanProcessEvents() == false)
			return;

		while (IsCurrentObjectiveComplete())
		{
			ScenarioObjectiveDefinition completed = CurrentObjective;
			Debug.Log($"[Scenario] Objective completed: {completed.ObjectiveId}");
			++currentObjectiveIndex;
			currentSettledOrderCount = 0;

			if (currentObjectiveIndex >= activeScenario.Objectives.Count)
			{
				isCleared = true;
				if (showNotice)
					ShowNotice(clearNoticeTitle, clearNoticeMessage);
				Debug.Log($"[Scenario] Scenario cleared: {activeScenario.ScenarioId}");
				OnObjectiveChanged?.Invoke();
				OnGoalCleared?.Invoke();
				return;
			}

			OnObjectiveChanged?.Invoke();
			if (showNotice)
				ShowCurrentObjectiveNotice();
		}
	}

	private bool IsCurrentObjectiveComplete()
	{
		ScenarioObjectiveDefinition objective = CurrentObjective;
		if (objective == null)
			return false;

		if (objective.HasOrderRequirement &&
			currentSettledOrderCount < objective.RequiredSettledOrderCount)
		{
			return false;
		}

		IReadOnlyList<string> researchUids = objective.RequiredResearchUids;
		if (researchUids != null)
		{
			for (int i = 0; i < researchUids.Count; ++i)
			{
				if (researchService?.IsResearched(researchUids[i]) != true)
					return false;
			}
		}

		if (objective.RequireMinimumReputation &&
			(economyService == null || economyService.Reputation < objective.MinimumReputation))
		{
			return false;
		}

		return true;
	}

	private bool CanProcessEvents()
	{
		return started &&
			isRestoring == false &&
			isCleared == false &&
			scenarioEnabled &&
			activeScenario != null;
	}

	private bool ValidateScenario()
	{
		if (scenarioEnabled == false)
			return false;

		if (activeScenario == null)
		{
			Debug.LogWarning("[Scenario] Active scenario is missing.", this);
			return false;
		}

		if (activeScenario.Validate(out string error))
			return true;

		Debug.LogError($"[Scenario] Invalid scenario '{activeScenario.name}': {error}", activeScenario);
		return false;
	}

	private ScenarioObjectiveDefinition GetObjective(int index)
	{
		if (activeScenario == null || index < 0 || index >= activeScenario.Objectives.Count)
			return null;

		return activeScenario.Objectives[index];
	}

	private int FindObjectiveIndex(string objectiveId)
	{
		if (activeScenario == null || string.IsNullOrWhiteSpace(objectiveId))
			return 0;

		for (int i = 0; i < activeScenario.Objectives.Count; ++i)
		{
			if (activeScenario.Objectives[i].ObjectiveId == objectiveId)
				return i;
		}

		Debug.LogWarning($"[Scenario] Saved objective '{objectiveId}' was not found. Starting from the first objective.", this);
		return 0;
	}

	private static bool MatchesContract(Order order, ContractDefinition targetContract)
	{
		if (targetContract == null)
			return order != null;
		if (order?.Lines == null)
			return false;

		for (int i = 0; i < order.Lines.Count; ++i)
		{
			OrderLine line = order.Lines[i];
			if (line?.SourceContract?.Definition == targetContract)
				return true;
		}

		return false;
	}

	private static bool IsOrderOnTime(Order order)
	{
		if (order?.Lines == null || GameContext.HasInstance == false)
			return false;

		int currentWeek = GameContext.Instance.GameTime.WeeksPassed;
		bool hasCompletedLine = false;
		for (int i = 0; i < order.Lines.Count; ++i)
		{
			OrderLine line = order.Lines[i];
			if (line == null || line.Status == OrderStatus.Cancelled)
				continue;

			hasCompletedLine = true;
			if (currentWeek > line.DueWeek)
				return false;
		}

		return hasCompletedLine;
	}

	private void ShowCurrentObjectiveNotice()
	{
		ScenarioObjectiveDefinition objective = CurrentObjective;
		if (objective != null)
			ShowNotice(objective.Title, objective.Description);
	}

	private void ShowNotice(string title, string message)
	{
		EventNoticeService service = GetEventNoticeService();
		if (service == null)
		{
			Debug.LogWarning($"[Scenario] EventNoticeService is missing. Notice skipped: {title} - {message}");
			return;
		}

		service.ShowNotice(new EventNoticeRequest(title, message));
	}

	private EventNoticeService GetEventNoticeService()
	{
		if (eventNoticeService == null)
			eventNoticeService = FindAnyObjectByType<EventNoticeService>(FindObjectsInactive.Include);

		return eventNoticeService;
	}
}
