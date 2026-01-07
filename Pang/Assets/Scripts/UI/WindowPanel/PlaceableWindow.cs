using Assets.Scripts.UI;
using UnityEngine;

public class PlaceableWindow : MonoBehaviour
{
	[SerializeField] private UIWindow window;
	[SerializeField] private RectTransform workerContent;

	[Header("Window MetaData")]
	[SerializeField] private string title = "Placeable Controller";
	[SerializeField] private Sprite icon;

	private void Awake()
	{
		window.SetTitle(title);
		window.SetIcon(icon);

		workerContent.SetParent(window.ContentRoot, false);
	}
}
