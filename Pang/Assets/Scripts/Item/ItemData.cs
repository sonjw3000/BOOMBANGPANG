using UnityEngine;

// 지금은 이걸 그대로 사용하지만
// 나중엔 이걸 따로 등록하고 걔를 가리키는 형식으로다가 하자
[System.Serializable]
public class ItemData
{
	[SerializeField] private string name;
	[SerializeField] private uint itemID;
	[SerializeField] private float size;
	// 혹시 모를 render를 위한 프리팹
	[SerializeField] private GameObject itemPrefab;

	public string Name => name;
	public uint ItemID => itemID;
	public float Size => size;
	public GameObject ItemPrefab => itemPrefab;
}


