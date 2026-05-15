using UnityEngine;
using TMPro;
using System.Text;
using System.Collections.Generic;

namespace Assets.Scripts.UI
{
	public class OrderWindow : MonoBehaviour
	{
		public enum TabType
		{
			All,
			Pending,
			InProgress,
			Completed,
			Stats
		}

		[SerializeField] private UIWindow window;
		[SerializeField] private TextMeshProUGUI statusText;

		[Header("Window MetaData")]
		[SerializeField] private string title = "Order Tracker";
		[SerializeField] private Sprite icon;

		private OrderManager orderMgr => GameContext.Instance.OrderMgr;
		private TabType currentTab = TabType.All;

		private bool tabsInitialized = false;

		private void Awake()
		{
			window.SetTitle(title);
			window.SetIcon(icon);

			if (statusText != null)
			{
				statusText.rectTransform.SetParent(window.ContentRoot, false);
			}

			SetupTabs();

			gameObject.SetActive(false);
		}

		private void SetupTabs()
		{
			if (tabsInitialized) return;

			window.ClearTabs();
			window.AddTab("All", SetTab);
			window.AddTab("Pending", SetTab);
			window.AddTab("InProgress", SetTab);
			window.AddTab("Completed", SetTab);
			window.AddTab("Stats", SetTab);

			window.UpdateTabVisuals((int)currentTab);
			tabsInitialized = true;
		}

		private void OnEnable()
		{
			SetupTabs(); // Ensure tabs are there
			UpdateOrderDisplay();
		}

		private void Update()
		{
			if (Time.frameCount % 60 == 0)
			{
				UpdateOrderDisplay();
			}
		}

		public void Open()
		{
			gameObject.SetActive(true);
			window.Open();
		}

		public void Close()
		{
			window.Close();
			gameObject.SetActive(false);
		}

		public void SetTab(int tabIndex)
		{
			currentTab = (TabType)tabIndex;
			UpdateOrderDisplay();
		}

		private string lastText = "";

		private void UpdateOrderDisplay()
		{
			if (orderMgr == null) return;

			string newText = "";
			if (currentTab == TabType.Stats)
			{
				newText = GetStatsDisplayText();
			}
			else
			{
				newText = GetOrderDisplayText();
			}

			if (newText != lastText)
			{
				statusText.text = newText;
				lastText = newText;

				UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(window.ContentRoot);
			}
		}

		private string GetOrderDisplayText()
		{
			StringBuilder sb = new StringBuilder();

			IEnumerable<Order> targetOrders = orderMgr.Orders;

			int count = 0;
			int totalExpectedMoney = 0;
			float totalExpectedRep = 0;
			int currentWeek = GameContext.Instance.GameTime.WeeksPassed;

			StringBuilder ordersSb = new StringBuilder();

			foreach (var order in targetOrders)
			{
				if (!ShouldShowInTab(order)) continue;

				// Calculate Order summary info
				int maxDueWeek = 0;
				int minStartWeek = int.MaxValue;
				int orderMoney = 0;
				float orderRep = 0;

				foreach (var line in order.Lines)
				{
					if (line.DueWeek > maxDueWeek) maxDueWeek = line.DueWeek;
					if (line.StartWeek < minStartWeek) minStartWeek = line.StartWeek;

					if (line.Status != OrderStatus.Cancelled)
					{
						// 아이템 가격 수익
						if (GameContext.Instance.ItemDB.GetItemData(line.ItemID, out var data))
						{
							orderMoney += data.Price * line.Quantity;
						}

						// 보상 및 지연 패널티 계산 (현재 시점 기준)
						int bonus = line.BaseReward;
						float rep = line.ReputationChange;
						if (currentWeek > line.DueWeek)
						{
							bonus -= line.DelayPenalty;
							rep *= 0.2f;
						}
						orderMoney += bonus;
						orderRep += rep;
					}
				}

				ordersSb.AppendLine($"Order ID: {order.OrderID} <color=#AAAAAA>[{order.Status}]</color>");
				ordersSb.AppendLine($"  Due: Week {minStartWeek} - {maxDueWeek}");
				ordersSb.AppendLine($"  Expected: <color=#FFD700>${orderMoney}</color> / <color=#00FF00>Rep {orderRep:F1}</color>");

				foreach (var line in order.Lines)
				{
					string itemName = GetItemName(line.ItemID);
					string delayStatus = (currentWeek > line.DueWeek && line.Status != OrderStatus.Completed) ? "<color=red>[DELAYED]</color> " : "";
					ordersSb.AppendLine($"    - {itemName} x{line.Quantity} {delayStatus}<color=#888888>[{line.Status}]</color> {BuildLineProgressSummary(line)}");
				}
				ordersSb.AppendLine();

				totalExpectedMoney += orderMoney;
				totalExpectedRep += orderRep;
				count++;
			}

			sb.AppendLine($"[ Tab: {currentTab} ]");
			sb.AppendLine($"Total Orders: {count}");
			sb.AppendLine($"Total Expected Rewards: <color=#FFD700>${totalExpectedMoney}</color> | <color=#00FF00>Rep {totalExpectedRep:F1}</color>");
			sb.AppendLine("----------------------------");
			sb.Append(ordersSb.ToString());

			return sb.ToString();
		}

