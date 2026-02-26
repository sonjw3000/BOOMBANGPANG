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
	public float baseMoveSpeed;
	public int monthlyCost;

	// 아 배열로 넣고 하고싶다,,,
	[Header("Worker의 능력, endabled off면 가지지 않음")]
	public CarryBoxConfig carryBoxConfig;
	public LabelingConfig labelingConfig;
	public PackageConfig packageConfig;
	public PickStoreConfig pickStoreConfig;
	public CargoHandlingConfig cargoHandlingConfig;

	public void SetupWorker(AIWorker worker)
	{
		if (carryBoxConfig.enabled) carryBoxConfig.Setup(worker);
		if (labelingConfig.enabled) labelingConfig.Setup(worker);
		if (packageConfig.enabled) packageConfig.Setup(worker);
		if (pickStoreConfig.enabled) pickStoreConfig.Setup(worker);
		if (cargoHandlingConfig.enabled) cargoHandlingConfig.Setup(worker);
	}

}
