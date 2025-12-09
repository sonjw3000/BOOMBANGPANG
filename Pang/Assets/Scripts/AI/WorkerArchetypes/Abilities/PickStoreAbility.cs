
[System.Serializable]
public class PickStoreConfig : AbilityConfigBase
{
	public float handSpeed;

	public override void Setup(AIWorker worker)
	{
		var ability = worker.gameObject.AddComponent<PickStoreAbility>();
		ability.Initailize(worker, this);
	}
}

public class PickStoreAbility : Ability<PickStoreConfig>
{
	private float handSpeed;

	protected override void OnInit()
	{
		handSpeed = Config.handSpeed;
	}
}
