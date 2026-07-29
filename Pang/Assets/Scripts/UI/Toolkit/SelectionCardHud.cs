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
		public readonly List<SelectionInspectorTab> Tabs = new();

		public void Clear()
		{
			OverviewRows.Clear();
			Actions.Clear();
			Tabs.Clear();
		}

		public void AddOverview(string label, Func<string> getValue)
		{
			OverviewRows.Add(new SelectionInspectorRow(label, getValue));
		}

		public void AddAction(string label, Action execute, Func<bool> canExecute = null, bool isDangerous = false,
			Func<UITooltipContent> tooltip = null)
		{
			Actions.Add(new SelectionInspectorAction(label, execute, canExecute, isDangerous, tooltip));
		}

		public void AddTab(string label, Func<int> getContentVersion, Func<SelectionDetailPanelModel> buildContent)
		{
			Tabs.Add(new SelectionInspectorTab(label, getContentVersion, buildContent));
		}
	}

	public sealed class SelectionInspectorTab
	{
		public string Label { get; }
		public Func<int> GetContentVersion { get; }
		public Func<SelectionDetailPanelModel> BuildContent { get; }

		public SelectionInspectorTab(string label, Func<int> getContentVersion, Func<SelectionDetailPanelModel> buildContent)
		{
			Label = label;
			GetContentVersion = getContentVersion;
			BuildContent = buildContent;
		}
	}

	public sealed class SelectionDetailPanelModel
	{
		public string Title { get; set; }
		public string Summary { get; set; }
		public readonly List<SelectionDetailRow> Rows = new();
		public bool HasSlider { get; set; }
		public string SliderLabel { get; set; }
		public float SliderValue { get; set; }
		public float SliderLowValue { get; set; }
		public float SliderHighValue { get; set; } = 100.0f;
		public bool SliderEnabled { get; set; } = true;
		public Action<float> SliderChanged { get; set; }
		public Func<UITooltipContent> SliderTooltip { get; set; }
		public SelectionDetailEditorModel Editor { get; set; }
	}

	public sealed class SelectionDetailEditorModel
	{
		public string Message { get; set; }
		public string DropdownLabel { get; set; }
		public List<string> DropdownChoices { get; set; } = new();
		public int DropdownIndex { get; set; }
		public bool DropdownEnabled { get; set; } = true;
		public Action<int> DropdownChanged { get; set; }
		public string ToggleLabel { get; set; }
		public readonly List<SelectionDetailToggleModel> Toggles = new();
		public string PrimaryActionLabel { get; set; }
		public Action PrimaryAction { get; set; }
		public string SecondaryActionLabel { get; set; }
		public Action SecondaryAction { get; set; }
	}

	public sealed class SelectionDetailToggleModel
	{
		public string Label { get; set; }
		public bool Value { get; set; }
		public bool Enabled { get; set; } = true;
		public Action<bool> Changed { get; set; }
	}

	public sealed class SelectionDetailRow
	{
		public string Primary { get; set; }
		public string Trailing { get; set; }
		public string Secondary { get; set; }
	}

	public static class SelectionDetailContentUtility
	{
		public static int GetItemContainerVersion(IItemContainer container)
		{
			if (container?.Stacks == null)
				return 0;

			unchecked
			{
				int version = 17;
				for (int i = 0; i < container.Stacks.Count; ++i)
				{
					ItemStack stack = container.Stacks[i];
					if (stack == null)
						continue;

					version = version * 31 + (int)stack.ItemID;
					version = version * 31 + stack.Quantity;
					version = version * 31 + stack.DamagePercent;
					version = version * 31 + stack.FreshnessPercent;
					if (ItemContainerDisplayUtility.CanDisplayTemperature)
						version = version * 31 + UnityEngine.Mathf.RoundToInt(stack.CurrentTemperatureCelsius * 10.0f);
				}
				if (ItemContainerDisplayUtility.CanDisplayTemperature &&
					container is IThermalItemContainer thermalContainer)
				{
					version = version * 31 +
						UnityEngine.Mathf.RoundToInt(thermalContainer.CurrentTemperatureCelsius * 10.0f);
				}
				if (GameContext.HasInstance && GameContext.Instance.GameTime != null)
					version = version * 31 + GameContext.Instance.GameTime.WeeksPassed;
				return version;
			}
		}

		public static SelectionDetailPanelModel BuildItemContainerPanel(
			string title,
			string summary,
			ItemContainerDisplayInfo display)
		{
			SelectionDetailPanelModel panel = new()
			{
				Title = title,
				Summary = summary,
			};

			if (display?.Items == null)
				return panel;

			if (ItemContainerDisplayUtility.CanDisplayTemperature &&
				display.Container is IThermalItemContainer thermalContainer)
			{
				panel.Rows.Add(new SelectionDetailRow
				{
					Primary = "Temperature",
					Secondary = $"{thermalContainer.CurrentTemperatureCelsius:0.0} °C",
				});
			}

			for (int i = 0; i < display.Items.Count; ++i)
			{
				ItemContainerItemDisplayInfo item = display.Items[i];
				string status = $"Damage {item.Damage}%";
				if (item.ShowsFreshness)
					status += $"   Fresh {item.Freshness}%";
				if (item.ShowsTemperature)
					status += $"   {item.TemperatureCelsius:0.0} °C";

				panel.Rows.Add(new SelectionDetailRow
				{
					Primary = item.ItemName,
					Trailing = $"×{item.Quantity}",
					Secondary = status,
				});
			}

			if (display.ManifestItems != null)
			{
				for (int i = 0; i < display.ManifestItems.Count; ++i)
				{
					ManifestContainerItemDisplayInfo manifest = display.ManifestItems[i];
					panel.Rows.Add(new SelectionDetailRow
					{
						Primary = $"Order {manifest.OrderId} · {manifest.ItemName}",
						Trailing = manifest.WeeksLeft == "Delayed" ? "Delayed" : $"{manifest.WeeksLeft}w",
						Secondary = $"{manifest.InBoxQuantity} · {manifest.OrderProgress}",
					});
				}
			}
			return panel;
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
		public Func<UITooltipContent> Tooltip { get; }

		public SelectionInspectorAction(string label, Action execute, Func<bool> canExecute, bool isDangerous,
			Func<UITooltipContent> tooltip)
		{
			Label = label;
			Execute = execute;
			CanExecute = canExecute;
			IsDangerous = isDangerous;
			Tooltip = tooltip;
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

		private sealed class InspectorTabBinding
		{
			public SelectionInspectorTab Tab;
			public Button Button;
		}

		private readonly List<InfoRowBinding> infoRows = new();
		private readonly List<InspectorRowBinding> inspectorRows = new();
		private readonly List<InspectorActionBinding> inspectorActions = new();
		private readonly List<InspectorTabBinding> inspectorTabs = new();
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
		private VisualElement detailPanel;
		private Label detailTitle;
		private Label detailSummary;
		private ListView detailList;
		private Label detailEmpty;
		private VisualElement detailSliderControl;
		private Label detailSliderLabel;
		private Slider detailSlider;
		private Label detailSliderValue;
		private VisualElement detailEditor;
		private Label detailEditorMessage;
		private Label detailEditorDropdownLabel;
		private DropdownField detailEditorDropdown;
		private Label detailEditorToggleLabel;
		private ScrollView detailEditorToggles;
		private VisualElement detailEditorActions;
		private Button detailEditorPrimaryAction;
		private Button detailEditorSecondaryAction;
		private Button focusButton;
		private Button detailsButton;
		private UIProviderBase displayedProvider;
		private Action focusAction;
		private Action detailsAction;
		private IVisualElementScheduledItem enterAnimation;
		private bool inspectorExpanded;
		private int activeTabIndex = -1;
		private int activeDetailVersion = int.MinValue;
		private SelectionDetailPanelModel activeDetailModel;
		private int expandedInspectorHeight = 170;
		private int expandedCardHeight = 280;

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
			detailPanel = documentRoot.Q<VisualElement>("selection-detail-panel");
			detailTitle = documentRoot.Q<Label>("selection-detail-title");
			detailSummary = documentRoot.Q<Label>("selection-detail-summary");
			detailList = documentRoot.Q<ListView>("selection-detail-list");
			detailEmpty = documentRoot.Q<Label>("selection-detail-empty");
			detailSliderControl = documentRoot.Q<VisualElement>("selection-detail-slider-control");
			detailSliderLabel = documentRoot.Q<Label>("selection-detail-slider-label");
			detailSlider = documentRoot.Q<Slider>("selection-detail-slider");
			detailSliderValue = documentRoot.Q<Label>("selection-detail-slider-value");
			detailEditor = documentRoot.Q<VisualElement>("selection-detail-editor");
			detailEditorMessage = documentRoot.Q<Label>("selection-detail-editor-message");
			detailEditorDropdownLabel = documentRoot.Q<Label>("selection-detail-editor-dropdown-label");
			detailEditorDropdown = documentRoot.Q<DropdownField>("selection-detail-editor-dropdown");
			detailEditorToggleLabel = documentRoot.Q<Label>("selection-detail-editor-toggle-label");
			detailEditorToggles = documentRoot.Q<ScrollView>("selection-detail-editor-toggles");
			detailEditorActions = documentRoot.Q<VisualElement>("selection-detail-editor-actions");
			detailEditorPrimaryAction = documentRoot.Q<Button>("selection-detail-editor-primary-action");
			detailEditorSecondaryAction = documentRoot.Q<Button>("selection-detail-editor-secondary-action");
			focusButton = documentRoot.Q<Button>("selection-card-focus-button");
			detailsButton = documentRoot.Q<Button>("selection-card-details-button");
			if (root == null || icon == null || title == null || subtitle == null || infoList == null ||
				inspector == null || detailTabs == null || overviewList == null || contextActions == null ||
				detailPanel == null || detailTitle == null || detailSummary == null || detailList == null || detailEmpty == null ||
				detailSliderControl == null || detailSliderLabel == null || detailSlider == null || detailSliderValue == null ||
				detailEditor == null || detailEditorMessage == null || detailEditorDropdownLabel == null || detailEditorDropdown == null ||
				detailEditorToggleLabel == null || detailEditorToggles == null || detailEditorActions == null ||
				detailEditorPrimaryAction == null || detailEditorSecondaryAction == null ||
				focusButton == null || detailsButton == null)
			{
				root = null;
				return false;
			}

			focusButton.clicked += InvokeFocus;
			detailsButton.clicked += InvokeDetails;
			ConfigureDetailList();
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
			RefreshDetailPanel();
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

		public bool ExpandInspector(UIProviderBase provider)
		{
			if (root == null || provider is not ISelectionInspectorProvider ||
				ReferenceEquals(displayedProvider, provider) == false)
				return false;

			SetInspectorExpanded(true);
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

			for (int i = 0; i < inspectorModel.Tabs.Count; ++i)
			{
				int tabIndex = i;
				SelectionInspectorTab inspectorTab = inspectorModel.Tabs[i];
				Button button = new(() => ToggleDetailTab(tabIndex)) { text = inspectorTab.Label };
				button.AddToClassList("selection-card__detail-tab");
				detailTabs.Add(button);
				inspectorTabs.Add(new InspectorTabBinding { Tab = inspectorTab, Button = button });
			}
			detailTabs.EnableInClassList("selection-card__detail-tabs--visible", inspectorTabs.Count > 0);

			for (int i = 0; i < inspectorModel.Actions.Count; ++i)
			{
				SelectionInspectorAction inspectorAction = inspectorModel.Actions[i];
				Button button = new(inspectorAction.Execute) { text = inspectorAction.Label };
				button.AddToClassList("selection-card__context-button");
				if (inspectorAction.IsDangerous)
					button.AddToClassList("selection-card__context-button--danger");
				if (inspectorAction.Tooltip != null)
				{
					VisualElement control = new();
					control.AddToClassList("selection-card__context-button-control");
					control.SetTooltip(inspectorAction.Tooltip);
					control.Add(button);
					contextActions.Add(control);
				}
				else
				{
					contextActions.Add(button);
				}
				inspectorActions.Add(new InspectorActionBinding { Action = inspectorAction, Button = button });
			}
			CalculateExpandedSize();
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
			CloseDetailPanel();
			inspectorRows.Clear();
			inspectorActions.Clear();
			inspectorTabs.Clear();
			inspectorModel.Clear();
			overviewList?.Clear();
			contextActions?.Clear();
			detailTabs?.Clear();
			detailTabs?.RemoveFromClassList("selection-card__detail-tabs--visible");
		}

		private void SetInspectorExpanded(bool expanded)
		{
			inspectorExpanded = expanded;
			if (expanded == false)
				CloseDetailPanel();
			if (inspector != null)
			{
				inspector.pickingMode = expanded ? PickingMode.Position : PickingMode.Ignore;
				inspector.style.height = expanded ? expandedInspectorHeight : 0;
			}
			if (root != null)
			{
				root.EnableInClassList("selection-card--expanded", expanded);
				root.style.height = expanded ? expandedCardHeight : 150;
			}
			if (detailsButton != null && displayedProvider is ISelectionInspectorProvider)
				detailsButton.text = expanded ? "Close" : "Details";
		}

		private void CalculateExpandedSize()
		{
			int tabHeight = inspectorTabs.Count > 0 ? 30 : 0;
			int overviewHeight = inspectorRows.Count * 20;
			int actionRowCount = (inspectorActions.Count + 1) / 2;
			int actionHeight = System.Math.Max(1, actionRowCount) * 28;
			expandedInspectorHeight = System.Math.Max(170, tabHeight + overviewHeight + actionHeight + 51);
			expandedCardHeight = System.Math.Min(430, expandedInspectorHeight + 110);
		}

		private void ConfigureDetailList()
		{
			detailList.selectionType = SelectionType.None;
			detailList.makeItem = () =>
			{
				VisualElement row = new();
				row.AddToClassList("selection-detail-row");
				VisualElement top = new();
				top.AddToClassList("selection-detail-row__top");
				Label primary = new() { name = "primary" };
				primary.AddToClassList("selection-detail-row__primary");
				Label trailing = new() { name = "trailing" };
				trailing.AddToClassList("selection-detail-row__trailing");
				Label secondary = new() { name = "secondary" };
				secondary.AddToClassList("selection-detail-row__secondary");
				top.Add(primary);
				top.Add(trailing);
				row.Add(top);
				row.Add(secondary);
				return row;
			};
			detailList.bindItem = (element, index) =>
			{
				if (activeDetailModel == null || index < 0 || index >= activeDetailModel.Rows.Count)
					return;

				SelectionDetailRow row = activeDetailModel.Rows[index];
				element.Q<Label>("primary").text = row.Primary ?? string.Empty;
				element.Q<Label>("trailing").text = row.Trailing ?? string.Empty;
				element.Q<Label>("secondary").text = row.Secondary ?? string.Empty;
			};
			detailSlider.RegisterValueChangedCallback(evt =>
			{
				activeDetailModel?.SliderChanged?.Invoke(evt.newValue);
				detailSliderValue.text = $"{evt.newValue:0}%";
			});
			detailSliderControl.SetTooltip(() => activeDetailModel?.SliderTooltip?.Invoke() ?? default);
			detailEditorDropdown.RegisterValueChangedCallback(evt =>
			{
				SelectionDetailEditorModel editor = activeDetailModel?.Editor;
				if (editor == null)
					return;

				int index = editor.DropdownChoices.IndexOf(evt.newValue);
				if (index >= 0)
					editor.DropdownChanged?.Invoke(index);
			});
			detailEditorPrimaryAction.clicked += () => activeDetailModel?.Editor?.PrimaryAction?.Invoke();
			detailEditorSecondaryAction.clicked += () => activeDetailModel?.Editor?.SecondaryAction?.Invoke();
		}

		private void ToggleDetailTab(int tabIndex)
		{
			if (inspectorExpanded == false || tabIndex < 0 || tabIndex >= inspectorTabs.Count)
				return;

			if (activeTabIndex == tabIndex)
			{
				CloseDetailPanel();
				return;
			}

			CloseDetailPanel();
			activeTabIndex = tabIndex;
			activeDetailVersion = int.MinValue;
			inspectorTabs[tabIndex].Button.AddToClassList("selection-card__detail-tab--active");
			detailPanel.AddToClassList("selection-detail-panel--open");
			detailPanel.style.height = expandedCardHeight;
			detailPanel.pickingMode = PickingMode.Position;
			RefreshDetailPanel();
		}

		private void RefreshDetailPanel()
		{
			if (activeTabIndex < 0 || activeTabIndex >= inspectorTabs.Count)
				return;

			SelectionInspectorTab tab = inspectorTabs[activeTabIndex].Tab;
			int nextVersion = tab.GetContentVersion?.Invoke() ?? 0;
			if (activeDetailModel != null && nextVersion == activeDetailVersion)
				return;

			activeDetailVersion = nextVersion;
			activeDetailModel = tab.BuildContent?.Invoke() ?? new SelectionDetailPanelModel();
			detailTitle.text = activeDetailModel.Title ?? tab.Label;
			detailSummary.text = activeDetailModel.Summary ?? string.Empty;
			detailPanel.style.height = activeDetailModel.Editor != null ? 400 : expandedCardHeight;
			detailSliderControl.style.display = activeDetailModel.HasSlider ? DisplayStyle.Flex : DisplayStyle.None;
			if (activeDetailModel.HasSlider)
			{
				detailSliderLabel.text = activeDetailModel.SliderLabel ?? string.Empty;
				detailSlider.lowValue = activeDetailModel.SliderLowValue;
				detailSlider.highValue = activeDetailModel.SliderHighValue;
				detailSlider.SetValueWithoutNotify(activeDetailModel.SliderValue);
				detailSlider.SetEnabled(activeDetailModel.SliderEnabled);
				detailSliderValue.text = $"{activeDetailModel.SliderValue:0}%";
			}
			RefreshDetailEditor(activeDetailModel.Editor);
			detailList.itemsSource = activeDetailModel.Rows;
			detailList.Rebuild();
			bool isEmpty = activeDetailModel.Rows.Count == 0;
			detailList.style.display = isEmpty ? DisplayStyle.None : DisplayStyle.Flex;
			detailEmpty.style.display = isEmpty && activeDetailModel.Editor == null ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private void RefreshDetailEditor(SelectionDetailEditorModel editor)
		{
			detailEditor.style.display = editor != null ? DisplayStyle.Flex : DisplayStyle.None;
			if (editor == null)
				return;

			detailEditorMessage.text = editor.Message ?? string.Empty;
			detailEditorMessage.style.display = string.IsNullOrWhiteSpace(editor.Message) ? DisplayStyle.None : DisplayStyle.Flex;
			detailEditorDropdownLabel.text = editor.DropdownLabel ?? string.Empty;
			detailEditorDropdown.choices = editor.DropdownChoices ?? new List<string>();
			if (detailEditorDropdown.choices.Count > 0)
			{
				int index = UnityEngine.Mathf.Clamp(editor.DropdownIndex, 0, detailEditorDropdown.choices.Count - 1);
				detailEditorDropdown.SetValueWithoutNotify(detailEditorDropdown.choices[index]);
			}
			else
			{
				detailEditorDropdown.SetValueWithoutNotify(string.Empty);
			}
			detailEditorDropdown.SetEnabled(editor.DropdownEnabled);
			detailEditorToggleLabel.text = editor.ToggleLabel ?? string.Empty;
			detailEditorToggles.Clear();
			for (int i = 0; i < editor.Toggles.Count; ++i)
			{
				SelectionDetailToggleModel toggleModel = editor.Toggles[i];
				Toggle toggle = new(toggleModel.Label) { value = toggleModel.Value };
				toggle.AddToClassList("selection-detail-editor__toggle");
				toggle.SetEnabled(toggleModel.Enabled);
				toggle.RegisterValueChangedCallback(evt => toggleModel.Changed?.Invoke(evt.newValue));
				detailEditorToggles.Add(toggle);
			}

			ConfigureEditorAction(detailEditorPrimaryAction, editor.PrimaryActionLabel, editor.PrimaryAction);
			ConfigureEditorAction(detailEditorSecondaryAction, editor.SecondaryActionLabel, editor.SecondaryAction);
			bool hasActions =
				(string.IsNullOrWhiteSpace(editor.PrimaryActionLabel) == false && editor.PrimaryAction != null) ||
				(string.IsNullOrWhiteSpace(editor.SecondaryActionLabel) == false && editor.SecondaryAction != null);
			detailEditorActions.style.display = hasActions ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private static void ConfigureEditorAction(Button button, string label, Action action)
		{
			bool visible = string.IsNullOrWhiteSpace(label) == false && action != null;
			button.text = label ?? string.Empty;
			button.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private void CloseDetailPanel()
		{
			if (activeTabIndex >= 0 && activeTabIndex < inspectorTabs.Count)
				inspectorTabs[activeTabIndex].Button.RemoveFromClassList("selection-card__detail-tab--active");

			activeTabIndex = -1;
			activeDetailVersion = int.MinValue;
			activeDetailModel = null;
			if (detailList != null)
			{
				detailList.itemsSource = null;
				detailList.Rebuild();
			}
			if (detailPanel != null)
			{
				detailPanel.RemoveFromClassList("selection-detail-panel--open");
				detailPanel.pickingMode = PickingMode.Ignore;
			}
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
