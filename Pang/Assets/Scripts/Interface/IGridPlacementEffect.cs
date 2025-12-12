using Unity.Mathematics;

// 생성/삭제시 본인의 cell 말고 다른 cell에도 영향을 주는 객체들에만 적용한다
// 주로 동적인 객체나 pickingposition을 보유한 객체에 적용

public interface IGridPlacementEffect
{
	public void OnPositionSet(int3 position);
	public void OnRemoved();
}