using Unity.Mathematics;
using UnityEngine;

public class DestroyContext
{
	public enum Destroycause
	{
		Explosion,
		Override,
	}

	public readonly Destroycause cause;
	public readonly GameObject destroyedBy;
	public readonly PlaceableDefinition overriddenByPlaceableDefinition;
	public readonly GameObject overriddenByObject;

	public bool IsOverride => cause == Destroycause.Override;

	public DestroyContext(
		Destroycause cause,
		GameObject destroyedBy = null,
		PlaceableDefinition overriddenByPlaceableDefinition = null,
		GameObject overriddenByObject = null)
	{
		this.cause = cause;
		this.destroyedBy = destroyedBy;
		this.overriddenByPlaceableDefinition = overriddenByPlaceableDefinition;
		this.overriddenByObject = overriddenByObject;
	}

	public static DestroyContext ForOverride(PlaceableDefinition overriddenByPlaceableDefinition, GameObject overriddenByObject = null)
	{
		return new DestroyContext(
			Destroycause.Override,
			destroyedBy: overriddenByObject,
			overriddenByPlaceableDefinition: overriddenByPlaceableDefinition,
			overriddenByObject: overriddenByObject
		);
	}
}

// 그리드 객체의 생명 주기만을 관리
// 생성 시점에만 OnPositionSet으로 본인의 position을 받아 저장함
public interface IGridPlaceable
{
	// grid actions
	public void OnPositionSet(in int3 position, FacingDirection direction);

	public void OnDestroyedBy(in DestroyContext ctx);

	public int3 GridPosition { get; }
	public FacingDirection Direction { get; }

	public WorkerStatusTarget BuildingTarget { get; }
}
