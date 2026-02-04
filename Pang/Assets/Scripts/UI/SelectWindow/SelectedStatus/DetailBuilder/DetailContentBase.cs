
using UnityEngine;

public abstract class DetailContentBase : MonoBehaviour
{
	protected abstract void LinkData();
}

public abstract class DetailContent<T> : DetailContentBase
	where T : UIProviderBase
{
	protected T provider = null;
	
	public void SetProvider(T provider)
	{
		this.provider = provider;
		LinkData();
		gameObject.SetActive(true);
	}

}
