using UnityEngine;

public enum WorkerType
{
	FullTime,
	PartTime,
	Illegal,
	Robot,
}

[CreateAssetMenu(menuName = "Worker/Archetype")]
public class WorkerArchetype : ScriptableObject
{
	[Header("기본 정보")]
	public string id;
	public WorkerType workerType;

	[Header("공통 스텟, 추가 예정")]
	public float baseMoveSpeedMultiplier = 1.0f;
	public float minimumMoveSpeedMultiplier = 0.5f;
	public float baseWorkSpeedMultiplier = 1.0f;
	public float minimumWorkSpeedMultiplier = 0.5f;
	public int monthlyCost;

	// 아 배열로 넣고 하고싶다,,,
	[Header("Worker's Ability")]
	public AbilityConfigBase[] abilities;

	public void SetupWorker(AIWorker worker)
	{
		foreach (var ability in abilities)
		{
			ability.Setup(worker);
		}
	}

}
