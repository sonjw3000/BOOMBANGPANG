using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectDetailUI : MonoBehaviour
{
	private DetailContentBase currentContent = null;
	private UIWindow window;
	private GameObject runtimeZoneDetailRoot;
	private TextMeshProUGUI runtimeZoneLabel;
	private Button runtimeZoneDeleteButton;

	private void Awake()
	{
		window = GetComponentInChildren<UIWindow>(true);
	}

	private void Start()
	{
		gameObject.SetActive(false);
	}

	public void SetDetailContent(DetailContentBase detailContent)
	{
		HideRuntimeZoneDetail();

		currentContent = detailContent;
		if (currentContent == null)
		{
			Debug.LogError("Current provider is null. Cannot set detail content.", this);
			return;
		}
	}

	public void SetZoneDetail(ZoneUIProvider provider)
	{
		if (provider == null)
		{
			Debug.LogError("Zone provider is null. Cannot set zone detail.", this);
			return;
		}

		currentContent?.gameObject.SetActive(false);
		EnsureRuntimeZoneDetail();

		var zone = provider.Target?.Zone;
		if (zone == null)
			return;

		runtimeZoneLabel.text = $"Name: {zone.DisplayName}\nType: {zone.Type}";
		runtimeZoneDeleteButton.onClick.RemoveAllListeners();
		runtimeZoneDeleteButton.onClick.AddListener(() => provider.DeleteObject());
		runtimeZoneDetailRoot.SetActive(true);
	}

	private void EnsureRuntimeZoneDetail()
	{
		if (runtimeZoneDetailRoot != null)
			return;

		if (window == null)
			window = GetComponentInChildren<UIWindow>(true);

		GameObject root = new("RuntimeZoneDetail", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
		root.transform.SetParent(window.ContentRoot, false);

		var layout = root.GetComponent<VerticalLayoutGroup>();
		layout.spacing = 12f;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		layout.childControlWidth = true;
		layout.childControlHeight = true;

		var fitter = root.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

		GameObject textObject = new("ZoneInfo", typeof(RectTransform), typeof(TextMeshProUGUI));
		textObject.transform.SetParent(root.transform, false);
		runtimeZoneLabel = textObject.GetComponent<TextMeshProUGUI>();
		runtimeZoneLabel.fontSize = 24f;
		runtimeZoneLabel.alignment = TextAlignmentOptions.TopLeft;
		runtimeZoneLabel.textWrappingMode = TextWrappingModes.Normal;
		runtimeZoneLabel.color = Color.white;

		GameObject buttonObject = new("DeleteButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
		buttonObject.transform.SetParent(root.transform, false);
		buttonObject.GetComponent<LayoutElement>().preferredHeight = 36f;
		buttonObject.GetComponent<Image>().color = new Color(0.75f, 0.2f, 0.2f, 1f);
		runtimeZoneDeleteButton = buttonObject.GetComponent<Button>();

		GameObject buttonTextObject = new("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
		buttonTextObject.transform.SetParent(buttonObject.transform, false);
		var buttonText = buttonTextObject.GetComponent<TextMeshProUGUI>();
		buttonText.text = "Delete";
		buttonText.fontSize = 20f;
		buttonText.alignment = TextAlignmentOptions.Center;
		buttonText.color = Color.white;
		buttonText.rectTransform.anchorMin = Vector2.zero;
		buttonText.rectTransform.anchorMax = Vector2.one;
		buttonText.rectTransform.offsetMin = Vector2.zero;
		buttonText.rectTransform.offsetMax = Vector2.zero;

		runtimeZoneDetailRoot = root;
		runtimeZoneDetailRoot.SetActive(false);
	}

	private void HideRuntimeZoneDetail()
	{
		if (runtimeZoneDetailRoot == null)
			return;

		runtimeZoneDeleteButton.onClick.RemoveAllListeners();
		runtimeZoneDetailRoot.SetActive(false);
	}
}
