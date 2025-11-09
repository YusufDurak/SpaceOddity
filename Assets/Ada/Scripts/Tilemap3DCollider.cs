using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

[RequireComponent(typeof(Tilemap))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(MeshRenderer))]
public class Tilemap3DMeshCollider : MonoBehaviour
{
    [Header("Collider Settings")]
    public float tileDepth = 1f; // Thickness in Z direction

    void Start()
    {
        BuildMeshCollider();
    }

    void BuildMeshCollider()
    {
        Tilemap tilemap = GetComponent<Tilemap>();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        int v = 0;

        foreach (var pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(pos))
            {
                Vector3 tileCenter = tilemap.CellToLocal(pos) + new Vector3(0.5f, 0.5f, 0);

                // Front face (towards +Z)
                vertices.Add(tileCenter + new Vector3(-0.5f, -0.5f, tileDepth / 2));
                vertices.Add(tileCenter + new Vector3(0.5f, -0.5f, tileDepth / 2));
                vertices.Add(tileCenter + new Vector3(0.5f, 0.5f, tileDepth / 2));
                vertices.Add(tileCenter + new Vector3(-0.5f, 0.5f, tileDepth / 2));

                // Back face (towards -Z)
                vertices.Add(tileCenter + new Vector3(-0.5f, -0.5f, -tileDepth / 2));
                vertices.Add(tileCenter + new Vector3(0.5f, -0.5f, -tileDepth / 2));
                vertices.Add(tileCenter + new Vector3(0.5f, 0.5f, -tileDepth / 2));
                vertices.Add(tileCenter + new Vector3(-0.5f, 0.5f, -tileDepth / 2));

                // Front
                triangles.AddRange(new int[] { v, v + 1, v + 2, v, v + 2, v + 3 });
                // Back
                triangles.AddRange(new int[] { v + 4, v + 6, v + 5, v + 4, v + 7, v + 6 });
                // Left
                triangles.AddRange(new int[] { v + 4, v, v + 3, v + 4, v + 3, v + 7 });
                // Right
                triangles.AddRange(new int[] { v + 1, v + 5, v + 6, v + 1, v + 6, v + 2 });
                // Top
                triangles.AddRange(new int[] { v + 3, v + 2, v + 6, v + 3, v + 6, v + 7 });
                // Bottom
                triangles.AddRange(new int[] { v + 4, v + 5, v + 1, v + 4, v + 1, v });

                v += 8;
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        // Assign mesh to MeshFilter & MeshCollider
        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
    }
}
