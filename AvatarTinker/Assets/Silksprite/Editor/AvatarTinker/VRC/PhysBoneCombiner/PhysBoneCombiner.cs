// MIT License
//
// Copyright (c) 2021 kaikoga
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Silksprite.AvatarTinker.VRC.PhysBoneCombiner
{
    public class PhysBoneCombinerWindow : EditorWindow
    {
        Vector2 _scrollPosition = new Vector2(0, 0);
        static bool _lockGeneratedFields = true;

        public void OnEnable()
        {
            titleContent = new GUIContent("Phys Bone Combiner");
        }

#if !VRC_SDK_VRCSDK3
        public void OnGUI()
        {
            EditorGUILayout.HelpBox("PhysBone合成ツールを利用する場合は、VRCSDK Avatar 3.0が必要です", MessageType.Error);
        }
#else
        [SerializeField] Animator avatarRoot;
        [SerializeField] List<VRCPhysBone> allPhysBones = new List<VRCPhysBone>();
        [SerializeField] List<PhysBoneRole> allPhysBonesRole = new List<PhysBoneRole>();
        
        [SerializeField] int targetPhysBoneIndex;
        [SerializeField] VRCPhysBone targetPhysBone;
        [SerializeField] PhysBoneRole targetPhysBoneRole;
        [SerializeField] Transform parentBone;
        [SerializeField] List<Transform> childBones = new List<Transform>();
        [SerializeField] List<VRCPhysBone> childPhysBones = new List<VRCPhysBone>();

        [SerializeField] PhysBoneDestination destination = PhysBoneDestination.PhysBoneRootTransform;

        public void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            void HelpLabel(string message)
            {
                GUILayout.Label(message.Replace(" ", " "), new GUIStyle{wordWrap = true});
            }

            var serializedObject = new SerializedObject(this);
            GUILayout.Label("PhysBone合成ツール", new GUIStyle{fontStyle = FontStyle.Bold});
            GUILayout.Space(4f);
            EditorGUILayout.HelpBox("同じ階層に存在する同じ設定のVRCPhysBoneを、MultiChildType=IgnoreとExcludeを駆使してまとめます。".Replace(" ", " "), MessageType.Info);
            GUILayout.Space(4f);
            _lockGeneratedFields = EditorGUILayout.ToggleLeft("出力フィールドを保護する（デバッグ用）", _lockGeneratedFields);
            GUILayout.Space(4f);
            HelpLabel("1. DynamicBoneは事前にPhysBoneに変換しておく。");
            GUILayout.Space(4f);
            HelpLabel("2. アバターを↓に入れてCollect PhysBonesボタンを押すと、使用されているPhysBoneコンポーネントがリストアップされる");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("avatarRoot"));
            using (new EditorGUI.DisabledScope(avatarRoot == null))
            {
                if (GUILayout.Button("Collect PhysBones"))
                {
                    CollectPhysBones();
                }
            }

            HelpLabel("3. 操作対象のPhysBoneコンポーネントを選んでSelectボタンを押す");
            if (allPhysBones.Count == 0)
            {
                GUILayout.Label("No VRCPhysBone components");
            }
            for (var i = 0; i < allPhysBones.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_lockGeneratedFields))
                    {
                        EditorGUILayout.ObjectField($"Element {i}", allPhysBones[i], typeof(VRCPhysBone), false);
                        GUILayout.Label(HumanPhysBoneRole(allPhysBonesRole[i], true), GUILayout.Width(24f));
                    }

                    if (GUILayout.Button("Select"))
                    {
                        SelectTarget(i, allPhysBones[i]);
                    }
                }
            }
            HelpLabel("4. ↓のリストに親ボーン候補と（同じ揺れ方をする）子ボーン候補が入る");
            using (new EditorGUI.DisabledScope(_lockGeneratedFields))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("targetPhysBoneIndex"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("targetPhysBone"));
                EditorGUILayout.LabelField(serializedObject.FindProperty("targetPhysBoneRole").displayName, HumanPhysBoneRole(targetPhysBoneRole, false));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("parentBone"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("childBones"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("childPhysBones"));
            }

            if (targetPhysBone)
            {
                if (targetPhysBone.GetRootTransform().parent == targetPhysBone.transform && CollectHumanoidBones(avatarRoot).Contains(targetPhysBone.transform))
                {
                    EditorGUILayout.HelpBox("Humanoidボーンに刺さっていたDynamicBoneが自動変換されたような構造が検知されました。アバターの前髪がJawボーンとして誤検知されていたりしないか確認してください。\nPhysBoneコンポーネントの修正が必要かもしれません。\nアバターによっては正常なので、その場合はこのメッセージを気にしないでください。", MessageType.Warning);
                }
            }

            HelpLabel("5. Child Phys Bonesの中身が２つ以上入っていたらそれらはコピペコンポーネントなので、動かしたい先にDestinationを設定してボタンを押す");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("destination"));
            using (new EditorGUI.DisabledScope(targetPhysBone == null || parentBone == null || childBones == null || childBones.Count == 0))
            {
                using (new EditorGUI.DisabledScope(targetPhysBoneRole != PhysBoneRole.Disassembled))
                {
                    if (GUILayout.Button("Assemble Multi Child"))
                    {
                        AssembleMultiChild();
                    }
                }
                using (new EditorGUI.DisabledScope(targetPhysBoneRole != PhysBoneRole.Composed))
                {
                    if (GUILayout.Button("Disassemble Multi Child"))
                    {
                        DisassembleMultiChild();
                    }
                }
                if (GUILayout.Button("Move PhysBone"))
                {
                    MovePhysBone();
                }
            }
            HelpLabel("6. うまくいくと、VRCPhysBoneのコンポーネント数が減ります");
            EditorGUILayout.HelpBox("PhysBoneを動かすことも、分解することもできます。\n（着せ替えでボーン構造を編集する際は分解しておいた方が安全です）", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();
        }

        void CollectPhysBones()
        {
            allPhysBones = avatarRoot.GetComponentsInChildren<VRCPhysBone>().ToList();
            allPhysBonesRole = allPhysBones.Select(GuessPhysBoneRole).ToList();
            targetPhysBoneIndex = 0;
            targetPhysBone = null;
            parentBone = null;
            childBones = new List<Transform>();
            childPhysBones = new List<VRCPhysBone>();
        }

        void SelectTarget(int index, VRCPhysBone target)
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

        void AssembleMultiChild()
        {
            var allAffectedList = childPhysBones.SelectMany(pb => IgnoreListToAffectedList(pb, true)).ToList();
            var ignoreList = AffectedListToIgnoreList(parentBone, allAffectedList, false).ToList();
            targetPhysBone = CreatePhysBone(parentBone, parentBone, childBones.First());
            targetPhysBone.multiChildType = VRCPhysBoneBase.MultiChildType.Ignore;
            targetPhysBone.ignoreTransforms = ignoreList;
            foreach (var childPhysBone in childPhysBones.ToArray())
            {
                if (childPhysBone) DestroyImmediate(childPhysBone);
            }

            CollectPhysBones();
        }

        void DisassembleMultiChild()
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
            
            DestroyImmediate(targetPhysBone);

            CollectPhysBones();
        }

        void MovePhysBone()
        {
            var newPhysBone = CreatePhysBone(targetPhysBone.GetRootTransform(), parentBone, childBones.First());
            DestroyImmediate(targetPhysBone);
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

        static IEnumerable<Transform> CollectHumanoidBones(Animator animator) => Enum.GetValues(typeof(HumanBodyBones)).OfType<HumanBodyBones>().Where(hbb => hbb != HumanBodyBones.LastBone).Select(animator.GetBoneTransform).ToArray();

        enum PhysBoneRole
        {
            Composed,
            Disassembled
        }

        string HumanPhysBoneRole(PhysBoneRole role, bool isLetter)
        {
            if (isLetter)
            {
                switch (role)
                {
                    case PhysBoneRole.Composed:
                        return "+";
                    case PhysBoneRole.Disassembled:
                        return "-";
                    default:
                        return "?";
                }
            }
            else
            {
                switch (role)
                {
                    case PhysBoneRole.Composed:
                        return "+ Composed";
                    case PhysBoneRole.Disassembled:
                        return "- Disassembled";
                    default:
                        return "? 不明";
                }
            }
        }
        
        enum PhysBoneDestination
        {
            AvatarRoot,
            HipParent,
            HipBone,
            PhysBoneRootTransformParent,
            PhysBoneRootTransform,
            ParentBone,
            FirstChildBone
        }

#endif

        [MenuItem("Window/Avatar Tinker/VRChat/Phys Bone Combiner", false, 60000)]
        public static void CreateWindow()
        {
            CreateInstance<PhysBoneCombinerWindow>().Show();
        }
    }
}