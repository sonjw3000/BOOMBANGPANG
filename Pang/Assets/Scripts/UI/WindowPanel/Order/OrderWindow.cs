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
            Completed
        }

        [SerializeField] private UIWindow window;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Window MetaData")]
        [SerializeField] private string title = "Order Tracker";
        [SerializeField] private Sprite icon;

        private OrderManager orderMgr => GameContext.Instance.OrderMgr;
        private TabType currentTab = TabType.All;

        private void Awake()
        {
            window.SetTitle(title);
            window.SetIcon(icon);

            if (statusText != null)
            {
                statusText.rectTransform.SetParent(window.ContentRoot, false);
            }
            
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
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

                foreach(var line in order.Lines)
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
                    ordersSb.AppendLine($"    - {itemName} x{line.Quantity} {delayStatus}<color=#888888>[{line.Status}]</color>");
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

            string newText = sb.ToString();
            if (newText != lastText)
            {
                statusText.text = newText;
                lastText = newText;
                
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(window.ContentRoot);
            }
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
    }
}
