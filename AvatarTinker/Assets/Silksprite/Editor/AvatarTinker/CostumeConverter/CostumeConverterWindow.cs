using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Silksprite.AvatarTinker.CostumeConverter
{
    public class CostumeConverterWindow : EditorWindow
    {
        Vector2 _scrollPosition = new Vector2(0, 0);
        static bool _lockGeneratedFields = true;

        [SerializeField] CostumeConverter core;

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

            var serializedCore = new SerializedObject(this).FindProperty("core");
            GUILayout.Label("着せ替え変換ツール", new GUIStyle{fontStyle = FontStyle.Bold});
            GUILayout.Space(4f);
            EditorGUILayout.HelpBox("入れ子ボーン式で着せ替えてしまったアバターや、デフォルト衣装が入れ子ボーン式でセットアップされているアバターをボーン共有式着せ替えに変換します。\n変換を行うためには、衣装と素体データのボーン構造が完全に一致している必要があります。".Replace(" ", " "), MessageType.Info);
            GUILayout.Space(4f);
            _lockGeneratedFields = EditorGUILayout.ToggleLeft("出力フィールドを保護する（デバッグ用）", _lockGeneratedFields);
            GUILayout.Space(4f);
            HelpLabel("1. アバターをUnpack Prefabする");
            HelpLabel("2. アバターのAnimatorを↓にセットする");
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("avatarRoot"));
            HelpLabel("3. 衣装のSkinnedMeshRendererを↓にセットする");
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("costumeRenderer"));
            HelpLabel("4. Match Bonesボタンを押す");
            using (new EditorGUI.DisabledScope(core.avatarRoot == null || core.costumeRenderer == null))
            {
                if (GUILayout.Button("Match Bones"))
                {
                    core.MatchBones();
                }
            }
            HelpLabel("5. 検出されたボーンの着せ方が↓に表示されるはず");

            using (new EditorGUI.DisabledScope(_lockGeneratedFields))
            {
                EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("humanoidHipsBone"));
            }

            using (new EditorGUI.IndentLevelScope(1))
            {
                var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight));
                GUI.Label(CostumeBoneMappingColumns(rect, 1), "Costume Bone");
                GUI.Label(CostumeBoneMappingColumns(rect, 2), "Base Bone");
                GUI.Label(CostumeBoneMappingColumns(rect, 3), "Relation");
            }
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("costumeBoneMappings"));

            DrawRelationHelp();

            HelpLabel("6. 個別に着せ方を調整するか、下のボタンで一括変換する\n※揺れものやConstraint、アニメーションが設定されているボーンは手動で除外してください！");
            using (new EditorGUI.DisabledScope(core.avatarRoot == null || core.costumeRenderer == null || core.costumeBoneMappings.Count == 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select All"))
                    {
                        core.SelectBoneMappings(true);
                    }
                    if (GUILayout.Button("Deselect All"))
                    {
                        core.SelectBoneMappings(false);
                    }
                }
                if (GUILayout.Button("Merge Redundant Bones"))
                {
                    core.MergeRedundantBones();
                }
            }
            HelpLabel("7. 入れ子ボーンによる着せ替えが結合され、ボーン数が削減されます。");
            serializedCore.serializedObject.ApplyModifiedProperties();
            
            EditorGUILayout.EndScrollView();
        }

        void DrawRelationHelp()
        {
            if (core.costumeBoneMappings == null) return;

            var relationsPresent = core.costumeBoneMappings.Select(mapping => mapping.relation).Distinct().ToArray();
            if (relationsPresent.Contains(CostumeConverter.CostumeRelation.NullBone))
            {
                EditorGUILayout.HelpBox("「*Null」: Null\nボーンの参照が見つかりません。\n心当たりがないならば、バックアップからやり直すことをお勧めします。", MessageType.Warning);
            }
            if (relationsPresent.Contains(CostumeConverter.CostumeRelation.Unrelated))
            {
                EditorGUILayout.HelpBox("「*Unrelated」: 着ていない\nアバターはこの衣装を着ていません。\nセットした参照が正しいか確認してください。", MessageType.Warning);
            }
            if (relationsPresent.Contains(CostumeConverter.CostumeRelation.Humanoid))
            {
                EditorGUILayout.HelpBox("「Humanoid」: Humanoidボーン\nHumanoidを構成するボーンが指定されています。\n削減できません。", MessageType.Info);
            }
            if (relationsPresent.Contains(CostumeConverter.CostumeRelation.Shared))
            {
                EditorGUILayout.HelpBox("「Shared」: 共有装飾ボーン\n他の衣装と共通で利用されているボーンが指定されています。\n削減できません。", MessageType.Info);
            }
            if (relationsPresent.Contains(CostumeConverter.CostumeRelation.Redundant))
            {
                EditorGUILayout.HelpBox("「+Redundant」: 入れ子ボーン\n入れ子式の着せ替えを行った形跡を検知しました。\nこのボーンは削減可能です。", MessageType.Warning);
            }
            if (relationsPresent.Contains(CostumeConverter.CostumeRelation.Positioning))
            {
                EditorGUILayout.HelpBox("「Positioning」: 位置調整ボーン\nサイズ調整や揺れものなどでボーンの位置が動かされています。\n削減できません。", MessageType.Info);
            }
            if (relationsPresent.Contains(CostumeConverter.CostumeRelation.IndependentChild))
            {
                EditorGUILayout.HelpBox("「Independent Child」: 固有装飾ボーン\nこの衣装固有のボーンです。\n削減できません。", MessageType.Info);
            }
        }

        [CustomPropertyDrawer(typeof(CostumeConverter.CostumeBoneMapping))]
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
                GUI.Label(rect3, HumanCostumeRelation((CostumeConverter.CostumeRelation)(object)property.FindPropertyRelative("relation").enumValueIndex));
                EditorGUIUtility.labelWidth = labelWidth;
            }

            static string HumanCostumeRelation(CostumeConverter.CostumeRelation relation)
            {
                switch (relation)
                {
                    case CostumeConverter.CostumeRelation.NullBone:
                        return "* Null";
                    case CostumeConverter.CostumeRelation.Unrelated:
                        return "* Unrelated";
                    case CostumeConverter.CostumeRelation.Humanoid:
                        return "Humanoid";
                    case CostumeConverter.CostumeRelation.Shared:
                        return "Shared";
                    case CostumeConverter.CostumeRelation.Redundant:
                        return "+ Redundant";
                    case CostumeConverter.CostumeRelation.Positioning:
                        return "Positioning";
                    case CostumeConverter.CostumeRelation.IndependentChild:
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

        [MenuItem("Window/Avatar Tinker/Costume Converter", false, 60000)]
        public static void CreateWindow()
        {
            CreateInstance<CostumeConverterWindow>().Show();
        }
    }
}