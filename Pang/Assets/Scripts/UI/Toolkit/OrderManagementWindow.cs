using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class OrderManagementWindow : MonoBehaviour
	{
		private const string SelectedTabClass = "order-tab-button--selected";

		private enum OrderSection
		{
			Active,
			Completed,
			Cancelled,
		}

		private UIWindow window;
		private VisualTreeAsset contentTemplate;
		private VisualTreeAsset orderRowTemplate;
		private VisualTreeAsset orderLineRowTemplate;
		private Button activeButton;
		private Button completedButton;
		private Button cancelledButton;
		private Label activeCountLabel;
		private Label delayedCountLabel;
		private Label dueSoonCountLabel;
		private Label expectedRewardLabel;
		private Label filterLabel;
		private Button clearFilterButton;
		private ScrollView orderList;
		private Label emptyLabel;
		private Label detailTitle;
		private Label detailStatus;
		private Label detailDestination;
		private Label detailDue;
		private Label detailProgress;
		private Label detailReward;
		private Label detailContracts;
		private ScrollView lineList;
		private OrderManager orderManager;
		private ItemDatabase itemDatabase;
		private GameTime gameTime;
		private Action<uint> openInventoryForItem;
		private OrderSection currentSection;
		private int? selectedOrderId;
		private uint? filteredItemId;
		[System.NonSerialized] private bool initialized;
		private bool started;

		public void Configure(UIWindow targetWindow, VisualTreeAsset targetContentTemplate,
			VisualTreeAsset targetOrderRowTemplate, VisualTreeAsset targetOrderLineRowTemplate)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
			orderRowTemplate = targetOrderRowTemplate;
			orderLineRowTemplate = targetOrderLineRowTemplate;
		}

		public void ConfigureNavigation(Action<uint> targetOpenInventoryForItem)
		{
			openInventoryForItem = targetOpenInventoryForItem;
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
			filteredItemId = null;
			OpenInternal();
		}

		public void OpenForItem(uint itemId)
		{
			filteredItemId = itemId;
			currentSection = OrderSection.Active;
			selectedOrderId = null;
			OpenInternal();
		}

		private void OpenInternal()
		{
			if (InitializeView() == false)
				return;

			if (orderManager == null)
				BindServices();

			RefreshAll();
			window.Open();
		}

		private bool InitializeView()
		{
			if (initialized)
				return true;

			if (window == null || contentTemplate == null || orderRowTemplate == null ||
				orderLineRowTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[OrderManagementWindow] Window or templates are missing.", this);
				return false;
			}

			TemplateContainer content = contentTemplate.CloneTree();
			activeButton = content.Q<Button>("orders-active-button");
			completedButton = content.Q<Button>("orders-completed-button");
			cancelledButton = content.Q<Button>("orders-cancelled-button");
			activeCountLabel = content.Q<Label>("orders-active-count");
			delayedCountLabel = content.Q<Label>("orders-delayed-count");
			dueSoonCountLabel = content.Q<Label>("orders-due-soon-count");
			expectedRewardLabel = content.Q<Label>("orders-expected-reward");
			filterLabel = content.Q<Label>("orders-filter-label");
			clearFilterButton = content.Q<Button>("orders-clear-filter");
			orderList = content.Q<ScrollView>("orders-list");
			emptyLabel = content.Q<Label>("orders-empty");
			detailTitle = content.Q<Label>("orders-detail-title");
			detailStatus = content.Q<Label>("orders-detail-status");
			detailDestination = content.Q<Label>("orders-detail-destination");
			detailDue = content.Q<Label>("orders-detail-due");
			detailProgress = content.Q<Label>("orders-detail-progress");
			detailReward = content.Q<Label>("orders-detail-reward");
			detailContracts = content.Q<Label>("orders-detail-contracts");
			lineList = content.Q<ScrollView>("orders-line-list");

			if (activeButton == null || completedButton == null || cancelledButton == null || activeCountLabel == null ||
				delayedCountLabel == null || dueSoonCountLabel == null || expectedRewardLabel == null || filterLabel == null ||
				clearFilterButton == null || orderList == null || emptyLabel == null || detailTitle == null ||
				detailStatus == null || detailDestination == null || detailDue == null || detailProgress == null ||
				detailReward == null || detailContracts == null || lineList == null)
			{
				Debug.LogError("[OrderManagementWindow] Required UXML elements are missing.", this);
				return false;
			}

			window.SetTitle("Orders Management");
			window.SetContent(content);
			activeButton.clicked += OpenActive;
			completedButton.clicked += OpenCompleted;
			cancelledButton.clicked += OpenCancelled;
			clearFilterButton.clicked += ClearFilter;
			initialized = true;
			return true;
		}

		private void UnbindControls()
		{
			if (activeButton != null)
				activeButton.clicked -= OpenActive;
			if (completedButton != null)
				completedButton.clicked -= OpenCompleted;
			if (cancelledButton != null)
				cancelledButton.clicked -= OpenCancelled;
			if (clearFilterButton != null)
				clearFilterButton.clicked -= ClearFilter;
		}

		private void BindServices()
		{
			UnbindServices();
			if (GameContext.HasInstance == false)
				return;

			orderManager = GameContext.Instance.OrderMgr;
			itemDatabase = GameContext.Instance.ItemDB;
			gameTime = GameContext.Instance.GameTime;
			if (orderManager != null)
				orderManager.OnOrdersChanged += OnSourceChanged;
			if (gameTime != null)
				gameTime.OnWeekPassed += OnSourceChanged;
		}

		private void UnbindServices()
		{
			if (orderManager != null)
				orderManager.OnOrdersChanged -= OnSourceChanged;
			if (gameTime != null)
				gameTime.OnWeekPassed -= OnSourceChanged;
			orderManager = null;
			itemDatabase = null;
			gameTime = null;
		}

		private void OnSourceChanged()
		{
			if (window != null && window.IsOpen)
				RefreshAll();
		}

		private void OpenActive()
		{
			currentSection = OrderSection.Active;
			selectedOrderId = null;
			RefreshAll();
		}

		private void OpenCompleted()
		{
			currentSection = OrderSection.Completed;
			selectedOrderId = null;
			RefreshAll();
		}

		private void OpenCancelled()
		{
			currentSection = OrderSection.Cancelled;
			selectedOrderId = null;
			RefreshAll();
		}

		private void ClearFilter()
		{
			filteredItemId = null;
			selectedOrderId = null;
			RefreshAll();
		}

		private void RefreshAll()
		{
			if (orderList == null)
				return;

			RefreshTabs();
			RefreshSummary();
			RefreshFilter();
			List<Order> visibleOrders = BuildVisibleOrders();
			Order selected = FindOrder(visibleOrders, selectedOrderId);
			if (selected == null && visibleOrders.Count > 0)
			{
				selected = visibleOrders[0];
				selectedOrderId = selected.OrderID;
			}

			orderList.Clear();
			for (int i = 0; i < visibleOrders.Count; ++i)
				orderList.Add(CreateOrderRow(visibleOrders[i]));
			emptyLabel.style.display = visibleOrders.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			RefreshDetail(selected);
		}

		private void RefreshTabs()
		{
			activeButton.EnableInClassList(SelectedTabClass, currentSection == OrderSection.Active);
			completedButton.EnableInClassList(SelectedTabClass, currentSection == OrderSection.Completed);
			cancelledButton.EnableInClassList(SelectedTabClass, currentSection == OrderSection.Cancelled);
		}

		private void RefreshSummary()
		{
			int active = 0;
			int delayed = 0;
			int dueSoon = 0;
			int expectedReward = 0;
			int currentWeek = gameTime?.WeeksPassed ?? 0;
			if (orderManager != null)
			{
				foreach (Order order in orderManager.Orders)
				{
					if (order == null || OrderPresentation.IsActive(order) == false)
						continue;
					active += 1;
					int weeksLeft = OrderPresentation.GetWeeksLeft(order, currentWeek);
					if (weeksLeft < 0)
						delayed += 1;
					else if (weeksLeft <= OrderPresentation.DueSoonWeeks)
						dueSoon += 1;
					expectedReward += OrderPresentation.CalculateExpectedMoney(order, ResolveItem, currentWeek);
				}
			}

			activeCountLabel.text = active.ToString("N0");
			delayedCountLabel.text = delayed.ToString("N0");
			dueSoonCountLabel.text = dueSoon.ToString("N0");
			expectedRewardLabel.text = $"${expectedReward:N0}";
		}

		private void RefreshFilter()
		{
			bool filtered = filteredItemId.HasValue;
			filterLabel.style.display = filtered ? DisplayStyle.Flex : DisplayStyle.None;
			clearFilterButton.style.display = filtered ? DisplayStyle.Flex : DisplayStyle.None;
			if (filtered)
				filterLabel.text = $"ITEM FILTER  ·  {OrderPresentation.GetItemName(filteredItemId.Value, ResolveItem)}";
		}

		private List<Order> BuildVisibleOrders()
		{
			List<Order> result = new();
			if (orderManager == null)
				return result;

			foreach (Order order in orderManager.Orders)
			{
				if (order == null || MatchesSection(order) == false || MatchesItemFilter(order) == false)
					continue;
				result.Add(order);
			}

			int currentWeek = gameTime?.WeeksPassed ?? 0;
			result.Sort((left, right) => OrderPresentation.CompareByUrgency(left, right, currentWeek));
			return result;
		}

		private VisualElement CreateOrderRow(Order order)
		{
			TemplateContainer row = orderRowTemplate.CloneTree();
			Button rowButton = row.Q<Button>("order-row-button");
			int currentWeek = gameTime?.WeeksPassed ?? 0;
			int requested = OrderPresentation.GetRequestedQuantity(order);
			int completed = OrderPresentation.GetCompletedQuantity(order);
			int weeksLeft = OrderPresentation.GetWeeksLeft(order, currentWeek);
			bool isDelayed = OrderPresentation.GetUrgency(order, currentWeek) == OrderUrgency.Delayed;

			row.Q<Label>("order-row-id").text = $"#{order.OrderID}";
			row.Q<Label>("order-row-lines").text = $"{order.Lines?.Count ?? 0} line(s)";
			row.Q<Label>("order-row-destination").text = order.Destination.ToString();
			row.Q<Label>("order-row-status").text = isDelayed ? "Delayed" : OrderPresentation.FormatStatus(order.Status);
			row.Q<Label>("order-row-progress").text = $"{completed:N0} / {requested:N0}  ·  {OrderPresentation.BuildStageSummary(order)}";
			row.Q<Label>("order-row-due").text = OrderPresentation.FormatWeeksLeft(order, weeksLeft);
			row.Q<Label>("order-row-reward").text = $"${OrderPresentation.CalculateExpectedMoney(order, ResolveItem, currentWeek):N0}";
			rowButton.EnableInClassList("order-row--selected", selectedOrderId == order.OrderID);
			rowButton.EnableInClassList("order-row--delayed", isDelayed);
			int orderId = order.OrderID;
			rowButton.clicked += () => SelectOrder(orderId);
			return row;
		}

		private void SelectOrder(int orderId)
		{
			selectedOrderId = orderId;
			RefreshAll();
		}

		private void RefreshDetail(Order order)
		{
			lineList.Clear();
			if (order == null)
			{
				detailTitle.text = "No orders";
				detailStatus.text = "No order matches the current section and item filter.";
				detailDestination.text = "—";
				detailDue.text = "—";
				detailProgress.text = "—";
				detailReward.text = "—";
				detailContracts.text = "—";
				return;
			}

			int currentWeek = gameTime?.WeeksPassed ?? 0;
			int weeksLeft = OrderPresentation.GetWeeksLeft(order, currentWeek);
			detailTitle.text = $"Order #{order.OrderID}";
			detailStatus.text = OrderPresentation.GetUrgency(order, currentWeek) == OrderUrgency.Delayed
				? "Delayed"
				: OrderPresentation.FormatStatus(order.Status);
			detailDestination.text = order.Destination.ToString();
			detailDue.text = OrderPresentation.FormatWeeksLeft(order, weeksLeft);
			detailProgress.text = $"{OrderPresentation.GetCompletedQuantity(order):N0} / {OrderPresentation.GetRequestedQuantity(order):N0}  ·  {OrderPresentation.BuildStageSummary(order)}";
			detailReward.text = $"${OrderPresentation.CalculateExpectedMoney(order, ResolveItem, currentWeek):N0}  ·  Rep {OrderPresentation.CalculateExpectedReputation(order, currentWeek):0.#}";
			detailContracts.text = OrderPresentation.BuildContractNames(order);

			if (order.Lines == null)
				return;
			foreach (OrderLine line in order.Lines)
			{
				if (line != null)
					lineList.Add(CreateOrderLineRow(line, currentWeek));
			}
		}

		private VisualElement CreateOrderLineRow(OrderLine line, int currentWeek)
		{
			TemplateContainer row = orderLineRowTemplate.CloneTree();
			row.Q<Label>("order-line-item").text = OrderPresentation.GetItemName(line.ItemID, ResolveItem);
			row.Q<Label>("order-line-contract").text = OrderPresentation.GetContractName(line);
			row.Q<Label>("order-line-requested").text = line.Quantity.ToString("N0");
			row.Q<Label>("order-line-due").text = OrderPresentation.FormatLineDue(line, currentWeek);
			row.Q<Label>("order-line-status").text = currentWeek > line.DueWeek && line.IsFinal == false
				? "Delayed"
				: OrderPresentation.FormatLineStatus(line.Status);
			row.Q<Label>("order-line-progress").text = OrderPresentation.BuildLineStageSummary(line);
			Button inventoryButton = row.Q<Button>("order-line-inventory");
			inventoryButton.SetEnabled(openInventoryForItem != null);
			uint itemId = line.ItemID;
			inventoryButton.clicked += () => OpenInventory(itemId);
			return row;
		}

		private void OpenInventory(uint itemId)
		{
			if (openInventoryForItem == null)
				return;
			window.Close();
			openInventoryForItem(itemId);
		}

		private bool MatchesSection(Order order)
		{
			return currentSection switch
			{
				OrderSection.Active => OrderPresentation.IsActive(order),
				OrderSection.Completed => order.Status == OrderTotalStatus.Completed,
				OrderSection.Cancelled => order.Status == OrderTotalStatus.Cancelled,
				_ => false,
			};
		}

		private bool MatchesItemFilter(Order order)
		{
			if (filteredItemId.HasValue == false)
				return true;
			if (order.Lines == null)
				return false;
			foreach (OrderLine line in order.Lines)
			{
				if (line != null && line.ItemID == filteredItemId.Value)
					return true;
			}
			return false;
		}

		private static Order FindOrder(List<Order> orders, int? orderId)
		{
			if (orderId.HasValue == false)
				return null;
			for (int i = 0; i < orders.Count; ++i)
			{
				if (orders[i].OrderID == orderId.Value)
					return orders[i];
			}
			return null;
		}

		private ItemDefinition ResolveItem(uint itemId)
		{
			return itemDatabase != null && itemDatabase.GetItemData(itemId, out ItemDefinition item)
				? item
				: null;
		}
	}
}
