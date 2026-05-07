using System.Collections.Generic;
using UnityEngine;

public class EconomyTransaction
{
	public enum Reason
	{
		Place,
		Remove,
		Payday,
		MontlyContract,
		OrderSettlement,
		}

	public int moneyDelta;
	public float reputationDelta;
	public Reason reason;
}


// 돈, 평판, 기타등등
public class EconomyService : MonoBehaviour
{
	private int money;
	private float reputation;

	private readonly List<EconomyTransaction> history = new();

	public int Money => money;
	public float Reputation => reputation;

	public bool CanAfford(int cost)
	{
		if (GameContext.CHEATMODE) return true;

		return money >= cost;
	}
	public void ApplyTransaction(EconomyTransaction transaction)
	{
		money += transaction.moneyDelta;
		reputation += transaction.reputationDelta;

		history.Add(transaction);
	}

	public void OnPlacement(PlacementContext context)
	{
		if (context.placementEvent != PlacementEvent.Normal)
			return;

		var transaction = new EconomyTransaction
		{
			moneyDelta = -context.placeableDefinition.Cost,
			reputationDelta = 0,
			reason = EconomyTransaction.Reason.Place,
		};

		ApplyTransaction(transaction);
	}

	// todo
	// 나중에 분리하자
	public void ProcessMonthlyPayment()
	{
		// worker transaction
		var workerTransaction = new EconomyTransaction
		{
			moneyDelta = -GameContext.Instance.WorkerMgr.CostPerMonth,
			reputationDelta = 0,
			reason = EconomyTransaction.Reason.Payday
		};

		ApplyTransaction(workerTransaction);

		// contract transaction
		//var montlyReward = GameContext.Instance.ContractMgr.GetMonthlyReward();
		//var contractTransaction = new EconomyTransaction
		//{
		//	moneyDelta = montlyReward.Item1,
		//	reputationDelta = montlyReward.Item2,
		//	reason = EconomyTransaction.Reason.MontlyContract
		//};

	}

}
