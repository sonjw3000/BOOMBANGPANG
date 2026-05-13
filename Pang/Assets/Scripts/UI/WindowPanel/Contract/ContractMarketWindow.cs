using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Contract;
using System.Linq;

namespace Assets.Scripts.UI
{
	public class ContractMarketWindow : MonoBehaviour
	{
		private const float MinimumWindowWidth = 600f;
		private const float MinimumDetailWidth = 320f;

		[SerializeField] private UIWindow window;
		
		[Header("Left List")]
		[SerializeField] private ContractMarketListButton listButtonPrefab;
		[SerializeField] private Transform listRoot;

		[Header("Right Detail")]
		[SerializeField] private ContractMarketItemView detailView;

		[Header("Window MetaData")]
		[SerializeField] private string title = "Contract Market";
		[SerializeField] private Sprite icon;

		private ContractService contractService => GameContext.Instance.ContractMgr;
		private GameObjectPool itemPool;
		private bool initialized;

		private void Awake()
		{
			EnsureInitialized();
		}

		public void Open()
		{
			EnsureInitialized();
			if (window == null)
				return;

			if (listRoot == null)
				listRoot = FindListRoot();

			EnsureLayout();
			window.Open();
			RefreshList();
		}

		public void Close()
		{
			EnsureInitialized();
			if (window == null)
				return;

			window.Close();
		}

		private void RefreshList()
		{
			EnsureInitialized();
			if (contractService == null) return;

			if (listRoot == null)
				listRoot = FindListRoot();

			if (itemPool == null)
			{
				if (listButtonPrefab != null && listRoot != null)
				{
					itemPool = new GameObjectPool(10, () => Instantiate(listButtonPrefab.gameObject, listRoot));
				}
				else
				{
					return;
				}
			}

			itemPool.ReleaseAll();
			var definitions = contractService.ContractDefinitions;

			for (int i = 0; i < definitions.Count; i++)
			{
				var item = itemPool.Get().GetComponent<ContractMarketListButton>();
				item.Setup(i, definitions[i], OnContractSelected);
			}

			if (definitions.Count > 0)
			{
				OnContractSelected(0, definitions[0]);
			}
		}

		private void OnContractSelected(int index, ContractDefinition def)
		{
			if (detailView != null)
			{
				detailView.gameObject.SetActive(true);
				detailView.Setup(index, def);
			}
		}

		private void EnsureInitialized()
		{
			window ??= GetComponentInChildren<UIWindow>(true);
			detailView ??= GetComponentInChildren<ContractMarketItemView>(true);
			if (listRoot == null)
				listRoot = FindListRoot();

			EnsureLayout();

			if (initialized == false && window != null)
			{
				window.SetTitle(title);
				window.SetIcon(icon);
				window.Close();
				initialized = true;
			}

			if (itemPool == null && listButtonPrefab != null && listRoot != null)
			{
				itemPool = new GameObjectPool(10, () => Instantiate(listButtonPrefab.gameObject, listRoot));
			}
		}

		private Transform FindListRoot()
		{
			Transform exactPath = transform.Find("WindowBase/ContentRoot/LeftPanel/ListRoot");
			if (exactPath != null)
				return exactPath;

			return GetComponentsInChildren<Transform>(true)
				.FirstOrDefault(child => child.name == "ListRoot" && child.parent != null && child.parent.name == "LeftPanel");
		}

		private void EnsureLayout()
		{
			RectTransform windowRect = GetComponent<RectTransform>();
			if (windowRect != null && windowRect.sizeDelta.x < MinimumWindowWidth)
			{
				windowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, MinimumWindowWidth);
			}

			if (detailView == null)
				return;

			LayoutElement detailLayout = detailView.GetComponent<LayoutElement>();
			if (detailLayout != null)
			{
				if (detailLayout.minWidth < MinimumDetailWidth)
					detailLayout.minWidth = MinimumDetailWidth;

				if (detailLayout.preferredWidth < MinimumDetailWidth)
					detailLayout.preferredWidth = MinimumDetailWidth;

				if (detailLayout.flexibleWidth < 1f)
					detailLayout.flexibleWidth = 1f;
			}

			RectTransform detailRect = detailView.GetComponent<RectTransform>();
			if (detailRect != null && detailRect.sizeDelta.x < MinimumDetailWidth)
			{
				detailRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, MinimumDetailWidth);
			}
		}
	}
}
