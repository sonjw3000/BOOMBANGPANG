using JetBrains.Annotations;
using System;
using UnityEngine;

[Flags]
public enum ItemTag
{
	None		= 0,
	Fragile		= 1 << 0,
	Food		= 1 << 1,
	Danger		= 1 << 2,
	Electric	= 1 << 3,
}

// 지금은 이걸 그대로 사용하지만
// 나중엔 이걸 따로 등록하고 걔를 가리키는 형식으로다가 하자
[System.Serializable]
public class ItemData
{
	[SerializeField] private string name;
	[SerializeField] private uint itemID;
	[SerializeField] private float size;
	[SerializeField] private ItemTag tag;
	// 혹시 모를 render를 위한 프리팹
	[SerializeField] private GameObject itemPrefab;
	public string Name => name;
	public uint ItemID => itemID;
	public float Size => size;
	public GameObject ItemPrefab => itemPrefab;
}


