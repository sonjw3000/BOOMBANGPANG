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

		private void Awake()
		{
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
			foreach (Transform child in workerListRoot)
				Destroy(child.gameObject);

			foreach (var archetype in so.EnumerateArchetypes(page, displayPerPage))
			{
				// Robot check if needed (user requested verification)
				if (so.name.ToLower().Contains("robot") || archetype.AbilityDefinition.workerType == WorkerType.Robot)
				{
					if (archetype.AbilityDefinition.workerType != WorkerType.Robot) continue;
				}

				GameObject go = Instantiate(workerItemPrefab, workerListRoot);
				var itemView = go.GetComponent<MarketWorkerItem>();
				itemView.Setup(archetype);
			}
		}
	}
}
