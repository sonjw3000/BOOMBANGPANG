using UnityEngine;

public class HumanWorker : AIWorker
{
	private const float baseWorkSpeed = 1.0f;
	private const float minimumWorkSpeed = 0.5f;

	private float experience;

	[SerializeField] private float fatigue;
	[SerializeField] private float fatigueIncreasePerTask = 2.0f;

	public float Fatigue => fatigue;

	public override float GetWorkSpeedMultiplier()
	{
		return Mathf.Lerp(baseWorkSpeed, minimumWorkSpeed, fatigue / 100.0f);
	}

	public override float GetMoveSpeedMultiplier()
	{
		return Mathf.Lerp(baseWorkSpeed, minimumWorkSpeed, fatigue / 100.0f);
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

