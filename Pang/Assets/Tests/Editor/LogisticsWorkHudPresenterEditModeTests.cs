using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UniverseLogistics.UI.Toolkit;

public sealed class LogisticsWorkHudPresenterEditModeTests
{
	private const string HudAssetPath = "Assets/UI/Toolkit/GlobalStatusHud.uxml";
	private readonly List<GameObject> createdObjects = new();

	[TearDown]
	public void TearDown()
	{
		for (int i = createdObjects.Count - 1; i >= 0; --i)
		{
			if (createdObjects[i] != null)
				Object.DestroyImmediate(createdObjects[i]);
		}

		createdObjects.Clear();
	}

	[Test]
	public void GlobalHudTemplate_ContainsFourBindableLogisticsRows()
	{
		TemplateContainer root = LoadRoot();
		LogisticsWorkHudPresenter presenter = new();

		Assert.That(presenter.BindView(root), Is.True);
		Assert.That(root.Q<Button>("logistics-work-hud-header"), Is.Not.Null);
		Assert.That(root.Q<Button>("logistics-work-hud-bottleneck"), Is.Not.Null);
		Assert.That(root.Q<Label>("logistics-work-hud-bottleneck-building"), Is.Not.Null);
		Assert.That(root.Q<Label>("logistics-work-hud-bottleneck-status"), Is.Not.Null);

		string[] prefixes =
		{
			"picking",
			"storing",
			"packing",
			"capsule-relocate",
		};

		for (int i = 0; i < prefixes.Length; ++i)
		{
			string prefix = $"logistics-work-hud-{prefixes[i]}";
			Assert.That(root.Q<VisualElement>($"{prefix}-row"), Is.Not.Null, prefix);
			Assert.That(root.Q<Label>($"{prefix}-demand"), Is.Not.Null, prefix);
			Assert.That(root.Q<Label>($"{prefix}-waiting"), Is.Not.Null, prefix);
			Assert.That(root.Q<Label>($"{prefix}-active"), Is.Not.Null, prefix);
			Assert.That(root.Q<Label>($"{prefix}-blocked"), Is.Not.Null, prefix);
		}

		presenter.Dispose();
	}

