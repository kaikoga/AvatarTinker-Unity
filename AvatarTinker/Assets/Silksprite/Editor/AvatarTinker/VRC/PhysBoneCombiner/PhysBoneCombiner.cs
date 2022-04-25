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
using Vector2 = UnityEngine.Vector2;

namespace Silksprite.AvatarTinker.VRC.PhysBoneCombiner
{
    public class PhysBoneCombinerWindow : EditorWindow
    {
        Vector2 _scrollPosition = new Vector2(0, 0);

        public void OnEnable()
        {
            titleContent = new GUIContent("Phys Bone Combiner");
        }

#if !VRC_SDK_VRCSDK3
        public void OnGUI()
        {
            EditorGUILayout.HelpBox("PhysBone昇進ツールを利用する場合は、VRCSDK Avatar 3.0が必要です", MessageType.Error);
        }
#else
        [SerializeField] Animator avatarRoot;
        [SerializeField] List<VRCPhysBone> allPhysBones = new List<VRCPhysBone>();
        
        [SerializeField] int targetPhysBoneIndex;
        [SerializeField] VRCPhysBone targetPhysBone;
        [SerializeField] bool isComposed;
        [SerializeField] Transform parentBone;
        [SerializeField] List<Transform> childBones = new List<Transform>();
        [SerializeField] List<VRCPhysBone> childPhysBones = new List<VRCPhysBone>();

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
            HelpLabel("0. DynamicBoneは事前にPhysBoneに変換しておく。");
            HelpLabel("1. アバターを↓に入れてCollect PhysBonesボタンを押すと、使用されているPhysBoneコンポーネントがリストアップされる");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("avatarRoot"));
            using (new EditorGUI.DisabledScope(avatarRoot == null))
            {
                if (GUILayout.Button("Collect PhysBones"))
                {
                    CollectPhysBones();
                }
            }

            HelpLabel("2. 操作対象のPhysBoneコンポーネントを選んでSelectボタンを押す");
            if (allPhysBones.Count == 0)
            {
                GUILayout.Label("PhysBoneコンポーネントが検出されていません");
            }
            for (var i = 0; i < allPhysBones.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField($"Element {i}", allPhysBones[i], typeof(VRCPhysBone), false);
                    if (GUILayout.Button("Select"))
                    {
                        SelectTarget(i, allPhysBones[i]);
                    }
                }
            }
            HelpLabel("3. ↓のリストに親ボーン候補と（同じ揺れ方をする）子ボーン候補が入る");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("targetPhysBoneIndex"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("targetPhysBone"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("isComposed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("parentBone"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("childBones"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("childPhysBones"));

            if (targetPhysBone)
            {
                if (targetPhysBone.GetRootTransform().parent == targetPhysBone.transform && CollectHumanoidBones(avatarRoot).Contains(targetPhysBone.transform))
                {
                    EditorGUILayout.HelpBox("Humanoidボーンに刺さっていたDynamicBoneが自動変換されたような構造が検知されました。アバターの前髪がJawボーンとして誤検知されていたりしないか確認してください。\nPhysBoneコンポーネントの修正が必要かもしれません。", MessageType.Warning);
                }
            }

            HelpLabel("4. Child Phys Bonesの中身が２つ以上入っていたらそれはコピペコンポーネントなので、ボタンを押す");
            using (new EditorGUI.DisabledScope(targetPhysBone == null || parentBone == null || childBones == null))
            {
                using (new EditorGUI.DisabledScope(isComposed))
                {
                    if (GUILayout.Button("Assemble Multi Child"))
                    {
                        AssembleMultiChild();
                    }
                }
                using (new EditorGUI.DisabledScope(!isComposed))
                {
                    if (GUILayout.Button("Disassemble Multi Child"))
                    {
                        DisassembleMultiChild();
                    }
                }
            }
            HelpLabel("5. うまくいくと、VRCPhysBoneのコンポーネント数が減ります");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();
        }

        void CollectPhysBones()
        {
            allPhysBones = avatarRoot.GetComponentsInChildren<VRCPhysBone>().ToList();
            targetPhysBoneIndex = 0;
            targetPhysBone = null;
            parentBone = null;
            childBones = new List<Transform>();
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

            isComposed = targetPhysBone.multiChildType == VRCPhysBoneBase.MultiChildType.Ignore;
            if (targetPhysBone.GetRootTransform().childCount == 1 && targetPhysBone.GetRootTransform().parent.childCount > 1)
            {
                isComposed = false;
            }

            if (isComposed)
            {
                parentBone = targetPhysBone.GetRootTransform();
                childBones = new List<Transform>();
                childPhysBones = new List<VRCPhysBone>();
                foreach (Transform transform in parentBone)
                {
                    if (targetPhysBone.ignoreTransforms.Contains(transform)) continue; 
                    childBones.Add(transform);
                }
            }
            else
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
            }
        }

        void AssembleMultiChild()
        {
            var allAffectedList = childPhysBones.SelectMany(pb => IgnoreListToAffectedList(pb, true)).ToList();
            var ignoreList = AffectedListToIgnoreList(parentBone, allAffectedList, false).ToList();
            targetPhysBone.rootTransform = parentBone;
            targetPhysBone.multiChildType = VRCPhysBoneBase.MultiChildType.Ignore;
            targetPhysBone.ignoreTransforms = ignoreList;
            foreach (var childPhysBone in childPhysBones.ToArray())
            {
                if (childPhysBone != targetPhysBone) DestroyImmediate(childPhysBone);
            }

            CollectPhysBones();
        }

        void DisassembleMultiChild()
        {
            var allAffectedList = IgnoreListToAffectedList(targetPhysBone, false).ToList();
            foreach (Transform child in parentBone)
            {
                var childAffectedList = allAffectedList.Where(t => IsDescendant(child, t)).ToList();
                if (!childAffectedList.Any()) continue;
                var ignoreList = AffectedListToIgnoreList(child, childAffectedList, false).ToList();

                var childPhysBone = child.gameObject.AddComponent<VRCPhysBone>();
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(targetPhysBone), childPhysBone);
                childPhysBone.rootTransform = child;
                childPhysBone.multiChildType = VRCPhysBoneBase.MultiChildType.Average;
                childPhysBone.ignoreTransforms = ignoreList;
            }
            
            DestroyImmediate(targetPhysBone);

            CollectPhysBones();
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

#endif

        [MenuItem("Window/Silksprite/Phys Bone Combiner", false, 60000)]
        public static void CreateWindow()
        {
            CreateInstance<PhysBoneCombinerWindow>().Show();
        }
    }
}