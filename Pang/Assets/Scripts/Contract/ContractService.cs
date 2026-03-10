using System.Collections.Generic;
using UnityEngine;
using System;

using Assets.Scripts.Contract;


public class ContractService : MonoBehaviour
{
	[SerializeField] private ContractCatalog[] contractCatalogs;
	
	private readonly List<ContractDefinition> definitions = new();
	private readonly List<ContractRuntime> currentActiveContracts = new();

	private readonly ContractHistory contractHistory = new();

	public IReadOnlyList<ContractDefinition> ContractDefinitions => definitions;

	// rocket item queue
	private readonly Queue<ItemStack> itemsToBeDelivered = new();

	// contract missions
	private readonly Dictionary<ItemDefinition, int> itemDeliveryMissions = new();

	public void ProcessMonthlyContracts()
	{
		List<ContractRuntime> expiredContracts = new();

		foreach (var contract in currentActiveContracts)
		{
			contract.RemainingDuration--;
			if (contract.RemainingDuration >= 0)
				continue;

			contractHistory.AddContractResult(contract, GameContext.Instance.GameTime.Month);
			expiredContracts.Add(contract);
		}

		currentActiveContracts.RemoveAll(c => expiredContracts.Contains(c));

		foreach (var contract in currentActiveContracts)
			itemsToBeDelivered.Enqueue(new (contract.Definition.ItemToHandle.ItemID, 10));
	}

	public Tuple<int, float> GetMonthlyReward()
	{
		// todo
		// 계약 유지 보너스와 같은 개념으로 접근해야함

		return default;
	}
}
