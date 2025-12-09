
[System.Serializable]
public class PackageConfig : AbilityConfigBase
{
	public float handSpeed;

	public override void Setup(AIWorker worker)
	{
		var ability = worker.gameObject.AddComponent<PackageAbility>();
		ability.Initailize(worker, this);
	}
}

public class PackageAbility : Ability<PackageConfig>
{
	private float handSpeed;

	protected override void OnInit()
	{
		handSpeed = Config.handSpeed;
	}
}

