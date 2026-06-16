using UnityEngine;

public sealed class DetailDefaultTabsView : MonoBehaviour
{
	[SerializeField] private RectTransform infoTabRoot = null;
	[SerializeField] private RectTransform actionTabRoot = null;

	public RectTransform InfoTabRoot => infoTabRoot;
	public RectTransform ActionTabRoot => actionTabRoot;
}
