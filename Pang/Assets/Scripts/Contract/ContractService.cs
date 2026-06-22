using System.Collections.Generic;
using UnityEngine;
using System;

using Assets.Scripts.Contract;
using Assets.Scripts.UI;


public partial class ContractService : MonoBehaviour
{
	[SerializeField] private ContractCatalog[] contractCatalogs;
	
	private readonly List<ContractDefinition> definitions = new();
	private readonly List<ContractRuntime> currentActiveContracts = new();
	private readonly ContractHistory contractHistory = new();
	private EventNoticeService eventNoticeService;
	private bool definitionsLoaded;
	[SerializeField, Min(1)] private int expiredContractExtensionMonths = 12;

	public IReadOnlyList<ContractDefinition> ContractDefinitions
	{
		get
		{
			EnsureDefinitionsLoaded();
			return definitions;
		}
	}
	public IReadOnlyList<ContractRuntime> ActiveContracts => currentActiveContracts;
	private EventNoticeService EventNoticeService
	{
		get
		{
			if (eventNoticeService == null)
				eventNoticeService = FindFirstObjectByType<EventNoticeService>(FindObjectsInactive.Include);

			return eventNoticeService;
		}
	}

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
			NotifyContractExpired(contract);
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

	public bool TryGetActiveContract(uint contractId, out ContractRuntime result)
	{
		result = currentActiveContracts.Find(contract => contract.Definition.ContractId == contractId);
		return result != null;
	}

	private void NotifyContractExpired(ContractRuntime contract)
	{
		if (contract?.Definition == null || EventNoticeService == null)
			return;

		string contractName = string.IsNullOrWhiteSpace(contract.Definition.ContractName)
			? "Unnamed Contract"
			: contract.Definition.ContractName;

		EventNoticeService.ShowNotice(new EventNoticeRequest(
			"Contract Expired",
			$"Contract '{contractName}' has expired.\nExtend the same contract for {expiredContractExtensionMonths} months if you want to keep it running.",
			extraAction: new EventNoticeAction(
				"Extend Contract",
				() => TryExtendExpiredContract(contract, expiredContractExtensionMonths))));
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
