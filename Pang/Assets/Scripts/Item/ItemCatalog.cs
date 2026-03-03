using UnityEngine;

[CreateAssetMenu(menuName = "Item/Item Catalog")]
public class ItemCatalog : ScriptableObject
{
	public ItemDefinition[] itemDefinitions;
}
