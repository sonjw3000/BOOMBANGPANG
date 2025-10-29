using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;

public class FindRoute : MonoBehaviour
{
    private Resources resources;
    private Cell[,,] map;
    private int3 mapSize;
    public float speed = 2f;
    public float rotationSpeed = 5f;
    public int3 goalCoordinate;

    private int currentIndex = 0;
    private int3 previous;
    List<int3> path;
    int path_size;

    // astar에서 쓰이는 변수들
    int[,,] distance;
    int3[,,] prev;
    PriorityQueue<int4> pq;
    int3[] directions;
    int4 curr;

    //moveontile에서 쓰이는 변수들
    Vector3 targetPos;

    public int type;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        resources = GameObject.Find("Resources").GetComponent<Resources>();
        map = resources.mapRef;
        mapSize = resources.mapSize;

        // astar에서 쓰이는 변수들
        distance = new int[mapSize.x, mapSize.y, mapSize.z];
        prev = new int3[mapSize.x, mapSize.y, mapSize.z];
        pq = new PriorityQueue<int4>();
        directions = new int3[4];
        curr = new int4();

        //moveontile에서 쓰이는 변수들
        targetPos = new Vector3();

        if (map == null)
        {
            Debug.LogError("mapRef is null!");
        }
        path_size = 0;
        path = new List<int3>();
        Astar();
    }

    // Update is called once per frame
    void Update()
    {
        
        if (path_size > 0)
        {
            MoveOnTile();
        }
        else
        {
            Astar();
        }
    }

    void MoveOnTile()
    {
        targetPos.x = path[currentIndex].x; targetPos.y = path[currentIndex].y + transform.position.y; targetPos.z = path[currentIndex].z;

        Vector3 direction = math.normalize(targetPos - transform.position);
        float dotProduct = math.dot(transform.forward, direction);
        if (dotProduct < 0.999f)   // 회전이 필요하면
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
            //Debug.Log("도착");
            if (previous.y >= 0 && previous.x >= 0 && previous.z >= 0)
            {
                map[previous.x, previous.y, previous.z].type = 0;
                map[previous.x, previous.y,previous.z].obj = null; //도착해서 이전 위치에 존재하는 gameobject를 null로 변경
            }

            if (currentIndex + 1 == path.Count)// 이후로 path가 없으면 (최종 목적지였다면)
            {
                //Debug.Log("finish");
                int3 rand = new int3(UnityEngine.Random.Range(0, mapSize.x), UnityEngine.Random.Range(0, mapSize.y), UnityEngine.Random.Range(0, mapSize.z));
                while (map[rand.x,rand.y,rand.z].type != 0)
                {
                    rand.x = UnityEngine.Random.Range(0, mapSize.x);
                    rand.y = UnityEngine.Random.Range(0, mapSize.y);
                    rand.z = UnityEngine.Random.Range(0, mapSize.z);
                }
                goalCoordinate = rand;
                path_size = 0;
                path.Clear();
            }
            else//다음 목적지로
            {
                int3 next = path[currentIndex + 1];
                if (map[next.x,next.y,next.z].type == 0)
                    // 다음 목적지가 이동 가능한 상태면
                {
                    previous = path[currentIndex++];
                    map[previous.x, previous.y, previous.z].type = int.MaxValue;
                    map[next.x, next.y, next.z].type = type;
                    map[next.x,next.y,next.z].obj = gameObject;
                }
                else
                {   // 다음 목적지로 이동이 불가능한 상태면
                    //Debug.Log(transform.name + "가 목적지로 갈 수 없습니다.");
                    path_size = 0;
                    path.Clear();
                }
            }
        }
    }

    

    void Astar()
    {
        curr.x = Mathf.RoundToInt(transform.position.x); curr.y = 0; curr.z = Mathf.RoundToInt(transform.position.z); curr.w = 0; // x,y,z,distance
        map[curr.x, curr.y, curr.z].type = type;
        map[curr.x, curr.y, curr.z].obj = gameObject;

        // distance 와 prev 배열 초기화
        for (int y = 0; y < mapSize.y; ++y)
        {
            for(int x = 0; x < mapSize.x; ++x)
            {
                for(int z = 0; z < mapSize.z; ++z)
                {
                    distance[x, y, z] = int.MaxValue;
                    prev[x, y, z].x = -1; prev[x, y, z].y = -1; prev[x, y, z].z = -1;
                }
            }
        }

        // pq 비우기
        while (pq.Count > 0)
        {
            pq.Dequeue();
        }

        pq.Enqueue(curr, 0);
        distance[curr.x, curr.y, curr.z] = 0;
        int lowest_heuristic = Heuristic(curr.xyz, 0);
        int3 nearest_goal = curr.xyz;
        while (pq.Count > 0)
        {
            int4 top = pq.Dequeue();

            if (top.y == goalCoordinate.y && top.x == goalCoordinate.x && top.z == goalCoordinate.z) break;

            if (distance[top.x, top.y, top.z] < top.w)
                continue;


            directions[0].x = top.x - 1; directions[0].y = top.y; directions[0].z = top.z;
            directions[1].x = top.x; directions[1].y = top.y; directions[1].z = top.z - 1;
            directions[2].x = top.x + 1; directions[2].y = top.y; directions[2].z = top.z;
            directions[3].x = top.x; directions[3].y = top.y; directions[3].z = top.z + 1;

            foreach (int3 dir in directions)
            {
                if (dir.x >= 0 && dir.x < mapSize.x && dir.y >= 0 && dir.y < mapSize.y && dir.z >= 0 && dir.z < mapSize.z)
                {
                    int dist = distance[top.x, top.y, top.z] + 1;
                    if (map[dir.x, dir.y, dir.z].type == 0 && distance[dir.x, dir.y, dir.z] > dist)
                    {
                        distance[dir.x, dir.y, dir.z] = dist;
                        prev[dir.x, dir.y, dir.z] = top.xyz;
                        int4 temp = new int4(dir.x, dir.y, dir.z, dist);
                        // 이 new int4는 어쩔 수 없이 써야함
                        int p = Heuristic(dir, dist);
                        pq.Enqueue(temp, p);
                        if (lowest_heuristic >= p)
                        {
                            lowest_heuristic = p;
                            nearest_goal = dir;
                        }
                    }
                }
            }
        }
        //Debug.Log("가장 가까운 노드" + nearest_goal);


        int3 back = nearest_goal;
        path_size = 0;
        while(back.x != -1 && back.y != -1 && back.z != -1)
        {
            ++path_size;
            path.Add(back);
            back = prev[back.x, back.y, back.z];
        }
        path.Reverse();

        currentIndex = 0;
        previous.x = -1; previous.y = -1; previous.z = -1;

        //string s = "";
        //s += transform.name;
        //foreach (int3 p in path)
        //{
        //    s += p + " -> ";
        //}
        //Debug.Log(s);
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

    public void RemoveThisObjectOnMap()
    {
        if (previous.x >= 0 && previous.y >= 0 && previous.z >= 0)
            map[previous.x, previous.y, previous.z].type = 0;
        map[path[currentIndex].x, path[currentIndex].y, path[currentIndex].z].type = 0;
    }
}
