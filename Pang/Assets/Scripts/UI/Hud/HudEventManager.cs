using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum HudEventType
{
	Info,
	Money,
	Reputation,
	Warning,
	Error,
}

public readonly struct HudEventRequest
{
	public readonly HudEventType Type;
	public readonly string Message;
	public readonly int MoneyDelta;
	public readonly float ReputationDelta;
	public readonly Object Source;
	public readonly float Duration;

	public HudEventRequest(
		HudEventType type,
		string message,
		int moneyDelta = 0,
		float reputationDelta = 0f,
		Object source = null,
		float duration = 0f)
	{
		Type = type;
		Message = message;
		MoneyDelta = moneyDelta;
		ReputationDelta = reputationDelta;
		Source = source;
		Duration = duration;
	}
}

public sealed class HudEventRecord
{
	public HudEventType Type { get; }
	public string Message { get; }
	public int MoneyDelta { get; }
	public float ReputationDelta { get; }
	public Object Source { get; }
	public float Time { get; }

	public HudEventRecord(HudEventRequest request, float time)
	{
		Type = request.Type;
		Message = request.Message;
		MoneyDelta = request.MoneyDelta;
		ReputationDelta = request.ReputationDelta;
		Source = request.Source;
		Time = time;
	}
}

public sealed class HudEventManager : MonoBehaviour
{
	[SerializeField, Min(1)] private int maxHistoryCount = 100;
	[SerializeField, Min(1)] private int maxVisibleCount = 5;
	[SerializeField, Min(0.1f)] private float defaultVisibleSeconds = 4f;
	[SerializeField, Min(0.1f)] private float fadeSeconds = 0.8f;
	[SerializeField] private Vector2 panelOffset = new(24f, 96f);
	[SerializeField] private Vector2 panelSize = new(460f, 220f);
	[SerializeField] private float entryHeight = 32f;
	[SerializeField] private float entrySpacing = 6f;
	[SerializeField] private int fontSize = 20;

	private readonly Queue<HudEventRecord> history = new();
	private readonly List<Entry> activeEntries = new();

	private Canvas canvas;
	private RectTransform entryRoot;

	public IReadOnlyCollection<HudEventRecord> History => history;

	private sealed class Entry
	{
		public GameObject Root;
		public CanvasGroup CanvasGroup;
		public TextMeshProUGUI Text;
		public float Elapsed;
		public float VisibleSeconds;
	}

	private void Awake()
	{
		EnsureView();
	}

	private void Update()
	{
		for (int i = activeEntries.Count - 1; i >= 0; --i)
		{
			Entry entry = activeEntries[i];
			entry.Elapsed += Time.deltaTime;

			float fadeStart = Mathf.Max(0.01f, entry.VisibleSeconds);
			float fadeT = Mathf.Clamp01((entry.Elapsed - fadeStart) / Mathf.Max(0.01f, fadeSeconds));
			entry.CanvasGroup.alpha = 1f - fadeT;

			if (fadeT >= 1f)
			{
				Destroy(entry.Root);
				activeEntries.RemoveAt(i);
			}
		}
	}

