using UnityEngine;
using System.Collections.Generic;

using Unity.IO.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;

public class FindRoute : MonoBehaviour
{
    public GameObject mapParent;
    private MapJson map;
    public int2 goalCoordinate;
    List<int2> path;
    private int currentIndex = 0;
    public float speed = 2f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        TilemapGenerator gen = mapParent.GetComponent<TilemapGenerator>();
        map = gen.getMapData();
        path = Astar();
        //foreach(int2 pos in path)
        //{
        //    Debug.Log($"({pos.x}, {pos.y})");
        //}
    }

    // Update is called once per frame
    void Update()
    {
        if(path != null)
        {
            Vector3 targetPos = new Vector3(path[currentIndex].x, transform.position.y, -path[currentIndex].y);
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
            if(Vector3.Distance(transform.position, targetPos) < 0.01f)
            {
                ++currentIndex;
                if(currentIndex >= path.Count)
                {
                    currentIndex = 0;
                    Debug.Log("finish");
                    path = null;
                }
            }
        }
        else
        {
            //path = Astar();
        }
    }
    
    List<int2> Astar()
    {
        int2 curr = new int2(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(-transform.position.z));
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
        PriorityQueue<int2> pq = new PriorityQueue<int2>();
        pq.Enqueue(curr, 0);
        distance[curr.y, curr.x] = 0;
        while (pq.Count > 0)
        {
            int2 top = pq.Dequeue();

            if (top.Equals(goalCoordinate))
                break;

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
                    if (map.data[dir.y * map.rows + dir.x] == 0 &&distance[dir.y,dir.x] > dist)
                    {
                        distance[dir.y, dir.x] = dist;
                        prev[dir.y, dir.x] = top;
                        pq.Enqueue(dir, Heuristic(dir, dist));
                    }
                }
            }
        }
        List<int2> path = new List<int2>();
        int2 back = goalCoordinate;
        while(back.x != -1 && back.y != -1)
        {
            path.Add(back);
            back = prev[back.y, back.x];
        }
        path.Reverse();
        currentIndex = 0;
        return path;
    }

    int Heuristic(int2 next, int dist)
    {
        int h = math.abs(goalCoordinate.x - next.x) + math.abs(goalCoordinate.y - next.y);
        int f = dist + h;
        return f;
    }
}
