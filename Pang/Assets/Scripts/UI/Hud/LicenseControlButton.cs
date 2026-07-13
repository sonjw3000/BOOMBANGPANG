using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LicenseControlButton : MonoBehaviour
{
	[SerializeField] private Button button;
	[SerializeField] private LicenseWindow targetWindow;
	[SerializeField] private TMP_Text label;
	[SerializeField] private string buttonText = "L";

	private void Awake()
	{
		Init();

		if (button != null)
		{
			button.onClick.RemoveListener(OnClicked);
			button.onClick.AddListener(OnClicked);
		}
	}

	private void OnValidate()
	{
		Init();
	}

	private void OnDestroy()
	{
		if (button != null)
			button.onClick.RemoveListener(OnClicked);
	}

	private void Init()
	{
		button ??= GetComponent<Button>();
		targetWindow ??= FindFirstObjectByType<LicenseWindow>(FindObjectsInactive.Include);
		label ??= GetComponentInChildren<TMP_Text>(true);

		if (label != null)
			label.text = buttonText;
	}

	private void OnClicked()
	{
		if (targetWindow == null)
			targetWindow = FindFirstObjectByType<LicenseWindow>(FindObjectsInactive.Include);

		if (targetWindow == null)
			return;

		if (targetWindow.gameObject.activeSelf)
			targetWindow.Close();
		else
			targetWindow.Open();
	}
}
