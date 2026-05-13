using UnityEngine;

[CreateAssetMenu(menuName = "Worker/Visual")]
public class WorkerVisualDefinition : ScriptableObject
{
	[SerializeField] private string visualId = "";
	public GameObject Prefab;
	public Sprite icon;

	public string VisualId => string.IsNullOrWhiteSpace(visualId) ? name : visualId;
}
