using TMPro;
using UnityEngine;

public sealed class TextRowView : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI text = null;

	public TextMeshProUGUI Text => text;
}
