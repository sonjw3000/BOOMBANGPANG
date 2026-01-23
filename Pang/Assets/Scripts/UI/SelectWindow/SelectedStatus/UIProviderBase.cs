using UnityEngine;

public abstract class UIProviderBase : MonoBehaviour
{
	public abstract bool TryBuild(out SelectionModel model);
}

public abstract class UIProvider<T> : UIProviderBase
	where T : MonoBehaviour
{
	[SerializeField] protected T targetComponent = null;

	protected virtual void OnValidate()
	{
		targetComponent = GetComponent<T>();
	}
}
