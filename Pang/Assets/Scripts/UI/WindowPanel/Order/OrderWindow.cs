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
            
            // Future Tab Header Placeholder
            sb.AppendLine($"[ Tab: {currentTab} ]");
            sb.AppendLine("----------------------------");

            IEnumerable<Order> targetOrders = orderMgr.Orders;
            
            int count = 0;
            foreach (var order in targetOrders)
            {
                if (!ShouldShowInTab(order)) continue;

                sb.AppendLine($"Order ID: {order.OrderID} <color=#AAAAAA>[{order.Status}]</color>");
                foreach (var line in order.Lines)
                {
                    string itemName = GetItemName(line.ItemID);
                    sb.AppendLine($"  - {itemName} (ID:{line.ItemID}): {line.Quantity} <color=#888888>[{line.Status}]</color>");
                }
                sb.AppendLine();
                count++;
            }

            sb.Insert(0, $"Total Displayed: {count}\n");
            
            string newText = sb.ToString();
            if (newText != lastText)
            {
                statusText.text = newText;
                lastText = newText;
                
                // Force layout update so ScrollRect knows the new size immediately
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
