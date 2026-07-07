using UnityEngine;

namespace Assets.Scripts.Contract.ItemContract
{
	[CreateAssetMenu(menuName = "Contract/Contract Catalog")]
	public class ContractCatalog : ScriptableObject
	{
		public ContractDefinition[] Contracts;
	}
}
