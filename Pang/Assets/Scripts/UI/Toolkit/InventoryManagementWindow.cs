using System;
using System.Collections.Generic;
using Assets.Scripts.Contract;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class InventoryManagementWindow : MonoBehaviour
	{
		private UIWindow window;
		private VisualTreeAsset contentTemplate;
		private VisualTreeAsset itemRowTemplate;
		private ScrollView itemList;
		private Label emptyLabel;
		private Label skuCountLabel;
		private Label outOfStockLabel;
		private Label fullyReservedLabel;
		private Label incomingLabel;
		private Label detailName;
		private Label detailStatus;
		private Label detailShelf;
		private Label detailReserved;
		private Label detailAvailable;
		private Label detailContracted;
		private Label detailNextDelivery;
		private Label detailDemand;
		private Label detailNotStarted;
		private Label detailOutboundWip;
		private Label detailStages;
		private Label detailContracts;
		private Button viewOrdersButton;
		private ItemDatabase itemDatabase;
		private ItemLedger itemLedger;
		private ContractService contractService;
		private OrderManager orderManager;
		private GameTime gameTime;
		private Action<uint> openOrdersForItem;
		private uint? selectedItemId;
		[System.NonSerialized] private bool initialized;
		private bool started;

		private sealed class ItemSnapshot
		{
			public uint ItemId;
			public string Name;
			public int Shelf;
			public int Reserved;
			public int Available;
			public float ContractedPerWeek;
			public int NextDeliveryWeeks = int.MaxValue;
			public int NextDeliveryQuantity;
			public int Demand;
			public int NotStarted;
			public int PickedWip;
			public int OutboundWip;
		}

		public void Configure(UIWindow targetWindow, VisualTreeAsset targetContentTemplate,
			VisualTreeAsset targetItemRowTemplate)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
			itemRowTemplate = targetItemRowTemplate;
		}

		public void ConfigureNavigation(Action<uint> targetOpenOrdersForItem)
		{
			openOrdersForItem = targetOpenOrdersForItem;
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

		private void OnDisable()
		{
			UnbindControls();
			UnbindServices();
			initialized = false;
		}

		public void Open()
		{
			if (InitializeView() == false)
				return;

			if (itemLedger == null)
				BindServices();

			RefreshAll();
			window.Open();
		}

		public void OpenForItem(uint itemId)
		{
			selectedItemId = itemId;
			Open();
		}

		private bool InitializeView()
		{
			if (initialized)
				return true;

			if (window == null || contentTemplate == null || itemRowTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[InventoryManagementWindow] Window or templates are missing.", this);
				return false;
			}

			TemplateContainer content = contentTemplate.CloneTree();
			itemList = content.Q<ScrollView>("inventory-item-list");
			emptyLabel = content.Q<Label>("inventory-empty");
			skuCountLabel = content.Q<Label>("inventory-sku-count");
			outOfStockLabel = content.Q<Label>("inventory-out-of-stock");
			fullyReservedLabel = content.Q<Label>("inventory-fully-reserved");
			incomingLabel = content.Q<Label>("inventory-incoming");
			detailName = content.Q<Label>("inventory-detail-name");
			detailStatus = content.Q<Label>("inventory-detail-status");
			detailShelf = content.Q<Label>("inventory-detail-shelf");
			detailReserved = content.Q<Label>("inventory-detail-reserved");
			detailAvailable = content.Q<Label>("inventory-detail-available");
			detailContracted = content.Q<Label>("inventory-detail-contracted");
			detailNextDelivery = content.Q<Label>("inventory-detail-next-delivery");
			detailDemand = content.Q<Label>("inventory-detail-demand");
			detailNotStarted = content.Q<Label>("inventory-detail-not-started");
			detailOutboundWip = content.Q<Label>("inventory-detail-outbound-wip");
			detailStages = content.Q<Label>("inventory-detail-stages");
			detailContracts = content.Q<Label>("inventory-detail-contracts");
			viewOrdersButton = content.Q<Button>("inventory-view-orders");

			if (itemList == null || emptyLabel == null || skuCountLabel == null || outOfStockLabel == null ||
				fullyReservedLabel == null || incomingLabel == null || detailName == null || detailStatus == null ||
				detailShelf == null || detailReserved == null || detailAvailable == null || detailContracted == null ||
				detailNextDelivery == null || detailDemand == null || detailNotStarted == null || detailOutboundWip == null ||
				detailStages == null || detailContracts == null || viewOrdersButton == null)
			{
				Debug.LogError("[InventoryManagementWindow] Required UXML elements are missing.", this);
				return false;
			}

			window.SetTitle("Inventory Management");
			window.SetContent(content);
			viewOrdersButton.clicked += OpenSelectedItemOrders;
			initialized = true;
			return true;
		}

		private void UnbindControls()
		{
			if (viewOrdersButton != null)
				viewOrdersButton.clicked -= OpenSelectedItemOrders;
		}

		private void BindServices()
		{
			UnbindServices();
			if (GameContext.HasInstance == false)
				return;

			GameContext context = GameContext.Instance;
			itemDatabase = context.ItemDB;
			itemLedger = context.WMSys != null ? context.WMSys.ItemLedger : null;
			contractService = context.ContractMgr;
			orderManager = context.OrderMgr;
			gameTime = context.GameTime;

			if (itemLedger != null)
				itemLedger.OnInventoryChanged += OnSourceChanged;
			if (contractService != null)
				contractService.OnContractsChanged += OnSourceChanged;
			if (orderManager != null)
				orderManager.OnOrdersChanged += OnSourceChanged;
			if (gameTime != null)
				gameTime.OnWeekPassed += OnSourceChanged;
		}

		private void UnbindServices()
		{
			if (itemLedger != null)
				itemLedger.OnInventoryChanged -= OnSourceChanged;
			if (contractService != null)
				contractService.OnContractsChanged -= OnSourceChanged;
			if (orderManager != null)
				orderManager.OnOrdersChanged -= OnSourceChanged;
			if (gameTime != null)
				gameTime.OnWeekPassed -= OnSourceChanged;

			itemDatabase = null;
			itemLedger = null;
			contractService = null;
			orderManager = null;
			gameTime = null;
		}

		private void OnSourceChanged()
		{
			if (window != null && window.IsOpen)
				RefreshAll();
		}

		private void RefreshAll()
		{
			if (itemList == null)
				return;

			List<ItemSnapshot> snapshots = BuildSnapshots();
			ItemSnapshot selected = FindSnapshot(snapshots, selectedItemId);
			if (selected == null && snapshots.Count > 0)
			{
				selected = snapshots[0];
				selectedItemId = selected.ItemId;
			}

			itemList.Clear();
			for (int i = 0; i < snapshots.Count; ++i)
				itemList.Add(CreateItemRow(snapshots[i]));

			emptyLabel.style.display = snapshots.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			RefreshSummary(snapshots);
			RefreshDetail(selected);
		}

		private List<ItemSnapshot> BuildSnapshots()
		{
			HashSet<uint> trackedItemIds = new();
			if (itemLedger != null)
			{
				foreach (uint itemId in itemLedger.ItemTotals.Keys)
					trackedItemIds.Add(itemId);
				foreach (uint itemId in itemLedger.ReservedItems.Keys)
					trackedItemIds.Add(itemId);
			}

			IReadOnlyList<ContractRuntime> contracts = contractService?.ActiveContracts;
			if (contracts != null)
			{
				for (int i = 0; i < contracts.Count; ++i)
				{
					ContractRuntime contract = contracts[i];
					if (contract?.Definition?.ItemToHandle != null)
						trackedItemIds.Add(contract.Definition.ItemToHandle.ItemID);
				}
			}

			if (orderManager != null)
			{
				foreach (Order order in orderManager.Orders)
				{
					if (order?.Lines == null)
						continue;
					foreach (OrderLine line in order.Lines)
					{
						if (line != null && line.IsFinal == false)
							trackedItemIds.Add(line.ItemID);
					}
				}
			}

			List<ItemSnapshot> result = new();
			foreach (uint itemId in trackedItemIds)
				result.Add(BuildSnapshot(itemId));

			result.Sort(CompareSnapshots);
			return result;
		}

		private ItemSnapshot BuildSnapshot(uint itemId)
		{
			ItemSnapshot snapshot = new()
			{
				ItemId = itemId,
				Name = GetItemName(itemId),
				Shelf = itemLedger?.GetTotal(itemId) ?? 0,
				Reserved = itemLedger?.GetReserved(itemId) ?? 0,
				Available = itemLedger?.GetAvailable(itemId) ?? 0,
			};

			IReadOnlyList<ContractRuntime> contracts = contractService?.ActiveContracts;
			if (contracts != null)
			{
				for (int i = 0; i < contracts.Count; ++i)
				{
					ContractRuntime contract = contracts[i];
					if (contract?.Definition?.ItemToHandle == null || contract.Definition.ItemToHandle.ItemID != itemId)
						continue;

					int interval = Mathf.Max(1, contract.Definition.DeliveryIntervalWeek);
					snapshot.ContractedPerWeek += contract.Definition.ItemCountsPerDelivery / (float)interval;
					int deliveryWeeks = Mathf.Max(0, contract.DeliveryDelta);
					if (deliveryWeeks < snapshot.NextDeliveryWeeks)
					{
						snapshot.NextDeliveryWeeks = deliveryWeeks;
						snapshot.NextDeliveryQuantity = contract.Definition.ItemCountsPerDelivery;
					}
					else if (deliveryWeeks == snapshot.NextDeliveryWeeks)
					{
						snapshot.NextDeliveryQuantity += contract.Definition.ItemCountsPerDelivery;
					}
				}
			}

			if (orderManager != null)
			{
				foreach (Order order in orderManager.Orders)
				{
					if (order?.Lines == null || order.Status == OrderTotalStatus.Completed || order.Status == OrderTotalStatus.Cancelled)
						continue;

					foreach (OrderLine line in order.Lines)
					{
						if (line == null || line.ItemID != itemId || line.IsFinal)
							continue;

						snapshot.Demand += Mathf.Max(0, line.Quantity - line.CompletedQuantity);
						snapshot.NotStarted += line.GetPickingAllocatableQuantity();
						int pickedWip = Mathf.Max(0, line.PickingCompletedQuantity - line.CompletedQuantity);
						snapshot.PickedWip += pickedWip;
						snapshot.OutboundWip += line.PickingAllocatedQuantity + pickedWip;
					}
				}
			}

			return snapshot;
		}

		private VisualElement CreateItemRow(ItemSnapshot snapshot)
		{
			TemplateContainer row = itemRowTemplate.CloneTree();
			Button rowButton = row.Q<Button>("inventory-row-button");
			row.Q<Label>("inventory-row-name").text = snapshot.Name;
			row.Q<Label>("inventory-row-id").text = $"Item {snapshot.ItemId}";
			row.Q<Label>("inventory-row-shelf").text = snapshot.Shelf.ToString("N0");
			row.Q<Label>("inventory-row-reserved").text = snapshot.Reserved.ToString("N0");
			row.Q<Label>("inventory-row-available").text = snapshot.Available.ToString("N0");
			row.Q<Label>("inventory-row-incoming").text = FormatIncoming(snapshot);
			row.Q<Label>("inventory-row-demand").text = snapshot.Demand.ToString("N0");
			row.Q<Label>("inventory-row-wip").text = snapshot.OutboundWip.ToString("N0");
			row.Q<Label>("inventory-row-status").text = GetStatus(snapshot);

			rowButton.EnableInClassList("inventory-row--selected", selectedItemId == snapshot.ItemId);
			rowButton.EnableInClassList("inventory-row--warning", HasDemandShortfall(snapshot));
			uint itemId = snapshot.ItemId;
			rowButton.clicked += () => SelectItem(itemId);
			return row;
		}

		private void SelectItem(uint itemId)
		{
			selectedItemId = itemId;
			RefreshAll();
		}

		private void RefreshSummary(List<ItemSnapshot> snapshots)
		{
			int outOfStock = 0;
			int fullyReserved = 0;
			int incomingNextWeek = 0;
			for (int i = 0; i < snapshots.Count; ++i)
			{
				ItemSnapshot snapshot = snapshots[i];
				if (snapshot.Shelf <= 0)
					outOfStock += 1;
				if (snapshot.Shelf > 0 && snapshot.Available <= 0)
					fullyReserved += 1;
				if (snapshot.NextDeliveryWeeks <= 1)
					incomingNextWeek += snapshot.NextDeliveryQuantity;
			}

			skuCountLabel.text = snapshots.Count.ToString("N0");
			outOfStockLabel.text = outOfStock.ToString("N0");
			fullyReservedLabel.text = fullyReserved.ToString("N0");
			incomingLabel.text = incomingNextWeek.ToString("N0");
		}

		private void RefreshDetail(ItemSnapshot snapshot)
		{
			bool hasSelection = snapshot != null;
			viewOrdersButton.SetEnabled(hasSelection && openOrdersForItem != null);
			if (hasSelection == false)
			{
				detailName.text = "No tracked inventory";
				detailStatus.text = "Stock appears here after a contract, receipt, reservation, or active order exists.";
				SetDetailValues("—");
				return;
			}

			detailName.text = snapshot.Name;
			detailStatus.text = GetStatus(snapshot);
			detailShelf.text = snapshot.Shelf.ToString("N0");
			detailReserved.text = snapshot.Reserved.ToString("N0");
			detailAvailable.text = snapshot.Available.ToString("N0");
			detailContracted.text = snapshot.ContractedPerWeek > 0f ? $"{snapshot.ContractedPerWeek:0.#} / week avg" : "No contracted supply";
			detailNextDelivery.text = snapshot.NextDeliveryWeeks == int.MaxValue
				? "No delivery scheduled"
				: $"{snapshot.NextDeliveryQuantity:N0} in {snapshot.NextDeliveryWeeks} week(s)";
			detailDemand.text = snapshot.Demand.ToString("N0");
			detailNotStarted.text = snapshot.NotStarted.ToString("N0");
			detailOutboundWip.text = snapshot.OutboundWip.ToString("N0");
			detailStages.text = BuildOrderStageSummary(snapshot.ItemId);
			detailContracts.text = BuildContractSummary(snapshot.ItemId);
		}

		private void SetDetailValues(string value)
		{
			detailShelf.text = value;
			detailReserved.text = value;
			detailAvailable.text = value;
			detailContracted.text = value;
			detailNextDelivery.text = value;
			detailDemand.text = value;
			detailNotStarted.text = value;
			detailOutboundWip.text = value;
			detailStages.text = value;
			detailContracts.text = value;
		}

		private string BuildOrderStageSummary(uint itemId)
		{
			int allocated = 0;
			int picked = 0;
			int packed = 0;
			int port = 0;
			int shipping = 0;
			int delivery = 0;
			int completed = 0;
			if (orderManager != null)
			{
				foreach (Order order in orderManager.Orders)
				{
					if (order?.Lines == null || order.Status == OrderTotalStatus.Completed || order.Status == OrderTotalStatus.Cancelled)
						continue;
					foreach (OrderLine line in order.Lines)
					{
						if (line == null || line.ItemID != itemId || line.Status == OrderStatus.Cancelled)
							continue;
						allocated += line.PickingAllocatedQuantity;
						picked += Mathf.Max(0, line.PickingCompletedQuantity - line.PackagingCompletedQuantity);
						packed += Mathf.Max(0, line.PackagingCompletedQuantity - line.WaitingForShippingQuantity);
						port += Mathf.Max(0, line.WaitingForShippingQuantity - line.ShippingQuantity);
						shipping += Mathf.Max(0, line.ShippingQuantity - line.InDeliveryQuantity);
						delivery += Mathf.Max(0, line.InDeliveryQuantity - line.CompletedQuantity);
						completed += line.CompletedQuantity;
					}
				}
			}

			List<string> parts = new();
			AddStage(parts, "Allocated", allocated);
			AddStage(parts, "Picked", picked);
			AddStage(parts, "Packed", packed);
			AddStage(parts, "Port", port);
			AddStage(parts, "Shipping", shipping);
			AddStage(parts, "Delivery", delivery);
			AddStage(parts, "Done", completed);
			return parts.Count > 0 ? string.Join("  ·  ", parts) : "No active outbound work";
		}

		private string BuildContractSummary(uint itemId)
		{
			List<string> lines = new();
			IReadOnlyList<ContractRuntime> contracts = contractService?.ActiveContracts;
			if (contracts != null)
			{
				for (int i = 0; i < contracts.Count; ++i)
				{
					ContractRuntime contract = contracts[i];
					if (contract?.Definition?.ItemToHandle == null || contract.Definition.ItemToHandle.ItemID != itemId)
						continue;
					string name = string.IsNullOrWhiteSpace(contract.Definition.ContractName)
						? contract.Definition.ItemToHandle.name
						: contract.Definition.ContractName;
					lines.Add($"{name}  ·  {contract.Definition.ItemCountsPerDelivery:N0} every {Mathf.Max(1, contract.Definition.DeliveryIntervalWeek)} weeks  ·  next in {Mathf.Max(0, contract.DeliveryDelta)} weeks");
				}
			}
			return lines.Count > 0 ? string.Join("\n", lines) : "No active supply contract";
		}

		private void OpenSelectedItemOrders()
		{
			if (selectedItemId.HasValue == false || openOrdersForItem == null)
				return;
			window.Close();
			openOrdersForItem(selectedItemId.Value);
		}

		private string GetItemName(uint itemId)
		{
			return itemDatabase != null && itemDatabase.GetItemData(itemId, out ItemDefinition item) && item != null
				? item.name
				: $"Unknown Item {itemId}";
		}

		private static ItemSnapshot FindSnapshot(List<ItemSnapshot> snapshots, uint? itemId)
		{
			if (itemId.HasValue == false)
				return null;
			for (int i = 0; i < snapshots.Count; ++i)
			{
				if (snapshots[i].ItemId == itemId.Value)
					return snapshots[i];
			}
			return null;
		}

		private static int CompareSnapshots(ItemSnapshot left, ItemSnapshot right)
		{
			int leftRisk = HasDemandShortfall(left) ? 0 : left.Available <= 0 ? 1 : 2;
			int rightRisk = HasDemandShortfall(right) ? 0 : right.Available <= 0 ? 1 : 2;
			int riskCompare = leftRisk.CompareTo(rightRisk);
			return riskCompare != 0 ? riskCompare : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
		}

		private static string FormatIncoming(ItemSnapshot snapshot)
		{
			return snapshot.NextDeliveryWeeks == int.MaxValue
				? "—"
				: $"{snapshot.NextDeliveryQuantity:N0} / {snapshot.NextDeliveryWeeks}w";
		}

		private static string GetStatus(ItemSnapshot snapshot)
		{
			if (HasDemandShortfall(snapshot))
				return "Demand shortfall";
			if (snapshot.Shelf <= 0)
				return snapshot.NextDeliveryQuantity > 0 ? "Awaiting supply" : "Out of stock";
			if (snapshot.Available <= 0)
				return "Fully reserved";
			return "Available";
		}

		private static bool HasDemandShortfall(ItemSnapshot snapshot)
		{
			return snapshot.Demand > snapshot.Reserved + snapshot.PickedWip;
		}

		private static void AddStage(List<string> parts, string label, int quantity)
		{
			if (quantity > 0)
				parts.Add($"{label} {quantity:N0}");
		}
	}
}
