using UnityEditor;
using UnityEngine;

namespace Silksprite.AvatarTinker.SpiderBracelet
{
    public class SpiderBraceletWindow : EditorWindow
    {
        Vector2 _scrollPosition = new Vector2(0, 0);

        [SerializeField] SpiderBracelet core;

        public void OnEnable()
        {
            titleContent = new GUIContent("Spider Bracelet");
        }

        public void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            void HelpLabel(string message)
            {
                GUILayout.Label(message.Replace(" ", " "), new GUIStyle{wordWrap = true});
            }

            var serializedCore = new SerializedObject(this).FindProperty("core");
            GUILayout.Label("スパイダーブレスレット", new GUIStyle{fontStyle = FontStyle.Bold});
            GUILayout.Space(4f);
            EditorGUILayout.HelpBox("メッシュを改変して、衣装の全ての頂点を一点に集めるシェイプキーを追加します。".Replace(" ", " "), MessageType.Info);
            GUILayout.Space(4f);
            HelpLabel("1. 衣装のSkinnedMeshRendererを↓にセットしつつ、体型補正用シェイプキーを調整しておく");
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("costumeRenderer"));
            HelpLabel("2. シェイプキーの名前や原点を設定する");
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("blendShapeName"));
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("origin"));
            HelpLabel("3. Executeボタンを押す");
            using (new EditorGUI.DisabledScope(core.costumeRenderer == null || core.blendShapeName == null))
            {
                if (GUILayout.Button("Execute"))
                {
                    core.Execute();
                }
            }
            HelpLabel("4. SkinnedMeshRendererのメッシュ参照が新たに生成されたメッシュアセットに差し代わるので、アセットはどこかに保存する");
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("generatedMesh"));
            serializedCore.serializedObject.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
        }

        [MenuItem("Window/Avatar Tinker/Spider Bracelet", false, 60000)]
        public static void CreateWindow()
        {
            CreateInstance<SpiderBraceletWindow>().Show();
        }
    }
}