using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class FacilityRuleEditWindowView : MonoBehaviour
{
	[SerializeField] private TMP_InputField presetNameInput = null;
	[SerializeField] private Slider redSlider = null;
	[SerializeField] private Slider greenSlider = null;
	[SerializeField] private Slider blueSlider = null;
	[SerializeField] private Image colorPreview = null;
	[SerializeField] private FacilityRuleEditorView ruleEditorView = null;
	[SerializeField] private TextButtonView saveButton = null;
	[SerializeField] private TextButtonView cancelButton = null;

	public TMP_InputField PresetNameInput => presetNameInput;
	public Slider RedSlider => redSlider;
	public Slider GreenSlider => greenSlider;
	public Slider BlueSlider => blueSlider;
	public Image ColorPreview => colorPreview;
	public FacilityRuleEditorView RuleEditorView => ruleEditorView;
	public TextButtonView SaveButton => saveButton;
	public TextButtonView CancelButton => cancelButton;
}
