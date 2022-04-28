// MIT License
//
// Copyright (c) 2022 kaikoga
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

namespace Silksprite.AvatarTinker.UnusedBoneDeleter
{
    public class UnusedBoneDeleterWindow : EditorWindow
    {
        Vector2 _scrollPosition = new Vector2(0, 0);

        [SerializeField] Animator avatarRoot;
        [SerializeField] Transform armatureRoot;
        [SerializeField] List<Transform> unusedBones;

        public void OnEnable()
        {
            titleContent = new GUIContent("Unused Bone Deleter");
        }

        public void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            void HelpLabel(string message)
            {
                GUILayout.Label(message.Replace(" ", " "), new GUIStyle{wordWrap = true});
            }

            var serializedObject = new SerializedObject(this);
            GUILayout.Label("余ったボーン削除くん", new GUIStyle{fontStyle = FontStyle.Bold});
            GUILayout.Space(4f);
            EditorGUILayout.HelpBox("使われていないボーンを検索して削除します。このツールはメッシュを編集しないのでSkinnedMeshRendererからボーンへの参照は壊れますが、ウェイトが塗られていないはずなのでたぶん安全です。".Replace(" ", " "), MessageType.Info);
            GUILayout.Space(4f);
            HelpLabel("1. アバターをUnpack Prefabする");
            HelpLabel("2. アバターのAnimatorを↓にセットする");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("avatarRoot"));
            HelpLabel("3. オプション：ボーン階層の一番上を↓にセットする（省略するとHipボーンが選択されます）");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("armatureRoot"));
            HelpLabel("4. Select Unused Bonesボタンを押す");
            EditorGUILayout.HelpBox("「Select Unused Bones in Skinned Mesh」はヒエラルキーからGameObjectを検索します。\n「Select Unused Bones in Armature」はウェイトが塗られていないボーンだけ検索します。".Replace(" ", " "), MessageType.Info);
            using (new EditorGUI.DisabledScope(avatarRoot == null))
            {
                if (GUILayout.Button("Select Unused Bones in Armature"))
                {
                    SelectUnusedBones();
                }
                if (GUILayout.Button("Select Unused Bone in Skinned Mesh"))
                {
                    SelectUnusedSkinnedMeshBones();
                }
            }
            HelpLabel("5. 検出された使われていないボーンが↓に表示されるはず");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("unusedBones"));

            HelpLabel("6. 個別に選択して手動で削除するか、一番下のボタンで一括削除する");
            using (new EditorGUI.DisabledScope(unusedBones == null))
            {
                if (GUILayout.Button("Delete Unused Bones"))
                {
                    DeleteUnusedBones();
                }
            }
            HelpLabel("7. ボーンが見えてしまう一部の環境で、ちょっとだけ綺麗になります。");
            serializedObject.ApplyModifiedProperties();
            
            EditorGUILayout.EndScrollView();
        }

        void SelectUnusedBones()
        {
            if (armatureRoot == null) armatureRoot = avatarRoot.GetBoneTransform(HumanBodyBones.Hips); 

            unusedBones = new List<Transform>();
            armatureRoot.GetComponentsInChildren(true, unusedBones);

            MarkBonesAsUsed();
        }

        void SelectUnusedSkinnedMeshBones()
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

        void DeleteUnusedBones()
        {
            foreach (var unusedBone in unusedBones.ToArray())
            {
                if (unusedBone) DestroyImmediate(unusedBone.gameObject);
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

        [MenuItem("Window/Avatar Tinker/Unused Bone Deleter", false, 60000)]
        public static void CreateWindow()
        {
            CreateInstance<UnusedBoneDeleterWindow>().Show();
        }
    }
}