	[Test]
	public void RenderBuildingBottleneck_UsesExplicitRiskTiersAndStableTieBreaks()
	{
		TemplateContainer root = LoadRoot();
		LogisticsWorkHudPresenter presenter = new();
		Assert.That(presenter.BindView(root), Is.True);
		Building first = new("First Storage", new List<GridCell>());
		Building second = new("Second Storage", new List<GridCell>());
		Building third = new("Third Storage", new List<GridCell>());
		BuildingManager manager = CreateBuildingManager(first, second, third);
		Dictionary<(LogisticsWorkCategory, uint), WorkDemandSnapshot> demands = new();
		Dictionary<(LogisticsWorkCategory, uint), TaskCountSnapshot> tasks = new();

		WorkDemandSnapshot ResolveDemand(LogisticsWorkCategory category, uint buildingId) =>
			demands.TryGetValue((category, buildingId), out WorkDemandSnapshot value) ? value : default;
		TaskCountSnapshot ResolveTasks(LogisticsWorkCategory category, uint buildingId) =>
			tasks.TryGetValue((category, buildingId), out TaskCountSnapshot value) ? value : default;

		tasks[(LogisticsWorkCategory.Picking, first.RuntimeBuildingId)] = new TaskCountSnapshot(1, 0, 2, 2);
		tasks[(LogisticsWorkCategory.PackingInput, second.RuntimeBuildingId)] = new TaskCountSnapshot(2, 0, 1, 1);
		tasks[(LogisticsWorkCategory.PackingOutput, second.RuntimeBuildingId)] = new TaskCountSnapshot(2, 0, 1, 1);
		tasks[(LogisticsWorkCategory.Picking, third.RuntimeBuildingId)] = new TaskCountSnapshot(0, 9, 0, 0);
		presenter.RenderBuildingBottleneck(manager.RegisteredBuildings, ResolveDemand, ResolveTasks);
		AssertBottleneck(root, $"Second Storage · #{second.RuntimeBuildingId}", "2 BLOCK", danger: true);

		tasks.Clear();
		demands.Clear();
		tasks[(LogisticsWorkCategory.Picking, first.RuntimeBuildingId)] = new TaskCountSnapshot(0, 2, 0, 0);
		demands[(LogisticsWorkCategory.Storing, second.RuntimeBuildingId)] = new WorkDemandSnapshot(50, 500);
		tasks[(LogisticsWorkCategory.Picking, third.RuntimeBuildingId)] = new TaskCountSnapshot(100, 0, 0, 0);
		presenter.RenderBuildingBottleneck(manager.RegisteredBuildings, ResolveDemand, ResolveTasks);
		AssertBottleneck(root, $"First Storage · #{first.RuntimeBuildingId}", "2 RETURN", warning: true);

		tasks.Clear();
		demands.Clear();
		demands[(LogisticsWorkCategory.Storing, second.RuntimeBuildingId)] = new WorkDemandSnapshot(5, 500);
		tasks[(LogisticsWorkCategory.Picking, third.RuntimeBuildingId)] = new TaskCountSnapshot(100, 0, 0, 0);
		presenter.RenderBuildingBottleneck(manager.RegisteredBuildings, ResolveDemand, ResolveTasks);
		AssertBottleneck(root, $"Second Storage · #{second.RuntimeBuildingId}", "5 NEED", warning: true);

		tasks.Clear();
		demands.Clear();
		tasks[(LogisticsWorkCategory.Picking, first.RuntimeBuildingId)] = new TaskCountSnapshot(100, 0, 0, 0);
		tasks[(LogisticsWorkCategory.Picking, third.RuntimeBuildingId)] = new TaskCountSnapshot(100, 0, 0, 0);
		presenter.RenderBuildingBottleneck(manager.RegisteredBuildings, ResolveDemand, ResolveTasks);
		AssertBottleneck(root, $"First Storage · #{first.RuntimeBuildingId}", "100 WAIT");

		tasks.Clear();
		tasks[(LogisticsWorkCategory.Picking, first.RuntimeBuildingId)] = new TaskCountSnapshot(0, 0, 10, 0);
		presenter.RenderBuildingBottleneck(manager.RegisteredBuildings, ResolveDemand, ResolveTasks);
		Button bottleneck = root.Q<Button>("logistics-work-hud-bottleneck");
		Assert.That(bottleneck.ClassListContains("logistics-work-hud__bottleneck--hidden"), Is.True);
		Assert.That(bottleneck.enabledSelf, Is.False);

		presenter.Dispose();
	}

	[Test]
	public void RenderBuildingBottleneck_KeepsUnservedDemandSeparatePerCategory()
	{
		TemplateContainer root = LoadRoot();
		LogisticsWorkHudPresenter presenter = new();
		Assert.That(presenter.BindView(root), Is.True);
		Building building = new("Mixed Work Storage", new List<GridCell>());
		BuildingManager manager = CreateBuildingManager(building);

		presenter.RenderBuildingBottleneck(
			manager.RegisteredBuildings,
			(category, buildingId) =>
				category == LogisticsWorkCategory.Storing && buildingId == building.RuntimeBuildingId
					? new WorkDemandSnapshot(5, 500)
					: default,
			(category, buildingId) =>
				category == LogisticsWorkCategory.Picking && buildingId == building.RuntimeBuildingId
					? new TaskCountSnapshot(1, 0, 1, 0)
					: default);

		AssertBottleneck(
			root,
			$"Mixed Work Storage · #{building.RuntimeBuildingId}",
			"5 NEED",
			warning: true);

		presenter.Dispose();
	}

