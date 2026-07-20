using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class UITooltipManipulator : Manipulator
	{
		private Func<UITooltipContent> contentProvider;

		public UITooltipManipulator(Func<UITooltipContent> targetContentProvider)
		{
			SetContentProvider(targetContentProvider);
		}

		public void SetContentProvider(Func<UITooltipContent> targetContentProvider)
		{
			contentProvider = targetContentProvider ?? throw new ArgumentNullException(nameof(targetContentProvider));
		}

		protected override void RegisterCallbacksOnTarget()
		{
			target.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
			target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
			target.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
			target.RegisterCallback<FocusInEvent>(OnFocusIn);
			target.RegisterCallback<FocusOutEvent>(OnFocusOut);
			target.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
		}

		protected override void UnregisterCallbacksFromTarget()
		{
			target.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
			target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
			target.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
			target.UnregisterCallback<FocusInEvent>(OnFocusIn);
			target.UnregisterCallback<FocusOutEvent>(OnFocusOut);
			target.UnregisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
		}

		private void OnPointerEnter(PointerEnterEvent evt)
		{
			UITooltipService.Show(contentProvider(), target, evt.position);
		}

		private void OnPointerMove(PointerMoveEvent evt)
		{
			UITooltipService.Move(target, evt.position);
		}

		private void OnPointerLeave(PointerLeaveEvent evt)
		{
			UITooltipService.Hide(target);
		}

		private void OnFocusIn(FocusInEvent evt)
		{
			Rect bounds = target.worldBound;
			UITooltipService.Show(contentProvider(), target, new Vector2(bounds.center.x, bounds.yMax));
		}

		private void OnFocusOut(FocusOutEvent evt)
		{
			UITooltipService.Hide(target);
		}

		private void OnDetachFromPanel(DetachFromPanelEvent evt)
		{
			UITooltipService.Hide(target);
		}
	}

	public static class UITooltipExtensions
	{
		private static readonly ConditionalWeakTable<VisualElement, UITooltipManipulator> Manipulators = new();

		public static void SetTooltip(this VisualElement element, UITooltipContent content)
		{
			element.SetTooltip(() => content);
		}

		public static void SetTooltip(this VisualElement element, Func<UITooltipContent> contentProvider)
		{
			if (element == null)
				throw new ArgumentNullException(nameof(element));

			if (Manipulators.TryGetValue(element, out UITooltipManipulator manipulator))
			{
				manipulator.SetContentProvider(contentProvider);
				return;
			}

			manipulator = new UITooltipManipulator(contentProvider);
			Manipulators.Add(element, manipulator);
			element.AddManipulator(manipulator);
		}

		public static void ClearTooltip(this VisualElement element)
		{
			if (element == null || Manipulators.TryGetValue(element, out UITooltipManipulator manipulator) == false)
				return;

			UITooltipService.Hide(element);
			element.RemoveManipulator(manipulator);
			Manipulators.Remove(element);
		}
	}
}
