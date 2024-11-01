using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

#region Class - MeshSimplifier
[ExecuteInEditMode]
public class MeshSimplifier : MonoBehaviour
{
    #region Fields and Settings
    [Header("Mesh Simplification Settings")]
    [Tooltip("The ratio of simplification, between 0.1 (high simplification) to 1.0 (no simplification).")]
    [Range(0.1f, 1.0f)]
    public float simplificationRatio = 0.5f;

    [Tooltip("How much to subdivide or simplify the mesh faces (1 = no subdivision).")]
    [Range(1, 4)]
    public int subdivisionLevel = 1;

    [SerializeField, Tooltip("The MeshFilter component of the target mesh.")]
    private MeshFilter targetMeshFilter;

    private Mesh originalMesh;
    private Mesh currentMesh;
    private int currentSubdivisionLevel = 1;
    private bool isSimplified = false;

    // Dictionary to cache meshes at different subdivision levels
    private Dictionary<int, Mesh> subdivisionCache = new Dictionary<int, Mesh>();
    #endregion

    #region Unity Lifecycle Methods
    private void Awake()
    {
        CacheOriginalMesh();
    }

    private void CacheOriginalMesh()
    {
        if (targetMeshFilter == null)
        {
            targetMeshFilter = GetComponent<MeshFilter>();
        }

        if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
        {
            originalMesh = targetMeshFilter.sharedMesh;
        }
    }

    private void OnValidate()
    {
        if (targetMeshFilter != null && originalMesh != null)
        {
            AdjustSubdivisionLevel();
        }
    }
    #endregion

    #region Mesh Simplification and Subdivision Management
    private void AdjustSubdivisionLevel()
    {
        if (subdivisionLevel > currentSubdivisionLevel)
        {
            // Increase subdivision
            for (int i = currentSubdivisionLevel + 1; i <= subdivisionLevel; i++)
            {
                ApplySubdivision(i);
            }
        }
        else if (subdivisionLevel < currentSubdivisionLevel)
        {
            // Decrease subdivision (restore from cache or reset)
            for (int i = currentSubdivisionLevel - 1; i >= subdivisionLevel; i--)
            {
                RestoreSubdivision(i);
            }
        }

        currentSubdivisionLevel = subdivisionLevel;
    }

    private void ApplySubdivision(int targetLevel)
    {
        if (!subdivisionCache.ContainsKey(targetLevel))
        {
            // If we don't have this level cached, create it
            if (currentMesh == null)
            {
                currentMesh = Instantiate(originalMesh);
            }

            Mesh meshToSubdivide = Instantiate(currentMesh);
            SubdivideMesh(meshToSubdivide);

            subdivisionCache[targetLevel] = meshToSubdivide;
        }

        // Apply the cached mesh for this subdivision level
        currentMesh = subdivisionCache[targetLevel];
        targetMeshFilter.sharedMesh = currentMesh;

        Debug.Log($"Subdivision increased to level {targetLevel}");
    }

    private void RestoreSubdivision(int targetLevel)
    {
        if (targetLevel == 1)
        {
            // Reset to the original mesh if we go back to level 1
            targetMeshFilter.sharedMesh = originalMesh;
        }
        else if (subdivisionCache.ContainsKey(targetLevel))
        {
            currentMesh = subdivisionCache[targetLevel];
            targetMeshFilter.sharedMesh = currentMesh;
        }

        Debug.Log($"Subdivision decreased to level {targetLevel}");
    }
    #endregion

    #region Mesh Simplification Methods
    public void SimplifyMesh()
    {
        if (originalMesh == null || targetMeshFilter == null)
        {
            Debug.LogWarning("No valid mesh found to simplify.");
            return;
        }

        Undo.RecordObject(targetMeshFilter, "Simplify Mesh");

        currentMesh = Instantiate(originalMesh);
        SimplifyMeshData(currentMesh, simplificationRatio, subdivisionLevel);

        targetMeshFilter.sharedMesh = currentMesh;
        isSimplified = true;

        Debug.Log("Mesh has been simplified.");
    }

