using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class DebugControlWindow : MonoBehaviour
	{
		private const int ExplosionTabIndex = 0;
		private const int FireTabIndex = 1;
		private const int TemperatureTabIndex = 2;
		private const int DamageTabIndex = 3;
		private const int WorkerTabIndex = 4;
		private const int ItemTabIndex = 5;
		private const int MoneyTabIndex = 6;
		private const int ResearchTabIndex = 7;
		private const string ExplosionTabName = "debug-explosion-tab";
		private const string FireTabName = "debug-fire-tab";
		private const string TemperatureTabName = "debug-temperature-tab";
		private const string DamageTabName = "debug-damage-tab";
		private const string WorkerTabName = "debug-worker-tab";
		private const string ItemTabName = "debug-item-tab";
		private const string MoneyTabName = "debug-money-tab";
		private const string ResearchTabName = "debug-research-tab";

		private static readonly string[] TabNames =
		{
			ExplosionTabName,
			FireTabName,
			TemperatureTabName,
			DamageTabName,
			WorkerTabName,
			ItemTabName,
			MoneyTabName,
			ResearchTabName,
		};

		private UIWindow window;
		private VisualTreeAsset contentTemplate;
		private IntegerField explosionRadiusField;
		private IntegerField explosionSeverityField;
		private IntegerField fireIntensityField;
		private FloatField temperatureCelsiusField;
		private FloatField damageAmountField;
		private Label explosionMessage;
		private Label fireMessage;
		private Label temperatureMessage;
		private Label damageMessage;
		private Label workerMessage;
		private Label itemSelection;
		private Label itemEmpty;
		private Label itemMessage;
		private ScrollView itemList;
		private Button itemRefreshButton;
		private DropdownField itemGrantItemField;
		private IntegerField itemGrantQuantityField;
		private Button itemGrantButton;
		private Label moneyCurrentLabel;
		private IntegerField moneyValueField;
		private Button moneyApplyButton;
		private Label moneyMessage;
		private DropdownField researchSelectField;
		private Button researchCompleteButton;
		private Button researchReturnButton;
		private Label researchMessage;
		private readonly List<ItemDefinition> grantItems = new();
		private readonly List<ResearchDefinition> researchDefinitions = new();
		private InteractionContext interaction;
		private EconomyService economyService;
		private ResearchService researchService;
		private GameObject inspectedItemTarget;
		private IItemContainer inspectedItemContainer;
		private string inspectedItemContainerName;
		// The VisualElement graph is recreated by UIDocument and cannot survive a domain reload.
		[System.NonSerialized] private bool initialized;
		private bool started;

		public void Configure(UIWindow targetWindow, VisualTreeAsset targetContentTemplate)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
		}

		private void OnEnable()
		{
			InitializeView();
			if (started)
				BindServices();
		}

		private void Start()
		{
			started = true;
			BindServices();
		}

		private void Update()
		{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
			if (Input.GetKeyDown(KeyCode.F1))
				Toggle();
#endif
		}

		private void OnDisable()
		{
			UnbindServices();
		}

		private bool InitializeView()
		{
			if (initialized)
				return true;

			if (window == null || contentTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[DebugControl] Window or content template is missing.", this);
				return false;
			}

			TemplateContainer explosionContent = CreateTabContent(ExplosionTabName);
			TemplateContainer fireContent = CreateTabContent(FireTabName);
			TemplateContainer temperatureContent = CreateTabContent(TemperatureTabName);
			TemplateContainer damageContent = CreateTabContent(DamageTabName);
			TemplateContainer workerContent = CreateTabContent(WorkerTabName);
			TemplateContainer itemContent = CreateTabContent(ItemTabName);
			TemplateContainer moneyContent = CreateTabContent(MoneyTabName);
			TemplateContainer researchContent = CreateTabContent(ResearchTabName);
			if (explosionContent == null || fireContent == null || temperatureContent == null ||
				damageContent == null || workerContent == null || itemContent == null || moneyContent == null ||
				researchContent == null)
			{
				Debug.LogError("[DebugControl] Required tab roots are missing.", this);
				return false;
			}

			explosionRadiusField = explosionContent.Q<IntegerField>("debug-explosion-radius");
			explosionSeverityField = explosionContent.Q<IntegerField>("debug-explosion-severity");
			fireIntensityField = fireContent.Q<IntegerField>("debug-fire-intensity");
			temperatureCelsiusField = temperatureContent.Q<FloatField>("debug-temperature-celsius");
			damageAmountField = damageContent.Q<FloatField>("debug-damage-amount");
			explosionMessage = explosionContent.Q<Label>("debug-explosion-message");
			fireMessage = fireContent.Q<Label>("debug-fire-message");
			temperatureMessage = temperatureContent.Q<Label>("debug-temperature-message");
			damageMessage = damageContent.Q<Label>("debug-damage-message");
			workerMessage = workerContent.Q<Label>("debug-worker-message");
			itemSelection = itemContent.Q<Label>("debug-item-selection");
			itemEmpty = itemContent.Q<Label>("debug-item-empty");
			itemMessage = itemContent.Q<Label>("debug-item-message");
			itemList = itemContent.Q<ScrollView>("debug-item-list");
			itemRefreshButton = itemContent.Q<Button>("debug-item-refresh");
			itemGrantItemField = itemContent.Q<DropdownField>("debug-item-grant-item");
			itemGrantQuantityField = itemContent.Q<IntegerField>("debug-item-grant-quantity");
			itemGrantButton = itemContent.Q<Button>("debug-item-grant-button");
			moneyCurrentLabel = moneyContent.Q<Label>("debug-money-current");
			moneyValueField = moneyContent.Q<IntegerField>("debug-money-value");
			moneyApplyButton = moneyContent.Q<Button>("debug-money-apply");
			moneyMessage = moneyContent.Q<Label>("debug-money-message");
			researchSelectField = researchContent.Q<DropdownField>("debug-research-select");
			researchCompleteButton = researchContent.Q<Button>("debug-research-complete");
			researchReturnButton = researchContent.Q<Button>("debug-research-return");
			researchMessage = researchContent.Q<Label>("debug-research-message");
			if (explosionRadiusField == null || explosionSeverityField == null || fireIntensityField == null ||
				temperatureCelsiusField == null || damageAmountField == null || explosionMessage == null ||
				fireMessage == null || temperatureMessage == null || damageMessage == null || workerMessage == null ||
				itemSelection == null || itemEmpty == null || itemMessage == null || itemList == null ||
				itemRefreshButton == null || itemGrantItemField == null || itemGrantQuantityField == null ||
				itemGrantButton == null || moneyCurrentLabel == null || moneyValueField == null ||
				moneyApplyButton == null || moneyMessage == null || researchSelectField == null ||
				researchCompleteButton == null || researchReturnButton == null || researchMessage == null)
			{
				Debug.LogError("[DebugControl] Required controls are missing.", this);
				return false;
			}

			explosionRadiusField.RegisterValueChangedCallback(evt =>
				explosionRadiusField.SetValueWithoutNotify(Mathf.Max(0, evt.newValue)));
			explosionSeverityField.RegisterValueChangedCallback(evt =>
				explosionSeverityField.SetValueWithoutNotify(Mathf.Clamp(evt.newValue, 1, 100)));
			fireIntensityField.RegisterValueChangedCallback(evt =>
				fireIntensityField.SetValueWithoutNotify(Mathf.Clamp(evt.newValue, 1, 100)));
			temperatureCelsiusField.RegisterValueChangedCallback(evt =>
			{
				float value = float.IsNaN(evt.newValue) || float.IsInfinity(evt.newValue)
					? GridCell.DefaultTemperatureCelsius
					: Mathf.Max(-273.15f, evt.newValue);
				temperatureCelsiusField.SetValueWithoutNotify(value);
			});
			damageAmountField.RegisterValueChangedCallback(evt =>
			{
				float value = float.IsNaN(evt.newValue) || float.IsInfinity(evt.newValue)
					? 1.0f
					: Mathf.Max(0.0f, evt.newValue);
				damageAmountField.SetValueWithoutNotify(value);
			});
			itemRefreshButton.clicked += RefreshInspectedItems;
			itemGrantQuantityField.RegisterValueChangedCallback(evt =>
				itemGrantQuantityField.SetValueWithoutNotify(Mathf.Max(1, evt.newValue)));
			itemGrantButton.clicked += GiveSelectedItem;
			itemGrantButton.SetEnabled(false);
			moneyApplyButton.clicked += ApplyMoney;
			researchSelectField.RegisterValueChangedCallback(_ => RefreshResearchActions());
			researchCompleteButton.clicked += CompleteSelectedResearch;
			researchReturnButton.clicked += ReturnSelectedResearch;

			window.SetTitle("Debug Controls");
			window.ClearTabs();
			window.AddTab("Explosion", explosionContent);
			window.AddTab("Fire", fireContent);
			window.AddTab("Temperature", temperatureContent);
			window.AddTab("Damage", damageContent);
			window.AddTab("Worker", workerContent);
			window.AddTab("Item", itemContent);
			window.AddTab("Money", moneyContent);
			window.AddTab("Research", researchContent);
			window.SelectTab(ExplosionTabIndex);
			initialized = true;
			return true;
		}

		private TemplateContainer CreateTabContent(string selectedTabName)
		{
			TemplateContainer content = contentTemplate.CloneTree();
			if (content.Q<VisualElement>(selectedTabName) == null)
				return null;

			for (int i = 0; i < TabNames.Length; ++i)
			{
				if (TabNames[i] == selectedTabName)
					continue;

				content.Q<VisualElement>(TabNames[i])?.RemoveFromHierarchy();
			}

			return content;
		}

		private void Toggle()
		{
			if (InitializeView() == false)
				return;

			if (window.IsOpen)
			{
				window.Close();
				return;
			}

			BindServices();
			window.Open();
		}

		private void BindServices()
		{
			UnbindServices();
			if (GameContext.HasInstance == false)
				return;

			interaction = GameContext.Instance.InteractionCtx;
			economyService = GameContext.Instance.EconomyService;
			researchService = GameContext.Instance.ResearchService;
			if (interaction != null)
				interaction.OnHandlePriorityLeftClick += HandleWorldClick;
			if (economyService != null)
			{
				economyService.OnMoneyChanged += OnMoneyChanged;
				OnMoneyChanged(economyService.Money);
			}
			if (researchService != null)
				researchService.OnResearchStateChanged += RefreshResearchChoices;

			RefreshGrantItemChoices();
			RefreshResearchChoices();
		}

		private void UnbindServices()
		{
			if (interaction != null)
				interaction.OnHandlePriorityLeftClick -= HandleWorldClick;
			if (economyService != null)
				economyService.OnMoneyChanged -= OnMoneyChanged;
			if (researchService != null)
				researchService.OnResearchStateChanged -= RefreshResearchChoices;

			interaction = null;
			economyService = null;
			researchService = null;
		}

		private bool HandleWorldClick(int3 position)
		{
			if (window == null || window.IsOpen == false)
				return false;

			switch (window.SelectedTabIndex)
			{
				case ExplosionTabIndex:
					TriggerExplosion(in position);
					break;

				case FireTabIndex:
					ApplyFire(in position);
					break;

				case TemperatureTabIndex:
					SetTemperature(in position);
					break;

				case DamageTabIndex:
					ApplyDamage(in position);
					break;

				case WorkerTabIndex:
					KnockoutWorker(in position);
					break;

				case ItemTabIndex:
					InspectItemContainer(in position);
					break;

				case MoneyTabIndex:
				case ResearchTabIndex:
					return false;
			}

			return true;
		}

		private void ApplyMoney()
		{
			if (economyService == null)
			{
				Report(moneyMessage, "EconomyService is unavailable.", LogType.Warning);
				return;
			}

			int target = moneyValueField.value;
			int previous = economyService.Money;
			long delta = (long)target - previous;
			if (delta < int.MinValue || delta > int.MaxValue)
			{
				Report(moneyMessage, "The requested adjustment is outside the supported transaction range.", LogType.Warning);
				return;
			}

			if (delta == 0)
			{
				Report(moneyMessage, $"Money is already ${target:N0}.");
				return;
			}

			economyService.ApplyTransaction(new EconomyTransaction
			{
				moneyDelta = (int)delta,
				reputationDelta = 0f,
				reason = EconomyTransaction.Reason.DebugAdjustment,
			});
			Report(moneyMessage, $"Money set: ${previous:N0} -> ${economyService.Money:N0}.");
		}

		private void OnMoneyChanged(int value)
		{
			if (moneyCurrentLabel != null)
				moneyCurrentLabel.text = $"Current Money: ${value:N0}";
			if (moneyValueField != null)
				moneyValueField.SetValueWithoutNotify(value);
		}

		private void RefreshResearchChoices()
		{
			string selectedId = GetSelectedResearch()?.Uid;
			researchDefinitions.Clear();
			List<string> choices = new();
			IReadOnlyList<ResearchDefinition> definitions = researchService?.Definitions;
			if (definitions != null)
			{
				for (int i = 0; i < definitions.Count; ++i)
				{
					ResearchDefinition definition = definitions[i];
					if (definition == null)
						continue;

					researchDefinitions.Add(definition);
					choices.Add($"{definition.DisplayName} [{researchService.GetState(definition.Uid)}]");
				}
			}

			researchSelectField.choices = choices;
			int selectedIndex = researchDefinitions.FindIndex(definition => definition.Uid == selectedId);
			researchSelectField.index = selectedIndex >= 0 ? selectedIndex : choices.Count > 0 ? 0 : -1;
			RefreshResearchActions();
		}

		private void RefreshResearchActions()
		{
			ResearchDefinition definition = GetSelectedResearch();
			bool completed = definition != null && researchService?.IsResearched(definition.Uid) == true;
			researchCompleteButton.SetEnabled(definition != null && completed == false);
			researchReturnButton.SetEnabled(completed);
		}

		private ResearchDefinition GetSelectedResearch()
		{
			int index = researchSelectField?.index ?? -1;
			return index >= 0 && index < researchDefinitions.Count ? researchDefinitions[index] : null;
		}

		private void CompleteSelectedResearch()
		{
			ResearchDefinition definition = GetSelectedResearch();
			if (definition == null || researchService == null)
			{
				Report(researchMessage, "Select a research to complete.", LogType.Warning);
				return;
			}

			if (researchService.TryCompleteResearch(definition.Uid) == false)
			{
				Report(researchMessage, "Research is already completed or unavailable.", LogType.Warning);
				return;
			}

			Report(researchMessage, $"Completed research: {definition.DisplayName}.");
		}

		private void ReturnSelectedResearch()
		{
			ResearchDefinition definition = GetSelectedResearch();
			if (definition == null || researchService == null)
			{
				Report(researchMessage, "Select a research to return.", LogType.Warning);
				return;
			}

			if (researchService.TryReturnResearch(definition.Uid, out ResearchReturnFailureReason reason) == false)
			{
				string message = reason == ResearchReturnFailureReason.RequiredByPlannedResearch
					? "Return its completed, active, or queued dependent research first."
					: reason == ResearchReturnFailureReason.NotCompleted
						? "Only completed research can be returned."
						: "Research return failed.";
				Report(researchMessage, message, LogType.Warning);
				return;
			}

			Report(researchMessage, $"Returned research: {definition.DisplayName}.");
		}

		private void TriggerExplosion(in int3 position)
		{
			int radius = Mathf.Max(0, explosionRadiusField.value);
			int severity = Mathf.Clamp(explosionSeverityField.value, 1, 100);
			ExplosionService explosion = GameContext.HasInstance ? GameContext.Instance.ExplosionSvc : null;
			if (explosion == null || explosion.TryEnqueueDebugExplosion(in position, radius, severity) == false)
			{
				Report(explosionMessage,
					$"Explosion request failed at {FormatPosition(in position)}.",
					LogType.Warning);
				return;
			}

			Report(explosionMessage,
				$"Explosion queued at {FormatPosition(in position)}. Radius {radius}, severity {severity}.");
		}

		private void ApplyDamage(in int3 position)
		{
			if (TryResolveTarget(in position, out GameObject target) == false)
			{
				Report(damageMessage, $"No GridPlaceable at {FormatPosition(in position)}.", LogType.Warning);
				return;
			}

			if (target.TryGetComponent<IHealth>(out var health) == false)
			{
				Report(damageMessage, $"{target.name} does not support damage.", LogType.Warning);
				return;
			}

			float amount = damageAmountField.value;
			if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0.0f)
			{
				Report(damageMessage, "Damage must be greater than zero.", LogType.Warning);
				return;
			}

			string targetName = target.name;
			float previousHealth = health.Health;
			float applied = health.ApplyDamage(amount);
			float currentHealth = health.Health;
			bool destroyed = false;
			if (currentHealth <= 0.0f && target.TryGetComponent<IFacility>(out var facility))
			{
				DestroyContext destroyContext = new(DestroyContext.Destroycause.Damage);
				destroyed = GameContext.Instance.FacilityMgr?.DestroyFacility(facility, in destroyContext) == true;
			}

			string suffix = destroyed ? " Destroyed." : string.Empty;
			Report(damageMessage,
				$"{targetName} damaged by {applied:0.##} HP: {previousHealth:0.##} -> " +
				$"{currentHealth:0.##}/{health.MaxHealth:0.##} HP.{suffix}");
		}

		private void SetTemperature(in int3 position)
		{
			GridService gridService = GameContext.HasInstance ? GameContext.Instance.GridService : null;
			GridCell cell = gridService?.GetCell(position);
			if (cell == null)
			{
				Report(temperatureMessage,
					$"No grid cell at {FormatPosition(in position)}.",
					LogType.Warning);
				return;
			}

			float temperature = temperatureCelsiusField.value;
			if (float.IsNaN(temperature) || float.IsInfinity(temperature))
			{
				Report(temperatureMessage, "Temperature must be a finite Celsius value.", LogType.Warning);
				return;
			}

			temperature = Mathf.Max(-273.15f, temperature);
			float previous = cell.TemperatureCelsius;
			gridService.TrySetTemperature(in position, temperature);
			Report(temperatureMessage,
				$"Temperature at {FormatPosition(in position)}: {previous:0.##}°C -> {cell.TemperatureCelsius:0.##}°C.");
		}

		private void ApplyFire(in int3 position)
		{
			int intensity = Mathf.Clamp(fireIntensityField.value, 1, 100);
			FireService fireService = GameContext.HasInstance ? GameContext.Instance.FireSvc : null;
			if (fireService == null ||
				fireService.TryApplyDebugFire(in position, intensity, out int affectedTargets) == false)
			{
				Report(fireMessage,
					$"No IGridPlaceable accepted fire at {FormatPosition(in position)}.",
					LogType.Warning);
				return;
			}

			Report(fireMessage,
				$"Applied Fire {intensity}% to {affectedTargets} target(s) at {FormatPosition(in position)}.");
		}

		private void KnockoutWorker(in int3 position)
		{
			if (TryResolveTarget(in position, out GameObject target) == false ||
				target.TryGetComponent<AIWorker>(out var worker) == false)
			{
				Report(workerMessage, $"No worker at {FormatPosition(in position)}.", LogType.Warning);
				return;
			}

			string workerName = worker.Name;
			string taskName = worker.CurrentTask != null ? worker.CurrentTask.GetType().Name : "None";
			WorkerOperationalState previousState = worker.OperationalState;
			if (worker.EnterIncapacitatedState(WorkerOperationalState.Knockout) == false)
			{
				Report(workerMessage,
					$"{workerName} knockout rejected. Current state: {worker.OperationalState}.",
					LogType.Warning);
				return;
			}

			Report(workerMessage,
				$"{workerName}: {previousState} -> Knockout. Returned task: {taskName}.");
		}

		private void InspectItemContainer(in int3 position)
		{
			ClearInspectedItems();
			if (TryResolveTarget(in position, out GameObject target) == false)
			{
				Report(itemMessage, $"No worker or item container at {FormatPosition(in position)}.", LogType.Warning);
				return;
			}

			if (target.TryGetComponent<AIWorker>(out var worker))
			{
				BoxBase carryingBox = worker.CarryingAbility?.CarryingBox;
				if (carryingBox == null)
				{
					Report(itemMessage, $"{worker.Name} is not carrying a box.", LogType.Warning);
					return;
				}

				inspectedItemTarget = target;
				inspectedItemContainer = carryingBox;
				inspectedItemContainerName = $"{worker.Name} / {carryingBox.name} #{carryingBox.BoxId}";
				RefreshInspectedItems();
				Report(itemMessage, $"Inspecting {inspectedItemContainerName} at {FormatPosition(in position)}.");
				return;
			}

			if (target.TryGetComponent<IItemContainer>(out var container) == false)
			{
				Report(itemMessage, $"{target.name} is not a direct item container.", LogType.Warning);
				return;
			}

			inspectedItemTarget = target;
			inspectedItemContainer = container;
			inspectedItemContainerName = target.name;
			RefreshInspectedItems();
			Report(itemMessage, $"Inspecting {inspectedItemContainerName} at {FormatPosition(in position)}.");
		}

		private void ClearInspectedItems()
		{
			inspectedItemTarget = null;
			inspectedItemContainer = null;
			inspectedItemContainerName = null;
			itemGrantButton?.SetEnabled(false);
			if (itemSelection != null)
				itemSelection.text = "Selected: None";
			if (itemList != null)
				itemList.contentContainer.Clear();
			if (itemEmpty != null && itemList != null)
			{
				itemEmpty.text = "Select an item container.";
				itemEmpty.style.display = DisplayStyle.Flex;
				itemList.contentContainer.Add(itemEmpty);
			}
		}

		private void RefreshInspectedItems()
		{
			if (TryValidateInspectedContainer(out int3 position) == false)
			{
				ClearInspectedItems();
				Report(itemMessage, "Container changed. Select it again.", LogType.Warning);
				return;
			}

			itemSelection.text = $"Selected: {inspectedItemContainerName}  {FormatPosition(in position)}";
			itemGrantButton.SetEnabled(grantItems.Count > 0);
			itemList.contentContainer.Clear();
			int rowCount = 0;
			IReadOnlyList<ItemStack> stacks = inspectedItemContainer.Stacks;
			for (int i = 0; i < stacks.Count; ++i)
			{
				ItemStack stack = stacks[i];
				if (stack == null || stack.Quantity <= 0)
					continue;

				itemList.contentContainer.Add(CreateItemRow(stack));
				++rowCount;
			}

			itemEmpty.text = rowCount == 0 ? "Container is empty." : string.Empty;
			itemEmpty.style.display = rowCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			itemList.contentContainer.Add(itemEmpty);
		}

		private void RefreshGrantItemChoices()
		{
			grantItems.Clear();
			List<string> choices = new();
			IReadOnlyList<ItemDefinition> definitions = GameContext.HasInstance
				? GameContext.Instance.ItemDB?.OrderedItems
				: null;

			if (definitions != null)
			{
				for (int i = 0; i < definitions.Count; ++i)
				{
					ItemDefinition definition = definitions[i];
					if (definition == null)
						continue;

					grantItems.Add(definition);
					choices.Add($"{definition.name} [{definition.ItemID}]");
				}
			}

			itemGrantItemField.choices = choices;
			itemGrantItemField.index = choices.Count > 0 ? 0 : -1;
			itemGrantButton.SetEnabled(inspectedItemContainer != null && grantItems.Count > 0);
		}

		private void GiveSelectedItem()
		{
			if (TryValidateInspectedContainer(out _) == false)
			{
				ClearInspectedItems();
				Report(itemMessage, "Container changed. Select it again.", LogType.Warning);
				return;
			}

			int selectedIndex = itemGrantItemField.index;
			if (selectedIndex < 0 || selectedIndex >= grantItems.Count)
			{
				Report(itemMessage, "Select an item to give.", LogType.Warning);
				return;
			}

			ItemDefinition item = grantItems[selectedIndex];
			int requested = Mathf.Max(1, itemGrantQuantityField.value);
			if (inspectedItemTarget.TryGetComponent<IFacility>(out var facility))
			{
				FacilityItemFilter itemFilter = new(
					item.Tag,
					new[] { item },
					new[] { ItemStatus.None });
				FacilityFilter filter = new(itemFilter: itemFilter);
				if (filter.MatchesCurrentRules(facility) == false)
				{
					Report(itemMessage,
						$"{item.name} [{item.ItemID}] rejected by {inspectedItemContainerName} Rule.",
						LogType.Warning);
					return;
				}
			}

			int added = inspectedItemContainer.AddItem(item.ItemID, requested);
			if (added <= 0)
			{
				Report(itemMessage,
					$"{inspectedItemContainerName} could not accept {item.name} [{item.ItemID}].",
					LogType.Warning);
				return;
			}

			Report(itemMessage,
				$"Added {item.name} [{item.ItemID}] x{added} to {inspectedItemContainerName}. Requested={requested}.");
			RefreshInspectedItems();
		}

		private VisualElement CreateItemRow(ItemStack stack)
		{
			VisualElement row = new();
			row.AddToClassList("debug-item-row");

			VisualElement header = new();
			header.AddToClassList("debug-item-row-header");
			Label name = new(ResolveItemName(stack.ItemID));
			name.AddToClassList("debug-item-row-name");
			Label quantity = new($"x{stack.Quantity}");
			quantity.AddToClassList("debug-item-row-quantity");
			header.Add(name);
			header.Add(quantity);
			row.Add(header);

			bool usesFreshness = UsesFreshness(stack.ItemID);
			row.Add(CreateConditionRow(
				"Freshness",
				usesFreshness ? stack.FreshnessPercent.ToString() : "N/A",
				usesFreshness,
				target => AdjustFreshness(stack, target),
				stack.FreshnessPercent));
			row.Add(CreateConditionRow(
				"Damage",
				stack.DamagePercent.ToString(),
				true,
				target => AdjustDamage(stack, target),
				stack.DamagePercent));
			return row;
		}

		private static VisualElement CreateConditionRow(
			string conditionName,
			string conditionValue,
			bool enabled,
			System.Action<int> setValue,
			int currentValue)
		{
			VisualElement row = new();
			row.AddToClassList("debug-item-condition-row");
			Label name = new(conditionName);
			name.AddToClassList("debug-item-condition-name");
			Label value = new(conditionValue);
			value.AddToClassList("debug-item-condition-value");
			row.Add(name);
			row.Add(value);
			row.Add(CreateAdjustButton("-10", enabled, () => setValue(currentValue - 10)));
			row.Add(CreateAdjustButton("+10", enabled, () => setValue(currentValue + 10)));
			row.Add(CreateAdjustButton("0", enabled, () => setValue(0)));
			row.Add(CreateAdjustButton("100", enabled, () => setValue(100)));
			return row;
		}

		private static Button CreateAdjustButton(string text, bool enabled, System.Action clicked)
		{
			Button button = new(clicked) { text = text };
			button.AddToClassList("debug-item-adjust-button");
			button.SetEnabled(enabled);
			return button;
		}

		private void AdjustFreshness(ItemStack stack, int targetFreshness)
		{
			if (TryValidateInspectedStack(stack, out _) == false)
			{
				Report(itemMessage, "Container contents changed. Select or refresh it again.", LogType.Warning);
				RefreshInspectedItems();
				return;
			}

			int previous = stack.FreshnessPercent;
			int current = Mathf.Clamp(targetFreshness, 0, 100);
			if (current == previous)
				return;

			stack.SetCurrentFreshness(stack.MaximumFreshness * current / 100.0f);
			Report(itemMessage,
				$"{ResolveItemName(stack.ItemID)} x{stack.Quantity} Freshness {previous} -> {current}, Container={inspectedItemContainerName}.");
			RefreshInspectedItems();
		}

		private void AdjustDamage(ItemStack stack, int targetDamage)
		{
			if (TryValidateInspectedStack(stack, out int3 position) == false)
			{
				Report(itemMessage, "Container contents changed. Select or refresh it again.", LogType.Warning);
				RefreshInspectedItems();
				return;
			}

			ItemDamageService damageService = GameContext.HasInstance ? GameContext.Instance.ItemDamage : null;
			if (damageService == null || damageService.TrySetDebugDamage(
				stack,
				targetDamage,
				in position,
				inspectedItemContainer,
				out ItemDamageChange change) == false)
			{
				return;
			}

			Report(itemMessage,
				$"{ResolveItemName(stack.ItemID)} x{stack.Quantity} Damage {change.PreviousDamage:0.##} -> {change.CurrentDamage:0.##} " +
				$"at {FormatPosition(in position)}, Container={inspectedItemContainerName}.");
			RefreshInspectedItems();
		}

		private bool TryValidateInspectedStack(ItemStack stack, out int3 position)
		{
			if (TryValidateInspectedContainer(out position) == false || stack == null)
				return false;

			IReadOnlyList<ItemStack> stacks = inspectedItemContainer.Stacks;
			for (int i = 0; i < stacks.Count; ++i)
			{
				if (ReferenceEquals(stacks[i], stack))
					return true;
			}

			return false;
		}

		private bool TryValidateInspectedContainer(out int3 position)
		{
			position = default;
			if (inspectedItemTarget == null || inspectedItemContainer == null)
				return false;

			if (inspectedItemTarget.TryGetComponent<AIWorker>(out var worker))
			{
				if (ReferenceEquals(worker.CarryingAbility?.CarryingBox, inspectedItemContainer) == false)
					return false;

				position = worker.GridPosition;
				return true;
			}

			if (inspectedItemTarget.TryGetComponent<IItemContainer>(out var currentContainer) == false ||
				ReferenceEquals(currentContainer, inspectedItemContainer) == false ||
				inspectedItemTarget.TryGetComponent<IGridPlaceable>(out var placeable) == false)
			{
				return false;
			}

			position = placeable.GridPosition;
			return true;
		}

		private static string ResolveItemName(uint itemId)
		{
			if (GameContext.HasInstance == false || GameContext.Instance.ItemDB == null)
				return $"Item {itemId}";

			return GameContext.Instance.ItemDB.GetItemData(itemId, out ItemDefinition definition) && definition != null
				? definition.name
				: $"Item {itemId}";
		}

		private static bool UsesFreshness(uint itemId)
		{
			return GameContext.HasInstance &&
				GameContext.Instance.ItemDB != null &&
				GameContext.Instance.ItemDB.GetItemData(itemId, out ItemDefinition definition) &&
				definition != null &&
				definition.UsesFreshness;
		}

		private static bool TryResolveTarget(in int3 position, out GameObject target)
		{
			target = null;
			if (GameContext.HasInstance == false)
				return false;

			GridService gridService = GameContext.Instance.GridService;
			GridCell cell = gridService?.GetCell(position);
			if (cell == null)
				return false;

			target = cell.OccupancyWorker != null
				? cell.OccupancyWorker.gameObject
				: cell.ObjectOnGrid != null
					? cell.ObjectOnGrid
					: cell.OccupancyObjectOnGrid;
			return target != null && target.TryGetComponent<IGridPlaceable>(out _);
		}

		private static string FormatPosition(in int3 position)
		{
			return $"({position.x},{position.y},{position.z})";
		}

		private static void Report(Label messageLabel, string message, LogType logType = LogType.Log)
		{
			if (messageLabel != null)
				messageLabel.text = message;

			switch (logType)
			{
				case LogType.Warning:
					Debug.LogWarning($"[DebugControl] {message}");
					break;

				case LogType.Error:
					Debug.LogError($"[DebugControl] {message}");
					break;

				default:
					Debug.Log($"[DebugControl] {message}");
					break;
			}
		}
	}
}
