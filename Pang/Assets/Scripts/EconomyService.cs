using System.Collections.Generic;
using UnityEngine;

public class EconomyTransaction
{
	public enum Reason
	{
		Place,
		Remove,
		Payday,
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

	private List<EconomyTransaction> history;
	private GameTime gameTime;

	public int Money => money;
	public float Reputation => reputation;

	private void Start()
	{
		if (gameTime == null)
			gameTime = FindFirstObjectByType<GameTime>();

		gameTime.OnMonthPassed += ProcessMonthlyPayment;
		GameContext.Instance.GridService.OnPlaceableInstalled += OnPlacement;
	}

	private void OnDestroy()
	{
		gameTime.OnMonthPassed -= ProcessMonthlyPayment;
		GameContext.Instance.GridService.OnPlaceableInstalled -= OnPlacement;
	}

	public void ApplyTransaction(EconomyTransaction transaction)
	{
		money += transaction.moneyDelta;
		reputation += transaction.reputationDelta;

		history.Add(transaction);
	}

	private void OnPlacement(PlacementContext context)
	{
		var transaction = new EconomyTransaction
		{
			moneyDelta = -context.placeableDefinition.Cost,
			reputationDelta = 0,
			reason = EconomyTransaction.Reason.Place,
		};

		ApplyTransaction(transaction);
	}

	public void ProcessMonthlyPayment()
	{
		var workerTransaction = new EconomyTransaction
		{
			moneyDelta = -GameContext.Instance.WorkerMgr.MontylyCost,
			reputationDelta = 0,
			reason = EconomyTransaction.Reason.Payday
		};

		ApplyTransaction(workerTransaction);
	}

}