	[Test]
	public void Render_AggregatesPackingPerDimensionAndKeepsCountsSeparate()
	{
		TemplateContainer root = LoadRoot();
		LogisticsWorkHudPresenter presenter = new();
		Assert.That(presenter.BindView(root), Is.True);

		Dictionary<LogisticsWorkCategory, WorkDemandSnapshot> demands = CreateEmptyDemandMap();
		demands[LogisticsWorkCategory.Picking] = new WorkDemandSnapshot(2, 101);
		demands[LogisticsWorkCategory.Storing] = new WorkDemandSnapshot(3, 103);
		demands[LogisticsWorkCategory.PackingInput] = new WorkDemandSnapshot(2, 107);
		demands[LogisticsWorkCategory.Packing] = new WorkDemandSnapshot(5, 109);
		demands[LogisticsWorkCategory.PackingOutput] = new WorkDemandSnapshot(7, 113);
		demands[LogisticsWorkCategory.CapsuleRelocate] = new WorkDemandSnapshot(11, 0);

		Dictionary<LogisticsWorkCategory, TaskCountSnapshot> tasks = CreateEmptyTaskMap();
		tasks[LogisticsWorkCategory.Picking] = new TaskCountSnapshot(3, 5, 7, 2);
		tasks[LogisticsWorkCategory.PackingInput] = new TaskCountSnapshot(1, 2, 3, 1);
		tasks[LogisticsWorkCategory.Packing] = new TaskCountSnapshot(4, 5, 6, 2);
		tasks[LogisticsWorkCategory.PackingOutput] = new TaskCountSnapshot(8, 9, 10, 3);
		tasks[LogisticsWorkCategory.CapsuleRelocate] = new TaskCountSnapshot(11, 13, 17, 0);

		presenter.Render(category => demands[category], category => tasks[category]);

		Assert.That(root.Q<Label>("logistics-work-hud-picking-demand").text, Is.EqualTo("2"));
		Assert.That(root.Q<Label>("logistics-work-hud-picking-waiting").text, Is.EqualTo("8"));
		Assert.That(root.Q<Label>("logistics-work-hud-picking-active").text, Is.EqualTo("7"));
		Assert.That(root.Q<Label>("logistics-work-hud-picking-blocked").text, Is.EqualTo("2"));
		Assert.That(root.Q<Label>("logistics-work-hud-packing-demand").text, Is.EqualTo("14"));
		Assert.That(root.Q<Label>("logistics-work-hud-packing-waiting").text, Is.EqualTo("29"));
		Assert.That(root.Q<Label>("logistics-work-hud-packing-active").text, Is.EqualTo("19"));
		Assert.That(root.Q<Label>("logistics-work-hud-packing-blocked").text, Is.EqualTo("6"));
		Assert.That(root.Q<Label>("logistics-work-hud-total-waiting").text, Is.EqualTo("61 WAIT"));
		Assert.That(root.Q<Label>("logistics-work-hud-total-active").text, Is.EqualTo("43 ACTIVE"));
		Assert.That(root.Q<Label>("logistics-work-hud-total-blocked").text, Is.EqualTo("8 BLOCK"));
		Assert.That(
			root.Q<VisualElement>("logistics-work-hud-packing-row")
				.ClassListContains("logistics-work-hud__row--blocked"),
			Is.True);
		Assert.That(
			root.Q<VisualElement>("logistics-work-hud-storing-row")
				.ClassListContains("logistics-work-hud__row--unserved"),
			Is.True);
		Assert.That(
			root.Q<Label>("logistics-work-hud-total-blocked")
				.ClassListContains("logistics-work-hud__total--blocked"),
			Is.True);

		Dictionary<LogisticsWorkCategory, WorkDemandSnapshot> emptyDemands = CreateEmptyDemandMap();
		Dictionary<LogisticsWorkCategory, TaskCountSnapshot> emptyTasks = CreateEmptyTaskMap();
		presenter.Render(category => emptyDemands[category], category => emptyTasks[category]);
		Assert.That(
			root.Q<VisualElement>("logistics-work-hud-packing-row")
				.ClassListContains("logistics-work-hud__row--blocked"),
			Is.False);
		Assert.That(
			root.Q<VisualElement>("logistics-work-hud-storing-row")
				.ClassListContains("logistics-work-hud__row--unserved"),
			Is.False);
		Assert.That(
			root.Q<Label>("logistics-work-hud-total-blocked")
				.ClassListContains("logistics-work-hud__total--blocked"),
			Is.False);

		presenter.Dispose();
	}

