using System.Collections.Generic;
using System.Text;
using AYellowpaper.SerializedCollections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum FloatingTextPreset
{
	Error,
	MoneyLoss,
	MoneyGain,
	WorkerStatus,
}

public enum FloatingTextPositionType
{
	Screen,
	World,
}

public readonly struct FloatingTextRequest
{
	public readonly FloatingTextPreset Preset;
	public readonly FloatingTextPositionType PositionType;
	public readonly Vector3 Position;
	public readonly string Text;
	public readonly float Duration;

	public FloatingTextRequest(
		FloatingTextPreset preset,
		FloatingTextPositionType positionType,
		Vector3 position,
		string text,
		float duration = 0f)
	{
		Preset = preset;
		PositionType = positionType;
		Position = position;
		Text = text;
		Duration = duration;
	}
}

[System.Serializable]
public struct FloatingTextStyle
{
	public float Duration;
	public float FontSize;
	public Color Color;

	public FloatingTextStyle(float duration, float fontSize, Color color)
	{
		Duration = duration;
		FontSize = fontSize;
		Color = color;
	}
}

public sealed class FloatingTextManager : MonoBehaviour
{
	private const string CanvasRootResourcePath = "UI/FloatingTextCanvasRoot";
	private const string EntryResourcePath = "UI/FloatingTextEntry";

	[SerializeField] private int initialPoolSize = 12;
	[SerializeField] private float riseDistance = 56f;
	[SerializeField] private float stackSpacing = 22f;
	[SerializedDictionary("Preset", "Style")]
	[SerializeField] private SerializedDictionary<FloatingTextPreset, FloatingTextStyle> presetStyles = new();

	private readonly List<Entry> pooledEntries = new();
	private readonly List<Entry> activeEntries = new();

	private Canvas canvas;
	private RectTransform canvasRect;
	private RectTransform textRoot;
	private AIWorker selectedWorker;
	private GameObject canvasRootPrefab;
	private GameObject entryPrefab;

	private sealed class Entry
	{
		public GameObject Root;
		public RectTransform Rect;
		public TextMeshProUGUI Text;
		public float Elapsed;
		public float Duration;
		public Vector2 StartPosition;
		public Color BaseColor;
	}

	private void Awake()
	{
		EnsurePresetStyles();
		EnsurePrefabs();
		EnsureCanvas();
		WarmPool();
		SubscribeSelection();
	}

	private void OnDestroy()
	{
		UnsubscribeSelection();
		DetachSelectedWorker();
	}

	private void OnValidate()
	{
		EnsurePresetStyles();
	}

	private void Update()
	{
		for (int i = activeEntries.Count - 1; i >= 0; --i)
		{
			Entry entry = activeEntries[i];
			entry.Elapsed += Time.deltaTime;

			float duration = Mathf.Max(0.01f, entry.Duration);
			float t = Mathf.Clamp01(entry.Elapsed / duration);

			entry.Rect.anchoredPosition = entry.StartPosition + Vector2.up * (riseDistance * t);

			Color color = entry.BaseColor;
			color.a *= 1f - t;
			entry.Text.color = color;

			if (t >= 1f)
				ReleaseEntry(i);
		}
	}

