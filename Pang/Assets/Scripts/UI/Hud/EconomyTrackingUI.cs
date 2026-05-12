using TMPro;
using UnityEngine;

public class EconomyTrackingUI : MonoBehaviour
{
	[SerializeField] private TMP_Text moneyText;
	[SerializeField] private TMP_Text reputationText;

	private EconomyService Economy => GameContext.Instance.EconomyService;

	private void Update()
	{
		if (Economy == null) return;

		moneyText.text = $"Money: ${Economy.Money:N0}";
		reputationText.text = $"Rep: {Economy.Reputation:F1}";
	}
}
