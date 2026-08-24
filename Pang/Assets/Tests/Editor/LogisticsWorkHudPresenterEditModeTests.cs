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
	public void NavigationSubmit_OpensMonitorOnceAndUnbindsCleanly()
	{
		VisualElement root = CreateHudPanelRoot();
		LogisticsWorkHudPresenter presenter = new();
		int openCount = 0;
		presenter.ConfigureNavigation(() => ++openCount);
		Assert.That(presenter.BindView(root), Is.True);

		Button header = root.Q<Button>("logistics-work-hud-header");
		Submit(header);
		Assert.That(openCount, Is.EqualTo(1));

		Assert.That(presenter.BindView(root), Is.True);
		Submit(header);
		Assert.That(openCount, Is.EqualTo(2));

		presenter.UnbindView();
		Submit(header);
		Assert.That(openCount, Is.EqualTo(2));

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
