
using UnityEngine;

public class SelectDetailUI : MonoBehaviour
{
	// 라인 컨텐츠 별 obj pool이 필요하다
	// 
	[Header("Content Parent")]
	[SerializeField] private Transform contentParent;


	private void Start()
	{

		gameObject.SetActive(false);
	}

	public void SetUpDetail(SelectionModel selectionModel)
	{

	}
}
