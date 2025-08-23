using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class Tilemap : MonoBehaviour
{
    public struct TileData
    {
        public Vector3 position; // 위치
        public int type;         // 타일 종류 (0=grass, 1=water 등)
        public bool walkable;    // 이동 가능 여부
    }

    public Mesh quadMesh;
    public Material instancedMaterial;
    private Matrix4x4[] matrices;
    public int width, height;
    private TileData[,] tiles;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        int i = 0;
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                var pos = tiles[x, z].position;
                pos.x += transform.position.x + 0.5f;
                pos.z += transform.position.z + 0.5f;
                matrices[i] = Matrix4x4.TRS(pos, Quaternion.Euler(90f,0,0), Vector3.one);
                i++;
            }
        }

        // 1023개씩 잘라서 그리기
        for (int start = 0; start < matrices.Length; start += 1023)
        {
            int count = Mathf.Min(1023, matrices.Length - start);
            Graphics.DrawMeshInstanced(quadMesh, 0, instancedMaterial,
                                       new List<Matrix4x4>(matrices).GetRange(start, count).ToArray());
        }
    }

    private void OnValidate()
    {
        GenerateTiles();
    }
    void GenerateTiles()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        tiles = new TileData[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                tiles[x, z] = new TileData
                {
                    position = new Vector3(x, 0, z), // x,z 좌표에 배치
                    type = 0,
                    walkable = true
                };
            }
        }
        matrices = new Matrix4x4[width * height];
    }
}
