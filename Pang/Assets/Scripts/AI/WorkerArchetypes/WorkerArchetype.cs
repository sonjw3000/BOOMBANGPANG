using UnityEngine;

[CreateAssetMenu(menuName = "Worker/Archetype")]
public class WorkerArchetype : ScriptableObject
{
	[Header("기본 정보")]
	public string id;
	public bool isHuman;
	public bool isIlligal;

	[Header("공통 스텟, 추가 예정")]
	public float baseMoveSpeed;

	[Header("Worker의 능력, endabled off면 가지지 않음")]
	public CarryBoxConfig carryBoxConfig;
	public LabelingConfig labelingConfig;
	public PackageConfig packageConfig;
	public PickStoreConfig pickStoreConfig;

	public void SetupWorker(AIWorker worker)
	{
		if (carryBoxConfig.enabled) carryBoxConfig.Setup(worker);
		if (labelingConfig.enabled) labelingConfig.Setup(worker);
		if (packageConfig.enabled) packageConfig.Setup(worker);
		if (pickStoreConfig.enabled) pickStoreConfig.Setup(worker);
	}

}
