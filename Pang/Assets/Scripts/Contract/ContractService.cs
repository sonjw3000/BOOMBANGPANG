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
	}

	public Tuple<int, float> GetMonthlyReward()
	{
		// todo
		// 계약 유지 보너스와 같은 개념으로 접근해야함

		return default;
	}
}
