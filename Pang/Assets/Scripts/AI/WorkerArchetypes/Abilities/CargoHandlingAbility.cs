using UnityEngine;
using static UnityEngine.Rendering.STP;

[System.Serializable]
public class CargoHandlingConfig : AbilityConfigBase
{
	public override void Setup(AIWorker worker)
	{
		var ability = worker.gameObject.AddComponent<CargoHandlingAbility>();
		ability.Initailize(worker, this);
	}
}

public class CargoHandlingAbility : Ability<CargoHandlingConfig>
{
	protected override void OnInit()
	{
	}
}
