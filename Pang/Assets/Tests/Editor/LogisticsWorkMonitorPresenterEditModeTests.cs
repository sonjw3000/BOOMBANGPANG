using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using UniverseLogistics.UI.Toolkit;

public sealed class LogisticsWorkMonitorPresenterEditModeTests
{
	private const string ContentAssetPath = "Assets/UI/Toolkit/WorkflowManagementContent.uxml";

	[Test]
	public void WorkflowTemplate_ContainsBindableFixedMonitorRows()
	{
		TemplateContainer root = LoadRoot();
		LogisticsWorkMonitorPresenter presenter = new();

		Assert.That(presenter.BindView(root), Is.True);
		Assert.That(root.Q<Button>("workflow-monitor-button"), Is.Not.Null);
		Assert.That(root.Q<VisualElement>("workflow-monitor-tab"), Is.Not.Null);

		string[] prefixes =
		{
			"picking",
			"storing",
			"packing-input",
			"packing",
			"packing-output",
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
	public void Render_KeepsDemandAndTaskStatesSeparateAndMarksRetryRisks()
	{
		TemplateContainer root = LoadRoot();
		LogisticsWorkMonitorPresenter presenter = new();
		Assert.That(presenter.BindView(root), Is.True);

		Dictionary<LogisticsWorkCategory, WorkDemandSnapshot> demands = new()
		{
			[LogisticsWorkCategory.Picking] = new WorkDemandSnapshot(2, 17),
			[LogisticsWorkCategory.Storing] = new WorkDemandSnapshot(3, 23),
			[LogisticsWorkCategory.PackingInput] = new WorkDemandSnapshot(4, 29),
			[LogisticsWorkCategory.Packing] = new WorkDemandSnapshot(5, 31),
			[LogisticsWorkCategory.PackingOutput] = new WorkDemandSnapshot(6, 37),
			[LogisticsWorkCategory.CapsuleRelocate] = new WorkDemandSnapshot(7, 0),
		};
		Dictionary<LogisticsWorkCategory, TaskCountSnapshot> tasks = new()
		{
			[LogisticsWorkCategory.Picking] = new TaskCountSnapshot(11, 1, 13, 2),
			[LogisticsWorkCategory.Storing] = new TaskCountSnapshot(19, 0, 23, 0),
			[LogisticsWorkCategory.PackingInput] = new TaskCountSnapshot(29, 0, 31, 0),
			[LogisticsWorkCategory.Packing] = new TaskCountSnapshot(37, 0, 41, 0),
			[LogisticsWorkCategory.PackingOutput] = new TaskCountSnapshot(43, 0, 47, 0),
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
}
