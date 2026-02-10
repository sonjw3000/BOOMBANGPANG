using System;
using System.Collections.Generic;
using UnityEngine;

// UI Provider
// InfoBlock을 생성해서 Card에 전달할 수 있다
// DetailContent에게 정보를 전달하기 위해 프로퍼티로 데이터를 노출한다

public abstract class UIProviderBase
{
	//public abstract bool TryBuild(out SelectionModel model);
	public event System.Action OnDataChanged;
	public abstract string Name { get; }
	public abstract Sprite Icon { get; }
	public IEnumerable<InfoBlock> InfoBlocks => infoBlocks;
	
	// info blocks
	protected List<InfoBlock> infoBlocks = new();

	public abstract bool IsTargetType(GameObject obj);
	public abstract void LinkObject(GameObject obj);
	public abstract void BuildInfoBlocks();

}

public abstract class UIProvider<T> : UIProviderBase
	where T : Component
{
	protected T currentTarget = null;

	public override bool IsTargetType(GameObject obj) => obj.TryGetComponent<T>(out _);
	public override void LinkObject(GameObject obj) => currentTarget = obj.GetComponent<T>();
}
