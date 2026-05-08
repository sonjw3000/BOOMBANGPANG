
using UnityEngine;

[CreateAssetMenu(menuName = "Worker/Archetype/Ability/PickStore")]
public class PickStoreConfig : AbilityConfigBase
{
	public override void Setup(AIWorker worker)
	{
		var ability = worker.gameObject.AddComponent<PickStoreAbility>();
		ability.Initialize(worker, this);
	}
}
