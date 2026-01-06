using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Tile/TileCatalog")]
public class TileCatalog : ScriptableObject
{
	[SerializeField] private List<GameObject> tilePrefabs;

	public GameObject GetObject(int id) => tilePrefabs[id];
}
