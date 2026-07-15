using System;
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
		PowerService,
		ResearchInvestment,
		OrderSettlement,
		WorkerHire,
		MedicalSubscription,
		MedicalDispatch,
		MaintenanceSubscription,
		MaintenanceDispatch,
		OccupationalClaimNotProcessed,
		}

	public int moneyDelta;
	public float reputationDelta;
	public Reason reason;
}


// 돈, 평판, 기타등등
public partial class EconomyService : MonoBehaviour
{
	private int money;
	private float reputation;

	private readonly List<EconomyTransaction> history = new();

	public event Action<float> OnReputationChanged;
	public event Action<int> OnMoneyChanged;
	public event Action<EconomyTransaction> OnTransactionApplied;

	public int Money => money;
	public float Reputation => reputation;
	public IReadOnlyList<EconomyTransaction> History => history;

	public bool CanAfford(int cost)
	{
		if (GameContext.CHEATMODE) return true;

		return money >= cost;
	}
	public void ApplyTransaction(EconomyTransaction transaction)
	{
		if (transaction == null)
			return;

		int previousMoney = money;
		float previousReputation = reputation;
		money += transaction.moneyDelta;
		reputation += transaction.reputationDelta;

		history.Add(transaction);
		PublishHudEvent(transaction);
		OnTransactionApplied?.Invoke(transaction);

		if (previousMoney != money)
			OnMoneyChanged?.Invoke(money);

		if (Mathf.Approximately(previousReputation, reputation) == false)
			OnReputationChanged?.Invoke(reputation);
	}

	private void PublishHudEvent(EconomyTransaction transaction)
	{
		if (GameContext.HasInstance == false || GameContext.Instance.HudEventManager == null)
			return;

		string reason = FormatReason(transaction.reason);
		if (transaction.moneyDelta != 0)
			GameContext.Instance.HudEventManager.PublishMoney(transaction.moneyDelta, reason, this);

		if (Mathf.Approximately(transaction.reputationDelta, 0f) == false)
			GameContext.Instance.HudEventManager.PublishReputation(transaction.reputationDelta, reason, this);
	}

	public static string FormatReason(EconomyTransaction.Reason reason)
	{
		return reason switch
		{
			EconomyTransaction.Reason.Place => "Placement",
			EconomyTransaction.Reason.Remove => "Removal",
			EconomyTransaction.Reason.Payday => "Payday",
			EconomyTransaction.Reason.MontlyContract => "Monthly Contract",
			EconomyTransaction.Reason.PowerService => "Power Service",
			EconomyTransaction.Reason.ResearchInvestment => "Research Investment",
			EconomyTransaction.Reason.OrderSettlement => "Order Settlement",
			EconomyTransaction.Reason.WorkerHire => "Worker Hire",
			EconomyTransaction.Reason.MedicalSubscription => "Medical Subscription",
			EconomyTransaction.Reason.MedicalDispatch => "Medical Dispatch",
			EconomyTransaction.Reason.MaintenanceSubscription => "Maintenance Subscription",
			EconomyTransaction.Reason.MaintenanceDispatch => "Maintenance Dispatch",
			EconomyTransaction.Reason.OccupationalClaimNotProcessed => "Occupational Claim Not Processed",
			_ => reason.ToString(),
		};
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

		if (context.placedObj == null)
		{
			GameContext.Instance.FloatingTextManager?.ShowScreen(
				FloatingTextPreset.MoneyLoss,
				$"-${context.placeableDefinition.Cost}",
				Input.mousePosition);
		}
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
