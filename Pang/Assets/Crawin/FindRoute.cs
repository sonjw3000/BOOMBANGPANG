using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;

public class FindRoute : MonoBehaviour
{
	//private Resources resources;
	private GridCell[,,] map => GameContext.Instance.GridService.Map;
	private int3 mapSize => GameContext.Instance.GridService.MapSize;

	public float speed = 2f;
	public float rotationSpeed = 5f;
	public int3 goalCoordinate;

	private int currentIndex = 0;
	private int3 previousNode;
	private int3 nextNode;
	List<int3> path;

	public int3 PreviousNode => previousNode;
	public int3 NextNode => nextNode;

	// astar에서 쓰이는 변수들
	int[,,] distance;
	int3[,,] prev;
	PriorityQueue<int4> pq;
	int3[] directions;
	int4 curr;

	struct Node
	{
		public int3 position;
		public int dist, head;
		public Node(int x, int y, int z, int d, int h)
		{
			position.x = x; position.y = y; position.z = z;
			dist = d; head = h;
		}
	}
	// astarLessRotate에서 쓰이는 변수
	PriorityQueue<Node> LRpq;
	Node LRcurr;

	//moveontile에서 쓰이는 변수들
	Vector3 targetPos;

	//public int type;
	Status mStatus;
	private AIWorker _Worker;
	public bool IsGoal { get; private set; }

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		// astar에서 쓰이는 변수들
		distance = new int[mapSize.x, mapSize.y, mapSize.z];
		prev = new int3[mapSize.x, mapSize.y, mapSize.z];
		pq = new PriorityQueue<int4>();
		directions = new int3[4];
		curr = new int4(Mathf.RoundToInt(transform.position.x), 0, Mathf.RoundToInt(transform.position.z), 0);
		previousNode.x = -1; previousNode.y = -1; previousNode.z = -1;
		nextNode.x = Mathf.RoundToInt(transform.position.x); nextNode.y = 0; nextNode.z = Mathf.RoundToInt(transform.position.z);

		//moveontile에서 쓰이는 변수들
		targetPos = new Vector3();

		if (map == null)
		{
			Debug.LogError("mapRef is null!");
		}
		path = new List<int3>();
		mStatus = gameObject.GetComponent<Status>();
		// todo 일단 임시로 status 최초 할당을 여기서 진행 -> 아마 나중엔 정원이가 만들어둔 매니저가 하지 않을까?
		if (mStatus)
		{
			mStatus.SetBattery(1);
			mStatus.SetWeight(0);
			mStatus.SetMaxStorage(100);
			mStatus.SetBatteryEfficiency(0.01f);
		}

		LRpq = new PriorityQueue<Node>();
		LRcurr = new Node();

