using Assets.Scripts.Contract;
using UnityEngine;

public partial class ContractService
{
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

	public void ResetRuntimeState()
	{
		currentActiveContracts.Clear();
	}
}