	public void Show(FloatingTextRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Text))
			return;

		EnsureCanvas();
		if (canvasRect == null || textRoot == null)
			return;

		if (TryResolveAnchoredPosition(request, out Vector2 anchoredPosition) == false)
			return;

		FloatingTextStyle style = ResolveStyle(request.Preset);
		float duration = request.Duration > 0f ? request.Duration : style.Duration;
		float stackOffset = CountNearbyEntries(anchoredPosition) * stackSpacing;

		Entry entry = GetEntry();
		if (entry.Root == null || entry.Rect == null || entry.Text == null)
			return;

		entry.Elapsed = 0f;
		entry.Duration = duration;
		entry.StartPosition = anchoredPosition + Vector2.up * stackOffset;
		entry.BaseColor = style.Color;
		entry.Rect.anchoredPosition = entry.StartPosition;
		entry.Text.text = request.Text;
		entry.Text.fontSize = style.FontSize;
		entry.Text.color = style.Color;
		entry.Root.SetActive(true);

		activeEntries.Add(entry);
	}

	public void ShowScreen(FloatingTextPreset preset, string text, Vector3 screenPosition, float duration = 0f)
	{
		Show(new FloatingTextRequest(preset, FloatingTextPositionType.Screen, screenPosition, text, duration));
	}

	public void ShowWorld(FloatingTextPreset preset, string text, Vector3 worldPosition, float duration = 0f)
	{
		Show(new FloatingTextRequest(preset, FloatingTextPositionType.World, worldPosition, text, duration));
	}

	private void SubscribeSelection()
	{
		if (GameContext.HasInstance == false || GameContext.Instance.InteractionCtx == null)
			return;

		GameContext.Instance.InteractionCtx.OnItemSelected -= HandleSelectionChanged;
		GameContext.Instance.InteractionCtx.OnItemSelected += HandleSelectionChanged;
	}

	private void UnsubscribeSelection()
	{
		if (GameContext.HasInstance == false || GameContext.Instance.InteractionCtx == null)
			return;

		GameContext.Instance.InteractionCtx.OnItemSelected -= HandleSelectionChanged;
	}

	private void HandleSelectionChanged(GameObject selectedObject)
	{
		AIWorker nextWorker = selectedObject != null ? selectedObject.GetComponentInParent<AIWorker>() : null;
		if (nextWorker == selectedWorker)
			return;

		DetachSelectedWorker();
		selectedWorker = nextWorker;

		if (selectedWorker != null)
			selectedWorker.OnStatusChanged += HandleWorkerStatusChanged;
	}

	private void HandleWorkerStatusChanged(AIWorker worker, WorkerStatusAction oldStatus, WorkerStatusAction newStatus)
	{
		if (worker == null || worker != selectedWorker || newStatus == WorkerStatusAction.None || oldStatus == newStatus)
			return;

		Vector3 worldPosition = worker.StatusSlot != null
			? worker.StatusSlot.position
			: worker.transform.position + Vector3.up * 1.6f;

		ShowWorld(FloatingTextPreset.WorkerStatus, BuildWorkerStatusText(worker, newStatus), worldPosition);
	}

	private void DetachSelectedWorker()
	{
		if (selectedWorker == null)
			return;

		selectedWorker.OnStatusChanged -= HandleWorkerStatusChanged;
		selectedWorker = null;
	}

	private void EnsureCanvas()
	{
		if (canvas != null && canvasRect != null && textRoot != null)
			return;

		EnsurePrefabs();
		if (canvasRootPrefab == null)
		{
			Debug.LogError("[FloatingText] Missing canvas root prefab.");
			return;
		}

		GameObject canvasObject = Instantiate(canvasRootPrefab, transform);
		canvasObject.transform.SetParent(transform, false);

		canvas = canvasObject.GetComponent<Canvas>();
		canvasRect = canvasObject.GetComponent<RectTransform>();
		Transform rootTransform = canvasObject.transform.Find("FloatingTextRoot");
		textRoot = rootTransform as RectTransform;
		if (textRoot == null)
			Debug.LogError("[FloatingText] FloatingTextRoot child is missing on canvas root prefab.");
	}

	private void WarmPool()
	{
		for (int i = pooledEntries.Count; i < initialPoolSize; ++i)
		{
			Entry entry = CreateEntry();
			if (entry.Root == null)
				break;

			pooledEntries.Add(entry);
		}
	}

	private void EnsurePresetStyles()
	{
		presetStyles ??= new SerializedDictionary<FloatingTextPreset, FloatingTextStyle>();
		ApplyDefaultStyle(FloatingTextPreset.Error);
		ApplyDefaultStyle(FloatingTextPreset.MoneyLoss);
		ApplyDefaultStyle(FloatingTextPreset.MoneyGain);
		ApplyDefaultStyle(FloatingTextPreset.WorkerStatus);
	}

	private void ApplyDefaultStyle(FloatingTextPreset preset)
	{
		if (presetStyles.ContainsKey(preset))
			return;

		presetStyles[preset] = CreateDefaultStyle(preset);
	}

	private Entry GetEntry()
	{
		for (int i = 0; i < pooledEntries.Count; ++i)
		{
			if (pooledEntries[i].Root != null && pooledEntries[i].Root.activeSelf == false)
				return pooledEntries[i];
		}

		Entry created = CreateEntry();
		if (created.Root == null)
			return created;

		pooledEntries.Add(created);
		return created;
	}

	private Entry CreateEntry()
	{
		EnsurePrefabs();
		if (entryPrefab == null)
		{
			Debug.LogError("[FloatingText] Missing entry prefab.");
			return new Entry();
		}

		GameObject textObject = Instantiate(entryPrefab, textRoot);

		RectTransform rect = textObject.GetComponent<RectTransform>();
		TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
		text.alignment = TextAlignmentOptions.Center;
		text.raycastTarget = false;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.overflowMode = TextOverflowModes.Overflow;

		textObject.SetActive(false);

		return new Entry
		{
			Root = textObject,
			Rect = rect,
			Text = text,
		};
	}

	private void EnsurePrefabs()
	{
		if (canvasRootPrefab == null)
			canvasRootPrefab = Resources.Load<GameObject>(CanvasRootResourcePath);

		if (entryPrefab == null)
			entryPrefab = Resources.Load<GameObject>(EntryResourcePath);
	}

	private void ReleaseEntry(int activeIndex)
	{
		Entry entry = activeEntries[activeIndex];
		entry.Text.text = string.Empty;
		entry.Root.SetActive(false);
		activeEntries.RemoveAt(activeIndex);
	}

	private bool TryResolveAnchoredPosition(FloatingTextRequest request, out Vector2 anchoredPosition)
	{
		Vector3 screenPosition = request.Position;

		if (request.PositionType == FloatingTextPositionType.World)
		{
			Camera camera = Camera.main;
			if (camera == null)
			{
				anchoredPosition = default;
				return false;
			}

			screenPosition = camera.WorldToScreenPoint(request.Position);
			if (screenPosition.z < 0f)
			{
				anchoredPosition = default;
				return false;
			}
		}

		return RectTransformUtility.ScreenPointToLocalPointInRectangle(
			canvasRect,
			screenPosition,
			null,
			out anchoredPosition);
	}

	private int CountNearbyEntries(Vector2 anchoredPosition)
	{
		int count = 0;
		for (int i = 0; i < activeEntries.Count; ++i)
		{
			if (Vector2.Distance(activeEntries[i].StartPosition, anchoredPosition) <= 48f)
				count++;
		}

		return count;
	}

	private FloatingTextStyle ResolveStyle(FloatingTextPreset preset)
	{
		if (presetStyles != null && presetStyles.TryGetValue(preset, out FloatingTextStyle style))
			return style;

		return CreateDefaultStyle(preset);
	}

	private static FloatingTextStyle CreateDefaultStyle(FloatingTextPreset preset)
	{
		return preset switch
		{
			FloatingTextPreset.Error => new FloatingTextStyle(1.1f, 30f, new Color(1f, 0.35f, 0.35f, 1f)),
			FloatingTextPreset.MoneyLoss => new FloatingTextStyle(1.0f, 30f, new Color(1f, 0.82f, 0.3f, 1f)),
			FloatingTextPreset.MoneyGain => new FloatingTextStyle(1.0f, 30f, new Color(0.45f, 1f, 0.45f, 1f)),
			FloatingTextPreset.WorkerStatus => new FloatingTextStyle(0.9f, 30f, new Color(0.9f, 0.96f, 1f, 1f)),
			_ => new FloatingTextStyle(1f, 30f, Color.white),
		};
	}

	private static string BuildWorkerStatusText(AIWorker worker, WorkerStatusAction action)
	{
		WorkerStatusTarget target = worker != null ? worker.WorkerState.Target : WorkerStatusTarget.None;

		return action switch
		{
			WorkerStatusAction.MovingTo => target != WorkerStatusTarget.None
				? $"Moving To {FormatEnumLabel(target.ToString())}"
				: "Moving",
			WorkerStatusAction.WaitingForTargetBuilding => target != WorkerStatusTarget.None
				? $"Waiting For {FormatEnumLabel(target.ToString())}"
				: "Waiting For Target",
			WorkerStatusAction.WaitingForItems => "Waiting For Items",
			WorkerStatusAction.TrafficBlock => "Traffic Blocked",
			WorkerStatusAction.HandlingMistake => "Handling Mistake",
			WorkerStatusAction.UsingAirlock => "Using Airlock",
			WorkerStatusAction.Working => "Working",
			WorkerStatusAction.Resting => "Resting",
			WorkerStatusAction.Charging => "Charging",
			WorkerStatusAction.Collapse => "Collapsed",
			WorkerStatusAction.Idle => "Idle",
			_ => FormatEnumLabel(action.ToString()),
		};
	}

	private static string FormatEnumLabel(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return string.Empty;

		StringBuilder builder = new(raw.Length + 8);
		for (int i = 0; i < raw.Length; ++i)
		{
			char current = raw[i];
			if (i > 0 && char.IsUpper(current) && char.IsLower(raw[i - 1]))
				builder.Append(' ');

			builder.Append(current);
		}

		return builder.ToString();
	}
}
