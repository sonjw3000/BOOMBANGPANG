using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TitleSceneController : MonoBehaviour
{
	[SerializeField] private string initialSceneName = "GameScene";
	[SerializeField] private string finalGameplaySceneName = "GameScene";
	[SerializeField] private float playerTurnDuration = 1.1f;
	[SerializeField] private float playerTurnAngle = 180f;

	private Button startNewButton;
	private Button loadButton;
	private Button settingsButton;
	private Button exitButton;
	private Canvas canvas;
	private Transform playerTransform;
	private TMP_Text templateText;
	private Button templateButton;

	[SerializeField] private GameLoadWindow gameLoadWindow;
	private RuntimeSimpleWindow settingsWindow;
	private bool transitionInProgress;

	// Runtime-created window callbacks must be rebuilt after a domain reload.
	private void OnEnable()
	{
		if (TryCacheReferences() == false)
		{
			Debug.LogWarning("[Title] Failed to initialize title scene controller.");
			enabled = false;
			return;
		}

		BuildRuntimeWindows();
		BindButtons();
	}

	private void OnDisable()
	{
		UnbindButtons();
	}

	private bool TryCacheReferences()
	{
		canvas = GetComponentInChildren<Canvas>(true);
		playerTransform = GameObject.Find("PlayerOfficeSpace/Player")?.transform;

		startNewButton = FindButton("Canvas/LeftTitleItems/Buttons/StartNew");
		loadButton = FindButton("Canvas/LeftTitleItems/Buttons/Load");
		settingsButton = FindButton("Canvas/LeftTitleItems/Buttons/Settings");
		exitButton = FindButton("Canvas/LeftTitleItems/Buttons/Exit");

		templateButton = loadButton != null ? loadButton : startNewButton;
		templateText = templateButton != null ? templateButton.GetComponentInChildren<TMP_Text>(true) : null;
		if (gameLoadWindow == null)
			gameLoadWindow = GetComponentInChildren<GameLoadWindow>(true);

		return canvas != null
			&& startNewButton != null
			&& loadButton != null
			&& settingsButton != null
			&& exitButton != null
			&& templateButton != null
			&& templateText != null
			&& gameLoadWindow != null;
	}

	private void BindButtons()
	{
		UnbindButtons();
		startNewButton.onClick.AddListener(HandleNewGameClicked);
		loadButton.onClick.AddListener(HandleLoadClicked);
		settingsButton.onClick.AddListener(HandleSettingsClicked);
		exitButton.onClick.AddListener(HandleExitClicked);
	}

	private void UnbindButtons()
	{
		if (startNewButton != null)
			startNewButton.onClick.RemoveListener(HandleNewGameClicked);

		if (loadButton != null)
			loadButton.onClick.RemoveListener(HandleLoadClicked);

		if (settingsButton != null)
			settingsButton.onClick.RemoveListener(HandleSettingsClicked);

		if (exitButton != null)
			exitButton.onClick.RemoveListener(HandleExitClicked);
	}

	private void HandleNewGameClicked()
	{
		BeginStartFlow(TitleSceneStartRequest.NewGame());
	}

	private void HandleLoadClicked()
	{
		if (transitionInProgress)
			return;

		settingsWindow?.Close();
		OpenLoadWindow();
	}

	private void HandleSettingsClicked()
	{
		if (transitionInProgress)
			return;

		gameLoadWindow.Close();
		settingsWindow?.Open();
	}

	private void HandleExitClicked()
	{
#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#else
		Application.Quit();
#endif
	}

	private void BeginStartFlow(TitleSceneStartRequest request)
	{
		if (transitionInProgress)
			return;

		transitionInProgress = true;
		SetMenuInteractable(false);
		gameLoadWindow.Close();
		settingsWindow?.Close();
		StartCoroutine(PlayStartFlow(request));
	}

	private IEnumerator PlayStartFlow(TitleSceneStartRequest request)
	{
		if (playerTransform != null)
		{
			Quaternion startRotation = playerTransform.rotation;
			Quaternion endRotation = startRotation * Quaternion.Euler(0f, playerTurnAngle, 0f);
			float elapsed = 0f;

			while (elapsed < playerTurnDuration)
			{
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, playerTurnDuration));
				playerTransform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
				yield return null;
			}

			playerTransform.rotation = endRotation;
		}

		TitleSceneLoadBridge.BeginRoute(request, initialSceneName, finalGameplaySceneName);
	}

	private void BuildRuntimeWindows()
	{
		gameLoadWindow.Initialize(HandleLoadSaveRequested);
		settingsWindow = CreateSettingsWindow();
	}

	private void OpenLoadWindow()
	{
		IReadOnlyList<GameSaveFileSummary> saveFiles = GameSaveFileCatalog.EnumerateAllJsonFiles();
		gameLoadWindow.Open(saveFiles);
	}

	private void HandleLoadSaveRequested(GameSaveFileSummary summary)
	{
		if (summary.IsLoadable == false)
			return;

		BeginStartFlow(TitleSceneStartRequest.LoadSave(summary.FilePath));
	}

	private RuntimeSimpleWindow CreateSettingsWindow()
	{
		Transform existingRoot = canvas.transform.Find("SettingsWindow");
		if (existingRoot != null && TryBindExistingSettingsWindow(existingRoot, out RuntimeSimpleWindow existingWindow))
			return existingWindow;

		if (existingRoot != null)
			Destroy(existingRoot.gameObject);

		RuntimeModalRoot modal = CreateModalRoot("SettingsWindow", "Settings");
		modal.Message.text = "Settings will be added here later.";

		Button closeButton = CloneButton(templateButton, modal.FooterRow, "CloseButton", "Close");
		RuntimeSimpleWindow window = new RuntimeSimpleWindow(modal.Root);
		closeButton.onClick.AddListener(window.Close);
		return window;
	}

	private static bool TryBindExistingSettingsWindow(Transform root, out RuntimeSimpleWindow window)
	{
		window = null;
		TMP_Text message = root.Find("Panel/Message")?.GetComponent<TMP_Text>();
		Button closeButton = root.Find("Panel/Footer/CloseButton")?.GetComponent<Button>();
		if (message == null || closeButton == null)
			return false;

		message.text = "Settings will be added here later.";
		window = new RuntimeSimpleWindow(root.gameObject);
		closeButton.onClick.RemoveAllListeners();
		closeButton.onClick.AddListener(window.Close);
		window.Close();
		return true;
	}

	private RuntimeModalRoot CreateModalRoot(string objectName, string title)
	{
		GameObject overlay = CreateUiObject(objectName, canvas.transform, typeof(Image));
		RectTransform overlayRect = overlay.GetComponent<RectTransform>();
		StretchToParent(overlayRect);
		Image overlayImage = overlay.GetComponent<Image>();
		overlayImage.color = new Color(0f, 0f, 0f, 0.55f);

		GameObject panelObject = CreateUiObject("Panel", overlay.transform, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
		RectTransform panelRect = panelObject.GetComponent<RectTransform>();
		panelRect.anchorMin = new Vector2(0.5f, 0.5f);
		panelRect.anchorMax = new Vector2(0.5f, 0.5f);
		panelRect.pivot = new Vector2(0.5f, 0.5f);
		panelRect.sizeDelta = new Vector2(820f, 560f);
		Image panelImage = panelObject.GetComponent<Image>();
		panelImage.color = new Color(0.11f, 0.13f, 0.17f, 0.96f);

		VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(28, 28, 24, 24);
		layout.spacing = 18f;
		layout.childAlignment = TextAnchor.UpperCenter;
		layout.childControlWidth = true;
		layout.childControlHeight = false;
		layout.childForceExpandHeight = false;
		layout.childForceExpandWidth = true;

		ContentSizeFitter fitter = panelObject.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

		LayoutElement panelLayout = panelObject.GetComponent<LayoutElement>();
		panelLayout.preferredWidth = 820f;
		panelLayout.preferredHeight = 560f;

		TMP_Text titleText = CloneText(templateText, panelObject.transform, "Title", title);
		titleText.fontSize = Mathf.Max(titleText.fontSize + 8f, 40f);
		titleText.alignment = TextAlignmentOptions.Center;

		TMP_Text messageText = CloneText(templateText, panelObject.transform, "Message", string.Empty);
		messageText.alignment = TextAlignmentOptions.Center;
		messageText.textWrappingMode = TextWrappingModes.Normal;

		GameObject contentObject = CreateUiObject("Content", panelObject.transform, typeof(LayoutElement));
		LayoutElement contentLayout = contentObject.GetComponent<LayoutElement>();
		contentLayout.flexibleHeight = 1f;
		contentLayout.minHeight = 280f;

		GameObject footerObject = CreateUiObject("Footer", panelObject.transform, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
		HorizontalLayoutGroup footerLayout = footerObject.GetComponent<HorizontalLayoutGroup>();
		footerLayout.spacing = 16f;
		footerLayout.childAlignment = TextAnchor.MiddleCenter;
		footerLayout.childControlHeight = true;
		footerLayout.childControlWidth = false;
		footerLayout.childForceExpandWidth = false;
		footerLayout.childForceExpandHeight = false;
		LayoutElement footerElement = footerObject.GetComponent<LayoutElement>();
		footerElement.minHeight = 72f;

		overlay.SetActive(false);
		return new RuntimeModalRoot(overlay, messageText, contentObject.transform, footerObject.transform);
	}

	private void SetMenuInteractable(bool interactable)
	{
		startNewButton.interactable = interactable;
		loadButton.interactable = interactable;
		settingsButton.interactable = interactable;
		exitButton.interactable = interactable;
	}

	private Button FindButton(string relativePath)
	{
		Transform target = transform.Find(relativePath);
		return target != null ? target.GetComponent<Button>() : null;
	}

	private static void StretchToParent(RectTransform rectTransform, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
	{
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.offsetMin = new Vector2(left, bottom);
		rectTransform.offsetMax = new Vector2(-right, -top);
	}

	private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] componentTypes)
	{
		GameObject gameObject = new(name, componentTypes.Prepend(typeof(RectTransform)).ToArray());
		gameObject.transform.SetParent(parent, false);
		return gameObject;
	}

	private TMP_Text CloneText(TMP_Text template, Transform parent, string objectName, string label)
	{
		TMP_Text text = Instantiate(template, parent);
		text.name = objectName;
		text.text = label;
		return text;
	}

	private Button CloneButton(Button template, Transform parent, string objectName, string label)
	{
		Button button = Instantiate(template, parent);
		button.name = objectName;
		RectTransform rectTransform = button.GetComponent<RectTransform>();
		rectTransform.localScale = Vector3.one;
		rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, Mathf.Max(rectTransform.sizeDelta.y, 56f));

		TMP_Text labelText = button.GetComponentInChildren<TMP_Text>(true);
		if (labelText != null)
		{
			labelText.text = label;
			labelText.textWrappingMode = TextWrappingModes.Normal;
			labelText.alignment = TextAlignmentOptions.Center;
		}

		button.onClick = new Button.ButtonClickedEvent();

		LayoutElement layout = button.GetComponent<LayoutElement>();
		if (layout == null)
			layout = button.gameObject.AddComponent<LayoutElement>();
		layout.minHeight = Mathf.Max(layout.minHeight, 56f);
		layout.preferredHeight = Mathf.Max(layout.preferredHeight, 56f);
		layout.flexibleWidth = 1f;

		return button;
	}

	private sealed class RuntimeModalRoot
	{
		public RuntimeModalRoot(GameObject root, TMP_Text message, Transform contentRoot, Transform footerRow)
		{
			Root = root;
			Message = message;
			ContentRoot = contentRoot;
			FooterRow = footerRow;
		}

		public GameObject Root { get; }
		public TMP_Text Message { get; }
		public Transform ContentRoot { get; }
		public Transform FooterRow { get; }
	}

	private sealed class RuntimeSimpleWindow
	{
		private readonly GameObject root;

		public RuntimeSimpleWindow(GameObject root)
		{
			this.root = root;
		}

		public void Open()
		{
			root.SetActive(true);
		}

		public void Close()
		{
			root.SetActive(false);
		}
	}

}

public static class TitleSceneRuntimeInstaller
{
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Install()
	{
		if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "TitleScene")
			return;

		GameObject titleRoot = GameObject.Find("UI_Title");
		if (titleRoot == null)
			return;

		if (titleRoot.GetComponent<TitleSceneController>() == null)
			titleRoot.AddComponent<TitleSceneController>();
	}
}
