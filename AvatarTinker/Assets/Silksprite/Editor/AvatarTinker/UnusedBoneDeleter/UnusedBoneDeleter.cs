using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Silksprite.AvatarTinker.UnusedBoneDeleter
{
    [Serializable]
    public class UnusedBoneDeleter
    {
        [SerializeField] public Animator avatarRoot;
        [SerializeField] public Transform armatureRoot;
        [SerializeField] public List<Transform> unusedBones;

        public void SelectUnusedBones()
        {
            if (armatureRoot == null) armatureRoot = avatarRoot.GetBoneTransform(HumanBodyBones.Hips); 

            unusedBones = new List<Transform>();
            armatureRoot.GetComponentsInChildren(true, unusedBones);

            MarkBonesAsUsed();
        }

        public void SelectUnusedSkinnedMeshBones()
        {
            unusedBones = CollectSkinnedMeshRendererBones(avatarRoot).ToList();

            MarkBonesAsUsed();
        }

        void MarkBonesAsUsed()
        {
            void MarkBoneAsUsed(Transform transform)
            {
                while (transform != null)
                {
                    unusedBones.Remove(transform);
                    transform = transform.parent;
                }
            }

            foreach (var humanoidBone in CollectHumanoidBones(avatarRoot))
            {
                MarkBoneAsUsed(humanoidBone);
            }

            foreach (var meshRendererBone in CollectComponentBones<MeshRenderer>(avatarRoot))
            {
                MarkBoneAsUsed(meshRendererBone);
            }

            foreach (var meshRendererBone in CollectComponentSuspiciousBones(avatarRoot))
            {
                MarkBoneAsUsed(meshRendererBone);
            }

            foreach (var skinnedMeshRendererBone in CollectSkinnedMeshRendererUsedBones(avatarRoot))
            {
                MarkBoneAsUsed(skinnedMeshRendererBone);
            }
        }

        public void DeleteUnusedBones()
        {
            foreach (var unusedBone in unusedBones.ToArray())
            {
                if (unusedBone) Object.DestroyImmediate(unusedBone.gameObject);
            };
        }

        IEnumerable<Transform> CollectComponentBones<T>(Animator animator)
        where T : Component
        {
            return animator.GetComponentsInChildren<T>(true)
                .Select(meshRenderer => meshRenderer.transform)
                .Distinct();
        }

        IEnumerable<Transform> CollectComponentSuspiciousBones(Animator animator)
        {
            return animator.GetComponentsInChildren<Component>(true)
                .Where(component => component != null) // remove Missing Scripts
                .Where(component =>
                {
                    var n = component.GetType().Name;
                    // Latest AI to guess this bone is required
                    // This should mark DynamicBones, VRCPhysBones and SpringBones
                    if ( n.Contains("Bone")) return true;
                    // This should mark Bone Colliders
                    if ( n.Contains("Collider")) return true;
                    // This should mark Constraints
                    if ( n.Contains("Constraint")) return true;
                    // Some other components we don't want to touch
                    if ( n.Contains("Mesh")) return true;
                    if ( n.Contains("Animation")) return true;
                    if ( n.Contains("Particle")) return true;
                    if ( n.Contains("Animator")) return true;
                    if ( n.Contains("Trail")) return true;
                    if ( n.Contains("Cloth")) return true;
                    if ( n.Contains("Light")) return true;
                    if ( n.Contains("RigidBody")) return true;
                    if ( n.Contains("Joint")) return true;
                    if ( n.Contains("Camera")) return true;
                    if ( n.Contains("FlareLayer")) return true;
                    if ( n.Contains("GUILayer")) return true;
                    if ( n.Contains("AudioSource")) return true;
                    if ( n.Contains("IK")) return true;
                    return false;
                })
                .Select(component => component.transform)
                .Distinct();
        }

        IEnumerable<Transform> CollectSkinnedMeshRendererBones(Animator animator)
        {
            return animator.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .SelectMany(skinnedMeshRenderer => skinnedMeshRenderer.bones)
                .Distinct();
        }

        IEnumerable<Transform> CollectSkinnedMeshRendererUsedBones(Animator animator)
        {
            var usedBones = new List<Transform>();
            
            foreach (var skinnedMeshRenderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = skinnedMeshRenderer.bones;
                var boneUsage = Enumerable.Repeat(false, bones.Length).ToArray();
                foreach (var boneWeight in skinnedMeshRenderer.sharedMesh.GetAllBoneWeights())
                {
                    boneUsage[boneWeight.boneIndex] = true;
                }

                usedBones.AddRange(bones.Where((t, i) => boneUsage[i]));
            }

            return usedBones.Distinct();
        }

        static IEnumerable<Transform> CollectHumanoidBones(Animator animator) => Enum.GetValues(typeof(HumanBodyBones)).OfType<HumanBodyBones>().Where(hbb => hbb != HumanBodyBones.LastBone).Select(animator.GetBoneTransform).ToArray();
    }
}