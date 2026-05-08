
using UnityEngine;

[CreateAssetMenu(menuName = "Worker/Archetype/Ability/Labeling")]
public class LabelingConfig : AbilityConfigBase
{
	public override void Setup(AIWorker worker)
	{
		var ability = worker.gameObject.AddComponent<LabelingAbility>();
		ability.Initialize(worker, this);
	}
}
