using System.Collections.Generic;
using UnityEngine;

// UI Provider
// InfoBlock을 생성해서 Card에 전달할 수 있다
// DetailContent에게 정보를 전달하기 위해 프로퍼티로 데이터를 노출한다

public abstract class UIProviderBase
{
	//public abstract bool TryBuild(out SelectionModel model);
	public abstract string Name { get; }
	public abstract Sprite Icon { get; }
	public IEnumerable<InfoBlock> InfoBlocks => infoBlocks;
	
	// info blocks
	protected List<InfoBlock> infoBlocks = new();

	private GameObject linkedObject = null;

	protected void SetGameObject(GameObject go) => linkedObject = go;

	public abstract bool IsTargetType(GameObject obj);
	public abstract void LinkObject(GameObject obj);
	public abstract void BuildInfoBlocks();
	protected virtual void OnDataChanged() { }
	public virtual void OnUpdate() { }

	public void DeleteObject()
	{
		if (linkedObject == null) return;

		var grid = linkedObject.GetComponent<IGridPlaceable>();

		if (grid == null)
		{
			Debug.LogError("Linked Object is not IGridPlaceable");
			return;
		}

		GameContext.Instance.GridService.OnRemove(linkedObject);
	}

}

public abstract class UIProvider<T> : UIProviderBase
	where T : Component
{
	protected T currentTarget = null;

	public T Target => currentTarget;

	public override bool IsTargetType(GameObject obj) => obj.TryGetComponent<T>(out _);
	public override void LinkObject(GameObject obj)
	{
		SetGameObject(obj);
		currentTarget = obj.GetComponent<T>();
	}
}
