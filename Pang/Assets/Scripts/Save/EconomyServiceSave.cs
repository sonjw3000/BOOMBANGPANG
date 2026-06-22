using UnityEngine;

public partial class EconomyService
{
	public EconomySaveData CaptureState()
	{
		return new EconomySaveData
		{
			Money = money,
			Reputation = reputation,
		};
	}

	public void RestoreState(EconomySaveData data)
	{
		if (data == null)
			return;

		float previousReputation = reputation;
		money = data.Money;
		reputation = data.Reputation;
		history.Clear();

		if (Mathf.Approximately(previousReputation, reputation) == false)
			OnReputationChanged?.Invoke(reputation);
	}
}
