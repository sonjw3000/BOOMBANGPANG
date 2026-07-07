using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Assets.Scripts.Contract.ItemContract;

namespace Assets.Scripts.UI
{
	public class ContractMarketListButton : MonoBehaviour
	{
		[SerializeField] private Button button;
		[SerializeField] private TMP_Text label;

		private int index;
		private ContractDefinition definition;
		private System.Action<int, ContractDefinition> onSelected;

		public void Setup(int index, ContractDefinition def, System.Action<int, ContractDefinition> onSelected)
		{
			this.index = index;
			this.definition = def;
			this.onSelected = onSelected;

			if (label != null) label.text = def.ContractName;
			
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(() => onSelected?.Invoke(index, def));
		}
	}
}
