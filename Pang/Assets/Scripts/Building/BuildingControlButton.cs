using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingControlButton : MonoBehaviour
{
	[SerializeField] private Button button;
	[SerializeField] private BuildingControlWindow targetWindow;
	[SerializeField] private TMP_Text label;
	[SerializeField] private string buttonText = "B";

	private void Awake()
	{
		if (button == null)
			button = GetComponent<Button>();

		if (targetWindow == null)
			targetWindow = FindFirstObjectByType<BuildingControlWindow>(FindObjectsInactive.Include);

		if (label == null)
			label = GetComponentInChildren<TMP_Text>(true);

		if (label != null)
			label.text = buttonText;

		if (button != null)
		{
			button.onClick.RemoveListener(OnClicked);
			button.onClick.AddListener(OnClicked);
		}
	}

	private void OnDestroy()
	{
		if (button != null)
			button.onClick.RemoveListener(OnClicked);
	}

	private void OnClicked()
	{
		targetWindow?.ToggleWindow();
	}
}
