using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Silksprite.AvatarTinker.SpiderBracelet
{
    [Serializable]
    public class SpiderBracelet
    {
        [SerializeField] public SkinnedMeshRenderer costumeRenderer;
        [SerializeField] public Vector3 origin;
        [SerializeField] public string blendShapeName;
        [SerializeField] public Mesh generatedMesh;

        public void Execute()
        {
            var sourceMesh = costumeRenderer.sharedMesh;

            var mesh = new Mesh();
            mesh.SetVertices(sourceMesh.vertices);
            mesh.boneWeights = sourceMesh.boneWeights;
            mesh.SetNormals(sourceMesh.normals);
            mesh.SetTangents(sourceMesh.tangents);
            mesh.SetColors(sourceMesh.colors32);
            mesh.SetUVs(0, sourceMesh.uv);

            mesh.subMeshCount = sourceMesh.subMeshCount;
            for (var subMeshIndex = 0; subMeshIndex < sourceMesh.subMeshCount; subMeshIndex++)
            {
                mesh.SetTriangles(sourceMesh.GetTriangles(subMeshIndex), subMeshIndex);
            }

            mesh.bindposes = sourceMesh.bindposes;
                
            var deltaVertices = new Vector3[mesh.vertexCount];
            var deltaNormals = new Vector3[mesh.vertexCount];
            var deltaTangents = new Vector3[mesh.vertexCount];
            var capturedVertices = mesh.vertices.Select(v => origin - v).ToArray();
            for (var blendShapeIndex = 0; blendShapeIndex < sourceMesh.blendShapeCount; blendShapeIndex++)
            {
                var blendShapeName = sourceMesh.GetBlendShapeName(blendShapeIndex);
                var frameWeight = sourceMesh.GetBlendShapeFrameWeight(blendShapeIndex, 0);

                sourceMesh.GetBlendShapeFrameVertices(blendShapeIndex, 0, deltaVertices, deltaNormals, deltaTangents);
                mesh.AddBlendShapeFrame(blendShapeName, frameWeight, deltaVertices, deltaNormals, deltaTangents);
                var capturedWeight = costumeRenderer.GetBlendShapeWeight(blendShapeIndex);
                for (var i = 0; i < mesh.vertexCount; i++)
                {
                    capturedVertices[i] -= deltaVertices[i] * capturedWeight;
                }
            }

            mesh.AddBlendShapeFrame(blendShapeName, 1f, capturedVertices, new Vector3[mesh.vertexCount], new Vector3[mesh.vertexCount]);

            generatedMesh = mesh;
            costumeRenderer.sharedMesh = mesh;
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath($"Assets/{sourceMesh.name}_with_{blendShapeName}.asset"));
        }
    }
}