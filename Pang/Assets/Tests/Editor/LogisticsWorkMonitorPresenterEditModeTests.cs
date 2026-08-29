using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UniverseLogistics.UI.Toolkit;

public sealed class LogisticsWorkMonitorPresenterEditModeTests
{
	private const string ContentAssetPath = "Assets/UI/Toolkit/WorkflowManagementContent.uxml";
	private const string PanelSettingsAssetPath = "Assets/Scripts/UI/Toolkit/New Panel Settings.asset";

	[Test]
	public void WorkflowTemplate_ContainsBindableFixedMonitorRows()
	{
		TemplateContainer root = LoadRoot();
		LogisticsWorkMonitorPresenter presenter = new();

		Assert.That(presenter.BindView(root), Is.True);
		Assert.That(root.Q<Button>("workflow-monitor-button"), Is.Not.Null);
		Assert.That(root.Q<VisualElement>("workflow-monitor-tab"), Is.Not.Null);
		Assert.That(root.Q<DropdownField>("workflow-monitor-building-scope"), Is.Not.Null);

		string[] prefixes =
		{
			"labeling",
			"picking",
			"storing",
			"packing-input",
			"packing",
			"packing-output",
			"launch-sort",
			"capsule-relocate",
		};

		for (int i = 0; i < prefixes.Length; ++i)
		{
			string prefix = $"workflow-monitor-{prefixes[i]}";
			Assert.That(root.Q<Label>($"{prefix}-demand"), Is.Not.Null, prefix);
			Assert.That(root.Q<Label>($"{prefix}-items"), Is.Not.Null, prefix);
			Assert.That(root.Q<Label>($"{prefix}-waiting"), Is.Not.Null, prefix);
			Assert.That(root.Q<Label>($"{prefix}-ready"), Is.Not.Null, prefix);
			Assert.That(root.Q<Label>($"{prefix}-returned"), Is.Not.Null, prefix);
			Assert.That(root.Q<Label>($"{prefix}-active"), Is.Not.Null, prefix);
			Assert.That(root.Q<Label>($"{prefix}-blocked"), Is.Not.Null, prefix);
		}

		presenter.Dispose();
	}

	[Test]
	public void BuildingScope_ListsBuildingsWithUniqueLabelsAndPassesSelectedIdToResolvers()
	{
		GameObject managerObject = new("LogisticsWorkMonitorPresenterEditModeTests.BuildingManager");
		GameObject documentObject = null;
		LogisticsWorkMonitorPresenter presenter = new();
		try
		{
			BuildingManager buildingManager = managerObject.AddComponent<BuildingManager>();
			Building firstDepot = new("Depot", new List<GridCell>());
			Building secondDepot = new("Depot", new List<GridCell>());
			buildingManager.Register(firstDepot);
			buildingManager.Register(secondDepot);

			VisualElement root = LoadAttachedRoot(out documentObject);
			Assert.That(presenter.BindView(root), Is.True);
			presenter.BindSources(null, null, null, null, buildingManager, null, null);

			DropdownField scope = root.Q<DropdownField>("workflow-monitor-building-scope");
			Assert.That(scope.choices, Is.EqualTo(new[]
			{
				"All Buildings",
				"Hub / Unassigned",
				$"Depot · #{firstDepot.RuntimeBuildingId}",
				$"Depot · #{secondDepot.RuntimeBuildingId}",
			}));

			AssertRenderScope(presenter, null);
			scope.index = 1;
			Assert.That(scope.value, Is.EqualTo("Hub / Unassigned"));
			AssertRenderScope(presenter, 0);
			scope.index = 3;
			Assert.That(scope.value, Is.EqualTo($"Depot · #{secondDepot.RuntimeBuildingId}"));
			AssertRenderScope(presenter, secondDepot.RuntimeBuildingId);
			Assert.That(presenter.TrySelectBuildingScope(firstDepot.RuntimeBuildingId), Is.True);
			AssertRenderScope(presenter, firstDepot.RuntimeBuildingId);
			Assert.That(presenter.TrySelectBuildingScope(uint.MaxValue), Is.False);
			AssertRenderScope(presenter, firstDepot.RuntimeBuildingId);
			Assert.That(presenter.TrySelectBuildingScope(secondDepot.RuntimeBuildingId), Is.True);

			buildingManager.Unregister(firstDepot);
			AssertRenderScope(presenter, secondDepot.RuntimeBuildingId);
			Assert.That(scope.value, Is.EqualTo($"Depot · #{secondDepot.RuntimeBuildingId}"));
			Assert.That(scope.index, Is.EqualTo(2));
		}
		finally
		{
			presenter.Dispose();
			if (documentObject != null)
				UnityEngine.Object.DestroyImmediate(documentObject);
			UnityEngine.Object.DestroyImmediate(managerObject);
		}
	}

