using UnityEngine;

namespace Assets.Scripts.Contract
{
	[CreateAssetMenu(menuName = "Contract/Contract Catalog")]
	public class ContractCatalog : ScriptableObject
	{
		public ContractDefinition[] Contracts;
	}
}
