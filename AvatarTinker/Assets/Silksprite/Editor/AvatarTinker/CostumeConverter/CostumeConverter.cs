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

namespace Silksprite.AvatarTinker.CostumeConverter
{
    public class CostumeConverterWindow : EditorWindow
    {
        Vector2 _scrollPosition = new Vector2(0, 0);
        static bool _lockGeneratedFields = true;

        [SerializeField] Animator avatarRoot;
        [SerializeField] SkinnedMeshRenderer costumeRenderer;
        [SerializeField] Transform humanoidHipsBone;
        [SerializeField] List<CostumeBoneMapping> costumeBoneMappings;

        public void OnEnable()
        {
            titleContent = new GUIContent("Costume Converter");
        }

        public void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            void HelpLabel(string message)
            {
                GUILayout.Label(message.Replace(" ", " "), new GUIStyle{wordWrap = true});
            }

            var serializedObject = new SerializedObject(this);
            GUILayout.Label("着せ替え変換ツール", new GUIStyle{fontStyle = FontStyle.Bold});
            GUILayout.Space(4f);
            EditorGUILayout.HelpBox("入れ子ボーン式で着せ替えてしまったアバターや、デフォルト衣装が入れ子ボーン式でセットアップされているアバターをボーン共有式着せ替えに変換します。\n変換を行うためには、衣装と素体データとボーン構造が完全に一致している必要があります。".Replace(" ", " "), MessageType.Info);
            GUILayout.Space(4f);
            _lockGeneratedFields = EditorGUILayout.ToggleLeft("出力フィールドを保護する（デバッグ用）", _lockGeneratedFields);
            GUILayout.Space(4f);
            HelpLabel("1. アバターをUnpack Prefabする");
            HelpLabel("2. アバターのAnimatorを↓にセットする");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("avatarRoot"));
            HelpLabel("3. 衣装のSkinnedMeshRendererを↓にセットする");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("costumeRenderer"));
            HelpLabel("4. Match Bonesボタンを押す");
            using (new EditorGUI.DisabledScope(avatarRoot == null || costumeRenderer == null))
            {
                if (GUILayout.Button("Match Bones"))
                {
                    MatchBones();
                }
            }
            HelpLabel("5. 検出されたボーンの着せ方が↓に表示されるはず");

            using (new EditorGUI.DisabledScope(_lockGeneratedFields))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("humanoidHipsBone"));
            }

            using (new EditorGUI.IndentLevelScope(1))
            {
                var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight));
                GUI.Label(CostumeBoneMappingColumns(rect, 1), "Costume Bone");
                GUI.Label(CostumeBoneMappingColumns(rect, 2), "Base Bone");
                GUI.Label(CostumeBoneMappingColumns(rect, 3), "Relation");
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("costumeBoneMappings"));

            DrawRelationHelp();

            HelpLabel("6. 個別に着せ方を調整するか、下のボタンで一括変換する\n※揺れものやConstraint、アニメーションが設定されているボーンは手動で除外してください！");
            using (new EditorGUI.DisabledScope(avatarRoot == null || costumeRenderer == null || costumeBoneMappings.Count == 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select All"))
                    {
                        SelectBoneMappings(true);
                    }
                    if (GUILayout.Button("Deselect All"))
                    {
                        SelectBoneMappings(false);
                    }
                }
                if (GUILayout.Button("Merge Redundant Bones"))
                {
                    MergeRedundantBones();
                }
            }
            HelpLabel("7. 入れ子ボーンによる着せ替えが結合され、ボーン数が削減されます。");
            serializedObject.ApplyModifiedProperties();
            
            EditorGUILayout.EndScrollView();
        }

        void DrawRelationHelp()
        {
            if (costumeBoneMappings == null) return;

            var relationsPresent = costumeBoneMappings.Select(mapping => mapping.relation).Distinct().ToArray();
            if (relationsPresent.Contains(CostumeRelation.NullBone))
            {
                EditorGUILayout.HelpBox("「*Null」: Null\nボーンの参照が見つかりません。\n心当たりがないならば、バックアップからやり直すことをお勧めします。", MessageType.Warning);
            }
            if (relationsPresent.Contains(CostumeRelation.Unrelated))
            {
                EditorGUILayout.HelpBox("「*Unrelated」: 着ていない\nアバターはこの衣装を着ていません。\nセットした参照が正しいか確認してください。", MessageType.Warning);
            }
            if (relationsPresent.Contains(CostumeRelation.Humanoid))
            {
                EditorGUILayout.HelpBox("「Humanoid」: Humanoidボーン\nHumanoidを構成するボーンが指定されています。\n削減できません。", MessageType.Info);
            }
            if (relationsPresent.Contains(CostumeRelation.Shared))
            {
                EditorGUILayout.HelpBox("「Shared」: 共有装飾ボーン\n他の衣装と共通で利用されているボーンが指定されています。\n削減できません。", MessageType.Info);
            }
            if (relationsPresent.Contains(CostumeRelation.Redundant))
            {
                EditorGUILayout.HelpBox("「+Redundant」: 入れ子ボーン\n入れ子式の着せ替えを行った形跡を検知しました。\nこのボーンは削減可能です。", MessageType.Warning);
            }
            if (relationsPresent.Contains(CostumeRelation.Positioning))
            {
                EditorGUILayout.HelpBox("「Positioning」: 位置調整ボーン\nサイズ調整や揺れものなどでボーンの位置が動かされています。\n削減できません。", MessageType.Info);
            }
            if (relationsPresent.Contains(CostumeRelation.IndependentChild))
            {
                EditorGUILayout.HelpBox("「Independent Child」: 固有装飾ボーン\nこの衣装固有のボーンです。\n削減できません。", MessageType.Info);
            }
        }

        void MatchBones()
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
                            // This should mark DynamicBones, VRCPhysBones and SpringBones
                            if ( n.Contains("Bone")) return true;
                            // This should mark Constraints
                            if ( n.Contains("Constraint")) return true;
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

        void SelectBoneMappings(bool value)
        {
            foreach (var mapping in costumeBoneMappings) mapping.selected = value;
        }

        void MergeRedundantBones()
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
                DestroyImmediate(mapping.bone.gameObject);
            }
            
            MatchBones();
        }

        static IEnumerable<Transform> CollectHumanoidBones(Animator animator) => Enum.GetValues(typeof(HumanBodyBones)).OfType<HumanBodyBones>().Where(hbb => hbb != HumanBodyBones.LastBone).Select(animator.GetBoneTransform).ToArray();

        [MenuItem("Window/AvatarTinker/Costume Converter", false, 60000)]
        public static void CreateWindow()
        {
            CreateInstance<CostumeConverterWindow>().Show();
        }

        [Serializable]
        class CostumeBoneMapping
        {
            public bool selected = true;
            public Transform bone;
            public Transform baseBone;
            public CostumeRelation relation;
            public bool IsPrefab => PrefabUtility.IsPartOfAnyPrefab(bone) && PrefabUtility.IsPartOfAnyPrefab(baseBone);
        }

        enum CostumeRelation
        {
            NullBone,
            Unrelated,
            Humanoid,
            Shared,
            Redundant,
            Positioning,
            IndependentChild
        }
    
        [CustomPropertyDrawer(typeof(CostumeBoneMapping))]
        class CostumeBoneMappingDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
            {
                var labelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 1f;
                var rect0 = CostumeBoneMappingColumns(rect, 0);
                EditorGUI.PropertyField(rect0, property.FindPropertyRelative("selected"), GUIContent.none);
                using (new EditorGUI.DisabledScope(_lockGeneratedFields))
                {
                    var rect1 = CostumeBoneMappingColumns(rect, 1);
                    EditorGUI.PropertyField(rect1, property.FindPropertyRelative("bone"), GUIContent.none);
                    var rect2 = CostumeBoneMappingColumns(rect, 2);
                    EditorGUI.PropertyField(rect2, property.FindPropertyRelative("baseBone"), GUIContent.none);
                }
                var rect3 = CostumeBoneMappingColumns(rect, 3);
                GUI.Label(rect3, HumanCostumeRelation((CostumeRelation)(object)property.FindPropertyRelative("relation").enumValueIndex));
                EditorGUIUtility.labelWidth = labelWidth;
            }

            static string HumanCostumeRelation(CostumeRelation relation)
            {
                switch (relation)
                {
                    case CostumeRelation.NullBone:
                        return "* Null";
                    case CostumeRelation.Unrelated:
                        return "* Unrelated";
                    case CostumeRelation.Humanoid:
                        return "Humanoid";
                    case CostumeRelation.Shared:
                        return "Shared";
                    case CostumeRelation.Redundant:
                        return "+ Redundant";
                    case CostumeRelation.Positioning:
                        return "Positioning";
                    case CostumeRelation.IndependentChild:
                        return "Independent Child";
                    default:
                        return "* Unknown";
                }
            }
        }

        static Rect CostumeBoneMappingColumns(Rect rect, int index)
        {
            const float toggleWidth = 32f;
            rect.xMin += toggleWidth;
            switch (index)
            {
                case 0:
                    return new Rect(rect.x - toggleWidth, rect.y, toggleWidth, rect.height);
                case 1:
                    return new Rect(rect.x, rect.y, rect.width * 0.3f, rect.height);
                case 2:
                    return new Rect(rect.x + rect.width * 0.3f, rect.y, rect.width * 0.3f, rect.height);
                case 3:
                    return new Rect(rect.x + rect.width * 0.6f, rect.y, rect.width * 0.4f, rect.height);
            }
            return rect;
        }
    }
}