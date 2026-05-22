using System;
using UnityEngine;

namespace Assets.Scripts.UI
{
	public sealed class EventNoticeAction
	{
		public string Label { get; }

		private readonly Action callback;

		public EventNoticeAction(string label, Action callback)
		{
			Label = string.IsNullOrWhiteSpace(label) ? "Action" : label;
			this.callback = callback;
		}

		public void Invoke()
		{
			callback?.Invoke();
		}
	}

	public sealed class EventNoticeRequest
	{
		public string Title { get; }
		public string Message { get; }
		public Sprite Icon { get; }
		public EventNoticeAction ExtraAction { get; }

		public EventNoticeRequest(string title, string message, Sprite icon = null, EventNoticeAction extraAction = null)
		{
			Title = string.IsNullOrWhiteSpace(title) ? "Event Notice" : title;
			Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
			Icon = icon;
			ExtraAction = extraAction;
		}
	}
}
