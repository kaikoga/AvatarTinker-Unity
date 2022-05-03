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
        [SerializeField] public PhysBoneInfo currentInfo;

        [SerializeField] public PhysBoneDestination destination = PhysBoneDestination.PhysBoneRootTransform;
        [SerializeField] public bool createDummyParent = true;

        public void CollectPhysBones()
        {
            allPhysBones = avatarRoot.GetComponentsInChildren<VRCPhysBone>().ToList();
            allPhysBoneInfos = allPhysBones.Select(pb => GuessTarget(pb, allPhysBones)).ToList();
            targetPhysBoneIndex = 0;
            currentInfo = null;
        }

        public void SelectTarget(int index)
        {
            targetPhysBoneIndex = index;
            currentInfo = allPhysBoneInfos[index];
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

            info.targetPhysBoneRole = PhysBoneRole.Disassembled;
            if (info.targetPhysBone.multiChildType == VRCPhysBoneBase.MultiChildType.Ignore)
            {
                info.targetPhysBoneRole = PhysBoneRole.Composed;
                if (info.targetPhysBone.GetRootTransform().childCount == 1 && info.targetPhysBone.GetRootTransform().parent.childCount > 1)
                {
                    info.targetPhysBoneRole = PhysBoneRole.Disassembled;
                }
            }

            switch (info.targetPhysBoneRole)
            {
                case PhysBoneRole.Composed:
                    info.parentBone = info.targetPhysBone.GetRootTransform();
                    foreach (Transform transform in info.parentBone)
                    {
                        if (info.targetPhysBone.ignoreTransforms.Contains(transform)) continue;
                        info.childBones.Add(transform);
                    }

                    if (info.childBones.Count == 1)
                    {
                        info.targetPhysBoneRole = PhysBoneRole.Independent;
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

                    if (info.childBones.Count == 1)
                    {
                        info.targetPhysBoneRole = PhysBoneRole.Independent;
                    }
                    break;
                case PhysBoneRole.Unknown:
                case PhysBoneRole.Independent:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return info;
        }

        public void AssembleAllMultiChild()
        {
            for (;;)
            {
                CollectPhysBones();
                currentInfo = allPhysBoneInfos.FirstOrDefault(info => info.targetPhysBoneRole == PhysBoneRole.Disassembled);
                if (currentInfo == null) break;
                AssembleMultiChild();
            }
            CollectPhysBones();
        }

        public void DisassembleAllMultiChild()
        {
            for (;;)
            {
                CollectPhysBones();
                currentInfo = allPhysBoneInfos.FirstOrDefault(info => info.targetPhysBoneRole == PhysBoneRole.Composed);
                if (currentInfo == null) break;
                DisassembleMultiChild();
            }
            CollectPhysBones();
        }

        public void AssembleMultiChild()
        {
            if (currentInfo.childBones.Count < 2)
            {
                Debug.LogError("Cannot combine single bone");
                return;
            }

            var allAffectedList = currentInfo.childPhysBones.SelectMany(pb => IgnoreListToAffectedList(pb, true)).ToList();
            if (createDummyParent) IntegrateDummyParent();
            var ignoreList = AffectedListToIgnoreList(currentInfo.parentBone, allAffectedList).ToList();
            var targetPhysBone = CreatePhysBone(currentInfo.parentBone, currentInfo.parentBone, currentInfo.childBones.First());
            targetPhysBone.multiChildType = VRCPhysBoneBase.MultiChildType.Ignore;
            targetPhysBone.ignoreTransforms = ignoreList;
            foreach (var childPhysBone in currentInfo.childPhysBones.ToArray())
            {
                if (childPhysBone) Object.DestroyImmediate(childPhysBone);
            }

            CollectPhysBones();

            void IntegrateDummyParent()
            {
                var dummyParent = new GameObject($"{currentInfo.parentBone.name}_PBGroup").transform;
                dummyParent.SetParent(currentInfo.parentBone, false);
                foreach (var child in currentInfo.childBones)
                {
                    child.transform.SetParent(dummyParent, false);
                }
                currentInfo.parentBone = dummyParent;
            }
        }

        public void DisassembleMultiChild()
        {
            var allAffectedList = IgnoreListToAffectedList(currentInfo.targetPhysBone, false).ToList();
            foreach (Transform child in currentInfo.childBones)
            {
                var childAffectedList = allAffectedList.Where(t => IsDescendant(child, t)).ToList();
                if (!childAffectedList.Any()) continue;
                var ignoreList = AffectedListToIgnoreList(child, childAffectedList).ToList();

                var childPhysBone = CreatePhysBone(child, currentInfo.parentBone, child);
                childPhysBone.multiChildType = VRCPhysBoneBase.MultiChildType.Average;
                childPhysBone.ignoreTransforms = ignoreList;
            }
            
            /* if (createDummyParent) */ DisintegrateDummyParent(); // NOTE: always disintegrate
            Object.DestroyImmediate(currentInfo.targetPhysBone);

            CollectPhysBones();

            void DisintegrateDummyParent()
            {
                const float epsilon = 0.0001f;
                var boneHasPosition = currentInfo.parentBone.localPosition.magnitude > epsilon || currentInfo.parentBone.localRotation != Quaternion.identity || (currentInfo.parentBone.localScale - Vector3.one).magnitude > epsilon;
                if (boneHasPosition) return;
                if (!currentInfo.parentBone.GetComponents<Component>().All(c => c is Transform || c is VRCPhysBone)) return;

                var actualParent = currentInfo.parentBone.parent;
                foreach (var child in currentInfo.childBones)
                {
                    child.transform.SetParent(actualParent, false);
                }

                // TransplantPhysBone(parentBone, actualParent.gameObject);
                // Just destroy because parent PhysBone is already disintegrated into child PhysBones
                Object.DestroyImmediate(currentInfo.parentBone.gameObject);
                currentInfo.parentBone = actualParent;
            }
        }

        public void MovePhysBone()
        {
            var newPhysBone = CreatePhysBone(currentInfo.targetPhysBone.GetRootTransform(), currentInfo.parentBone, currentInfo.childBones.First());
            Object.DestroyImmediate(currentInfo.targetPhysBone);
            currentInfo.targetPhysBone = newPhysBone;

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
            return TransplantPhysBone(currentInfo.targetPhysBone, physBoneRoot, destinationObject);
        }

        VRCPhysBone TransplantPhysBone(VRCPhysBone source, Transform physBoneRoot, GameObject destinationObject)
        {
            var physBone = destinationObject.AddComponent<VRCPhysBone>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), physBone);
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
        
        static IEnumerable<Transform> AffectedListToIgnoreList(Transform rootTransform, List<Transform> affectedList)
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

        public IEnumerable<Transform> CollectHumanoidBones() => Enum.GetValues(typeof(HumanBodyBones)).OfType<HumanBodyBones>().Where(hbb => hbb != HumanBodyBones.LastBone).Select(avatarRoot.GetBoneTransform).ToArray();

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
            Unknown,
            Composed,
            Disassembled,
            Independent
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