using UnityEngine;
using System.Collections.Generic;

using Unity.IO.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using Unity.VisualScripting;

public class FindRoute : MonoBehaviour
{
    public GameObject mapParent;
    private MapJson map;
    public int2 goalCoordinate;
    List<int2> path;
    private int currentIndex = 0;
    public float speed = 2f;
    public float rotationSpeed = 5f;
    TilemapGenerator gen;
    private int2 previous;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(mapParent == null)
        {
            Debug.LogError("mapParent is null!");
        }
        gen = mapParent.GetComponent<TilemapGenerator>();
        if (gen == null)
        {
            Debug.LogError("TilemapGenerator component not found!");
        }
        map = gen.mapRef;
        if (map == null)
        {
            Debug.LogError("mapRef is null!");
        }
        path = Astar();
    }

    // Update is called once per frame
    void Update()
    {
        if (path != null)
        {
            MoveOnTile();
        }
        else
        {
            path = Astar();
        }
    }

    void MoveOnTile()
    {
        Vector3 targetPos = new Vector3(path[currentIndex].x, transform.position.y, path[currentIndex].y);

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
            if (previous.y >= 0 && previous.x >= 0)
                map.data[previous.y * map.cols + previous.x] = 0; //도착해서 이전 위치 0으로 초기화


            if (currentIndex + 1 == path.Count)// 이후로 path가 없으면 (최종 목적지였다면)
            {
                //Debug.Log("finish");
                int2 rand = new int2(UnityEngine.Random.Range(0, map.cols), UnityEngine.Random.Range(0, map.rows));
                while (map.data[rand.y * map.cols + rand.x] != 0)
                {
                    rand.x = UnityEngine.Random.Range(0, map.cols);
                    rand.y = UnityEngine.Random.Range(0, map.rows);
                }
                goalCoordinate = rand;
                path = null;
            }
            else//다음 목적지로
            {
                int2 next = path[currentIndex + 1];
                if (map.data[next.y * map.cols + next.x] == 0)
                    // 다음 목적지가 이동 가능한 상태면
                {
                    previous = path[currentIndex];
                    ++currentIndex;
                    map.data[path[currentIndex].y * map.cols + path[currentIndex].x] = 2;
                }
                else
                {   // 다음 목적지로 이동이 불가능한 상태면
                    Debug.Log(transform.name + "가 목적지로 갈 수 없습니다.");
                    path = null;
                }
            }
        }
    }

    

    List<int2> Astar()
    {
        int3 curr = new int3(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.z), 0);
        map.data[curr.y * map.cols + curr.x] = 2;

        int[,] distance = new int[map.rows, map.cols];
        int2[,] prev = new int2[map.rows, map.cols];
        for (int y = 0; y < map.rows; ++y)
        {
            for (int x = 0; x < map.cols; ++x)
            {
                distance[y, x] = int.MaxValue;
                prev[y, x] = new int2(-1, -1);
            }
        }

        PriorityQueue<int3> pq = new PriorityQueue<int3>();
        pq.Enqueue(curr, 0);
        distance[curr.y, curr.x] = 0;
        int lowest_heuristic = Heuristic(curr.xy,0);
        int2 nearest_goal = curr.xy;
        while (pq.Count > 0)
        {
            int3 top = pq.Dequeue();

            if (top.x == goalCoordinate.x && top.y == goalCoordinate.y) break;

            if (distance[top.y, top.x] < top.z)
                continue;

            int2[] directions = new int2[] {
                new int2(top.x-1,top.y),
                new int2(top.x,top.y-1),
                new int2(top.x+1,top.y),
                new int2(top.x,top.y+1)
            };

            foreach (int2 dir in directions)
            {
                if(dir.x >= 0 && dir.x < map.cols && dir.y >=0 && dir.y < map.rows)
                {
                    int dist = distance[top.y, top.x] + 1;
                    if (map.data[dir.y * map.cols + dir.x] == 0 && distance[dir.y,dir.x] > dist)
                    {
                        distance[dir.y, dir.x] = dist;
                        prev[dir.y, dir.x] = top.xy;
                        int3 temp = new int3(dir.x, dir.y, dist);
                        int p = Heuristic(dir, dist);
                        pq.Enqueue(temp, p);
                        if(lowest_heuristic >= p)
                        {
                            lowest_heuristic = p;
                            nearest_goal = dir;
                        }
                    }
                }
            }
        }
        //Debug.Log("가장 가까운 노드" + nearest_goal);

        List<int2> path = new List<int2>();

        int2 back = nearest_goal;
        while(back.x != -1 && back.y != -1)
        {
            path.Add(back);
            back = prev[back.y, back.x];
        }
        path.Reverse();

        currentIndex = 0;
        previous = new int2(-1, -1);

        //string s = "";
        //s += transform.name;
        //foreach (int2 p in path)
        //{
        //    s += p + " -> ";
        //}
        //Debug.Log(s);

        return path;
    }

    int Heuristic(int2 next, int dist)
    {
        int x = math.abs(goalCoordinate.x - next.x);
        int y = math.abs(goalCoordinate.y - next.y);
        int h = x*x + y*y;
        int f = dist + h;
        return f;
    }
}
