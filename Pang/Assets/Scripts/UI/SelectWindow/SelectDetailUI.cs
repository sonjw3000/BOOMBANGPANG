
using UnityEngine;

public class SelectDetailUI : MonoBehaviour
{
	private DetailContentBase currentContent = null;

	private void Start()
	{
		gameObject.SetActive(false);
	}

	public void SetDetailContent(DetailContentBase detailContent)
	{
		currentContent = detailContent;
		if (currentContent == null)
		{
			Debug.LogError("Current provider is null. Cannot set detail content.", this);
			return;
		}

	}
}
