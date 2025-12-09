
[System.Serializable]
public class LabelingConfig : AbilityConfigBase
{
	public float handSpeed;

	public override void Setup(AIWorker worker)
	{
		var ability = worker.gameObject.AddComponent<LabelingAbility>();
		ability.Initailize(worker, this);
	}
}

public class LabelingAbility : Ability<LabelingConfig>
{
	private float handSpeed;

	protected override void OnInit()
	{
		handSpeed = Config.handSpeed;
	}
}


