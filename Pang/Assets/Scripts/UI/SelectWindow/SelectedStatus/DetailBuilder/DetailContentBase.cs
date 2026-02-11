
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UI;

public abstract class DetailContentBase : MonoBehaviour
{
	[SerializeField] protected Button deleteButton = null;
	protected UIProviderBase provider = null;

	public Button.ButtonClickedEvent DeleteButtonEvent => deleteButton.onClick;

	private void OnValidate()
	{
		if (deleteButton == null)
		{
			Debug.LogError("Delete Button is not assigned!", this);
		}
	}

	private void Awake()
	{
		DeleteButtonEvent.AddListener(() => provider?.DeleteObject());
	}

	public abstract bool IsTargetType(GameObject obj);
	public void SetProvider(UIProviderBase provider)
	{
		this.provider = provider;
		LinkData();
		gameObject.SetActive(true);
	}

	protected abstract void LinkData();
	protected virtual void UpdateData() { }
}

public abstract class DetailContent<T> : DetailContentBase
	where T : Component
{
	
	public override bool IsTargetType(GameObject obj) => obj.TryGetComponent<T>(out _);
	
	private void Update()
	{
		UpdateData();
	}


}
