using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Contract;

namespace Assets.Scripts.UI
{
	public sealed class EventNoticeService : MonoBehaviour
	{
		private const int DefaultContractExtensionMonths = 12;

		[SerializeField] private EventNoticeWindow eventNoticeWindowPrefab;
		[SerializeField] private Transform windowParent;
		[SerializeField, Min(1)] private int initialPoolSize = 1;
		[SerializeField] private Vector2 initialWindowPosition = Vector2.zero;
		[SerializeField] private Vector2 stackedWindowOffset = new(32f, -32f);
		[SerializeField, Min(1)] private int expiredContractExtensionMonths = DefaultContractExtensionMonths;

		private readonly Stack<EventNoticeWindow> pooledWindows = new();
		private readonly List<EventNoticeWindow> activeWindows = new();
		private bool poolInitialized;
		private ContractService ContractService => GameContext.HasInstance ? GameContext.Instance.ContractMgr : null;
		private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;

		private void Awake()
		{
			EnsurePoolInitialized();
		}

		private void OnEnable()
		{
			if (ContractService != null)
				ContractService.OnContractExpired += HandleContractExpired;

			if (GridService != null)
				GridService.OnPlaceableOverridden += HandlePlaceableOverridden;
		}

		private void OnDisable()
		{
			if (ContractService != null)
				ContractService.OnContractExpired -= HandleContractExpired;

			if (GridService != null)
				GridService.OnPlaceableOverridden -= HandlePlaceableOverridden;
		}

		private void EnsurePoolInitialized()
		{
			if (poolInitialized || eventNoticeWindowPrefab == null)
				return;

			int targetCount = Mathf.Max(1, initialPoolSize);
			for (int i = 0; i < targetCount; ++i)
			{
				EventNoticeWindow instance = CreateWindowInstance();
				ReleaseWindow(instance);
			}

			poolInitialized = true;
		}

		private EventNoticeWindow CreateWindowInstance()
		{
			Transform parent = windowParent != null ? windowParent : transform;
			EventNoticeWindow instance = Instantiate(eventNoticeWindowPrefab, parent);
			instance.name = eventNoticeWindowPrefab.name;
			instance.gameObject.SetActive(false);
			instance.Dismissed -= HandleWindowDismissed;
			instance.Dismissed += HandleWindowDismissed;
			return instance;
		}

		private EventNoticeWindow AcquireWindow()
		{
			EnsurePoolInitialized();

			if (pooledWindows.Count > 0)
				return pooledWindows.Pop();

			return CreateWindowInstance();
		}

		private void ReleaseWindow(EventNoticeWindow window)
		{
			if (window == null)
				return;

			window.transform.SetParent(windowParent != null ? windowParent : transform, false);
			window.gameObject.SetActive(false);
			pooledWindows.Push(window);
		}

		private void HandleWindowDismissed(EventNoticeWindow window)
		{
			if (window == null)
				return;

			activeWindows.Remove(window);
			RepositionActiveWindows();
			ReleaseWindow(window);
		}

		private void RepositionActiveWindows()
		{
			for (int i = 0; i < activeWindows.Count; ++i)
			{
				RectTransform rect = activeWindows[i].GetComponent<RectTransform>();
				if (rect == null)
					continue;

				rect.anchoredPosition = GetWindowPosition(i);
			}
		}

		private Vector2 GetWindowPosition(int index)
		{
			return initialWindowPosition + stackedWindowOffset * index;
		}

		public void ShowNotice(EventNoticeRequest request)
		{
			if (request == null)
				return;

			EventNoticeWindow window = AcquireWindow();
			activeWindows.Add(window);
			window.Show(request, GetWindowPosition(activeWindows.Count - 1));
		}

		private void HandleContractExpired(ContractRuntime contract)
		{
			if (contract?.Definition == null)
				return;

			string contractName = string.IsNullOrWhiteSpace(contract.Definition.ContractName)
				? "Unnamed Contract"
				: contract.Definition.ContractName;

			ShowNotice(new EventNoticeRequest(
				"Contract Expired",
				$"Contract '{contractName}' has expired.\nExtend the same contract for {expiredContractExtensionMonths} months if you want to keep it running.",
				extraAction: new EventNoticeAction(
					"Extend Contract",
					() => ContractService?.TryExtendExpiredContract(contract, expiredContractExtensionMonths))));
		}

		private void HandlePlaceableOverridden(PlaceableOverrideReport report)
		{
			if (report?.TargetObject == null)
				return;

			if (report.TargetObject.TryGetComponent<AIWorker>(out var worker) == false)
				return;

			ShowNotice(new EventNoticeRequest(
				"Worker Overridden",
				$"Worker '{worker.Name}' was overridden by {GetOverridingName(report)}.\nReview the placement area before continuing.",
				report.OverridingDefinition != null ? report.OverridingDefinition.icon : null));
		}

		private static string GetOverridingName(PlaceableOverrideReport report)
		{
			if (report == null)
				return "another placement";

			if (report.OverridingDefinition != null && string.IsNullOrWhiteSpace(report.OverridingDefinition.displayName) == false)
				return report.OverridingDefinition.displayName;

			if (report.OverridingObject != null)
				return report.OverridingObject.name;

			return "another placement";
		}
	}
}