	[Test]
	public void BuildingScope_WhenSelectedBuildingIsRemoved_FallsBackToAllBuildings()
	{
		GameObject managerObject = new("LogisticsWorkMonitorPresenterEditModeTests.BuildingManager");
		GameObject documentObject = null;
		LogisticsWorkMonitorPresenter presenter = new();
		try
		{
			BuildingManager buildingManager = managerObject.AddComponent<BuildingManager>();
			Building storage = new("Storage Alpha", new List<GridCell>());
			buildingManager.Register(storage);

			VisualElement root = LoadAttachedRoot(out documentObject);
			Assert.That(presenter.BindView(root), Is.True);
			presenter.BindSources(null, null, null, null, buildingManager, null, null);
			DropdownField scope = root.Q<DropdownField>("workflow-monitor-building-scope");
			scope.index = 2;
			AssertRenderScope(presenter, storage.RuntimeBuildingId);

			buildingManager.Unregister(storage);

			Assert.That(scope.value, Is.EqualTo("All Buildings"));
			Assert.That(scope.index, Is.Zero);
			Assert.That(scope.choices, Is.EqualTo(new[] { "All Buildings", "Hub / Unassigned" }));
			AssertRenderScope(presenter, null);
		}
		finally
		{
			presenter.Dispose();
			if (documentObject != null)
				UnityEngine.Object.DestroyImmediate(documentObject);
			UnityEngine.Object.DestroyImmediate(managerObject);
		}
	}

