using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public abstract class ObjectStatus
{
	protected string mName;
	protected int mId;

	public abstract void GetStatus();
	public abstract void GetStatus(Transform Viewport, GameObject gameobject, bool init);
	public void SetName(string name)
	{
		mName = name;
	}
	public void SetId(int id)
	{
		mId = id;
	}
	public int GetID()
	{
		return mId;
	}
	public string GetName()
	{
		return mName;
	}
}

public class ShelfStatus : ObjectStatus
{
	protected string content;
	protected int max_storage;
	protected int left_weight;
	public override void GetStatus()
	{
		Debug.Log("[" + mName + "] { \n\t" +
			"content - " + content + "\n\t" +
			"max_weight - " + max_storage + " \n\t" +
			"left_weight - " + left_weight + "\n}");
	}
	public override void GetStatus(Transform Viewport, GameObject gameobject, bool init)
	{
		//GetStatus();
		Transform Content = Viewport.GetChild(0);

		if (init)
		{
			Debug.Log("좌클릭 오브젝트가 변경되어 최초로 칸 배정");
			int slotcnt = Content.childCount;
			var items = gameobject.GetComponent<Shelf>().Stacks;
			int itemcnt = items.Count;
			itemcnt = 10;
			if (itemcnt > slotcnt)
			{
				Debug.Log("아이템 칸의 갯수가 모자라 칸 생성");
				// 아이템 갯수만큼 칸 생성
				for (int i = slotcnt; i < itemcnt; ++i)
				{
					GameObject child = new GameObject();
					child.transform.SetParent(Content, false);
					Image img = child.AddComponent<Image>();

					// 임시로 칸 색 구별
					// i를 0~1 범위로 정규화
					float t = (float)(i - slotcnt) / (itemcnt - slotcnt - 1); // 0 ~ 1

					// 검정 → 흰색으로 점점 밝게
					img.color = Color.Lerp(Color.black, Color.white, t);
					//img.color = Color.black;

				}
			}
			else
			{
				// 남는 칸 삭제
				for (int i = 0; i < slotcnt - itemcnt; ++i)
				{
					Debug.Log("i = " + i + ", itemcnt = " + itemcnt);
					UnityEngine.Object.Destroy(Content.GetChild(i).gameObject);

					//Content.GetChild(i).gameObject.SetActive(false);
					// 는 하지 말고 그냥 disable 시켜두자
					// 하기엔 클래스에서 비활성화된 자식 객체의 갯수를 파악하기 힘들다 -> for문 돌아야함
				}
				//Debug.Log(slotcnt - items.Count + " 만큼 삭제");
			}

			int index = 0;
			foreach (var item in items)
			{
				Content.GetChild(index).name = item.ToString();
				++index;
			}
		}
		else
		{
			//Debug.Log("초기화 작업이 아님");
		}
		return;
	}
}

public class RobotStatus : ObjectStatus
{
	protected int3 goal;
	protected float battery;
	protected int weight;
	protected int max_storage;
	protected float batteryEfficiency;
	public override void GetStatus()
	{
		Debug.Log("[" + mName + "] { \n\t" +
	"goal - " + goal + "\n\t" +
	"battery - " + battery + " \n\t" +
	"weight - " + weight + "\n}");
	}
	public override void GetStatus(Transform Viewport, GameObject gameobject, bool init)
	{
		foreach (Transform element in Viewport)
		{
			if (element.name == "Goal")
			{
				TextMeshProUGUI text = element.GetChild(0).GetChild(1).GetComponent<TextMeshProUGUI>();
				string s = "(" + goal.x + ", " + goal.z + ")";
				text.text = s;
				Image percent = element.GetChild(1).GetChild(0).GetComponent<Image>();
				FindRoute fr = gameobject.GetComponent<FindRoute>();
				percent.fillAmount = fr.GetPathPercent();
			}
			else if (element.name == "Battery")
			{
				Image percent = element.GetChild(1).GetChild(0).GetComponent<Image>();
				percent.fillAmount = battery;
			}
			else if (element.name == "Weight")
			{
				Image percent = element.GetChild(1).GetChild(0).GetComponent<Image>();
				percent.fillAmount = (float)weight / max_storage;
			}
		}
		return;
	}
	public void SetGoal(int3 position)
	{
		goal = position;
	}
	public void SetBattery(float left)
	{
		battery = left;
	}
	public void SetWeight(int w)
	{
		weight = w;
	}
	public void SetMaxStorage(int m)
	{
		max_storage = m;
	}
	public void SetBatteryEfficiency(float e)
	{
		batteryEfficiency = e;
	}

	public int3 GetGoal()
	{
		return goal;
	}

	public float GetBattery()
	{
		return battery;
	}

	public int GetWeight()
	{
		return weight;
	}

	public int GetMaxStorage()
	{
		return max_storage;
	}

	public void DecreaseBattery()
	{
		battery -= batteryEfficiency * Time.deltaTime;
	}
}

[System.Serializable]
public class ObjectData
{
	public int type;
	public int x, y, z;
	public int head;
	//head 는 *90도 로 계산 -> 0 == 0도, 1 == 90도, 2 == 180 ...
	public ObjectData(int x, int y, int z, int type)
	{
		this.type = type;
		this.x = x;
		this.y = y;
		this.z = z;
		this.head = 0;
	}

}

[System.Serializable]
public class MapJson
{
	public int X, Y, Z;
	public List<ObjectData> buildingData;
	public List<ObjectData> robotdata;
	public MapJson()
	{
		buildingData = new List<ObjectData>();
		robotdata = new List<ObjectData>();
	}
}

public class Cell
{
	public int type;
	public int previousType;
	public GameObject obj;
}