		this.enabled = false;
		//Astar();
	}

	// Update is called once per frame
	void Update()
	{
		if (path.Count > 0)
		{
			MoveOnTile();
			mStatus.DecreaseBattery();
		}
		else
		{
			//Astar();
			AstarLessRotate();
		}
	}

	void MoveOnTile()
	{
		targetPos.x = nextNode.x;
		targetPos.y = nextNode.y + transform.position.y;
		targetPos.z = nextNode.z;

		Vector3 direction = math.normalize(targetPos - transform.position);
		float dotProduct = math.dot(transform.forward, direction);
		if (dotProduct < 0.999f)
		{
			Quaternion targetRotation;
			targetRotation = Quaternion.LookRotation(direction);
			transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
		}
		else
		{
			transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
		}

		if (Vector3.Distance(transform.position, targetPos) < 0.01f)    //목적지 도착
		{
			if (previousNode.y >= 0 && previousNode.x >= 0 && previousNode.z >= 0)
			{
				_Worker.SetPosition(nextNode);
			}

			if (currentIndex + 1 == path.Count)// 이후로 path가 없으면 (최종 목적지였다면)
			{
				IsGoal = true;
				_Worker.enabled = true;
				this.enabled = false;
				path.Clear();
			}
			else//다음 목적지로
			{
				nextNode = path[currentIndex + 1];
				if (map[nextNode.x, nextNode.y, nextNode.z].IsPassable)
				{
					previousNode = path[currentIndex++];
				}
				else
				{
					Debug.Log(transform.name + "가 목적지로 갈 수 없습니다.");
					path.Clear();
				}
			}
		}
	}

	int Heuristic(int3 next, int dist)
	{
		int x = math.abs(goalCoordinate.x - next.x);
		int y = math.abs(goalCoordinate.y - next.y);
		int z = math.abs(goalCoordinate.z - next.z);
		int h = x * x + y * y + z * z;
		int f = dist + h;
		return f;
	}

	void AstarLessRotate()
	{
		IsGoal = false;
		LRcurr.position.x = Mathf.RoundToInt(transform.position.x); LRcurr.position.y = 0;
		LRcurr.position.z = Mathf.RoundToInt(transform.position.z); LRcurr.dist = 0;
		LRcurr.head = ((Mathf.RoundToInt(gameObject.transform.eulerAngles.y / 90f) % 4) + 4) % 4; // x,y,z,distance,head
		//Debug.Log(LRcurr.head+"방향을 쳐다보고 있어");

		int id = mStatus.GetID();
		if (map[LRcurr.position.x, LRcurr.position.y, LRcurr.position.z].IsPassable == false)
		{
			//Debug.LogError("얘 지금 이상한짓 해요" + gameObject.name + "가 " + map[LRcurr.position.x, LRcurr.position.y, LRcurr.position.z].type + "을 " + id + "로 바꾼다");
			return;
		}

		// distance 와 prev 배열 초기화
		for (int y = 0; y < mapSize.y; ++y)
		{
			for (int x = 0; x < mapSize.x; ++x)
			{
				for (int z = 0; z < mapSize.z; ++z)
				{
					distance[x, y, z] = int.MaxValue;
					prev[x, y, z].x = -1; prev[x, y, z].y = -1; prev[x, y, z].z = -1;
				}
			}
		}

		// pq 비우기
		while (LRpq.Count > 0)
		{
			LRpq.Dequeue();
		}

		LRpq.Enqueue(LRcurr, 0);
		distance[LRcurr.position.x, LRcurr.position.y, LRcurr.position.z] = 0;
		int lowest_heuristic = HeuristicLessRotate(LRcurr.position.xyz, 0, 0);
		int3 nearest_goal = LRcurr.position.xyz;
		while (LRpq.Count > 0)
		{
			Node top = LRpq.Dequeue();

			if (top.position.y == goalCoordinate.y && top.position.x == goalCoordinate.x && top.position.z == goalCoordinate.z)
			{
				nearest_goal = top.position.xyz;
				break;
			}

			if (distance[top.position.x, top.position.y, top.position.z] < top.dist)
				continue;

			directions[0].x = top.position.x; directions[0].y = top.position.y; directions[0].z = top.position.z + 1;
			directions[1].x = top.position.x + 1; directions[1].y = top.position.y; directions[1].z = top.position.z;
			directions[2].x = top.position.x; directions[2].y = top.position.y; directions[2].z = top.position.z - 1;
			directions[3].x = top.position.x - 1; directions[3].y = top.position.y; directions[3].z = top.position.z;

			int head = 0;
			foreach (int3 dir in directions)
			{
				if (dir.x >= 0 && dir.x < mapSize.x && dir.y >= 0 && dir.y < mapSize.y && dir.z >= 0 && dir.z < mapSize.z)
				{
					int dist = distance[top.position.x, top.position.y, top.position.z] + 1;
					if (map[dir.x, dir.y, dir.z].IsPassable && distance[dir.x, dir.y, dir.z] > dist)
					{
						distance[dir.x, dir.y, dir.z] = dist;
						prev[dir.x, dir.y, dir.z] = top.position.xyz;
						Node temp = new Node(dir.x, dir.y, dir.z, dist, head);
						// 이 new int4는 어쩔 수 없이 써야함
						int p = HeuristicLessRotate(dir, dist, (head == top.head) ? 0 : 10000);
						//회전 가중치 고치려면 고치세요
						LRpq.Enqueue(temp, p);
						if (lowest_heuristic >= p)
						{
							lowest_heuristic = p;
							nearest_goal = dir;
						}
					}
				}
				++head;
			}
		}
		//Debug.Log("가장 가까운 노드" + nearest_goal);


		int3 back = nearest_goal;
		path.Clear();
		while (back.x != -1 && back.y != -1 && back.z != -1)
		{
			path.Add(back);
			back = prev[back.x, back.y, back.z];
		}
		path.Reverse();

		currentIndex = 0;
		nextNode = path[currentIndex];
		previousNode.x = -1; previousNode.y = -1; previousNode.z = -1;

		//string s = "";
		//s += transform.name;
		//foreach (int3 p in path)
		//{
		//	s += p + " -> ";
		//}
		//Debug.Log("이거로 확정이야" + s);
	}

	int HeuristicLessRotate(int3 next, int dist, int turn_cost)
	{
		int x = math.abs(goalCoordinate.x - next.x);
		int y = math.abs(goalCoordinate.y - next.y);
		int z = math.abs(goalCoordinate.z - next.z);
		int h = x * x + y * y + z * z;
		int f = dist + h + turn_cost;
		// 회전 가중치 고치려면 고치세요
		return f;
	}

	public void RemoveThisObjectOnMap()
	{

	}

	public int3 GetRandomPos()
	{
		//Debug.Log("finish");
		int3 rand = new int3(UnityEngine.Random.Range(0, mapSize.x), UnityEngine.Random.Range(0, mapSize.y), UnityEngine.Random.Range(0, mapSize.z));
		while (map[rand.x, rand.y, rand.z].IsPassable)
		{
			rand.x = UnityEngine.Random.Range(0, mapSize.x);
			rand.y = UnityEngine.Random.Range(0, mapSize.y);
			rand.z = UnityEngine.Random.Range(0, mapSize.z);
		}
		goalCoordinate = rand;
		//path.Clear();

		return rand;
	}

	public bool SetGoalPosition(int3 goalPos)
	{
		goalCoordinate = goalPos;
		mStatus.SetGoal(goalPos);
		//Astar();
		AstarLessRotate();
		//Debug.Log(path_size);
		//this.enabled = true;
		return true;
	}

	public void SetAIMaster(AIWorker worker)
	{
		_Worker = worker;
	}

	public float GetPathPercent()
	{
		return (float)currentIndex / path.Count;
	}
}
