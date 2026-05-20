using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public abstract class DetailContentBase : MonoBehaviour
{
	[SerializeField] protected Button deleteButton = null;
	protected UIProviderBase provider = null;

	public Button.ButtonClickedEvent DeleteButtonEvent => deleteButton.onClick;

	private void OnValidate()
	{
		if (deleteButton == null)
		{
			Debug.LogError("Delete Button is not assigned!", this);
		}
	}

	private void OnEnable()
	{
		DeleteButtonEvent.AddListener(() => provider?.DeleteObject());

		AddListener();
	}

	private void OnDisable()
	{
		DeleteButtonEvent.RemoveAllListeners();
		RemoveListeners();
	}

	protected virtual void AddListener() { }
	protected virtual void RemoveListeners() { }

	public abstract bool IsTargetType(GameObject obj);
	public void SetProvider(UIProviderBase provider)
	{
		this.provider = provider;
		LinkData();
		gameObject.SetActive(true);
	}

	protected void HideLegacyVisuals()
	{
		foreach (Transform child in transform)
		{
			child.gameObject.SetActive(false);
		}

		if (deleteButton != null)
			deleteButton.gameObject.SetActive(false);
	}

	protected Button CreateRuntimeActionButton(Transform parent, string label, UnityAction onClick)
	{
		GameObject buttonRoot = new(label.Replace(" ", string.Empty) + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
		buttonRoot.transform.SetParent(parent, false);

		LayoutElement layout = buttonRoot.GetComponent<LayoutElement>();
		layout.preferredHeight = 40f;
		layout.minHeight = 40f;
		layout.preferredWidth = 180f;

		Image image = buttonRoot.GetComponent<Image>();
		image.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);

		Button button = buttonRoot.GetComponent<Button>();
		button.onClick.AddListener(onClick);

		GameObject textRoot = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
		textRoot.transform.SetParent(buttonRoot.transform, false);

		TextMeshProUGUI text = textRoot.GetComponent<TextMeshProUGUI>();
		text.text = label;
		text.fontSize = 20f;
		text.alignment = TextAlignmentOptions.Center;
		text.color = Color.white;

		RectTransform textRect = text.rectTransform;
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = Vector2.zero;
		textRect.offsetMax = Vector2.zero;

		return button;
	}

	protected Button CreateDeleteActionButton(Transform parent)
	{
		return CreateRuntimeActionButton(parent, "Delete", () => provider?.DeleteObject());
	}

	protected abstract void LinkData();
	protected virtual void UpdateData() { }
}

public abstract class DetailContent<T> : DetailContentBase
	where T : Component
{
	
	public override bool IsTargetType(GameObject obj) => obj.TryGetComponent<T>(out _);
	
	private void Update()
	{
		UpdateData();
	}


}
