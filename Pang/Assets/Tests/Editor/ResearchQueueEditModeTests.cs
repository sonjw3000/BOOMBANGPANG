using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ResearchQueueEditModeTests
{
	private GameContext previousContext;
	private GameObject economyObject;
	private GameObject timeObject;
	private EconomyService economyService;
	private GameTime gameTime;
	private ResearchCatalog catalog;
	private ResearchService researchService;

	[SetUp]
	public void SetUp()
	{
		previousContext = GetCurrentGameContext();
		SetCurrentGameContext(null);

		catalog = AssetDatabase.LoadAssetAtPath<ResearchCatalog>(
			"Assets/ScriptableObjs/ResearchCatalog.asset");
		Assert.That(catalog, Is.Not.Null);
		Assert.That(catalog.ValidateKeys(), Is.True);

		economyObject = new GameObject("Research Queue Test Economy");
		timeObject = new GameObject("Research Queue Test Time");
		economyObject.SetActive(false);
		timeObject.SetActive(false);
		economyService = economyObject.AddComponent<EconomyService>();
		gameTime = timeObject.AddComponent<GameTime>();
		SetMoney(1000);

		researchService = new ResearchService();
		researchService.Initialize(catalog, economyService, gameTime);
	}

	[TearDown]
	public void TearDown()
	{
		researchService?.Unbind();
		if (economyObject != null)
			Object.DestroyImmediate(economyObject);
		if (timeObject != null)
			Object.DestroyImmediate(timeObject);

		SetCurrentGameContext(previousContext);
	}

	[Test]
	public void TryEnqueueResearch_MissingPrerequisite_IsRejected()
	{
		bool enqueued = researchService.TryEnqueueResearch(
			ResearchIds.NavigationNetwork,
			out ResearchStartFailureReason reason);

		Assert.That(enqueued, Is.False);
		Assert.That(reason, Is.EqualTo(ResearchStartFailureReason.MissingPrerequisite));
		Assert.That(researchService.QueuedResearchCount, Is.Zero);
		Assert.That(researchService.IsResearching, Is.False);
	}

	[Test]
	public void TryEnqueueResearch_PlannedPrerequisites_EnableDependentQueue()
	{
		AssertEnqueued(ResearchIds.InventoryDigitization);
		AssertEnqueued(ResearchIds.RoboticWorkforce);
		AssertEnqueued(ResearchIds.NavigationNetwork);

		Assert.That(researchService.ActiveResearchId, Is.EqualTo(ResearchIds.InventoryDigitization));
		Assert.That(
			researchService.QueuedResearchIds,
			Is.EqualTo(new[] { ResearchIds.RoboticWorkforce, ResearchIds.NavigationNetwork }));
		Assert.That(
			researchService.GetState(ResearchIds.NavigationNetwork),
			Is.EqualTo(ResearchState.Queued));
	}

	[Test]
	public void TryEnqueueResearch_WhenIdle_StartsImmediatelyAndChargesOnce()
	{
		ResearchDefinition definition = GetDefinition(ResearchIds.TemperatureMonitoring);

		AssertEnqueued(definition.Uid);

		Assert.That(researchService.ActiveResearchId, Is.EqualTo(definition.Uid));
		Assert.That(researchService.RemainingWeeks, Is.EqualTo(definition.DurationWeeks));
		Assert.That(researchService.QueuedResearchCount, Is.Zero);
		Assert.That(economyService.Money, Is.EqualTo(1000 - definition.Cost));
		Assert.That(economyService.History, Has.Count.EqualTo(1));
		Assert.That(
			economyService.History[0].reason,
			Is.EqualTo(EconomyTransaction.Reason.ResearchInvestment));
	}

	[Test]
	public void OnWeekPassed_CompletesCurrentAndStartsNextWithoutConsumingExtraWeek()
	{
		ResearchDefinition monitoring = GetDefinition(ResearchIds.TemperatureMonitoring);
		ResearchDefinition thermal = GetDefinition(ResearchIds.ThermalOperations);
		AssertEnqueued(monitoring.Uid);
		AssertEnqueued(thermal.Uid);

		PassWeek();

		Assert.That(researchService.IsResearched(monitoring.Uid), Is.True);
		Assert.That(researchService.ActiveResearchId, Is.EqualTo(thermal.Uid));
		Assert.That(researchService.RemainingWeeks, Is.EqualTo(thermal.DurationWeeks));
		Assert.That(economyService.Money, Is.EqualTo(1000 - monitoring.Cost - thermal.Cost));

		PassWeek();
		Assert.That(researchService.RemainingWeeks, Is.EqualTo(thermal.DurationWeeks - 1));
	}

	[Test]
	public void QueuedResearch_InsufficientFunds_PausesThenStartsOnMoneyChange()
	{
		ResearchDefinition definition = GetDefinition(ResearchIds.TemperatureMonitoring);
		SetMoney(definition.Cost - 1);

		AssertEnqueued(definition.Uid);

		Assert.That(researchService.IsResearching, Is.False);
		Assert.That(researchService.QueuedResearchIds, Is.EqualTo(new[] { definition.Uid }));
		Assert.That(researchService.TryGetQueueBlockReason(out ResearchStartFailureReason reason), Is.True);
		Assert.That(reason, Is.EqualTo(ResearchStartFailureReason.InsufficientFunds));

		economyService.ApplyTransaction(new EconomyTransaction
		{
			moneyDelta = 1,
			reason = EconomyTransaction.Reason.DebugAdjustment,
		});

		Assert.That(researchService.ActiveResearchId, Is.EqualTo(definition.Uid));
		Assert.That(researchService.QueuedResearchCount, Is.Zero);
		Assert.That(economyService.Money, Is.Zero);
		Assert.That(economyService.History, Has.Count.EqualTo(2));
		Assert.That(
			economyService.History[1].reason,
			Is.EqualTo(EconomyTransaction.Reason.ResearchInvestment));
	}

	[Test]
	public void QueueEdit_ThatBreaksPrerequisiteOrder_IsRejectedWithoutMutation()
	{
		AssertEnqueued(ResearchIds.InventoryDigitization);
		AssertEnqueued(ResearchIds.RoboticWorkforce);
		AssertEnqueued(ResearchIds.NavigationNetwork);
		AssertEnqueued(ResearchIds.TrafficControl);

		bool moved = researchService.TryMoveQueuedResearch(
			ResearchIds.NavigationNetwork,
			0,
			out ResearchStartFailureReason moveReason);
		Assert.That(moved, Is.False);
		Assert.That(moveReason, Is.EqualTo(ResearchStartFailureReason.InvalidQueueOrder));
		Assert.That(
			researchService.QueuedResearchIds,
			Is.EqualTo(new[]
			{
				ResearchIds.RoboticWorkforce,
				ResearchIds.NavigationNetwork,
				ResearchIds.TrafficControl,
			}));

		bool removed = researchService.TryRemoveQueuedResearch(
			ResearchIds.RoboticWorkforce,
			out ResearchStartFailureReason removeReason);
		Assert.That(removed, Is.False);
		Assert.That(removeReason, Is.EqualTo(ResearchStartFailureReason.InvalidQueueOrder));
		Assert.That(researchService.GetQueueIndex(ResearchIds.RoboticWorkforce), Is.Zero);
	}

	[Test]
	public void CaptureAndRestore_PreservesActiveProgressAndQueueWithoutChargingAgain()
	{
		AssertEnqueued(ResearchIds.InventoryDigitization);
		AssertEnqueued(ResearchIds.RoboticWorkforce);
		AssertEnqueued(ResearchIds.NavigationNetwork);
		ResearchServiceSaveData saveData = researchService.CaptureState();
		int moneyBeforeRestore = economyService.Money;

		researchService.Unbind();
		researchService = new ResearchService();
		researchService.Initialize(catalog, economyService, gameTime);
		researchService.RestoreState(saveData);

		Assert.That(researchService.ActiveResearchId, Is.EqualTo(ResearchIds.InventoryDigitization));
		Assert.That(researchService.RemainingWeeks, Is.EqualTo(1));
		Assert.That(
			researchService.QueuedResearchIds,
			Is.EqualTo(new[] { ResearchIds.RoboticWorkforce, ResearchIds.NavigationNetwork }));
		Assert.That(economyService.Money, Is.EqualTo(moneyBeforeRestore));
		Assert.That(economyService.History, Has.Count.EqualTo(1));
	}

	[Test]
	public void RestoreState_OldJsonWithoutQueueField_UsesEmptyQueue()
	{
		ResearchServiceSaveData oldSave = JsonUtility.FromJson<ResearchServiceSaveData>(
			"{\"ResearchedIds\":[],\"ActiveResearchId\":\"\",\"RemainingWeeks\":0}");

		researchService.RestoreState(oldSave);

		Assert.That(researchService.IsResearching, Is.False);
		Assert.That(researchService.QueuedResearchCount, Is.Zero);
	}

	[Test]
	public void RestoreState_InvalidQueueEntries_AreFilteredWithoutBreakingLaterValidOrder()
	{
		ResearchServiceSaveData saveData = new()
		{
			ResearchedIds = new() { ResearchIds.InventoryDigitization },
			ActiveResearchId = ResearchIds.RoboticWorkforce,
			RemainingWeeks = 1,
			QueuedResearchIds = new()
			{
				"unknown_research",
				ResearchIds.RoboticWorkforce,
				ResearchIds.TrafficControl,
				ResearchIds.NavigationNetwork,
				ResearchIds.TrafficControl,
				ResearchIds.TrafficControl,
				ResearchIds.InventoryDigitization,
			},
		};

		researchService.RestoreState(saveData);

		Assert.That(researchService.ActiveResearchId, Is.EqualTo(ResearchIds.RoboticWorkforce));
		Assert.That(
			researchService.QueuedResearchIds,
			Is.EqualTo(new[] { ResearchIds.NavigationNetwork, ResearchIds.TrafficControl }));
	}

	[Test]
	public void TryEnqueueResearch_DoesNotCompleteOrPublishCompletionEvent()
	{
		int completionCount = 0;
		researchService.OnResearchCompleted += _ => ++completionCount;

		AssertEnqueued(ResearchIds.TemperatureMonitoring);

		Assert.That(researchService.IsResearched(ResearchIds.TemperatureMonitoring), Is.False);
		Assert.That(completionCount, Is.Zero);
	}

	[Test]
	public void InitializeTwice_MoneyChangeStartsQueuedResearchOnlyOnce()
	{
		ResearchDefinition definition = GetDefinition(ResearchIds.TemperatureMonitoring);
		SetMoney(definition.Cost - 1);
		AssertEnqueued(definition.Uid);
		researchService.Initialize(catalog, economyService, gameTime);

		economyService.ApplyTransaction(new EconomyTransaction
		{
			moneyDelta = 1,
			reason = EconomyTransaction.Reason.DebugAdjustment,
		});

		Assert.That(researchService.ActiveResearchId, Is.EqualTo(definition.Uid));
		Assert.That(economyService.History, Has.Count.EqualTo(2));
		Assert.That(economyService.Money, Is.Zero);
	}

	private void AssertEnqueued(string researchId)
	{
		Assert.That(
			researchService.TryEnqueueResearch(researchId, out ResearchStartFailureReason reason),
			Is.True,
			$"Failed to enqueue {researchId}: {reason}");
	}

	private ResearchDefinition GetDefinition(string researchId)
	{
		Assert.That(catalog.TryGet(researchId, out ResearchDefinition definition), Is.True);
		return definition;
	}

	private void SetMoney(int money)
	{
		economyService.RestoreState(new EconomySaveData
		{
			Money = money,
			Reputation = 0f,
		});
	}

	private void PassWeek()
	{
		MethodInfo method = typeof(GameTime).GetMethod(
			"PassWeek",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null);
		method.Invoke(gameTime, null);
	}

	private static GameContext GetCurrentGameContext()
	{
		FieldInfo field = typeof(GameContext).GetField(
			"instance",
			BindingFlags.Static | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null);
		return (GameContext)field.GetValue(null);
	}

	private static void SetCurrentGameContext(GameContext context)
	{
		FieldInfo field = typeof(GameContext).GetField(
			"instance",
			BindingFlags.Static | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null);
		field.SetValue(null, context);
	}
}
