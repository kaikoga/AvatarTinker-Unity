#if VRC_SDK_VRCSDK3

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace Silksprite.AvatarTinker.VRChat.PhysBoneCombiner
{
    [Serializable]
    public class PhysBoneCombiner
    {
        [SerializeField] public Animator avatarRoot;
        [SerializeField] public List<VRCPhysBone> allPhysBones = new List<VRCPhysBone>();
        [SerializeField] public List<PhysBoneRole> allPhysBonesRole = new List<PhysBoneRole>();
        
        [SerializeField] public int targetPhysBoneIndex;
        [SerializeField] public VRCPhysBone targetPhysBone;
        [SerializeField] public PhysBoneRole targetPhysBoneRole;
        [SerializeField] public Transform parentBone;
        [SerializeField] public List<Transform> childBones = new List<Transform>();
        [SerializeField] public List<VRCPhysBone> childPhysBones = new List<VRCPhysBone>();

        [SerializeField] PhysBoneDestination destination = PhysBoneDestination.PhysBoneRootTransform;

        public void CollectPhysBones()
        {
            allPhysBones = avatarRoot.GetComponentsInChildren<VRCPhysBone>().ToList();
            allPhysBonesRole = allPhysBones.Select(GuessPhysBoneRole).ToList();
            targetPhysBoneIndex = 0;
            targetPhysBone = null;
            parentBone = null;
            childBones = new List<Transform>();
            childPhysBones = new List<VRCPhysBone>();
        }

        public void SelectTarget(int index, VRCPhysBone target)
        {
            targetPhysBoneIndex = index;
            targetPhysBone = target;

            if (targetPhysBone == null)
            {
                parentBone = null;
                childBones = new List<Transform>();
                return;
            }

            Debug.Log(ExtractSettingString(target));
            targetPhysBoneRole = GuessPhysBoneRole(targetPhysBone);

            switch (targetPhysBoneRole)
            {
                case PhysBoneRole.Composed:
                {
                    parentBone = targetPhysBone.GetRootTransform();
                    childBones = new List<Transform>();
                    childPhysBones = new List<VRCPhysBone>();
                    foreach (Transform transform in parentBone)
                    {
                        if (targetPhysBone.ignoreTransforms.Contains(transform)) continue; 
                        childBones.Add(transform);
                    }
                    break;
                }
                case PhysBoneRole.Disassembled:
                {
                    parentBone = targetPhysBone.GetRootTransform().parent;
                    childBones = new List<Transform>();
                    childPhysBones = new List<VRCPhysBone>();
                    foreach (var childPhysBone in allPhysBones)
                    {
                        var childTransform = childPhysBone.GetRootTransform(); 
                        if (childTransform.parent != parentBone) continue;
                        if (!IsCommonSettings(targetPhysBone, childPhysBone)) continue;
                        childBones.Add(childTransform);
                        childPhysBones.Add(childPhysBone);
                    }
                    break;
                }
                default:
                    parentBone = null;
                    childBones = new List<Transform>();
                    break;
            }
        }

        static PhysBoneRole GuessPhysBoneRole(VRCPhysBone physBone)
        {
            var result = physBone.multiChildType == VRCPhysBoneBase.MultiChildType.Ignore;
            if (physBone.GetRootTransform().childCount == 1 && physBone.GetRootTransform().parent.childCount > 1)
            {
                result = false;
            }

            return result ? PhysBoneRole.Composed : PhysBoneRole.Disassembled;
        }

        public void AssembleMultiChild()
        {
            var allAffectedList = childPhysBones.SelectMany(pb => IgnoreListToAffectedList(pb, true)).ToList();
            var ignoreList = AffectedListToIgnoreList(parentBone, allAffectedList, false).ToList();
            targetPhysBone = CreatePhysBone(parentBone, parentBone, childBones.First());
            targetPhysBone.multiChildType = VRCPhysBoneBase.MultiChildType.Ignore;
            targetPhysBone.ignoreTransforms = ignoreList;
            foreach (var childPhysBone in childPhysBones.ToArray())
            {
                if (childPhysBone) Object.DestroyImmediate(childPhysBone);
            }

            CollectPhysBones();
        }

        public void DisassembleMultiChild()
        {
            var allAffectedList = IgnoreListToAffectedList(targetPhysBone, false).ToList();
            foreach (Transform child in childBones)
            {
                var childAffectedList = allAffectedList.Where(t => IsDescendant(child, t)).ToList();
                if (!childAffectedList.Any()) continue;
                var ignoreList = AffectedListToIgnoreList(child, childAffectedList, false).ToList();

                var childPhysBone = CreatePhysBone(child, parentBone, child);
                childPhysBone.multiChildType = VRCPhysBoneBase.MultiChildType.Average;
                childPhysBone.ignoreTransforms = ignoreList;
            }
            
            Object.DestroyImmediate(targetPhysBone);

            CollectPhysBones();
        }

        public void MovePhysBone()
        {
            var newPhysBone = CreatePhysBone(targetPhysBone.GetRootTransform(), parentBone, childBones.First());
            Object.DestroyImmediate(targetPhysBone);
            targetPhysBone = newPhysBone;

            CollectPhysBones();
        }

        VRCPhysBone CreatePhysBone(Transform physBoneRoot, Transform parentBone, Transform firstChildBone)
        {
            VRCPhysBone physBone;
            switch (destination)
            {
                case PhysBoneDestination.AvatarRoot:
                    physBone = avatarRoot.gameObject.AddComponent<VRCPhysBone>();
                    break;
                case PhysBoneDestination.HipParent:
                    physBone = avatarRoot.GetBoneTransform(HumanBodyBones.Hips).parent.gameObject.AddComponent<VRCPhysBone>();
                    break;
                case PhysBoneDestination.HipBone:
                    physBone = avatarRoot.GetBoneTransform(HumanBodyBones.Hips).gameObject.AddComponent<VRCPhysBone>();
                    break;
                case PhysBoneDestination.PhysBoneRootTransformParent:
                    physBone = physBoneRoot.parent.gameObject.AddComponent<VRCPhysBone>();
                    break;
                case PhysBoneDestination.PhysBoneRootTransform:
                    physBone = physBoneRoot.gameObject.AddComponent<VRCPhysBone>();
                    break;
                case PhysBoneDestination.ParentBone:
                    physBone = parentBone.gameObject.AddComponent<VRCPhysBone>();
                    break;
                case PhysBoneDestination.FirstChildBone:
                    physBone = firstChildBone.gameObject.AddComponent<VRCPhysBone>();
                    break;
                default:
                    physBone = physBoneRoot.gameObject.AddComponent<VRCPhysBone>();
                    break;
            }
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(targetPhysBone), physBone);
            physBone.rootTransform = physBoneRoot;
            return physBone;
        }

        static bool IsCommonSettings(VRCPhysBone a, VRCPhysBone b)
        {
            return ExtractSettingString(a) == ExtractSettingString(b);
        }

        static string ExtractSettingString(VRCPhysBone physBone)
        {
            var rootA = physBone.rootTransform;
            var ignoreA = physBone.ignoreTransforms;
            physBone.rootTransform = null;
            physBone.ignoreTransforms = new List<Transform>();
            var json = JsonUtility.ToJson(physBone);
            physBone.rootTransform = rootA;
            physBone.ignoreTransforms = ignoreA;
            return json;
        }

        static IEnumerable<Transform> IgnoreListToAffectedList(VRCPhysBone physBone, bool includeRoot)
        {
            return IgnoreListToAffectedList(physBone.GetRootTransform(), physBone.ignoreTransforms, includeRoot);
        }

        static IEnumerable<Transform> IgnoreListToAffectedList(Transform rootTransform, List<Transform> ignoreList, bool includeRoot = true)
        {
            var result = new List<Transform>();
            if (includeRoot)
            {
                result.Add(rootTransform);
            }
            foreach (Transform child in rootTransform)
            {
                if (ignoreList.Contains(child)) continue;
                result.AddRange(IgnoreListToAffectedList(child, ignoreList));
            }
            return result;
        }
        
        static IEnumerable<Transform> AffectedListToIgnoreList(Transform rootTransform, List<Transform> affectedList, bool includeRoot = true)
        {
            var result = new List<Transform>();
            foreach (Transform child in rootTransform)
            {
                if (affectedList.Contains(child)) continue;
                result.Add(child);
            }
            return result;
        }
        
        static bool IsDescendant(Transform parent, Transform transform)
        {
            while (transform != null)
            {
                if (parent == transform) return true;
                transform = transform.parent;
            }
            return false;
        }

        public static IEnumerable<Transform> CollectHumanoidBones(Animator animator) => Enum.GetValues(typeof(HumanBodyBones)).OfType<HumanBodyBones>().Where(hbb => hbb != HumanBodyBones.LastBone).Select(animator.GetBoneTransform).ToArray();

        public enum PhysBoneRole
        {
            Composed,
            Disassembled
        }

        public enum PhysBoneDestination
        {
            AvatarRoot,
            HipParent,
            HipBone,
            PhysBoneRootTransformParent,
            PhysBoneRootTransform,
            ParentBone,
            FirstChildBone
        }

        public static Type PhysBoneType => typeof(VRCPhysBone);
    }
}
#endif