		private string GetStatsDisplayText()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("[ Last Week's Throughput ]");
			sb.AppendLine("----------------------------");

			var stats = GameContext.Instance.ProcessStats;

			sb.AppendLine(FormatStatRow("Picking", stats.GetStats(WorkerTask.TaskType.Picking).ProcessedLastWeek));
			sb.AppendLine(FormatStatRow("Packing", stats.GetStats(WorkerTask.TaskType.Packing).ProcessedLastWeek));
			sb.AppendLine(FormatStatRow("Loading", stats.GetStats(WorkerTask.TaskType.Loading).ProcessedLastWeek));

			sb.AppendLine("\n[ Current Week's Progress ]");
			sb.AppendLine("----------------------------");
			sb.AppendLine(FormatStatRow("Picking", stats.GetStats(WorkerTask.TaskType.Picking).ProcessedThisWeek));
			sb.AppendLine(FormatStatRow("Packing", stats.GetStats(WorkerTask.TaskType.Packing).ProcessedThisWeek));
			sb.AppendLine(FormatStatRow("Loading", stats.GetStats(WorkerTask.TaskType.Loading).ProcessedThisWeek));

			return sb.ToString();
		}

		private string FormatStatRow(string label, int value)
		{
			return $"{label,-15} : <color=#00FF00>{value}</color> units";
		}

		private bool ShouldShowInTab(Order order)
		{
			if (currentTab == TabType.All) return true;
			if (currentTab == TabType.Pending && order.Status == OrderTotalStatus.Pending) return true;
			if (currentTab == TabType.InProgress && order.Status == OrderTotalStatus.InProgress) return true;
			if (currentTab == TabType.Completed && order.Status == OrderTotalStatus.Completed) return true;
			return false;
		}

		private string GetItemName(uint itemID)
		{
			if (GameContext.Instance.ItemDB.GetItemData(itemID, out var data))
			{
				return data.name;
			}
			return "Unknown Item";
		}

		private static string BuildLineProgressSummary(OrderLine line)
		{
			List<string> parts = new();

			if (line.PickingAllocatedQuantity > 0 && line.PickingCompletedQuantity == 0)
				parts.Add($"Alloc {line.PickingAllocatedQuantity}/{line.Quantity}");

			if (line.PickingCompletedQuantity > 0)
				parts.Add($"Pick {line.PickingCompletedQuantity}/{line.Quantity}");

			if (line.PackagingCompletedQuantity > 0)
				parts.Add($"Pack {line.PackagingCompletedQuantity}/{line.Quantity}");

			if (line.WaitingForShippingQuantity > 0)
				parts.Add($"Port {line.WaitingForShippingQuantity}/{line.Quantity}");

			if (line.ShippingQuantity > 0)
				parts.Add($"Ship {line.ShippingQuantity}/{line.Quantity}");

			if (line.InDeliveryQuantity > 0)
				parts.Add($"Flight {line.InDeliveryQuantity}/{line.Quantity}");

			if (line.CompletedQuantity > 0 || line.Status == OrderStatus.Completed)
				parts.Add($"Done {line.CompletedQuantity}/{line.Quantity}");

			if (parts.Count == 0)
				parts.Add($"Pending 0/{line.Quantity}");

			return $"({string.Join(" | ", parts)})";
		}
	}
}