	public void Publish(HudEventRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Message))
			return;

		HudEventRecord record = new(request, Time.time);
		history.Enqueue(record);
		while (history.Count > maxHistoryCount)
		{
			history.Dequeue();
		}

		ShowRecord(record, request.Duration > 0f ? request.Duration : defaultVisibleSeconds);
	}

	public void Publish(HudEventType type, string message, Object source = null, float duration = 0f)
	{
		Publish(new HudEventRequest(type, message, source: source, duration: duration));
	}

	public void PublishMoney(int delta, string reason, Object source = null, float duration = 0f)
	{
		if (delta == 0)
			return;

		string sign = delta > 0 ? "+" : "-";
		Publish(new HudEventRequest(
			HudEventType.Money,
			$"{sign}${Mathf.Abs(delta)} {reason}",
			moneyDelta: delta,
			source: source,
			duration: duration));
	}

	public void PublishReputation(float delta, string reason, Object source = null, float duration = 0f)
	{
		if (Mathf.Approximately(delta, 0f))
			return;

		string sign = delta > 0f ? "+" : "-";
		Publish(new HudEventRequest(
			HudEventType.Reputation,
			$"{sign}{Mathf.Abs(delta):0.#} Rep {reason}",
			reputationDelta: delta,
			source: source,
			duration: duration));
	}

	private void ShowRecord(HudEventRecord record, float visibleSeconds)
	{
		EnsureView();
		if (entryRoot == null)
			return;

		while (activeEntries.Count >= maxVisibleCount)
		{
			Entry oldest = activeEntries[0];
			Destroy(oldest.Root);
			activeEntries.RemoveAt(0);
		}

		GameObject entryObject = CreateEntryObject(record);
		Entry entry = new()
		{
			Root = entryObject,
			CanvasGroup = entryObject.GetComponent<CanvasGroup>(),
			Text = entryObject.GetComponentInChildren<TextMeshProUGUI>(),
			Elapsed = 0f,
			VisibleSeconds = visibleSeconds,
		};

		activeEntries.Add(entry);
	}

	private GameObject CreateEntryObject(HudEventRecord record)
	{
		GameObject entryObject = new("HudEventEntry", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(LayoutElement));
		entryObject.transform.SetParent(entryRoot, false);

		RectTransform rect = entryObject.GetComponent<RectTransform>();
		rect.sizeDelta = new Vector2(panelSize.x, entryHeight);

		LayoutElement layout = entryObject.GetComponent<LayoutElement>();
		layout.preferredHeight = entryHeight;

		Image background = entryObject.GetComponent<Image>();
		background.color = new Color(0.06f, 0.07f, 0.08f, 0.72f);
		background.raycastTarget = false;

		GameObject textObject = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
		textObject.transform.SetParent(entryObject.transform, false);

		RectTransform textRect = textObject.GetComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = new Vector2(10f, 2f);
		textRect.offsetMax = new Vector2(-10f, -2f);

		TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
		text.text = record.Message;
		text.fontSize = fontSize;
		text.alignment = TextAlignmentOptions.MidlineLeft;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.overflowMode = TextOverflowModes.Ellipsis;
		text.raycastTarget = false;
		text.color = ResolveColor(record.Type, record.MoneyDelta, record.ReputationDelta);

		return entryObject;
	}

	private void EnsureView()
	{
		if (canvas != null && entryRoot != null)
			return;

		GameObject canvasObject = new("HudEventCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		canvasObject.transform.SetParent(transform, false);

		canvas = canvasObject.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 20;

		CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;

		GameObject panelObject = new("HudEventPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
		panelObject.transform.SetParent(canvasObject.transform, false);

		entryRoot = panelObject.GetComponent<RectTransform>();
		entryRoot.anchorMin = Vector2.zero;
		entryRoot.anchorMax = Vector2.zero;
		entryRoot.pivot = Vector2.zero;
		entryRoot.anchoredPosition = panelOffset;
		entryRoot.sizeDelta = panelSize;

		VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
		layout.spacing = entrySpacing;
		layout.childAlignment = TextAnchor.LowerLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
	}

	private static Color ResolveColor(HudEventType type, int moneyDelta, float reputationDelta)
	{
		return type switch
		{
			HudEventType.Money when moneyDelta >= 0 => new Color(0.45f, 1f, 0.45f, 1f),
			HudEventType.Money => new Color(1f, 0.82f, 0.3f, 1f),
			HudEventType.Reputation when reputationDelta >= 0f => new Color(0.55f, 0.85f, 1f, 1f),
			HudEventType.Reputation => new Color(1f, 0.55f, 0.55f, 1f),
			HudEventType.Warning => new Color(1f, 0.78f, 0.35f, 1f),
			HudEventType.Error => new Color(1f, 0.35f, 0.35f, 1f),
			_ => Color.white,
		};
	}
}