	[Test]
	public void NavigationSubmit_SeparatesAllAndBuildingMonitorAndUnbindsCleanly()
	{
		VisualElement root = CreateHudPanelRoot();
		LogisticsWorkHudPresenter presenter = new();
		Building building = new("Navigation Storage", new List<GridCell>());
		BuildingManager manager = CreateBuildingManager(building);
		int openAllCount = 0;
		int openBuildingCount = 0;
		uint openedBuildingId = 0;
		presenter.ConfigureNavigation(
			() => ++openAllCount,
			buildingId =>
			{
				openBuildingCount += 1;
				openedBuildingId = buildingId;
			});
		Assert.That(presenter.BindView(root), Is.True);
		RenderWaitingBottleneck(presenter, manager, building.RuntimeBuildingId);

		Button header = root.Q<Button>("logistics-work-hud-header");
		Button bottleneck = root.Q<Button>("logistics-work-hud-bottleneck");
		Submit(header);
		Submit(bottleneck);
		Assert.That(openAllCount, Is.EqualTo(1));
		Assert.That(openBuildingCount, Is.EqualTo(1));
		Assert.That(openedBuildingId, Is.EqualTo(building.RuntimeBuildingId));

		Assert.That(presenter.BindView(root), Is.True);
		RenderWaitingBottleneck(presenter, manager, building.RuntimeBuildingId);
		Submit(header);
		Submit(bottleneck);
		Assert.That(openAllCount, Is.EqualTo(2));
		Assert.That(openBuildingCount, Is.EqualTo(2));

		presenter.UnbindView();
		Submit(header);
		Submit(bottleneck);
		Assert.That(openAllCount, Is.EqualTo(2));
		Assert.That(openBuildingCount, Is.EqualTo(2));

		presenter.Dispose();
	}

	private static Dictionary<LogisticsWorkCategory, WorkDemandSnapshot> CreateEmptyDemandMap()
	{
		Dictionary<LogisticsWorkCategory, WorkDemandSnapshot> snapshots = new();
		foreach (LogisticsWorkCategory category in System.Enum.GetValues(typeof(LogisticsWorkCategory)))
			snapshots[category] = default;
		return snapshots;
	}

	private static Dictionary<LogisticsWorkCategory, TaskCountSnapshot> CreateEmptyTaskMap()
	{
		Dictionary<LogisticsWorkCategory, TaskCountSnapshot> snapshots = new();
		foreach (LogisticsWorkCategory category in System.Enum.GetValues(typeof(LogisticsWorkCategory)))
			snapshots[category] = default;
		return snapshots;
	}

