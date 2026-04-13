using UnityEngine;

public class HumanWorker : AIWorker
{
	private float experience;

	[SerializeField] private float fatigue;
	[SerializeField] private float fatigueIncreasePerTask = 2.0f;

	public float Fatigue => fatigue;

	public override float GetWorkSpeedMultiplier()
	{
		return Mathf.Lerp(BaseWorkSpeedMultiplier, MinimumWorkSpeedMultiplier, fatigue / 100.0f);
	}

	public override float GetMoveSpeedMultiplier()
	{
		return Mathf.Lerp(BaseMoveSpeedMultiplier, MinimumMoveSpeedMultiplier, fatigue / 100.0f);
	}

	public override void OnTaskCompleted()
	{
		base.OnTaskCompleted();
		experience += 10.0f; // 경험치 증가
		fatigue += fatigueIncreasePerTask; // 피로도 증가
	}

	public override void TickVitals(float deltaTime)
	{
		//fatigue += deltaTime * 0.1f;
	}
}

