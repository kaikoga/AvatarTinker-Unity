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
        [SerializeField] public List<PhysBoneInfo> allPhysBoneInfos = new List<PhysBoneInfo>();
        
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
            allPhysBoneInfos = allPhysBones.Select(pb => GuessTarget(pb, allPhysBones)).ToList();
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

            var info = allPhysBoneInfos[index];
            targetPhysBoneRole = info.targetPhysBoneRole;
            parentBone = info.parentBone;
            childBones = info.childBones;
            childPhysBones = info.childPhysBones;
        }

        static PhysBoneInfo GuessTarget(VRCPhysBone target, List<VRCPhysBone> allPhysBones)
        {
            var info = new PhysBoneInfo
            {
                targetPhysBone = target,
                parentBone = null,
                childBones = new List<Transform>(),
                childPhysBones = new List<VRCPhysBone>()
            };

            if (target == null)
            {
                return info;
            }

            Debug.Log(ExtractSettingString(target));
            info.targetPhysBoneRole = GuessPhysBoneRole(info.targetPhysBone);

            switch (info.targetPhysBoneRole)
            {
                case PhysBoneRole.Composed:
                    info.parentBone = info.targetPhysBone.GetRootTransform();
                    foreach (Transform transform in info.parentBone)
                    {
                        if (info.targetPhysBone.ignoreTransforms.Contains(transform)) continue;
                        info.childBones.Add(transform);
                    }
                    break;
                case PhysBoneRole.Disassembled:
                    info.parentBone = info.targetPhysBone.GetRootTransform().parent;
                    foreach (var childPhysBone in allPhysBones)
                    {
                        var childTransform = childPhysBone.GetRootTransform();
                        if (childTransform.parent != info.parentBone) continue;
                        if (!IsCommonSettings(info.targetPhysBone, childPhysBone)) continue;
                        info.childBones.Add(childTransform);
                        info.childPhysBones.Add(childPhysBone);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return info;
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
            if (childBones.Count < 2)
            {
                Debug.LogError("Cannot combine single bone");
                return;
            }

            var allAffectedList = childPhysBones.SelectMany(pb => IgnoreListToAffectedList(pb, true)).ToList();
            var ignoreList = AffectedListToIgnoreList(parentBone, allAffectedList, false).ToList();
            IntegrateDummyParent();
            targetPhysBone = CreatePhysBone(parentBone, parentBone, childBones.First());
            targetPhysBone.multiChildType = VRCPhysBoneBase.MultiChildType.Ignore;
            targetPhysBone.ignoreTransforms = ignoreList;
            foreach (var childPhysBone in childPhysBones.ToArray())
            {
                if (childPhysBone) Object.DestroyImmediate(childPhysBone);
            }

            CollectPhysBones();

            void IntegrateDummyParent()
            {
                var dummyParent = new GameObject($"{parentBone.name}_PBGroup").transform;
                dummyParent.SetParent(parentBone, false);
                foreach (var child in childBones)
                {
                    child.transform.SetParent(dummyParent, false);
                }
                parentBone = dummyParent;
            }
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
            
            DisintegrateDummyParent();
            Object.DestroyImmediate(targetPhysBone);

            CollectPhysBones();

            void DisintegrateDummyParent()
            {
                const float epsilon = 0.0001f;
                var boneHasPosition = parentBone.localPosition.magnitude > epsilon || parentBone.localRotation != Quaternion.identity || (parentBone.localScale - Vector3.one).magnitude > epsilon;
                if (boneHasPosition) return;
                if (!parentBone.GetComponents<Component>().All(c => c is Transform || c is VRCPhysBone)) return;

                var actualParent = parentBone.parent;
                foreach (var child in childBones)
                {
                    child.transform.SetParent(actualParent, false);
                }

                // TransplantPhysBone(parentBone, actualParent.gameObject);
                // Just destroy because parent PhysBone is already disintegrated into child PhysBones
                Object.DestroyImmediate(parentBone.gameObject);
                parentBone = actualParent;
            }
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
            GameObject destinationObject;
            switch (destination)
            {
                case PhysBoneDestination.AvatarRoot:
                    destinationObject = avatarRoot.gameObject;
                    break;
                case PhysBoneDestination.HipParent:
                    destinationObject = avatarRoot.GetBoneTransform(HumanBodyBones.Hips).parent.gameObject;
                    break;
                case PhysBoneDestination.HipBone:
                    destinationObject = avatarRoot.GetBoneTransform(HumanBodyBones.Hips).gameObject;
                    break;
                case PhysBoneDestination.PhysBoneRootTransformParent:
                    destinationObject = physBoneRoot.parent.gameObject;
                    break;
                case PhysBoneDestination.PhysBoneRootTransform:
                    destinationObject = physBoneRoot.gameObject;
                    break;
                case PhysBoneDestination.ParentBone:
                    destinationObject = parentBone.gameObject;
                    break;
                case PhysBoneDestination.FirstChildBone:
                    destinationObject = firstChildBone.gameObject;
                    break;
                default:
                    destinationObject = physBoneRoot.gameObject;
                    break;
            }
            return TransplantPhysBone(physBoneRoot, destinationObject);
        }

        VRCPhysBone TransplantPhysBone(Transform physBoneRoot, GameObject destinationObject)
        {
            var physBone = destinationObject.AddComponent<VRCPhysBone>();
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

        [Serializable]
        public class PhysBoneInfo
        {
            public VRCPhysBone targetPhysBone;
            public PhysBoneRole targetPhysBoneRole;
            public List<VRCPhysBone> childPhysBones;
            public Transform parentBone;
            public List<Transform> childBones;
        }

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