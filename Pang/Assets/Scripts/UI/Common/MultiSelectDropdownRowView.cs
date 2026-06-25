using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MultiSelectDropdownRowView : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI labelText = null;
	[SerializeField] private Button toggleButton = null;
	[SerializeField] private TextMeshProUGUI summaryText = null;
	[SerializeField] private TextMeshProUGUI arrowText = null;
	[SerializeField] private RectTransform popupRoot = null;
	[SerializeField] private ToggleRowView[] optionRows = null;

	private static MultiSelectDropdownRowView expandedView;

	public TextMeshProUGUI LabelText => labelText;
	public Button ToggleButton => toggleButton;
	public TextMeshProUGUI SummaryText => summaryText;
	public RectTransform PopupRoot => popupRoot;
	public ToggleRowView[] OptionRows => optionRows;
	public bool IsExpanded => popupRoot != null && popupRoot.gameObject.activeSelf;

	private void Awake()
	{
		if (toggleButton != null)
		{
			toggleButton.onClick.RemoveListener(ToggleExpanded);
			toggleButton.onClick.AddListener(ToggleExpanded);
		}

		SetExpanded(false, false);
	}

	private void OnDisable()
	{
		SetExpanded(false, false);
	}

	public void SetExpanded(bool expanded, bool collapseOthers = true)
	{
		if (expanded && collapseOthers && expandedView != null && expandedView != this)
			expandedView.SetExpanded(false, false);

		if (popupRoot != null)
			popupRoot.gameObject.SetActive(expanded);

		if (arrowText != null)
			arrowText.text = expanded ? "^" : "v";

		if (expanded)
		{
			expandedView = this;
		}
		else if (expandedView == this)
		{
			expandedView = null;
		}
	}

	public void Collapse()
	{
		SetExpanded(false, false);
	}

	private void ToggleExpanded()
	{
		SetExpanded(IsExpanded == false);
	}
}
