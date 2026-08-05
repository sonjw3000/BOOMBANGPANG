using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class UITooltipPresenter : MonoBehaviour
	{
		private const string LockedClass = "ui-tooltip--locked";
		private const int DefaultShowDelayMilliseconds = 350;
		private const float PointerOffsetX = 16f;
		private const float PointerOffsetY = 18f;
		private const float PanelEdgePadding = 12f;

		private VisualElement documentRoot;
		private VisualElement tooltipRoot;
		private Label titleLabel;
		private Label descriptionLabel;
		private VisualElement requirementRoot;
		private Label requirementLabel;
		private IVisualElementScheduledItem pendingShow;
		private VisualElement requestedTarget;
		private Vector2 requestedPosition;
		[System.NonSerialized] private bool initialized;

		public void Initialize(VisualElement root)
		{
			documentRoot = root;
			tooltipRoot = documentRoot?.Q<VisualElement>("ui-tooltip");
			titleLabel = documentRoot?.Q<Label>("ui-tooltip-title");
			descriptionLabel = documentRoot?.Q<Label>("ui-tooltip-description");
			requirementRoot = documentRoot?.Q<VisualElement>("ui-tooltip-requirement");
			requirementLabel = documentRoot?.Q<Label>("ui-tooltip-requirement-label");

			initialized = tooltipRoot != null && titleLabel != null && descriptionLabel != null &&
				requirementRoot != null && requirementLabel != null;
			if (initialized == false)
			{
				Debug.LogError("[UITooltipPresenter] Required UXML elements are missing.", this);
				return;
			}

			HideImmediate();
			UITooltipService.Register(this);
		}

		public void RequestShow(
			UITooltipContent content,
			VisualElement target,
			Vector2 panelPosition,
			int delayMilliseconds = DefaultShowDelayMilliseconds)
		{
			CancelPendingShow();
			if (initialized == false || content.HasContent == false || target == null)
				return;

			requestedTarget = target;
			requestedPosition = panelPosition;
			pendingShow = documentRoot.schedule.Execute(() => ShowImmediate(content, target));
			pendingShow.StartingIn(Mathf.Max(0, delayMilliseconds));
		}

		public void Move(VisualElement target, Vector2 panelPosition)
		{
			if (target == null || ReferenceEquals(requestedTarget, target) == false)
				return;

			requestedPosition = panelPosition;
			if (tooltipRoot.resolvedStyle.display != DisplayStyle.None)
				PositionTooltip();
		}

		public void Hide(VisualElement target)
		{
			if (target != null && ReferenceEquals(requestedTarget, target) == false)
				return;

			HideImmediate();
		}

		private void OnEnable()
		{
			if (initialized)
				UITooltipService.Register(this);
		}

		private void OnDisable()
		{
			UITooltipService.Unregister(this);
			HideImmediate();
		}

		private void ShowImmediate(UITooltipContent content, VisualElement target)
		{
			pendingShow = null;
			if (initialized == false || ReferenceEquals(requestedTarget, target) == false || target.panel == null)
				return;

			titleLabel.text = content.Title;
			titleLabel.style.display = string.IsNullOrWhiteSpace(content.Title)
				? DisplayStyle.None
				: DisplayStyle.Flex;
			descriptionLabel.text = content.Description;
			descriptionLabel.style.display = string.IsNullOrWhiteSpace(content.Description)
				? DisplayStyle.None
				: DisplayStyle.Flex;
			requirementLabel.text = content.Requirement;
			requirementRoot.style.display = content.HasRequirement
				? DisplayStyle.Flex
				: DisplayStyle.None;
			tooltipRoot.EnableInClassList(LockedClass, content.Tone == UITooltipTone.Locked);
			tooltipRoot.style.visibility = Visibility.Hidden;
			tooltipRoot.style.display = DisplayStyle.Flex;
			tooltipRoot.schedule.Execute(() =>
			{
				if (ReferenceEquals(requestedTarget, target) == false)
					return;

				PositionTooltip();
				tooltipRoot.style.visibility = Visibility.Visible;
			});
		}

		private void PositionTooltip()
		{
			if (documentRoot == null || tooltipRoot == null)
				return;

			float panelWidth = documentRoot.resolvedStyle.width;
			float panelHeight = documentRoot.resolvedStyle.height;
			float tooltipWidth = tooltipRoot.resolvedStyle.width;
			float tooltipHeight = tooltipRoot.resolvedStyle.height;
			if (float.IsNaN(panelWidth) || float.IsNaN(panelHeight) ||
				float.IsNaN(tooltipWidth) || float.IsNaN(tooltipHeight))
			{
				return;
			}

			float left = requestedPosition.x + PointerOffsetX;
			float top = requestedPosition.y + PointerOffsetY;
			if (left + tooltipWidth + PanelEdgePadding > panelWidth)
				left = requestedPosition.x - tooltipWidth - PointerOffsetX;
			if (top + tooltipHeight + PanelEdgePadding > panelHeight)
				top = requestedPosition.y - tooltipHeight - PointerOffsetY;

			tooltipRoot.style.left = Mathf.Clamp(left, PanelEdgePadding, Mathf.Max(PanelEdgePadding, panelWidth - tooltipWidth - PanelEdgePadding));
			tooltipRoot.style.top = Mathf.Clamp(top, PanelEdgePadding, Mathf.Max(PanelEdgePadding, panelHeight - tooltipHeight - PanelEdgePadding));
		}

		private void HideImmediate()
		{
			CancelPendingShow();
			requestedTarget = null;
			if (tooltipRoot != null)
			{
				tooltipRoot.style.display = DisplayStyle.None;
				tooltipRoot.style.visibility = Visibility.Hidden;
			}
		}

		private void CancelPendingShow()
		{
			pendingShow?.Pause();
			pendingShow = null;
		}
	}

	internal static class UITooltipService
	{
		private static UITooltipPresenter presenter;

		public static void Register(UITooltipPresenter targetPresenter)
		{
			presenter = targetPresenter;
		}

		public static void Unregister(UITooltipPresenter targetPresenter)
		{
			if (ReferenceEquals(presenter, targetPresenter))
				presenter = null;
		}

		public static void Show(UITooltipContent content, VisualElement target, Vector2 panelPosition)
		{
			presenter?.RequestShow(content, target, panelPosition);
		}

		public static void Move(VisualElement target, Vector2 panelPosition)
		{
			presenter?.Move(target, panelPosition);
		}

		public static void Hide(VisualElement target)
		{
			presenter?.Hide(target);
		}
	}
}
