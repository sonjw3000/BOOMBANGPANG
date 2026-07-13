using System.Collections.Generic;
using UnityEngine;
using System;

using Assets.Scripts.Contract;
using Assets.Scripts.Contract.ItemContract;


public partial class ContractService : MonoBehaviour
{
	[SerializeField] private ContractCatalog[] contractCatalogs;
	
	private readonly List<ContractDefinition> definitions = new();
	private readonly List<ContractRuntime> currentActiveContracts = new();
	private readonly ContractHistory contractHistory = new();
	private bool definitionsLoaded;

	public IReadOnlyList<ContractDefinition> ContractDefinitions
	{
		get
		{
			EnsureDefinitionsLoaded();
			return definitions;
		}
	}
	public IReadOnlyList<ContractCatalog> ContractCatalogs => contractCatalogs ?? Array.Empty<ContractCatalog>();
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
		if (index < 0 || index >= definitions.Count)
			return;

		TryAddContract(definitions[index], duration, type);
	}

	public bool TryAddContract(ContractDefinition definition, int duration, ContractType type = ContractType.Standard)
	{
		if (definition == null || duration <= 0 || TryGetCatalog(definition, out ContractCatalog catalog) == false)
			return false;

		if (IsCatalogUnlocked(catalog) == false)
			return false;

		currentActiveContracts.Add(new ContractRuntime(definition, duration, type));
		return true;
	}

	public bool IsCatalogUnlocked(ContractCatalog catalog)
	{
		if (catalog == null)
			return false;

		IReadOnlyList<ContractLicenseRequirement> requirements = catalog.RequiredLicenses;
		if (requirements == null || requirements.Count == 0)
			return true;

		LicenseService licenseService = GameContext.HasInstance ? GameContext.Instance.LicenseService : null;
		if (licenseService == null)
			return false;

		foreach (ContractLicenseRequirement requirement in requirements)
		{
			if (requirement?.License == null ||
				licenseService.MeetsRequirement(requirement.LicenseId, requirement.MinimumGrade) == false)
			{
				return false;
			}
		}

		return true;
	}

	public bool TryGetCatalog(ContractDefinition definition, out ContractCatalog result)
	{
		result = null;
		if (definition == null || contractCatalogs == null)
			return false;

		foreach (ContractCatalog catalog in contractCatalogs)
		{
			if (catalog?.Contracts == null || Array.IndexOf(catalog.Contracts, definition) < 0)
				continue;

			result = catalog;
			return true;
		}

		return false;
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
		if (contract?.Definition == null || GameContext.HasInstance == false)
			return;

		string contractName = string.IsNullOrWhiteSpace(contract.Definition.ContractName)
			? "Unnamed Contract"
			: contract.Definition.ContractName;

		GameContext.Instance.HudEventManager?.Publish(
			HudEventType.Warning,
			$"Contract expired: {contractName}",
			this);
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
