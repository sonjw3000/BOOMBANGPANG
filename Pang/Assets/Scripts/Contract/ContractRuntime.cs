using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Contract
{
	public enum Status
	{
		Success,
		Failed,
		Delayed,
	}



	public class ContractRuntime
	{
		private class ContractResult
		{
			private readonly Dictionary<Status, int> contractHistoryPerWeek = new();

			public Dictionary<Status, int> ContractHistoryPerWeek => contractHistoryPerWeek;

			public ContractResult()
			{
				foreach (Status status in System.Enum.GetValues(typeof(Status)))
				{
					contractHistoryPerWeek[status] = 0;
				}
			}
		}

		const int MaximumHistory = 48;
		public ContractDefinition Definition { get; private set; }

		private int remainDuration;
		private int deliveryDelta = 0;

		public int RemainingDuration => remainDuration;
		public int DeliveryInterval => Definition.DeliveryIntervalWeek;

		private DeliveryService DeliveryService => GameContext.Instance.DeliveryService;

		public bool AutoRenewal { get; set; } = true;

		private readonly LinkedList<ContractResult> resultPerWeek = new();

		public ContractRuntime(ContractDefinition definition, int duration)
		{
			Definition = definition;
			remainDuration = duration * 4;
			resultPerWeek.AddLast(new ContractResult());
		}

		public bool AdvanceWeek()
		{
			//Debug.Log($"WeekAdvanced, remain: {remainDuration}, delivery: {deliveryDelta}");
			
			--remainDuration;
			if (remainDuration < 0)
				return false;

			if (resultPerWeek.Count >= MaximumHistory)
			{
				resultPerWeek.RemoveFirst();
			}
			resultPerWeek.AddLast(new ContractResult());

			--deliveryDelta;
			if (deliveryDelta < 0)
			{
				deliveryDelta = Definition.DeliveryIntervalWeek;

				// add delivery queue
				DeliveryService.RequestDelivery(Definition.ContractId, Definition.ItemToHandle, Definition.ItemCountsPerDelivery);
			}

			return true;
		}

		public void AddResult(Status status, int cnt)
		{
			resultPerWeek.Last.Value.ContractHistoryPerWeek[status] += cnt;
		}
	}
}
