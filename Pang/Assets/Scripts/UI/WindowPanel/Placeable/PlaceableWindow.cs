using Assets.Scripts.UI;
using System.Threading;
using UnityEngine;

public class PlaceableWindow : MonoBehaviour
{
	[SerializeField] private UIWindow window;
	[SerializeField] private RectTransform workerContent;

	[Header("Window MetaData")]
	[SerializeField] private string title = "Facility Control";
	[SerializeField] private Sprite icon;

	[Header("Button UI")]
	[SerializeField] private PlaceableButtonView buttonPrefab;

	private BuildPlaceableCatalog catalog => GameContext.Instance.BuildPlaceableCatalog;

	private void Awake()
	{
		window.SetTitle(title);
		window.SetIcon(icon);

		workerContent.SetParent(window.ContentRoot, false);
		
		window.gameObject.SetActive(true);
		BuildContentUI();

		buttonPrefab.gameObject.SetActive(false);
		gameObject.SetActive(false);
	}

	public void Open()
	{
		gameObject.SetActive(true);
		window.Open();
	}

	public void Close()
	{
		window.Close();
		gameObject.SetActive(false);
	}

	private void BuildContentUI()
{
		if (catalog == null)
		{
			Debug.LogError("No Caltalog!");
			return;
		}

		foreach (var def in catalog.EnumerateDefinitions())
		{
			var btn = Instantiate(buttonPrefab, workerContent);
			btn.gameObject.SetActive(true);
			btn.Bind(def, HandleClickEvent);
			//Debug.Log($"name: {def.name}");
		}
	}

	private void HandleClickEvent(PlaceableDefinition def)
	{
		//OnPlaceableSelected?

		// todo
		// event 형식으로 바꾸는게 좋을거같긴해

		gameObject.SetActive(false);
		GameContext.Instance.InteractionCtx.EnterPlacementMode(def);
	}
}
