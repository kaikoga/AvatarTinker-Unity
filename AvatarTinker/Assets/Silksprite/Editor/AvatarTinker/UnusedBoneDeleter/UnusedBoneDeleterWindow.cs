using UnityEditor;
using UnityEngine;

namespace Silksprite.AvatarTinker.UnusedBoneDeleter
{
    public class UnusedBoneDeleterWindow : EditorWindow
    {
        Vector2 _scrollPosition = new Vector2(0, 0);

        [SerializeField] UnusedBoneDeleter core;

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

            var serializedCore = new SerializedObject(this).FindProperty("core");
            GUILayout.Label("余ったボーン削除くん", new GUIStyle{fontStyle = FontStyle.Bold});
            GUILayout.Space(4f);
            EditorGUILayout.HelpBox("使われていないボーンを検索して削除します。このツールはメッシュを編集しないのでSkinnedMeshRendererからボーンへの参照は壊れますが、ウェイトが塗られていないはずなのでたぶん安全です。".Replace(" ", " "), MessageType.Info);
            GUILayout.Space(4f);
            HelpLabel("1. アバターをUnpack Prefabする");
            HelpLabel("2. アバターのAnimatorを↓にセットする");
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("avatarRoot"));
            HelpLabel("3. オプション：ボーン階層の一番上を↓にセットする（無効な参照の場合、Hipボーンが選択されます）");
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("armatureRoot"));
            HelpLabel("4. Select Unused Bonesボタンを押す");
            EditorGUILayout.HelpBox("「Select Unused Bones in Armature」はヒエラルキーからGameObjectを検索します。\n「Select Unused Bones in Skinned Mesh」はウェイトが塗られていないボーンだけ検索します。".Replace(" ", " "), MessageType.Info);
            using (new EditorGUI.DisabledScope(core.avatarRoot == null))
            {
                if (GUILayout.Button("Select Unused Bones in Armature"))
                {
                    core.SelectUnusedBones();
                }
                if (GUILayout.Button("Select Unused Bone in Skinned Mesh"))
                {
                    core.SelectUnusedSkinnedMeshBones();
                }
            }
            HelpLabel("5. 検出された使われていないボーンが↓に表示されるはず");

            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("unusedBones"));

            HelpLabel("6. 個別に選択して手動で削除するか、一番下のボタンで一括削除する");
            using (new EditorGUI.DisabledScope(core.unusedBones == null))
            {
                if (GUILayout.Button("Delete Unused Bones"))
                {
                    core.DeleteUnusedBones();
                }
            }
            HelpLabel("7. ボーンが見えてしまう一部の環境で、ちょっとだけ綺麗になります。");
            serializedCore.serializedObject.ApplyModifiedProperties();
            
            EditorGUILayout.EndScrollView();
        }

        [MenuItem("Window/Avatar Tinker/Unused Bone Deleter", false, 60000)]
        public static void CreateWindow()
        {
            CreateInstance<UnusedBoneDeleterWindow>().Show();
        }
    }
}