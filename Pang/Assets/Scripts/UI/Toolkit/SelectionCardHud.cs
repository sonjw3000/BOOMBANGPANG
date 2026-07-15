using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public interface ISelectionInspectorProvider
	{
		void BuildInspectorModel(SelectionInspectorModel model);
	}

	public sealed class SelectionInspectorModel
	{
		public readonly List<SelectionInspectorRow> OverviewRows = new();
		public readonly List<SelectionInspectorAction> Actions = new();

		public void Clear()
		{
			OverviewRows.Clear();
			Actions.Clear();
		}

		public void AddOverview(string label, Func<string> getValue)
		{
			OverviewRows.Add(new SelectionInspectorRow(label, getValue));
		}

		public void AddAction(string label, Action execute, Func<bool> canExecute = null, bool isDangerous = false)
		{
			Actions.Add(new SelectionInspectorAction(label, execute, canExecute, isDangerous));
		}
	}

	public sealed class SelectionInspectorRow
	{
		public string Label { get; }
		public Func<string> GetValue { get; }

		public SelectionInspectorRow(string label, Func<string> getValue)
		{
			Label = label;
			GetValue = getValue;
		}
	}

	public sealed class SelectionInspectorAction
	{
		public string Label { get; }
		public Action Execute { get; }
		public Func<bool> CanExecute { get; }
		public bool IsDangerous { get; }

		public SelectionInspectorAction(string label, Action execute, Func<bool> canExecute, bool isDangerous)
		{
			Label = label;
			Execute = execute;
			CanExecute = canExecute;
			IsDangerous = isDangerous;
		}
	}

	public sealed class SelectionCardHud
	{
		private const int MaxInfoRows = 4;
		private const string EnterClass = "selection-card--enter";
		private const string ResetClass = "selection-card--reset";

		private sealed class InfoRowBinding
		{
			public InfoBlock Block;
			public Label Value;
		}

		private sealed class InspectorRowBinding
		{
			public SelectionInspectorRow Row;
			public Label Value;
		}

		private sealed class InspectorActionBinding
		{
			public SelectionInspectorAction Action;
			public Button Button;
		}

		private readonly List<InfoRowBinding> infoRows = new();
		private readonly List<InspectorRowBinding> inspectorRows = new();
		private readonly List<InspectorActionBinding> inspectorActions = new();
		private readonly SelectionInspectorModel inspectorModel = new();
		private VisualElement root;
		private Image icon;
		private Label title;
		private Label subtitle;
		private VisualElement infoList;
		private VisualElement inspector;
		private VisualElement detailTabs;
		private VisualElement overviewList;
		private VisualElement contextActions;
		private Button focusButton;
		private Button detailsButton;
		private UIProviderBase displayedProvider;
		private Action focusAction;
		private Action detailsAction;
		private IVisualElementScheduledItem enterAnimation;
		private bool inspectorExpanded;

		public bool IsBound => root != null;

		public bool Bind(VisualElement documentRoot)
		{
			if (documentRoot == null) return false;
			UnbindButtons();
			root = documentRoot.Q<VisualElement>("selection-card");
			icon = documentRoot.Q<Image>("selection-card-icon");
			title = documentRoot.Q<Label>("selection-card-title");
			subtitle = documentRoot.Q<Label>("selection-card-subtitle");
			infoList = documentRoot.Q<VisualElement>("selection-card-info-list");
			inspector = documentRoot.Q<VisualElement>("selection-card-inspector");
			detailTabs = documentRoot.Q<VisualElement>("selection-card-detail-tabs");
			overviewList = documentRoot.Q<VisualElement>("selection-card-overview-list");
			contextActions = documentRoot.Q<VisualElement>("selection-card-context-actions");
			focusButton = documentRoot.Q<Button>("selection-card-focus-button");
			detailsButton = documentRoot.Q<Button>("selection-card-details-button");
			if (root == null || icon == null || title == null || subtitle == null || infoList == null ||
				inspector == null || detailTabs == null || overviewList == null || contextActions == null ||
				focusButton == null || detailsButton == null)
			{
				root = null;
				return false;
			}

			focusButton.clicked += InvokeFocus;
			detailsButton.clicked += InvokeDetails;
			Hide();
			return true;
		}

		public void SetActions(Action onFocus, Action onDetails)
		{
			focusAction = onFocus;
			detailsAction = onDetails;
		}

		public void Show(UIProviderBase provider)
		{
			if (root == null || provider == null) return;
			displayedProvider = provider;
			SetInspectorExpanded(false);
			RebuildInfoRows(provider);
			RebuildInspector(provider);
			Refresh(provider);
			enterAnimation?.Pause();
			root.style.display = DisplayStyle.Flex;
			root.AddToClassList(ResetClass);
			root.AddToClassList(EnterClass);
			enterAnimation = root.schedule.Execute(() =>
			{
				root.RemoveFromClassList(ResetClass);
				root.RemoveFromClassList(EnterClass);
			}).StartingIn(16);
		}

		public bool Refresh(UIProviderBase provider)
		{
			if (root == null || provider == null || ReferenceEquals(displayedProvider, provider) == false)
				return false;

			title.text = provider.Name;
			subtitle.text = provider.Subtitle;
			icon.sprite = provider.Icon;
			icon.style.display = provider.Icon != null ? DisplayStyle.Flex : DisplayStyle.None;
			for (int i = 0; i < infoRows.Count; ++i)
			{
				InfoRowBinding binding = infoRows[i];
				binding.Value.text = binding.Block switch
				{
					KeyValueBlock keyValue => keyValue.Value,
					ProgressBlock progress => progress.Text,
					_ => binding.Block.GetContent(),
				};
			}
			RefreshInspector();
			return true;
		}

		public bool ToggleInspector(UIProviderBase provider)
		{
			if (root == null || provider is not ISelectionInspectorProvider ||
				ReferenceEquals(displayedProvider, provider) == false)
				return false;

			SetInspectorExpanded(inspectorExpanded == false);
			return true;
		}

		public void Hide()
		{
			enterAnimation?.Pause();
			displayedProvider = null;
			SetInspectorExpanded(false);
			infoRows.Clear();
			infoList?.Clear();
			ClearInspector();
			if (root == null) return;
			root.RemoveFromClassList(EnterClass);
			root.RemoveFromClassList(ResetClass);
			root.style.display = DisplayStyle.None;
		}

		private void RebuildInfoRows(UIProviderBase provider)
		{
			infoRows.Clear();
			infoList.Clear();
			int count = 0;
			foreach (InfoBlock block in provider.InfoBlocks)
			{
				if (block == null || count >= MaxInfoRows) break;
				string key = block switch
				{
					KeyValueBlock keyValue => keyValue.Key,
					ProgressBlock progress => progress.Label,
					_ => string.Empty,
				};

				VisualElement row = new();
				row.AddToClassList("selection-card-info-row");
				Label keyLabel = new(key);
				keyLabel.AddToClassList("selection-card-info-row__key");
				Label valueLabel = new();
				valueLabel.AddToClassList("selection-card-info-row__value");
				row.Add(keyLabel);
				row.Add(valueLabel);
				infoList.Add(row);
				infoRows.Add(new InfoRowBinding { Block = block, Value = valueLabel });
				count += 1;
			}
		}

		private void RebuildInspector(UIProviderBase provider)
		{
			ClearInspector();
			if (provider is not ISelectionInspectorProvider inspectorProvider)
			{
				detailsButton.text = "Details";
				return;
			}

			inspectorProvider.BuildInspectorModel(inspectorModel);
			for (int i = 0; i < inspectorModel.OverviewRows.Count; ++i)
			{
				SelectionInspectorRow inspectorRow = inspectorModel.OverviewRows[i];
				VisualElement row = new();
				row.AddToClassList("selection-card-info-row");
				Label keyLabel = new(inspectorRow.Label);
				keyLabel.AddToClassList("selection-card-info-row__key");
				Label valueLabel = new();
				valueLabel.AddToClassList("selection-card-info-row__value");
				row.Add(keyLabel);
				row.Add(valueLabel);
				overviewList.Add(row);
				inspectorRows.Add(new InspectorRowBinding { Row = inspectorRow, Value = valueLabel });
			}

			for (int i = 0; i < inspectorModel.Actions.Count; ++i)
			{
				SelectionInspectorAction inspectorAction = inspectorModel.Actions[i];
				Button button = new(inspectorAction.Execute) { text = inspectorAction.Label };
				button.AddToClassList("selection-card__context-button");
				if (inspectorAction.IsDangerous)
					button.AddToClassList("selection-card__context-button--danger");
				contextActions.Add(button);
				inspectorActions.Add(new InspectorActionBinding { Action = inspectorAction, Button = button });
			}
			detailsButton.text = "Details";
			RefreshInspector();
		}

		private void RefreshInspector()
		{
			for (int i = 0; i < inspectorRows.Count; ++i)
			{
				InspectorRowBinding binding = inspectorRows[i];
				binding.Value.text = binding.Row.GetValue?.Invoke() ?? "-";
			}

			for (int i = 0; i < inspectorActions.Count; ++i)
			{
				InspectorActionBinding binding = inspectorActions[i];
				binding.Button.SetEnabled(binding.Action.CanExecute?.Invoke() ?? true);
			}
		}

		private void ClearInspector()
		{
			inspectorRows.Clear();
			inspectorActions.Clear();
			inspectorModel.Clear();
			overviewList?.Clear();
			contextActions?.Clear();
			detailTabs?.Clear();
		}

		private void SetInspectorExpanded(bool expanded)
		{
			inspectorExpanded = expanded;
			if (inspector != null)
				inspector.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
			if (root != null)
				root.EnableInClassList("selection-card--expanded", expanded);
			if (detailsButton != null && displayedProvider is ISelectionInspectorProvider)
				detailsButton.text = expanded ? "Close" : "Details";
		}

		private void InvokeFocus() => focusAction?.Invoke();
		private void InvokeDetails() => detailsAction?.Invoke();

		private void UnbindButtons()
		{
			if (focusButton != null) focusButton.clicked -= InvokeFocus;
			if (detailsButton != null) detailsButton.clicked -= InvokeDetails;
		}
	}
}
