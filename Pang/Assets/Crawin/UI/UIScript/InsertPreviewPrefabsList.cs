using UnityEngine;

public class InsertPreviewPrefabsList : MonoBehaviour
{
	public int ID;
	Picking picking;
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		picking = GameObject.Find("MousePicking").GetComponent<Picking>();
	}
	// Update is called once per frame
	void Update()
	{

	}

	public void Onclick()
	{
		if (ID == int.MinValue)
		{
			Debug.Log("빈칸이야 조심해");
			return;
		}

		picking.SetBuildingID(ID);

		// UI의 최종 부모 찾아서 disable하기
		Transform current = transform;
		while (current.parent != null && current.parent.GetComponent<Canvas>() == null)
		{
			current = current.parent;
		}
		current.gameObject.SetActive(false);

		Debug.Log("내 아이디는 : " + ID + "입니다 하하하");
	}
}