	[Test]
	public void Render_KeepsDemandAndTaskStatesSeparateAndMarksRetryRisks()
	{
		TemplateContainer root = LoadRoot();
		LogisticsWorkMonitorPresenter presenter = new();
		Assert.That(presenter.BindView(root), Is.True);

		Dictionary<LogisticsWorkCategory, WorkDemandSnapshot> demands = new()
		{
			[LogisticsWorkCategory.Labeling] = new WorkDemandSnapshot(1, 11),
			[LogisticsWorkCategory.Picking] = new WorkDemandSnapshot(2, 17),
			[LogisticsWorkCategory.Storing] = new WorkDemandSnapshot(3, 23),
			[LogisticsWorkCategory.PackingInput] = new WorkDemandSnapshot(4, 29),
			[LogisticsWorkCategory.Packing] = new WorkDemandSnapshot(5, 31),
			[LogisticsWorkCategory.PackingOutput] = new WorkDemandSnapshot(6, 37),
			[LogisticsWorkCategory.LaunchSort] = new WorkDemandSnapshot(8, 41),
			[LogisticsWorkCategory.CapsuleRelocate] = new WorkDemandSnapshot(7, 0),
		};
		Dictionary<LogisticsWorkCategory, TaskCountSnapshot> tasks = new()
		{
			[LogisticsWorkCategory.Labeling] = new TaskCountSnapshot(7, 0, 5, 0),
			[LogisticsWorkCategory.Picking] = new TaskCountSnapshot(11, 1, 13, 2),
			[LogisticsWorkCategory.Storing] = new TaskCountSnapshot(19, 0, 23, 0),
			[LogisticsWorkCategory.PackingInput] = new TaskCountSnapshot(29, 0, 31, 0),
			[LogisticsWorkCategory.Packing] = new TaskCountSnapshot(37, 0, 41, 0),
			[LogisticsWorkCategory.PackingOutput] = new TaskCountSnapshot(43, 0, 47, 0),
			[LogisticsWorkCategory.LaunchSort] = new TaskCountSnapshot(61, 0, 67, 0),
			[LogisticsWorkCategory.CapsuleRelocate] = new TaskCountSnapshot(53, 0, 59, 0),
		};

		presenter.Render(category => demands[category], category => tasks[category]);

		Assert.That(root.Q<Label>("workflow-monitor-picking-demand").text, Is.EqualTo("2"));
		Assert.That(root.Q<Label>("workflow-monitor-picking-items").text, Is.EqualTo("17"));
		Assert.That(root.Q<Label>("workflow-monitor-picking-waiting").text, Is.EqualTo("12"));
		Assert.That(root.Q<Label>("workflow-monitor-picking-ready").text, Is.EqualTo("11"));
		Assert.That(root.Q<Label>("workflow-monitor-picking-returned").text, Is.EqualTo("1"));
		Assert.That(root.Q<Label>("workflow-monitor-picking-active").text, Is.EqualTo("13"));
		Assert.That(root.Q<Label>("workflow-monitor-picking-blocked").text, Is.EqualTo("2"));
		Assert.That(
			root.Q<Label>("workflow-monitor-picking-returned").ClassListContains("workflow-monitor-value--warning"),
			Is.True);
		Assert.That(
			root.Q<Label>("workflow-monitor-picking-blocked").ClassListContains("workflow-monitor-value--danger"),
			Is.True);
		Assert.That(root.Q<Label>("workflow-monitor-packing-output-demand").text, Is.EqualTo("6"));
		Assert.That(root.Q<Label>("workflow-monitor-packing-output-items").text, Is.EqualTo("37"));
		Assert.That(root.Q<Label>("workflow-monitor-labeling-items").text, Is.EqualTo("11"));
		Assert.That(root.Q<Label>("workflow-monitor-launch-sort-items").text, Is.EqualTo("41"));
		Assert.That(root.Q<Label>("workflow-monitor-capsule-relocate-demand").text, Is.EqualTo("7"));
		Assert.That(root.Q<Label>("workflow-monitor-capsule-relocate-items").text, Is.EqualTo("—"));

		tasks[LogisticsWorkCategory.Picking] = new TaskCountSnapshot(11, 0, 13, 0);
		presenter.Render(category => demands[category], category => tasks[category]);
		Assert.That(
			root.Q<Label>("workflow-monitor-picking-returned").ClassListContains("workflow-monitor-value--warning"),
			Is.False);
		Assert.That(
			root.Q<Label>("workflow-monitor-picking-blocked").ClassListContains("workflow-monitor-value--danger"),
			Is.False);

		presenter.Dispose();
	}

	private static TemplateContainer LoadRoot()
	{
		VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ContentAssetPath);
		Assert.That(template, Is.Not.Null);
		return template.CloneTree();
	}

	private static VisualElement LoadAttachedRoot(out GameObject documentObject)
	{
		VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ContentAssetPath);
		PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsAssetPath);
		Assert.That(template, Is.Not.Null);
		Assert.That(panelSettings, Is.Not.Null);

		documentObject = new GameObject("Logistics Work Monitor UI Test");
		documentObject.SetActive(false);
		UIDocument document = documentObject.AddComponent<UIDocument>();
		document.panelSettings = panelSettings;
		document.visualTreeAsset = template;
		documentObject.SetActive(true);
		Assert.That(document.rootVisualElement.panel, Is.Not.Null);
		return document.rootVisualElement;
	}

	private static void AssertRenderScope(LogisticsWorkMonitorPresenter presenter, uint? expectedBuildingId)
	{
		uint? demandBuildingId = uint.MaxValue;
		uint? taskBuildingId = uint.MaxValue;
		presenter.Render(
			(_, buildingId) =>
			{
				demandBuildingId = buildingId;
				return default;
			},
			(_, buildingId) =>
			{
				taskBuildingId = buildingId;
				return default;
			});

		Assert.That(demandBuildingId, Is.EqualTo(expectedBuildingId));
		Assert.That(taskBuildingId, Is.EqualTo(expectedBuildingId));
	}
}
