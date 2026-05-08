using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Worker/MarketData")]
public class WorkforceMarketData_SO : ScriptableObject
{
	[SerializeField] private List<WorkerArchetype> availableArchetypes;
	public List<WorkerArchetype> AvailableArchetypes => availableArchetypes;
}
