using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.UI;

public class ContractControlButton : MonoBehaviour
{
	[SerializeField] private Button button;
	[SerializeField] private ContractWindow targetWindow;
	[SerializeField] private TMP_Text label;
	[SerializeField] private string buttonText = "C";

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

	private void Init()
	{
		if (button == null)
			button = GetComponent<Button>();

		if (targetWindow == null)
			targetWindow = FindFirstObjectByType<ContractWindow>(FindObjectsInactive.Include);

		if (label == null)
			label = GetComponentInChildren<TMP_Text>(true);

		if (label != null)
			label.text = buttonText;
	}

	private void OnDestroy()
	{
		if (button != null)
			button.onClick.RemoveListener(OnClicked);
	}

	private void OnClicked()
	{
		if (targetWindow != null)
		{
			if (targetWindow.gameObject.activeSelf)
				targetWindow.Close();
			else
				targetWindow.Open();
		}
	}
}
