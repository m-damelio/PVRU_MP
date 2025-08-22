using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;


//Creates a new mesh with barycentric coordinates such that a shader can create a wireframe look
public class BarycentricCoordinatesGenerator
{
    // Adds a menu item to the GameObject context menu and the Assets menu.
    [MenuItem("GameObject/Generate Barycentric Mesh", false, 40)]
    [MenuItem("Assets/Generate Barycentric Mesh", false, 40)]
    private static void GenerateBarycentricMesh()
    {
        GameObject selectedGo = Selection.activeGameObject;
        if (selectedGo == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a GameObject in the Scene or a Prefab in the Project window.", "OK");
            return;
        }

        SkinnedMeshRenderer skinnedRenderer = selectedGo.GetComponent<SkinnedMeshRenderer>();
        MeshFilter meshFilter = selectedGo.GetComponent<MeshFilter>();
        Mesh sourceMesh = null;

        if (skinnedRenderer != null) sourceMesh = skinnedRenderer.sharedMesh;
        else if (meshFilter != null) sourceMesh = meshFilter.sharedMesh;

        if (sourceMesh == null)
        {
            EditorUtility.DisplayDialog("Error", "No MeshFilter or SkinnedMeshRenderer found on the selected GameObject.", "OK");
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath(sourceMesh);
        string directory = Path.GetDirectoryName(sourcePath);
        string newMeshName = sourceMesh.name + "_Barycentric";
        string newPath = Path.Combine(directory, newMeshName + ".asset");

        // Create a new mesh to store the modified data.
        Mesh newMesh = new Mesh();
        newMesh.name = newMeshName;

        // --- UNWELD MESH LOGIC ---
        int[] sourceTriangles = sourceMesh.triangles;
        Vector3[] sourceVertices = sourceMesh.vertices;
        Vector3[] sourceNormals = sourceMesh.normals;
        Vector2[] sourceUVs = sourceMesh.uv;
        BoneWeight[] sourceBoneWeights = sourceMesh.boneWeights;

        int triangleCount = sourceTriangles.Length;
        int newVertexCount = triangleCount;

        var newVertices = new List<Vector3>(newVertexCount);
        var newNormals = new List<Vector3>(newVertexCount);
        var newUVs = new List<Vector2>(newVertexCount);
        var newColors = new List<Color>(newVertexCount);
        var newTriangles = new int[newVertexCount];
        var newBoneWeights = new List<BoneWeight>(newVertexCount);
        var originalVertexMap = new List<int>(newVertexCount); // Map to link new vertices to original ones

        bool hasNormals = sourceNormals != null && sourceNormals.Length > 0;
        bool hasUVs = sourceUVs != null && sourceUVs.Length > 0;
        bool hasBoneWeights = sourceBoneWeights != null && sourceBoneWeights.Length > 0;

        Color[] barycentricValues = { new Color(1, 0, 0, 0), new Color(0, 1, 0, 0), new Color(0, 0, 1, 0) };

        for (int i = 0; i < triangleCount; i++)
        {
            int originalVertexIndex = sourceTriangles[i];
            originalVertexMap.Add(originalVertexIndex); // Store the original index

            newVertices.Add(sourceVertices[originalVertexIndex]);
            if (hasNormals) newNormals.Add(sourceNormals[originalVertexIndex]);
            if (hasUVs) newUVs.Add(sourceUVs[originalVertexIndex]);
            if (hasBoneWeights) newBoneWeights.Add(sourceBoneWeights[originalVertexIndex]);

            newColors.Add(barycentricValues[i % 3]);
            newTriangles[i] = i;
        }

        newMesh.SetVertices(newVertices);
        newMesh.SetNormals(newNormals);
        newMesh.SetUVs(0, newUVs);
        newMesh.SetColors(newColors);
        newMesh.SetTriangles(newTriangles, 0);

        if (hasBoneWeights)
        {
            newMesh.boneWeights = newBoneWeights.ToArray();
            newMesh.bindposes = sourceMesh.bindposes;
        }
        
        // --- HANDLE BLEND SHAPES ---
        if (sourceMesh.blendShapeCount > 0)
        {
            for (int shapeIndex = 0; shapeIndex < sourceMesh.blendShapeCount; shapeIndex++)
            {
                string shapeName = sourceMesh.GetBlendShapeName(shapeIndex);
                int frameCount = sourceMesh.GetBlendShapeFrameCount(shapeIndex);

                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    // Get the original delta values for the blend shape frame
                    Vector3[] deltaVertices = new Vector3[sourceMesh.vertexCount];
                    Vector3[] deltaNormals = new Vector3[sourceMesh.vertexCount];
                    Vector3[] deltaTangents = new Vector3[sourceMesh.vertexCount];
                    sourceMesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);

                    // Create new delta arrays sized for the unwelded mesh
                    Vector3[] newDeltaVertices = new Vector3[newVertexCount];
                    Vector3[] newDeltaNormals = new Vector3[newVertexCount];
                    Vector3[] newDeltaTangents = new Vector3[newVertexCount];

                    // Remap the deltas from the original vertices to the new vertices
                    for (int i = 0; i < newVertexCount; i++)
                    {
                        int originalIndex = originalVertexMap[i];
                        newDeltaVertices[i] = deltaVertices[originalIndex];
                        newDeltaNormals[i] = deltaNormals[originalIndex];
                        newDeltaTangents[i] = deltaTangents[originalIndex];
                    }

                    float frameWeight = sourceMesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);
                    newMesh.AddBlendShapeFrame(shapeName, frameWeight, newDeltaVertices, newDeltaNormals, newDeltaTangents);
                }
            }
        }

        newMesh.RecalculateBounds();

        AssetDatabase.CreateAsset(newMesh, AssetDatabase.GenerateUniqueAssetPath(newPath));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (skinnedRenderer != null)
        {
            Undo.RecordObject(skinnedRenderer, "Assign Barycentric Mesh");
            skinnedRenderer.sharedMesh = newMesh;
        }
        else if (meshFilter != null)
        {
            Undo.RecordObject(meshFilter, "Assign Barycentric Mesh");
            meshFilter.sharedMesh = newMesh;
        }

        EditorUtility.DisplayDialog("Success", "Barycentric mesh created and assigned at:\n" + newPath, "OK");
        Selection.activeObject = newMesh;
    }

    [MenuItem("GameObject/Generate Barycentric Mesh", true)]
    [MenuItem("Assets/Generate Barycentric Mesh", true)]
    private static bool ValidateGenerateBarycentricMesh()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null) return false;
        return go.GetComponent<MeshFilter>() != null || go.GetComponent<SkinnedMeshRenderer>() != null;
    }
}
