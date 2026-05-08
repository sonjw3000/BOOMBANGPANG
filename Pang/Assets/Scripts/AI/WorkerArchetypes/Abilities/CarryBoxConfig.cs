using UnityEngine;

[CreateAssetMenu(menuName = "Worker/Archetype/Ability/CarryBox")]
public class CarryBoxConfig : AbilityConfigBase
{
	public override void Setup(AIWorker worker)
	{
		var ability = worker.gameObject.AddComponent<CarryBoxAbility>();
		ability.Initialize(worker, this);
	}
}
