using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class PlaceableButtonView : MonoBehaviour
{
	[SerializeField] private Button button;
	[SerializeField] private TMP_Text text;
	[SerializeField] private Image icon;

	private PlaceableDefinition def;

	private Action<PlaceableDefinition> onClick;

	public void Bind(PlaceableDefinition def, Action<PlaceableDefinition> action)
	{
		this.def = def;
		onClick = action;

		text.text = def.displayName;
		icon.sprite = def.icon;

		button.onClick.RemoveAllListeners();
		button.onClick.AddListener(() => onClick?.Invoke(def));
	}

}

