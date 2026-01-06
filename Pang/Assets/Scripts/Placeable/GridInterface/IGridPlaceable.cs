using Unity.Mathematics;
using UnityEngine;

public class DestroyContext
{
	public enum Destroycause
	{
		Explosion,
	}

	public readonly Destroycause cause;
	public GameObject destroyedBy;
}

// 그리드 객체의 생명 주기만을 관리
// 생성 시점에만 OnPositionSet으로 본인의 position을 받아 저장함
public interface IGridPlaceable
{
	// grid actions
	public void OnPositionSet(in int3 position);

	public void OnDestroyedBy(in DestroyContext ctx);

	public int3 GridPosition { get; }
}

