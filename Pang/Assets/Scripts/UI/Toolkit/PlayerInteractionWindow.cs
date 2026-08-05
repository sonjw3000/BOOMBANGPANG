using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class PlayerInteractionWindow : MonoBehaviour
	{
		[SerializeField] private UIWindow window;
		[SerializeField] private VisualTreeAsset contentTemplate;
		[SerializeField] private VisualTreeAsset itemRowTemplate;
		private Label workerName;
		private Label workerState;
		private DropdownField targetDropdown;
		private Label availableKinds;
		private Label message;
		private Label carriedName;
		private Label carriedCapacity;
		private ScrollView carriedList;
		private Label carriedEmpty;
		private Button putBoxButton;
		private Label targetName;
		private Label targetCapacity;
		private ScrollView targetList;
		private Label targetEmpty;
		private Button pickBoxButton;
		private Button refreshButton;
		private Button releaseControlButton;
		private PlayerOverrideService playerOverrideService;
		private readonly List<PlayerInteractionTarget> interactionTargets = new();
		private readonly List<string> targetChoices = new();
		private AIWorker targetWorker;
		private int selectedTargetIndex = -1;
		private string statusMessage = string.Empty;
		[System.NonSerialized] private bool initialized;
		private bool started;

		public void Configure(
			UIWindow targetWindow,
			VisualTreeAsset targetContentTemplate,
			VisualTreeAsset targetItemRowTemplate)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
			itemRowTemplate = targetItemRowTemplate;
		}

		private void OnEnable()
		{
			InitializeView();
			if (started)
				BindService();
		}

		private void Start()
		{
			started = true;
			BindService();
		}

		private void Update()
		{
			if (window == null || window.IsOpen == false)
				return;

			if (targetWorker == null || targetWorker.IsPlayerOverride == false)
				window.Close();
		}

		private void OnDisable()
		{
			UnbindControls();
			UnbindService();
			targetWorker = null;
			interactionTargets.Clear();
			targetChoices.Clear();
			initialized = false;
		}

		public bool Open(AIWorker worker)
		{
			if (worker == null || worker.IsPlayerOverride == false || InitializeView() == false)
				return false;

			if (playerOverrideService == null)
				BindService();

			if (playerOverrideService == null)
				return false;

			targetWorker = worker;
			selectedTargetIndex = -1;
			statusMessage = string.Empty;
			window.SetTitle($"Player Interaction · {worker.Name}");
			RefreshAll();
			window.Open();
			return true;
		}

		private bool InitializeView()
		{
			if (initialized)
				return true;

			if (window == null || contentTemplate == null || itemRowTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[PlayerInteractionWindow] Window or templates are missing.", this);
				return false;
			}

			TemplateContainer content = contentTemplate.CloneTree();
			workerName = content.Q<Label>("player-interaction-worker-name");
			workerState = content.Q<Label>("player-interaction-worker-state");
			targetDropdown = content.Q<DropdownField>("player-interaction-target");
			availableKinds = content.Q<Label>("player-interaction-kinds");
			message = content.Q<Label>("player-interaction-message");
			carriedName = content.Q<Label>("player-interaction-carried-name");
			carriedCapacity = content.Q<Label>("player-interaction-carried-capacity");
			carriedList = content.Q<ScrollView>("player-interaction-carried-list");
			carriedEmpty = content.Q<Label>("player-interaction-carried-empty");
			putBoxButton = content.Q<Button>("player-interaction-put-box");
			targetName = content.Q<Label>("player-interaction-target-name");
			targetCapacity = content.Q<Label>("player-interaction-target-capacity");
			targetList = content.Q<ScrollView>("player-interaction-target-list");
			targetEmpty = content.Q<Label>("player-interaction-target-empty");
			pickBoxButton = content.Q<Button>("player-interaction-pick-box");
			refreshButton = content.Q<Button>("player-interaction-refresh");
			releaseControlButton = content.Q<Button>("player-interaction-release-control");

			if (workerName == null || workerState == null || targetDropdown == null || availableKinds == null ||
				message == null || carriedName == null || carriedCapacity == null || carriedList == null ||
				carriedEmpty == null || putBoxButton == null || targetName == null || targetCapacity == null ||
				targetList == null || targetEmpty == null || pickBoxButton == null || refreshButton == null ||
				releaseControlButton == null)
			{
				Debug.LogError("[PlayerInteractionWindow] Required UXML elements are missing.", this);
				return false;
			}

			window.SetTitle("Player Interaction");
			window.SetContent(content);
			window.Closed += OnWindowClosed;
			targetDropdown.RegisterValueChangedCallback(OnTargetChanged);
			refreshButton.clicked += RefreshAll;
			putBoxButton.clicked += RequestPutWholeBox;
			pickBoxButton.clicked += RequestPickWholeBox;
			releaseControlButton.clicked += ReleaseControl;
			initialized = true;
			return true;
		}

		private void UnbindControls()
		{
			if (window != null)
				window.Closed -= OnWindowClosed;
			if (targetDropdown != null)
				targetDropdown.UnregisterValueChangedCallback(OnTargetChanged);
			if (refreshButton != null)
				refreshButton.clicked -= RefreshAll;
			if (putBoxButton != null)
				putBoxButton.clicked -= RequestPutWholeBox;
			if (pickBoxButton != null)
				pickBoxButton.clicked -= RequestPickWholeBox;
			if (releaseControlButton != null)
				releaseControlButton.clicked -= ReleaseControl;
		}

		private void BindService()
		{
			UnbindService();
			if (GameContext.HasInstance == false)
				return;

			playerOverrideService = GameContext.Instance.PlayerOverrideSvc;
			if (playerOverrideService == null)
				return;

			playerOverrideService.OnWorkerStateChanged += OnWorkerStateChanged;
			playerOverrideService.OnInteractionWindowRequested += OnInteractionWindowRequested;
		}

		private void UnbindService()
		{
			if (playerOverrideService != null)
			{
				playerOverrideService.OnWorkerStateChanged -= OnWorkerStateChanged;
				playerOverrideService.OnInteractionWindowRequested -= OnInteractionWindowRequested;
			}

			playerOverrideService = null;
		}

		private void OnWorkerStateChanged(AIWorker worker)
		{
			if (worker == null || ReferenceEquals(worker, targetWorker) == false)
				return;

			if (worker.IsPlayerOverride == false)
			{
				window?.Close();
				return;
			}

			if (worker.PlayerOverridePhase == PlayerOverridePhase.AwaitingCommand)
				statusMessage = string.Empty;
			RefreshAll();
		}

		private void OnInteractionWindowRequested(AIWorker worker)
		{
			Open(worker);
		}

		private void OnWindowClosed()
		{
			targetWorker = null;
			selectedTargetIndex = -1;
			statusMessage = string.Empty;
			interactionTargets.Clear();
			targetChoices.Clear();
			carriedList?.Clear();
			targetList?.Clear();
		}

		private void OnTargetChanged(ChangeEvent<string> evt)
		{
			int index = targetChoices.IndexOf(evt.newValue);
			if (index < 0 || index >= interactionTargets.Count)
				return;

			selectedTargetIndex = index;
			statusMessage = string.Empty;
			RefreshContainerViews();
		}

		private void RefreshAll()
		{
			if (initialized == false || targetWorker == null)
				return;

			if (targetWorker.IsPlayerOverride == false)
			{
				window.Close();
				return;
			}

			workerName.text = targetWorker.Name;
			workerState.text = $"PLAYER OVERRIDE · {targetWorker.PlayerOverridePhase}";
			releaseControlButton.SetEnabled(true);
			RefreshTargets();
			RefreshContainerViews();
		}

		private void RefreshTargets()
		{
			int previousIndex = selectedTargetIndex;
			Component previousTarget = previousIndex >= 0 && previousIndex < interactionTargets.Count
				? interactionTargets[previousIndex].Component
				: null;
			interactionTargets.Clear();
			targetChoices.Clear();
			playerOverrideService?.GetInteractionTargets(targetWorker, interactionTargets);

			for (int i = 0; i < interactionTargets.Count; ++i)
			{
				string displayName = interactionTargets[i].DisplayName;
				targetChoices.Add(string.IsNullOrWhiteSpace(displayName)
					? $"Interaction Target {i + 1}"
					: $"{displayName} [{i + 1}]");
			}

			int restoredIndex = previousTarget != null
				? interactionTargets.FindIndex(target => target.Component == previousTarget)
				: -1;
			selectedTargetIndex = interactionTargets.Count <= 0
				? -1
				: restoredIndex >= 0
					? restoredIndex
					: Mathf.Clamp(previousIndex < 0 ? 0 : previousIndex, 0, interactionTargets.Count - 1);

			targetDropdown.choices = targetChoices;
			targetDropdown.SetEnabled(interactionTargets.Count > 1);
			targetDropdown.SetValueWithoutNotify(
				selectedTargetIndex >= 0 ? targetChoices[selectedTargetIndex] : "No interaction target");
		}

		private void RefreshContainerViews()
		{
			carriedList.Clear();
			targetList.Clear();

			BoxBase carriedBox = targetWorker?.CarryingAbility?.CarryingBox;
			IItemContainer carriedContainer = carriedBox;
			bool hasTarget = TryGetSelectedTarget(out PlayerInteractionTarget interactionTarget);
			IItemContainer facilityContainer = hasTarget ? interactionTarget.ResolveContainer() : null;
			bool canIssueCommand = targetWorker != null &&
				targetWorker.IsPlayerOverride &&
				targetWorker.PlayerOverridePhase == PlayerOverridePhase.AwaitingCommand;

			RefreshContainerHeader(carriedContainer, carriedName, carriedCapacity, "No carried container");
			RefreshContainerHeader(facilityContainer, targetName, targetCapacity,
				hasTarget ? interactionTarget.DisplayName : "No interaction target");

			bool manifestLocked = HasPickingManifest(carriedContainer) || HasPickingManifest(facilityContainer);
			bool canPutItems = hasTarget && canIssueCommand &&
				(interactionTarget.AvailableKinds & InteractionKind.Put) != 0 &&
				carriedContainer != null && facilityContainer != null && manifestLocked == false;
			bool canPickItems = hasTarget && canIssueCommand &&
				(interactionTarget.AvailableKinds & InteractionKind.Pick) != 0 &&
				facilityContainer != null && carriedContainer != null && manifestLocked == false;

			AddItemRows(carriedList, carriedContainer, interactionTarget, PlayerTransferDirection.PutToTarget, canPutItems, manifestLocked);
			AddItemRows(targetList, facilityContainer, interactionTarget, PlayerTransferDirection.PickFromTarget, canPickItems, manifestLocked);

			bool carriedHasItems = carriedContainer?.Stacks != null && carriedContainer.Stacks.Count > 0;
			bool targetHasItems = facilityContainer?.Stacks != null && facilityContainer.Stacks.Count > 0;
			carriedEmpty.text = carriedContainer == null
				? "The worker is not carrying a container."
				: "The carried container is empty.";
			carriedEmpty.style.display = carriedHasItems ? DisplayStyle.None : DisplayStyle.Flex;
			targetEmpty.text = facilityContainer == null
				? "No item container is available at this interaction point."
				: "The facility container is empty.";
			targetEmpty.style.display = targetHasItems ? DisplayStyle.None : DisplayStyle.Flex;

			bool canHandleWholeBox = hasTarget && interactionTarget.CanHandleWholeBox;
			putBoxButton.SetEnabled(
				canIssueCommand && canHandleWholeBox && carriedBox != null &&
				(interactionTarget.AvailableKinds & InteractionKind.Put) != 0);
			pickBoxButton.SetEnabled(
				canIssueCommand && canHandleWholeBox && carriedBox == null &&
				(interactionTarget.AvailableKinds & InteractionKind.Pick) != 0);
			putBoxButton.text = carriedBox != null ? $"Put {GetBoxName(carriedBox)}" : "Put Carried Container";
			pickBoxButton.text = "Pick Entire Container";
			availableKinds.text = hasTarget ? FormatInteractionKinds(interactionTarget.AvailableKinds) : "No interaction";

			if (string.IsNullOrWhiteSpace(statusMessage))
			{
				statusMessage = hasTarget == false
					? "No facility interaction is available on the worker's current cell."
					: manifestLocked
						? "Order cargo cannot be moved by item. Move the entire box or capsule."
						: canIssueCommand
							? "Choose an item quantity or move the entire container."
							: $"The worker is {targetWorker.PlayerOverridePhase}. Wait until it can accept a command.";
			}

			message.text = statusMessage;
		}

		private void AddItemRows(
			ScrollView list,
			IItemContainer container,
			PlayerInteractionTarget interactionTarget,
			PlayerTransferDirection direction,
			bool transferEnabled,
			bool manifestLocked)
		{
			if (container?.Stacks == null)
				return;

			for (int i = 0; i < container.Stacks.Count; ++i)
			{
				ItemStack stack = container.Stacks[i];
				if (stack == null || stack.Quantity <= 0)
					continue;

				TemplateContainer row = itemRowTemplate.CloneTree();
				Label name = row.Q<Label>("player-interaction-item-name");
				Label state = row.Q<Label>("player-interaction-item-state");
				Label quantity = row.Q<Label>("player-interaction-item-quantity");
				IntegerField moveQuantity = row.Q<IntegerField>("player-interaction-item-move-quantity");
				Button moveButton = row.Q<Button>("player-interaction-item-move");
				if (name == null || state == null || quantity == null || moveQuantity == null || moveButton == null)
					continue;

				name.text = GetItemName(stack.ItemID);
				state.text = manifestLocked
					? $"ORDER CARGO · {BuildStackState(stack)}"
					: BuildStackState(stack);
				quantity.text = stack.Quantity.ToString("N0");
				moveQuantity.SetValueWithoutNotify(1);
				int maximum = stack.Quantity;
				moveQuantity.RegisterValueChangedCallback(evt =>
				{
					int clamped = Mathf.Clamp(evt.newValue, 1, maximum);
					if (clamped != evt.newValue)
						moveQuantity.SetValueWithoutNotify(clamped);
				});
				moveButton.text = direction == PlayerTransferDirection.PickFromTarget ? "← Pick" : "Put →";
				moveButton.SetEnabled(transferEnabled);
				moveButton.tooltip = manifestLocked
					? "Order cargo must be moved with its entire box or capsule."
					: transferEnabled ? string.Empty : "This transfer is not available at the current interaction point.";

				PlayerItemStackKey stackKey = PlayerItemStackKey.From(stack);
				moveButton.clicked += () => RequestItemTransfer(interactionTarget, direction, stackKey, moveQuantity.value);
				list.Add(row);
			}
		}

		private void RequestItemTransfer(
			PlayerInteractionTarget interactionTarget,
			PlayerTransferDirection direction,
			PlayerItemStackKey stackKey,
			int quantity)
		{
			if (targetWorker == null || playerOverrideService == null)
				return;

			bool requested = playerOverrideService.TryRequestItemTransfer(
				targetWorker,
				interactionTarget,
				direction,
				stackKey,
				Mathf.Max(1, quantity),
				out string requestMessage);
			statusMessage = requested
				? "Item transfer requested."
				: string.IsNullOrWhiteSpace(requestMessage) ? "Item transfer could not be requested." : requestMessage;
			RefreshAll();
		}

		private void RequestPickWholeBox()
		{
			RequestWholeBoxTransfer(PlayerTransferDirection.PickFromTarget);
		}

		private void RequestPutWholeBox()
		{
			RequestWholeBoxTransfer(PlayerTransferDirection.PutToTarget);
		}

		private void RequestWholeBoxTransfer(PlayerTransferDirection direction)
		{
			if (targetWorker == null || playerOverrideService == null ||
				TryGetSelectedTarget(out PlayerInteractionTarget interactionTarget) == false)
			{
				return;
			}

			bool requested = playerOverrideService.TryRequestWholeBoxTransfer(
				targetWorker,
				interactionTarget,
				direction,
				out string requestMessage);
			statusMessage = requested
				? "Container transfer requested."
				: string.IsNullOrWhiteSpace(requestMessage) ? "Container transfer could not be requested." : requestMessage;
			RefreshAll();
		}

		private void ReleaseControl()
		{
			if (targetWorker == null || playerOverrideService == null)
				return;

			if (playerOverrideService.TryReleaseControl(targetWorker, out string releaseMessage))
			{
				window.Close();
				return;
			}

			statusMessage = string.IsNullOrWhiteSpace(releaseMessage)
				? "Player control could not be released."
				: releaseMessage;
			RefreshAll();
		}

		private bool TryGetSelectedTarget(out PlayerInteractionTarget interactionTarget)
		{
			if (selectedTargetIndex >= 0 && selectedTargetIndex < interactionTargets.Count)
			{
				interactionTarget = interactionTargets[selectedTargetIndex];
				return true;
			}

			interactionTarget = default;
			return false;
		}

		private static void RefreshContainerHeader(
			IItemContainer container,
			Label name,
			Label capacity,
			string emptyName)
		{
			name.text = container != null ? GetContainerName(container) : emptyName;
			capacity.text = container != null
				? $"{container.TotalSize:0.#} / {container.MaxSize:0.#}"
				: "0 / 0";
		}

		private static string GetContainerName(IItemContainer container)
		{
			if (container is BoxBase box)
				return GetBoxName(box);
			if (container is Component component)
				return component.name;
			return "Item Container";
		}

		private static string GetBoxName(BoxBase box)
		{
			return box != null ? $"{box.Type} #{box.BoxId}" : "Container";
		}

		private static string GetItemName(uint itemId)
		{
			if (GameContext.HasInstance && GameContext.Instance.ItemDB != null &&
				GameContext.Instance.ItemDB.GetItemData(itemId, out ItemDefinition definition) && definition != null)
			{
				return definition.name;
			}

			return $"Item {itemId}";
		}

		private static string BuildStackState(ItemStack stack)
		{
			List<string> parts = new();
			if (stack.Status != ItemStatus.None)
				parts.Add(stack.Status.ToString());
			if (stack.Quality != ItemQuality.None)
				parts.Add(stack.Quality.ToString());
			if (stack.OutboundStage != PackageOutboundStage.None)
				parts.Add(stack.OutboundStage.ToString());
			parts.Add($"Damage {stack.DamagePercent}%");
			parts.Add($"Fresh {stack.FreshnessPercent}%");
			if (ItemContainerDisplayUtility.CanDisplayTemperature)
				parts.Add($"{stack.CurrentTemperatureCelsius:0.0} °C");
			return string.Join(" · ", parts);
		}

		private static bool HasPickingManifest(IItemContainer container)
		{
			BoxBase box = container switch
			{
				BoxBase directBox => directBox,
				CapsuleBuffer buffer => buffer.DockedCapsule,
				_ => null,
			};
			return box != null &&
				GameContext.HasInstance &&
				GameContext.Instance.OBWorkflowSvc != null &&
				GameContext.Instance.OBWorkflowSvc.TryGetPickingManifest(box, out PickingManifest manifest) &&
				manifest != null &&
				manifest.IsEmpty == false;
		}

		private static string FormatInteractionKinds(InteractionKind kinds)
		{
			List<string> labels = new();
			if ((kinds & InteractionKind.Pick) != 0)
				labels.Add("PICK");
			if ((kinds & InteractionKind.Put) != 0)
				labels.Add("PUT");
			if ((kinds & InteractionKind.Work) != 0)
				labels.Add("WORK");
			return labels.Count > 0 ? string.Join(" / ", labels) : "No interaction";
		}
	}
}
