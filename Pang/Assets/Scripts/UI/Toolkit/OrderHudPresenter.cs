using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class OrderHudPresenter : IDisposable
	{
		private const string HiddenClass = "order-hud--hidden";
		private const string DueSoonClass = "order-hud__order--due-soon";
		private const string DueThisWeekClass = "order-hud__order--due-this-week";
		private const string DelayedClass = "order-hud__order--delayed";
		private const string RiskWarningClass = "order-hud__risk--warning";
		private const string RiskDangerClass = "order-hud__risk--danger";
		private const string RiskCountDangerClass = "order-hud__risk-count--danger";

		private sealed class CompactSlot
		{
			public Button Root;
			public Label OrderId;
			public Label Destination;
			public Label Item;
			public Label Due;

			public bool IsValid =>
				Root != null && OrderId != null && Destination != null && Item != null && Due != null;
		}

		private readonly List<Order> activeOrders = new();
		private readonly CompactSlot[] compactSlots = { new(), new() };

		private VisualElement orderRoot;
		private Label activeCount;
		private Label riskCount;
		private VisualElement orderBody;
		private Button primaryRoot;
		private Label primaryOrderId;
		private Label primaryDestination;
		private Label primaryDue;
		private Label primaryItem;
		private Label stagePending;
		private Label stagePicking;
		private Label stagePacking;
		private Label stagePort;
		private Label stageFlight;
		private Label stageCompleted;
		private Label primaryRisk;
		private Button moreButton;
		private Label emptyLabel;

		private OrderManager orderManager;
		private ItemDatabase itemDatabase;
		private GameTime gameTime;

		public bool BindView(VisualElement documentRoot)
		{
			UnbindView();
			if (documentRoot == null)
				return false;

			orderRoot = documentRoot.Q<VisualElement>("order-hud");
			activeCount = documentRoot.Q<Label>("order-hud-active-count");
			riskCount = documentRoot.Q<Label>("order-hud-risk-count");
			orderBody = documentRoot.Q<VisualElement>("order-hud-body");
			primaryRoot = documentRoot.Q<Button>("order-hud-primary");
			primaryOrderId = documentRoot.Q<Label>("order-hud-primary-id");
			primaryDestination = documentRoot.Q<Label>("order-hud-primary-destination");
			primaryDue = documentRoot.Q<Label>("order-hud-primary-due");
			primaryItem = documentRoot.Q<Label>("order-hud-primary-item");
			stagePending = documentRoot.Q<Label>("order-hud-stage-pending");
			stagePicking = documentRoot.Q<Label>("order-hud-stage-picking");
			stagePacking = documentRoot.Q<Label>("order-hud-stage-packing");
			stagePort = documentRoot.Q<Label>("order-hud-stage-port");
			stageFlight = documentRoot.Q<Label>("order-hud-stage-flight");
			stageCompleted = documentRoot.Q<Label>("order-hud-stage-completed");
			primaryRisk = documentRoot.Q<Label>("order-hud-primary-risk");
			moreButton = documentRoot.Q<Button>("order-hud-more");
			emptyLabel = documentRoot.Q<Label>("order-hud-empty");

			BindCompactSlot(compactSlots[0], documentRoot, "order-hud-secondary-1");
			BindCompactSlot(compactSlots[1], documentRoot, "order-hud-secondary-2");

			bool valid = orderRoot != null && activeCount != null && riskCount != null && orderBody != null &&
				primaryRoot != null && primaryOrderId != null && primaryDestination != null && primaryDue != null &&
				primaryItem != null && stagePending != null && stagePicking != null && stagePacking != null &&
				stagePort != null && stageFlight != null && stageCompleted != null && primaryRisk != null &&
				moreButton != null && emptyLabel != null && compactSlots[0].IsValid && compactSlots[1].IsValid;

			if (valid == false)
			{
				UnbindView();
				return false;
			}

			SetRootVisible(false);
			return true;
		}

		public void UnbindView()
		{
			SetRootVisible(false);
			orderRoot = null;
			activeCount = null;
			riskCount = null;
			orderBody = null;
			primaryRoot = null;
			primaryOrderId = null;
			primaryDestination = null;
			primaryDue = null;
			primaryItem = null;
			stagePending = null;
			stagePicking = null;
			stagePacking = null;
			stagePort = null;
			stageFlight = null;
			stageCompleted = null;
			primaryRisk = null;
			moreButton = null;
			emptyLabel = null;

			for (int i = 0; i < compactSlots.Length; ++i)
				ClearCompactSlot(compactSlots[i]);
		}

		public void BindSources(OrderManager targetOrderManager, ItemDatabase targetItemDatabase, GameTime targetGameTime)
		{
			UnbindSources();
			orderManager = targetOrderManager;
			itemDatabase = targetItemDatabase;
			gameTime = targetGameTime;

			if (orderManager != null)
				orderManager.OnOrdersChanged += OnSourceChanged;
			if (gameTime != null)
				gameTime.OnWeekPassed += OnSourceChanged;

			Refresh();
		}

		public void UnbindSources()
		{
			if (orderManager != null)
				orderManager.OnOrdersChanged -= OnSourceChanged;
			if (gameTime != null)
				gameTime.OnWeekPassed -= OnSourceChanged;

			orderManager = null;
			itemDatabase = null;
			gameTime = null;
			SetRootVisible(false);
		}

		public void Refresh()
		{
			if (orderManager == null)
			{
				SetRootVisible(false);
				return;
			}

			Render(orderManager.Orders, ResolveItem, gameTime != null ? gameTime.WeeksPassed : 0);
		}

		public void Render(IEnumerable<Order> orders, Func<uint, ItemDefinition> resolveItem, int currentWeek)
		{
			if (orderRoot == null)
				return;

			activeOrders.Clear();
			if (orders != null)
			{
				foreach (Order order in orders)
				{
					if (OrderPresentation.IsActive(order))
						activeOrders.Add(order);
				}
			}

			activeOrders.Sort((left, right) => OrderPresentation.CompareByUrgency(left, right, currentWeek));
			SetRootVisible(true);

			int delayed = 0;
			int atRisk = 0;
			for (int i = 0; i < activeOrders.Count; ++i)
			{
				OrderUrgency urgency = OrderPresentation.GetUrgency(activeOrders[i], currentWeek);
				if (urgency == OrderUrgency.Delayed)
					++delayed;
				if (urgency == OrderUrgency.Delayed || urgency == OrderUrgency.DueThisWeek || urgency == OrderUrgency.DueSoon)
					++atRisk;
			}

			activeCount.text = $"{activeOrders.Count:N0} ACTIVE";
			riskCount.text = $"{atRisk:N0} RISK";
			riskCount.EnableInClassList(RiskCountDangerClass, delayed > 0);

			bool hasOrders = activeOrders.Count > 0;
			orderBody.style.display = hasOrders ? DisplayStyle.Flex : DisplayStyle.None;
			emptyLabel.style.display = hasOrders ? DisplayStyle.None : DisplayStyle.Flex;
			primaryRoot.style.display = hasOrders ? DisplayStyle.Flex : DisplayStyle.None;

			if (hasOrders)
				RenderPrimary(activeOrders[0], resolveItem, currentWeek);

			for (int i = 0; i < compactSlots.Length; ++i)
			{
				int orderIndex = i + 1;
				bool visible = orderIndex < activeOrders.Count;
				compactSlots[i].Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
				if (visible)
					RenderCompact(compactSlots[i], activeOrders[orderIndex], resolveItem, currentWeek);
			}

			int remaining = Math.Max(0, activeOrders.Count - 1 - compactSlots.Length);
			moreButton.style.display = remaining > 0 ? DisplayStyle.Flex : DisplayStyle.None;
			moreButton.text = $"+{remaining:N0} ORDERS";
		}

		public void Dispose()
		{
			UnbindSources();
			UnbindView();
		}

		private void OnSourceChanged()
		{
			Refresh();
		}

		private void RenderPrimary(Order order, Func<uint, ItemDefinition> resolveItem, int currentWeek)
		{
			OrderUrgency urgency = OrderPresentation.GetUrgency(order, currentWeek);
			OrderRepresentativeItem representative = OrderPresentation.GetRepresentativeItem(order, resolveItem);
			OrderStageQuantities stages = OrderPresentation.GetExclusiveStageQuantities(order);

			primaryOrderId.text = $"#{order.OrderID}";
			primaryDestination.text = FormatDestination(order.Destination);
			primaryDue.text = FormatDue(order, currentWeek);
			primaryItem.text = FormatRepresentativeItem(representative);
			stagePending.text = stages.Pending.ToString("N0");
			stagePicking.text = (stages.Allocated + stages.Picked).ToString("N0");
			stagePacking.text = stages.Packed.ToString("N0");
			stagePort.text = stages.AtPort.ToString("N0");
			stageFlight.text = (stages.Shipping + stages.InDelivery).ToString("N0");
			stageCompleted.text = stages.Completed.ToString("N0");

			int expectedMoney = OrderPresentation.CalculateExpectedMoney(order, resolveItem, currentWeek);
			int delayLoss = OrderPresentation.CalculateDelayMoneyLossAtWeek(order, currentWeek);
			float expectedReputation = OrderPresentation.CalculateExpectedReputation(order, currentWeek);
			primaryRisk.text = delayLoss > 0
				? $"EXPECTED ${expectedMoney:N0} · DELAY -${delayLoss:N0} · REP {FormatSigned(expectedReputation)}"
				: $"EXPECTED ${expectedMoney:N0} · REP {FormatSigned(expectedReputation)}";

			ApplyUrgency(primaryRoot, urgency);
			primaryRisk.EnableInClassList(RiskWarningClass,
				urgency == OrderUrgency.DueSoon || urgency == OrderUrgency.DueThisWeek);
			primaryRisk.EnableInClassList(RiskDangerClass, urgency == OrderUrgency.Delayed);
		}

		private static void RenderCompact(
			CompactSlot slot,
			Order order,
			Func<uint, ItemDefinition> resolveItem,
			int currentWeek)
		{
			OrderRepresentativeItem representative = OrderPresentation.GetRepresentativeItem(order, resolveItem);
			slot.OrderId.text = $"#{order.OrderID}";
			slot.Destination.text = FormatDestination(order.Destination);
			slot.Item.text = FormatRepresentativeItem(representative);
			slot.Due.text = FormatDue(order, currentWeek);
			ApplyUrgency(slot.Root, OrderPresentation.GetUrgency(order, currentWeek));
		}

		private ItemDefinition ResolveItem(uint itemId)
		{
			return itemDatabase != null && itemDatabase.GetItemData(itemId, out ItemDefinition item) ? item : null;
		}

		private static void BindCompactSlot(CompactSlot slot, VisualElement root, string name)
		{
			slot.Root = root.Q<Button>(name);
			slot.OrderId = root.Q<Label>($"{name}-id");
			slot.Destination = root.Q<Label>($"{name}-destination");
			slot.Item = root.Q<Label>($"{name}-item");
			slot.Due = root.Q<Label>($"{name}-due");
		}

		private static void ClearCompactSlot(CompactSlot slot)
		{
			slot.Root = null;
			slot.OrderId = null;
			slot.Destination = null;
			slot.Item = null;
			slot.Due = null;
		}

		private static void ApplyUrgency(VisualElement element, OrderUrgency urgency)
		{
			element.EnableInClassList(DueSoonClass, urgency == OrderUrgency.DueSoon);
			element.EnableInClassList(DueThisWeekClass, urgency == OrderUrgency.DueThisWeek);
			element.EnableInClassList(DelayedClass, urgency == OrderUrgency.Delayed);
		}

		private static string FormatDue(Order order, int currentWeek)
		{
			int weeksLeft = OrderPresentation.GetWeeksLeft(order, currentWeek);
			if (weeksLeft < 0)
				return $"LATE {-weeksLeft}W";
			return weeksLeft == 0 ? "DUE NOW" : $"{weeksLeft}W";
		}

		private static string FormatDestination(OrderDestination destination)
		{
			return destination == OrderDestination.None ? "UNASSIGNED" : destination.ToString().ToUpperInvariant();
		}

		private static string FormatRepresentativeItem(OrderRepresentativeItem representative)
		{
			if (representative.HasValue == false)
				return "No cargo";

			string additional = representative.AdditionalItemTypeCount > 0
				? $" +{representative.AdditionalItemTypeCount:N0} types"
				: string.Empty;
			return $"{representative.ItemName} ×{representative.Quantity:N0}{additional}";
		}

		private static string FormatSigned(float value)
		{
			return value.ToString("+0.#;-0.#;0");
		}

		private void SetRootVisible(bool visible)
		{
			orderRoot?.EnableInClassList(HiddenClass, visible == false);
		}
	}
}
