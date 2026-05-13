using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Contract;

namespace Assets.Scripts.UI
{
	public class ContractWindow : MonoBehaviour
	{
		[SerializeField] private UIWindow window;
		[SerializeField] private ContractItemView itemPrefab;
		[SerializeField] private Transform listRoot;
		[SerializeField] private UnityEngine.UI.Button openMarketButton;
		[SerializeField] private UnityEngine.UI.Button historyButton; // Placeholder for now
		[SerializeField] private ContractMarketWindow marketWindow;

		[Header("Window MetaData")]
		[SerializeField] private string title = "Contract Management";
		[SerializeField] private Sprite icon;

		private ContractService contractService => GameContext.Instance.ContractMgr;
		private GameObjectPool itemPool;

		private void Awake()
		{
			if (window == null) window = GetComponentInChildren<UIWindow>(true);

			window.SetTitle(title);
			window.SetIcon(icon);
			
			if (itemPrefab != null && listRoot != null)
			{
				itemPool = new GameObjectPool(10, () => Instantiate(itemPrefab.gameObject, listRoot));
			}

			if (openMarketButton != null)
			{
				openMarketButton.onClick.AddListener(OpenMarket);
				var btnText = openMarketButton.GetComponentInChildren<TMP_Text>();
				if (btnText != null) btnText.text = "Sign Contract";
			}

			gameObject.SetActive(false);
		}

		private void OpenMarket()
		{
			if (marketWindow != null)
			{
				marketWindow.Open();
			}
		}

		private void OnEnable()
		{
			RefreshList();
		}

		private void Update()
		{
			if (Time.frameCount % 60 == 0) // Refresh every 60 frames
			{
				RefreshList();
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

		private void RefreshList()
		{
			if (contractService == null || itemPool == null) return;

			itemPool.ReleaseAll();
			var contracts = contractService.ActiveContracts;

			foreach (var contract in contracts)
			{
				var item = itemPool.Get().GetComponent<ContractItemView>();
				item.Setup(contract);
			}
		}
	}
}
