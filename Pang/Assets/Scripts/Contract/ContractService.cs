using System.Collections.Generic;
using UnityEngine;

public enum ContractStatus
{
	Active,
	Completed,
	Failed,
}

public class Someting
{
	public ContractDefinition ContractDefinition;
	

}
//public class 

public class ContractService : MonoBehaviour
{
	[SerializeField] private ContractDefinition[] AvailableContracts;

	private List<ContractDefinition> activeContracts = new();

	private GameTime gameTime;



	public IReadOnlyList<ContractDefinition> ContractDefinitions => AvailableContracts;

	private EconomyService Economy => GameContext.Instance.EconomyService;


	private void Start()
	{
		if (gameTime == null)
			gameTime = FindFirstObjectByType<GameTime>();

		gameTime.OnMonthPassed += ProcessMonthlyContracts;
	}

	private void ProcessMonthlyContracts()
	{
		int moneyChange = 0;
		float reputationChange = 0f;

		foreach (var contract in activeContracts)
		{

		}
	}
}

