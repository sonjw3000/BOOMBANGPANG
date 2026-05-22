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
	private bool definitionsLoaded;

	public event Action<ContractRuntime> OnContractExpired;

	public IReadOnlyList<ContractDefinition> ContractDefinitions
	{
		get
		{
			EnsureDefinitionsLoaded();
			return definitions;
		}
	}
	public IReadOnlyList<ContractRuntime> ActiveContracts => currentActiveContracts;

	// rocket item queue
	private readonly Queue<ItemStack> itemsToBeDelivered = new();

	// contract missions
	//private readonly Dictionary<ItemDefinition, int> itemDeliveryMissions = new();

	private void Awake()
	{
		EnsureDefinitionsLoaded();
	}

	public void AdvanceWeek()
	{
		List<ContractRuntime> expiredContracts = new();

		foreach (var contract in currentActiveContracts)
		{
			if (contract.AdvanceWeek())
				continue;

			contractHistory.AddContractResult(contract, GameContext.Instance.GameTime.WeeksPassed);
			OnContractExpired?.Invoke(contract);
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

	public void AddContract(int index, int duration, ContractType type = ContractType.Standard)
	{
		EnsureDefinitionsLoaded();
		currentActiveContracts.Add(new ContractRuntime(definitions[index], duration, type));
	}

	public bool TryExtendExpiredContract(ContractRuntime contract, int durationMonths)
	{
		if (contract == null || durationMonths <= 0)
			return false;

		if (currentActiveContracts.Contains(contract))
			return false;

		contractHistory.RemoveContractResult(contract);
		contract.Restart(durationMonths);
		currentActiveContracts.Add(contract);
		return true;
	}

	public ContractServiceSaveData CaptureState()
	{
		ContractServiceSaveData data = new();
		foreach (var contract in currentActiveContracts)
			data.ActiveContracts.Add(contract.CaptureState());

		return data;
	}

	public void RestoreState(ContractServiceSaveData data)
	{
		EnsureDefinitionsLoaded();
		ResetRuntimeState();
		if (data == null)
			return;

		foreach (var contractData in data.ActiveContracts)
		{
			ContractDefinition definition = definitions.Find(def => def.ContractId == contractData.ContractId);
			if (definition == null)
				continue;

			ContractRuntime contract = new(definition, Mathf.CeilToInt(contractData.RemainingDuration / 4.0f), contractData.Type);
			contract.RestoreState(contractData.RemainingDuration, contractData.DeliveryDelta, contractData.AutoRenewal);
			currentActiveContracts.Add(contract);
		}
	}

	public bool TryGetActiveContract(uint contractId, out ContractRuntime result)
	{
		result = currentActiveContracts.Find(contract => contract.Definition.ContractId == contractId);
		return result != null;
	}

	public void ResetRuntimeState()
	{
		currentActiveContracts.Clear();
	}

	private void EnsureDefinitionsLoaded()
	{
		if (definitionsLoaded)
			return;

		definitions.Clear();

		foreach (var catalog in contractCatalogs)
		{
			if (catalog == null || catalog.Contracts == null)
				continue;

			definitions.AddRange(catalog.Contracts);
		}

		definitionsLoaded = true;
	}

}
