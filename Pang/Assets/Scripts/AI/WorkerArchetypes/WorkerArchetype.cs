using Unity.VisualScripting;
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
	public string workerName;
	public WorkerType workerType;

	[Header("공통 스텟, 추가 예정")]
	public float baseMoveSpeedMultiplier = 1.0f;
	public float minimumMoveSpeedMultiplier = 0.5f;
	public float baseWorkSpeedMultiplier = 1.0f;
	public float minimumWorkSpeedMultiplier = 0.5f;
	public int monthlyCost;

	// 아 배열로 넣고 하고싶다,,,
	[Header("Worker's Ability")]
	public WorkerAbility abilities;

	public void SetupWorker(AIWorker worker)
	{
		if (abilities.HasFlag(WorkerAbility.CargoHandling))		worker.AddComponent<CargoHandlingAbility>();
		if (abilities.HasFlag(WorkerAbility.CarryBox))			worker.AddComponent<CarryBoxAbility>();
		if (abilities.HasFlag(WorkerAbility.Labeling))			worker.AddComponent<LabelingAbility>();
		if (abilities.HasFlag(WorkerAbility.Packing))			worker.AddComponent<PackageAbility>();
		if (abilities.HasFlag(WorkerAbility.PickingStoring))	worker.AddComponent<PickStoreAbility>();
	}

}
