using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Silksprite.AvatarTinker.CostumeConverter
{
    [Serializable]
    public class CostumeConverter
    {
        [SerializeField] public Animator avatarRoot;
        [SerializeField] public SkinnedMeshRenderer costumeRenderer;
        [SerializeField] public Transform humanoidHipsBone;
        [SerializeField] public List<CostumeBoneMapping> costumeBoneMappings;

        public void MatchBones()
        {
            humanoidHipsBone = avatarRoot.GetBoneTransform(HumanBodyBones.Hips);
            var costumeBones = costumeRenderer.bones;
            var otherCostumeBones = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>().Where(renderer => renderer != costumeRenderer).SelectMany(renderer => renderer.bones);
            var humanoidBones = CollectHumanoidBones(avatarRoot);
            costumeBoneMappings = costumeBones.Select(bone =>
            {
                if (bone == null)
                {
                    return new CostumeBoneMapping
                    {
                        bone = null,
                        baseBone = null,
                        relation = CostumeRelation.NullBone
                    };
                }
                if (humanoidBones.Contains(bone))
                {
                    return new CostumeBoneMapping
                    {
                        bone = bone,
                        baseBone = bone,
                        relation = CostumeRelation.Humanoid
                    };
                }
                var directParent = bone.parent;
                if (costumeBones.Contains(directParent))
                {
                    if (otherCostumeBones.Contains(bone))
                    {
                        return new CostumeBoneMapping
                        {
                            bone = bone,
                            baseBone = bone,
                            relation = CostumeRelation.Shared 
                        };
                    }
                    else
                    {
                        return new CostumeBoneMapping
                        {
                            bone = bone,
                            baseBone = directParent,
                            relation = CostumeRelation.IndependentChild 
                        };
                    }
                }
                if (humanoidHipsBone.GetComponentsInChildren<Transform>().Contains(directParent))
                {
                    const float epsilon = 0.0001f;
                    var boneHasPosition = bone.localPosition.magnitude > epsilon || bone.localRotation != Quaternion.identity || (bone.localScale - Vector3.one).magnitude > epsilon;
                    var boneIsReactive = bone.GetComponents<Component>().Select(c => c.GetType().Name)
                        .Any(n =>
                        {
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
                        }); 
                    var boneIsIdentity = !(boneIsReactive || boneHasPosition);
                    return new CostumeBoneMapping
                    {
                        bone = bone,
                        baseBone = directParent,
                        relation = boneIsIdentity ? CostumeRelation.Redundant : CostumeRelation.Positioning
                    };
                }
                return new CostumeBoneMapping
                {
                    bone = bone,
                    baseBone = humanoidHipsBone,
                    relation = CostumeRelation.Unrelated
                };
            }).ToList();
        }

        public void SelectBoneMappings(bool value)
        {
            foreach (var mapping in costumeBoneMappings) mapping.selected = value;
        }

        public void MergeRedundantBones()
        {
            if (costumeBoneMappings.Any(mapping => mapping.IsPrefab)) throw new InvalidOperationException("Unpack Prefab please");

            foreach (var mapping in costumeBoneMappings.Where(mapping => mapping.selected && mapping.relation == CostumeRelation.Redundant))
            {
                foreach (var renderer in avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var bones = renderer.bones;
                    var index = Array.IndexOf(bones, mapping.bone);
                    if (index > -1)
                    {
                        bones[index] = mapping.baseBone;
                        renderer.bones = bones;
                    }
                    if (renderer.rootBone == mapping.bone) renderer.rootBone = mapping.baseBone;
                }

                while (mapping.bone.childCount > 0)
                {
                    mapping.bone.GetChild(0).SetParent(mapping.baseBone, true);
                }
                foreach (var m in costumeBoneMappings)
                {
                    if (m.baseBone == mapping.bone) m.baseBone = mapping.baseBone;
                }
                Object.DestroyImmediate(mapping.bone.gameObject);
            }
            
            MatchBones();
        }

        static IEnumerable<Transform> CollectHumanoidBones(Animator animator) => Enum.GetValues(typeof(HumanBodyBones)).OfType<HumanBodyBones>().Where(hbb => hbb != HumanBodyBones.LastBone).Select(animator.GetBoneTransform).ToArray();

        [Serializable]
        public class CostumeBoneMapping
        {
            public bool selected = true;
            public Transform bone;
            public Transform baseBone;
            public CostumeRelation relation;
            public bool IsPrefab => PrefabUtility.IsPartOfAnyPrefab(bone) && PrefabUtility.IsPartOfAnyPrefab(baseBone);
        }

        public enum CostumeRelation
        {
            NullBone,
            Unrelated,
            Humanoid,
            Shared,
            Redundant,
            Positioning,
            IndependentChild
        }
    }
}