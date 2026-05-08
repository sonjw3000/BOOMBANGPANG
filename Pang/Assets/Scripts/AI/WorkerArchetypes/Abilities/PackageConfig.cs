
using UnityEngine;

[CreateAssetMenu(menuName = "Worker/Archetype/Ability/PackageHandle")]
public class PackageConfig : AbilityConfigBase
{
	public override void Setup(AIWorker worker)
	{
		var ability = worker.gameObject.AddComponent<PackageAbility>();
		ability.Initialize(worker, this);
	}
}
