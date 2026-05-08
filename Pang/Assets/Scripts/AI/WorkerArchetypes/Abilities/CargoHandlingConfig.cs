using UnityEngine;

[CreateAssetMenu(menuName = "Worker/Archetype/Ability/CargoHandle")]
public class CargoHandlingConfig : AbilityConfigBase
{
	public override void Setup(AIWorker worker)
	{
		var ability = worker.gameObject.AddComponent<CargoHandlingAbility>();
		ability.Initialize(worker, this);
	}
}
