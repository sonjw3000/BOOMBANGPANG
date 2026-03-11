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
	//private readonly Dictionary<ItemDefinition, int> itemDeliveryMissions = new();

	private void Start()
	{
		foreach (var catalog in contractCatalogs)
		{
			definitions.AddRange(catalog.Contracts);
		}
	}

	public void AdvanceWeek()
	{
		List<ContractRuntime> expiredContracts = new();

		foreach (var contract in currentActiveContracts)
		{
			if (contract.AdvanceWeek())
				continue;

			contractHistory.AddContractResult(contract, GameContext.Instance.GameTime.WeeksPassed);
			expiredContracts.Add(contract);
		}

		// todo
		// 지우기 전에 계약 보상을 저거해야함
		currentActiveContracts.RemoveAll(c => expiredContracts.Contains(c));
	}

	public Tuple<int, float> GetMonthlyReward()
	{
		// todo
		// 계약 유지 보너스와 같은 개념으로 접근해야함

		return default;
	}

	public void AddContract(int index, int duration)
	{
		currentActiveContracts.Add(new ContractRuntime(definitions[index], duration));
	}

}
