using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;
using System;

namespace Assets.Scripts.UI
{
	public class WorkforceMarketWindow : MonoBehaviour
	{
		[SerializeField] private UIWindow window;

		[Header("Data")]
		[SerializeField] private List<WorkforceMarketData_SO> humanCategories;
		[SerializeField] private List<WorkforceMarketData_SO> robotCategories;

		[Header("UI References")]
		[SerializeField] private Transform categoryListRoot;
		[SerializeField] private Transform workerListRoot;
		[SerializeField] private GameObject categoryItemPrefab;
		[SerializeField] private GameObject workerItemPrefab;

		[Header("Window MetaData")]
		[SerializeField] private string title = "Workforce Market";
		[SerializeField] private Sprite icon;

		[SerializeField] private int displayPerPage = 15;
		[SerializeField] private int page = 0;

		private GameObjectPool workerListPool;

		private int randomSeed = 0;

		private void Awake()
		{
			workerListPool = new(15, () => { return Instantiate(workerItemPrefab, workerListRoot); });

			if (window == null) window = GetComponentInChildren<UIWindow>(true);
			window.SetTitle(title);
			window.SetIcon(icon);
			gameObject.SetActive(false);
		}

		public void Open()
		{
			gameObject.SetActive(true);
			window.Open();
			RefreshCategories();
		}

		public void Close()
		{
			window.Close();
			gameObject.SetActive(false);
		}

		private void RefreshCategories()
		{
			// set random seed
			randomSeed = UnityEngine.Random.Range(0, int.MaxValue);

			// Clear existing
			foreach (Transform child in categoryListRoot) Destroy(child.gameObject);

			// Add Human Header
			CreateCategoryHeader("HUMAN");
			foreach (var so in humanCategories)
			{
				CreateCategoryItem(so);
			}

			// Add Robot Header
			CreateCategoryHeader("ROBOT");
			foreach (var so in robotCategories)
			{
				CreateCategoryItem(so);
			}
		}

		private void CreateCategoryHeader(string label)
		{
			// Simplified: just a text label
			GameObject go = new GameObject(label, typeof(RectTransform), typeof(TextMeshProUGUI));
			go.transform.SetParent(categoryListRoot, false);
			var txt = go.GetComponent<TextMeshProUGUI>();
			txt.text = label;
			txt.fontSize = 16;
			txt.fontStyle = FontStyles.Bold;
		}

		private void CreateCategoryItem(WorkforceMarketData_SO so)
		{
			GameObject go = Instantiate(categoryItemPrefab, categoryListRoot);
			// Assuming categoryItem has a text component or a script
			var btn = go.GetComponent<Button>();
			btn.GetComponentInChildren<TMP_Text>().text = $" - {so.WorkForceMarketName}";
			btn.onClick.AddListener(() => DisplayWorkerList(so));
		}

		private void DisplayWorkerList(WorkforceMarketData_SO so)
		{
			workerListPool.ReleaseAll();

			int seed = randomSeed + page * 1000 + page;
			System.Random rng = new(seed);

			for (int i = 0; i < displayPerPage && page * displayPerPage + i < so.GetMaxCount(); ++i)
			{
				MarketWorkerItem workerItem = workerListPool.Get().GetComponent<MarketWorkerItem>();
				so.FillWorkerArchetype(workerItem.CurrentArchetype, rng, page, i);
				workerItem.Setup();
			}
		}
	}
}