	private static TemplateContainer LoadRoot()
	{
		VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudAssetPath);
		Assert.That(template, Is.Not.Null);
		return template.CloneTree();
	}

	private BuildingManager CreateBuildingManager(params Building[] buildings)
	{
		GameObject managerObject = new("Logistics Work HUD Test Building Manager");
		createdObjects.Add(managerObject);
		BuildingManager manager = managerObject.AddComponent<BuildingManager>();
		for (int i = 0; i < buildings.Length; ++i)
			manager.Register(buildings[i]);
		return manager;
	}

	[Test]
	public void Render_UnchangedValuesAllocateNothing_ChangedRowPreservesOtherText()
	{
		TemplateContainer root = LoadRoot();
		using LogisticsWorkHudPresenter presenter = new();
		Assert.That(presenter.BindView(root), Is.True);
		Building building = new("Stable Building", new List<GridCell>());
		BuildingManager manager = CreateBuildingManager(building);
		int pickingCount = 12;
		System.Func<LogisticsWorkCategory, WorkDemandSnapshot> demands = _ => default;
		System.Func<LogisticsWorkCategory, TaskCountSnapshot> tasks = category =>
			new TaskCountSnapshot(category == LogisticsWorkCategory.Picking ? pickingCount : 23, 0, 0, 0);
		System.Func<LogisticsWorkCategory, uint, WorkDemandSnapshot> buildingDemands = (_, _) => default;
		System.Func<LogisticsWorkCategory, uint, TaskCountSnapshot> buildingTasks = (category, _) => tasks(category);
		for (int i = 0; i < 10; ++i)
		{
			presenter.Render(demands, tasks);
			presenter.RenderBuildingBottleneck(manager.RegisteredBuildings, buildingDemands, buildingTasks);
		}
		long before = System.GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 100; ++i)
		{
			presenter.Render(demands, tasks);
			presenter.RenderBuildingBottleneck(manager.RegisteredBuildings, buildingDemands, buildingTasks);
		}
		long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.That(allocated, Is.Zero);

		Label storing = root.Q<Label>("logistics-work-hud-storing-waiting");
		string previousText = storing.text;
		pickingCount = 14;
		presenter.Render(demands, tasks);
		Assert.That(root.Q<Label>("logistics-work-hud-picking-waiting").text, Is.EqualTo("14"));
		Assert.That(storing.text, Is.SameAs(previousText));
	}

	private static void RenderWaitingBottleneck(
		LogisticsWorkHudPresenter presenter,
		BuildingManager manager,
		uint buildingId)
	{
		presenter.RenderBuildingBottleneck(
			manager.RegisteredBuildings,
			(_, _) => default,
			(category, candidateBuildingId) =>
				category == LogisticsWorkCategory.Picking && candidateBuildingId == buildingId
					? new TaskCountSnapshot(1, 0, 0, 0)
					: default);
	}

	private static void AssertBottleneck(
		VisualElement root,
		string expectedBuilding,
		string expectedStatus,
		bool danger = false,
		bool warning = false)
	{
		Button bottleneck = root.Q<Button>("logistics-work-hud-bottleneck");
		Assert.That(bottleneck.ClassListContains("logistics-work-hud__bottleneck--hidden"), Is.False);
		Assert.That(bottleneck.ClassListContains("logistics-work-hud__bottleneck--danger"), Is.EqualTo(danger));
		Assert.That(bottleneck.ClassListContains("logistics-work-hud__bottleneck--warning"), Is.EqualTo(warning));
		Assert.That(root.Q<Label>("logistics-work-hud-bottleneck-building").text, Is.EqualTo(expectedBuilding));
		Assert.That(root.Q<Label>("logistics-work-hud-bottleneck-status").text, Is.EqualTo(expectedStatus));
	}

	private VisualElement CreateHudPanelRoot()
	{
		VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudAssetPath);
		PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
			"Assets/Scripts/UI/Toolkit/New Panel Settings.asset");
		Assert.That(template, Is.Not.Null);
		Assert.That(panelSettings, Is.Not.Null);

		GameObject documentObject = new("Logistics Work HUD Navigation Test");
		documentObject.SetActive(false);
		UIDocument document = documentObject.AddComponent<UIDocument>();
		document.panelSettings = panelSettings;
		document.visualTreeAsset = template;
		createdObjects.Add(documentObject);
		documentObject.SetActive(true);

		Assert.That(document.rootVisualElement.panel, Is.Not.Null);
		return document.rootVisualElement;
	}

	private static void Submit(Button button)
	{
		Assert.That(button, Is.Not.Null);
		Assert.That(button.panel, Is.Not.Null);
		using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
		evt.target = button;
		button.SendEvent(evt);
	}
}
