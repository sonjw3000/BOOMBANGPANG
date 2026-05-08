using UnityEngine;

[System.Serializable]
public abstract class AbilityConfigBase : ScriptableObject
{
	public abstract void Setup(AIWorker worker);
}


public abstract class AbilityBase : MonoBehaviour
{
	protected AIWorker worker;

	public AIWorker Worker => worker;

	protected void Init(AIWorker worker)
	{
		this.worker = worker;
		OnInit();
	}

	protected virtual void OnInit() { }
}

public abstract class Ability<TConfig> : AbilityBase 
	where TConfig : AbilityConfigBase
{
	protected TConfig Config { get; private set; }

	public void Initialize(AIWorker worker, TConfig config)
	{
		this.Config = config;
		Init(worker);
	}
}
