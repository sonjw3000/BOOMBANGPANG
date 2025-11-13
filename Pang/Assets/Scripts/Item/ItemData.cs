using System;
using System.Collections.Generic;
using UnityEngine;
using ToteElement = System.ValueTuple<ItemData, int>;

// 지금은 이걸 그대로 사용하지만
// 나중엔 이걸 따로 등록하고 걔를 가리키는 형식으로다가 하자
[Serializable]
public class ItemData
{
	public string Name { get; private set; }
	public int ItemID { get; private set; }
	public float Size { get; private set; }

	// 혹시 모를 render를 위한 프리팹
	private GameObject itemPrefab;
}

//public class Pallet
//{
//	public List<ItemData> Items { get; private set; }

//	public Pallet(List<ItemData> data) { Items = data; }
//}

//public class TruckManifest
//{
//	public List<Pallet> Pallets { get; private set; } = new List<Pallet>();

//	public TruckManifest(List<Pallet> data) { Pallets = data; }
//}

public class ToteBox
{
	public float Capacity { get; private set; } = 10.0f;
	public float Size { get; private set; } = 0.0f;
	public Stack<ToteElement> Items { get; private set; } = new Stack<ToteElement>();
	public ToteBox(float capacity = 10.0f) => Capacity = capacity;
}