    public void RestoreOriginalMesh()
    {
        if (!isSimplified || originalMesh == null || targetMeshFilter == null)
        {
            Debug.LogWarning("No simplified mesh to restore.");
            return;
        }

        Undo.RecordObject(targetMeshFilter, "Restore Original Mesh");
        targetMeshFilter.sharedMesh = originalMesh;
        isSimplified = false;

        Debug.Log("Mesh restored to original.");
    }

    private void SimplifyMeshData(Mesh mesh, float simplificationFactor, int subdivisionLevel)
    {
        if (simplificationFactor < 1.0f)
        {
            ReduceMeshTriangles(mesh, simplificationFactor);
        }

        for (int i = 1; i < subdivisionLevel; i++)
        {
            SubdivideMesh(mesh);
        }

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Debug.Log($"Mesh simplified to {mesh.triangles.Length / 3} faces and subdivided {subdivisionLevel} times.");
    }
    #endregion

    #region Triangle Reduction Method
    private void ReduceMeshTriangles(Mesh mesh, float simplificationFactor)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        int targetTriangleCount = Mathf.FloorToInt(triangles.Length * simplificationFactor / 3) * 3;
        int[] newTriangles = new int[targetTriangleCount];

        for (int i = 0; i < targetTriangleCount; i++)
        {
            newTriangles[i] = triangles[i];
        }

        mesh.triangles = newTriangles;
    }
    #endregion

    #region Mesh Subdivision Methods
    private void SubdivideMesh(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        List<Vector3> newVertices = new List<Vector3>(vertices);
        List<int> newTriangles = new List<int>();
        Dictionary<long, int> midpointCache = new Dictionary<long, int>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];

            int a = GetMidpointIndex(newVertices, midpointCache, v0, v1);
            int b = GetMidpointIndex(newVertices, midpointCache, v1, v2);
            int c = GetMidpointIndex(newVertices, midpointCache, v2, v0);

            newTriangles.Add(v0); newTriangles.Add(a); newTriangles.Add(c);
            newTriangles.Add(v1); newTriangles.Add(b); newTriangles.Add(a);
            newTriangles.Add(v2); newTriangles.Add(c); newTriangles.Add(b);
            newTriangles.Add(a); newTriangles.Add(b); newTriangles.Add(c);
        }

        mesh.vertices = newVertices.ToArray();
        mesh.triangles = newTriangles.ToArray();
    }

    private int GetMidpointIndex(List<Vector3> vertices, Dictionary<long, int> midpointCache, int v1, int v2)
    {
        long smallerIndex = Mathf.Min(v1, v2);
        long largerIndex = Mathf.Max(v1, v2);
        long key = (smallerIndex << 32) + largerIndex;

        if (midpointCache.TryGetValue(key, out int midpointIndex))
        {
            return midpointIndex;
        }

        Vector3 midpoint = (vertices[v1] + vertices[v2]) * 0.5f;
        midpointIndex = vertices.Count;
        vertices.Add(midpoint);

        midpointCache[key] = midpointIndex;

        return midpointIndex;
    }
    #endregion

    #region Utility Methods
    private void Reset()
    {
        CacheOriginalMesh();
    }
    #endregion
}

#if UNITY_EDITOR
#region Custom Editor for MeshSimplifier
[CustomEditor(typeof(MeshSimplifier))]
public class MeshSimplifierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MeshSimplifier meshSimplifier = (MeshSimplifier)target;

        if (GUILayout.Button("Simplify Mesh"))
        {
            meshSimplifier.SimplifyMesh();
        }

        if (GUILayout.Button("Restore Original Mesh"))
        {
            meshSimplifier.RestoreOriginalMesh();
        }
    }
}
#endregion
#endif
#endregion
