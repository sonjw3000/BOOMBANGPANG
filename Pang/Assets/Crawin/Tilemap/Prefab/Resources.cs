using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEditor;
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
			int slotcnt = Content.childCount;
			var items = gameobject.GetComponent<Shelf>().Items;
			int itemcnt = items.Count;
			itemcnt = 9;
			if (itemcnt > slotcnt)
			{
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
				}
			}
			else
			{
				// 남는 칸 삭제
				for (int i = slotcnt; i > itemcnt; --i)
				{
					UnityEngine.Object.Destroy(Content.GetChild(i).gameObject);
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

	public ObjectData(int x, int y, int z, int type, int head)
	{
		this.type = type;
		this.x = x;
		this.y = y;
		this.z = z;
		this.head = head;
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
	//public List<Material[]> originalMats;

	public List<int3> GetBuildRange(int type)   // 배치된 타입에 따라 범위를 리턴해주는 함수
	{
		List<int3> result = new List<int3>();
		//int3 coord = new int3();
		switch (type)
		{
			case 2:
				break;
			case 3:
				break;
			case 4:
				break;
			case 5:
				break;
		}
		return result;
	}
	public List<int3> GetBuildRange()
	{
		return GetBuildRange(type);
	}

	public void Reset(Cell[,,] map)
	{
		Shelf shelf = obj.GetComponent<Shelf>();	// 선반을 갖고 있다면 pickingposition 또한 없애줘야함
		if (shelf)
		{
			int3 PickPosition = shelf.PickingPosition;
			if (map[PickPosition.x, PickPosition.y, PickPosition.z].type < 0)	// pickingposition위에 아무것도 없는 경우엔 삭제, 뭔가 있다 == 로봇이 올라가 있다 -> 삭제하면 안됨
			{
				map[PickPosition.x, PickPosition.y, PickPosition.z].type = 0;
			}
			map[PickPosition.x, PickPosition.y, PickPosition.z].previousType = 0;
		}

		type = previousType;
		if (type == 0 && obj != null)	// 빈칸인데 오브젝트가 존재하면 제거
		{
			UnityEngine.Object.Destroy(obj);
			obj = null;
		}
		//if (originalMats != null)
		//{
		//	originalMats.Clear();
		//}
	}

	public void Set(int id, Cell[,,] map, GameObject obj)
	{
		this.type = id;
		this.obj = obj;

		Shelf shelf = obj.GetComponent<Shelf>();
		if (shelf)
		{
			int3 PickPosition = shelf.PickingPosition;
			if (map[PickPosition.x, PickPosition.y, PickPosition.z].type == 0)
			{
				map[PickPosition.x, PickPosition.y, PickPosition.z].type = -1;
			}
			map[PickPosition.x, PickPosition.y, PickPosition.z].previousType = -1;
		}

		Status st = obj.GetComponent<Status>();
		if (st != null)
		{
			st.SetInit(obj.name, id);
		}
	}
}
[DefaultExecutionOrder(-100)]
[System.Serializable]
public class Resources// : MonoBehaviour
{
	public class RendererTemplate
	{
		// "path" -> sharedMaterials
		public Dictionary<string, Material[]> pathToMaterials = new Dictionary<string, Material[]>();
	}

	public Dictionary<int, RendererTemplate> IndexToMaterials = new Dictionary<int, RendererTemplate>();

	public TextAsset mapJsonFile;
	public GameObject[] Prefabs;
	private MapJson mapJson;
	public ref MapJson mapJsonRef => ref mapJson;

	[HideInInspector]
	public int3 mapSize;

	private Cell[,,] map;
	public ref Cell[,,] mapRef => ref map;

	public void Initialize()
	{
		mapJson = JsonUtility.FromJson<MapJson>(mapJsonFile.text);
		mapSize = new int3(mapJson.X, mapJson.Y, mapJson.Z);
		map = new Cell[mapSize.x, mapSize.y, mapSize.z];
		for (int y = 0; y < mapSize.y; ++y)
		{
			for (int x = 0; x < mapSize.x; ++x)
			{
				for (int z = 0; z < mapSize.z; ++z)
				{
					map[x, y, z] = new Cell();
					map[x, y, z].type = 0;
				}
			}
		}

		BuildRendererTemplates();

	}

	private void BuildRendererTemplates()
	{
		IndexToMaterials.Clear();

		for (int i = 0; i < Prefabs.Length; ++i)
		{
			//foreach (var prefab in Prefabs)
			var rendererTpl = new RendererTemplate();
			var root = Prefabs[i].transform;
			var renderers = Prefabs[i].GetComponentsInChildren<Renderer>(true);

			foreach (var rend in renderers)
			{
				var stack = new Stack<string>();
				for (var cur = rend.transform; cur != root.transform; cur = cur.parent)
					stack.Push(cur.name);

				string path = string.Join("/", stack);
				rendererTpl.pathToMaterials[path] = rend.sharedMaterials;
			}

			IndexToMaterials[i] = rendererTpl;
		}
	}

	// Update is called once per frame
	//void Update()
	//{
	//}

	private void OnValidate()
	{

	}

	public int getNewRobotID()
	{
		int cnt = 0;
		//while (robots.ContainsKey(cnt)) {
		//    ++cnt;
		//}
		return cnt;
	}

	// Find prefab index by name
	public int FindPrefabIndexByName(string name)
	{
		for (int i = 0; i < Prefabs.Length; ++i)
		{
			if (Prefabs[i].name == name)
				return i;
		}
		return -1;
	}
}
