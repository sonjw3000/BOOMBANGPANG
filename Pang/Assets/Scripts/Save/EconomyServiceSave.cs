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

		int previousMoney = money;
		float previousReputation = reputation;
		money = data.Money;
		reputation = data.Reputation;
		history.Clear();

		if (previousMoney != money)
			OnMoneyChanged?.Invoke(money);

		if (Mathf.Approximately(previousReputation, reputation) == false)
			OnReputationChanged?.Invoke(reputation);
	}
}
