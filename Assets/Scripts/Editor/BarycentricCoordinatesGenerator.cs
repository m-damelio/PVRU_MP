using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

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

        // Determine if we are working with a SkinnedMeshRenderer or a regular MeshFilter.
        SkinnedMeshRenderer skinnedRenderer = selectedGo.GetComponent<SkinnedMeshRenderer>();
        MeshFilter meshFilter = selectedGo.GetComponent<MeshFilter>();
        Mesh sourceMesh = null;

        if (skinnedRenderer != null)
        {
            sourceMesh = skinnedRenderer.sharedMesh;
        }
        else if (meshFilter != null)
        {
            sourceMesh = meshFilter.sharedMesh;
        }

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

        // Copy all essential data from the source mesh.
        newMesh.vertices = sourceMesh.vertices;
        newMesh.normals = sourceMesh.normals;
        newMesh.uv = sourceMesh.uv;
        newMesh.uv2 = sourceMesh.uv2;
        newMesh.uv3 = sourceMesh.uv3;
        newMesh.uv4 = sourceMesh.uv4;
        newMesh.tangents = sourceMesh.tangents;
        newMesh.subMeshCount = sourceMesh.subMeshCount;
        for(int i = 0; i < sourceMesh.subMeshCount; i++)
        {
            newMesh.SetTriangles(sourceMesh.GetTriangles(i), i);
        }

        // Copy bone weights and bind poses if they exist.
        if (skinnedRenderer != null)
        {
            newMesh.bindposes = sourceMesh.bindposes;
            newMesh.boneWeights = sourceMesh.boneWeights;
        }

        //copy blend shapes for facial animation, etc
        if (sourceMesh.blendShapeCount > 0)
        {
            for (int shapeIndex = 0; shapeIndex < sourceMesh.blendShapeCount; shapeIndex++)
            {
                string shapeName = sourceMesh.GetBlendShapeName(shapeIndex);
                int frameCount = sourceMesh.GetBlendShapeFrameCount(shapeIndex);
                
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    float frameWeight = sourceMesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);
                    
                    // The delta arrays must be the same size as the source mesh's vertex count.
                    Vector3[] deltaVertices = new Vector3[sourceMesh.vertexCount];
                    Vector3[] deltaNormals = new Vector3[sourceMesh.vertexCount];
                    Vector3[] deltaTangents = new Vector3[sourceMesh.vertexCount];
                    
                    sourceMesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                    
                    newMesh.AddBlendShapeFrame(shapeName, frameWeight, deltaVertices, deltaNormals, deltaTangents);
                }
            }
        }

        // Get triangles and vertices from the source mesh.
        int[] triangles = sourceMesh.triangles;
        Vector3[] vertices = sourceMesh.vertices;
        int vertexCount = vertices.Length;
        int triangleCount = triangles.Length;

        // Create an array for the new vertex colors.
        Color[] barycentricColors = new Color[vertexCount];
        bool[] vertexProcessed = new bool[vertexCount]; // To avoid overwriting colors on shared vertices

        // Define the barycentric coordinates to be assigned to each vertex of a triangle.
        Color[] barycentricValues = new Color[] {
            new Color(1, 0, 0, 0),
            new Color(0, 1, 0, 0),
            new Color(0, 0, 1, 0)
        };
        
        // Iterate through each triangle in the mesh.
        for (int i = 0; i < triangleCount; i += 3)
        {
            // Get the indices of the three vertices that form the triangle.
            int vertexIndex1 = triangles[i];
            int vertexIndex2 = triangles[i + 1];
            int vertexIndex3 = triangles[i + 2];

            // Assign barycentric coordinates if the vertex hasn't been processed for another triangle.
            // This approach works for simple models.
            if (!vertexProcessed[vertexIndex1]) { barycentricColors[vertexIndex1] = barycentricValues[0]; vertexProcessed[vertexIndex1] = true; }
            if (!vertexProcessed[vertexIndex2]) { barycentricColors[vertexIndex2] = barycentricValues[1]; vertexProcessed[vertexIndex2] = true; }
            if (!vertexProcessed[vertexIndex3]) { barycentricColors[vertexIndex3] = barycentricValues[2]; vertexProcessed[vertexIndex3] = true; }
        }

        // Assign the new colors to the mesh.
        newMesh.colors = barycentricColors;

        // Recalculate bounds and normals for proper rendering.
        newMesh.RecalculateBounds();
        newMesh.RecalculateNormals();

        // Save the new mesh as an asset.
        AssetDatabase.CreateAsset(newMesh, AssetDatabase.GenerateUniqueAssetPath(newPath));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Assign the new mesh back to the renderer component.
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
        
        // Select the newly created mesh in the Project window.
        Selection.activeObject = newMesh;
    }

    // Validation function to enable the menu item only if a valid GameObject is selected.
    [MenuItem("GameObject/Generate Barycentric Mesh", true)]
    [MenuItem("Assets/Generate Barycentric Mesh", true)]
    private static bool ValidateGenerateBarycentricMesh()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null) return false;
        return go.GetComponent<MeshFilter>() != null || go.GetComponent<SkinnedMeshRenderer>() != null;
    }
}
