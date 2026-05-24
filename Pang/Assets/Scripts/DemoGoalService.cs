using System;
using Assets.Scripts.UI;
using UnityEngine;

public sealed class DemoGoalService : MonoBehaviour
{
	[SerializeField] private bool goalEnabled = true;
	[SerializeField, Min(0f)] private float targetReputation = 50f;
	[SerializeField] private string startNoticeTitle = "Demo Goal";
	[SerializeField] private string startNoticeMessage = "Your Goal Is To Reach 50 Rep";
	[SerializeField] private string clearNoticeTitle = "Game Clear";
	[SerializeField] private string clearNoticeMessage = "Goal Complete! You Reached 50 Rep";

	private EconomyService economyService;
	private EventNoticeService eventNoticeService;
	private bool startNoticeShown;
	private bool isCleared;

	public event Action OnGoalCleared;

	public bool IsCleared => isCleared;
	public float TargetReputation => targetReputation;

	private void OnEnable()
	{
		SubscribeEconomy();
	}

	private void Start()
	{
		if (goalEnabled == false)
			return;

		ShowStartNotice();
		CheckReputationGoal(GetCurrentReputation());
	}

	private void OnDisable()
	{
		UnsubscribeEconomy();
	}

	private void SubscribeEconomy()
	{
		if (economyService != null)
			return;

		if (GameContext.HasInstance)
			economyService = GameContext.Instance.EconomyService;

		if (economyService == null)
			economyService = FindFirstObjectByType<EconomyService>();

		if (economyService != null)
			economyService.OnReputationChanged += OnReputationChanged;
	}

	private void UnsubscribeEconomy()
	{
		if (economyService == null)
			return;

		economyService.OnReputationChanged -= OnReputationChanged;
		economyService = null;
	}

	private void OnReputationChanged(float currentReputation)
	{
		if (goalEnabled == false)
			return;

		CheckReputationGoal(currentReputation);
	}

	private float GetCurrentReputation()
	{
		if (economyService == null)
			SubscribeEconomy();

		return economyService != null ? economyService.Reputation : 0f;
	}

	private void CheckReputationGoal(float currentReputation)
	{
		if (isCleared || currentReputation < targetReputation)
			return;

		isCleared = true;
		ShowClearNotice();
		Debug.Log($"[DemoGoal] Game cleared by reaching reputation goal. Current: {currentReputation:F1}, Target: {targetReputation:F1}");
		OnGoalCleared?.Invoke();
	}

	private void ShowStartNotice()
	{
		if (startNoticeShown)
			return;

		startNoticeShown = true;
		ShowNotice(startNoticeTitle, startNoticeMessage);
	}

	private void ShowClearNotice()
	{
		ShowNotice(clearNoticeTitle, clearNoticeMessage);
	}

	private void ShowNotice(string title, string message)
	{
		EventNoticeService service = GetEventNoticeService();
		if (service == null)
		{
			Debug.LogWarning($"[DemoGoal] EventNoticeService is missing. Notice skipped: {title} - {message}");
			return;
		}

		service.ShowNotice(new EventNoticeRequest(title, message));
	}

	private EventNoticeService GetEventNoticeService()
	{
		if (eventNoticeService == null)
			eventNoticeService = FindFirstObjectByType<EventNoticeService>(FindObjectsInactive.Include);

		return eventNoticeService;
	}
}
