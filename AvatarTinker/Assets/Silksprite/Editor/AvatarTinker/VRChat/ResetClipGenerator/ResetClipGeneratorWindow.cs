using UnityEditor;
using UnityEngine;

namespace Silksprite.AvatarTinker.VRChat.ResetClipGenerator
{
    public class ResetClipGeneratorWindow : EditorWindow
    {
        public void OnEnable()
        {
            titleContent = new GUIContent("Reset Clip Generator");
        }

#if !VRC_SDK_VRCSDK3
        public void OnGUI()
        {
            EditorGUILayout.HelpBox("リセットアニメーション自動生成くんを利用する場合は、VRCSDK Avatar 3.0が必要です", MessageType.Error);
        }
#else
        [SerializeField] ResetClipGenerator core = new ResetClipGenerator();

        public void OnGUI()
        {
            void HelpLabel(string message)
            {
                GUILayout.Label(message.Replace(" ", " "), new GUIStyle{wordWrap = true});
            }

            var serializedCore = new SerializedObject(this).FindProperty("core");
            GUILayout.Label("リセットアニメーション自動生成くん", new GUIStyle{fontStyle = FontStyle.Bold});
            GUILayout.Space(4f);
            EditorGUILayout.HelpBox("Write Defaults Offで運用する際に必要になる、アニメーションしていないプロパティを初期状態に戻すためのリセットアニメーションを生成します。\nこのツールはシーンに置いてある状態を初期状態として利用します。".Replace(" ", " "), MessageType.Info);
            GUILayout.Space(4f);
            HelpLabel("1. あらかじめリセットアニメーション用の空のAnimation ClipをAnimator Controllerの一番上のレイヤーに組み込んでおく");
            HelpLabel("2. シーン上のAvatarDescriptorを↓にセットする");
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("avatarDescriptor"));
            HelpLabel("3. Find Reset Clipsボタンを押す");
            if (GUILayout.Button("Find Reset Clips"))
            {
                core.FindResetClips();
            }
            HelpLabel("4. ↓のリストにリセットアニメーションの候補が入るが、余計なものが入ってるのでリセットアニメーションではないものを人の手で取り除く");
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("resetClips"));
            HelpLabel("5. Generate Reset Clipsボタンを押す");
            using (new EditorGUI.DisabledScope(core.avatarDescriptor == null || core.resetClips.Count == 0))
            {
                if (GUILayout.Button("Generate Reset Clips"))
                {
                    core.GenerateResetClips();
                }
            }
            HelpLabel("6. リセットアニメーションが含まれるAnimator Controllerが動かす全てのプロパティがシーン上の現在のアバターの状態にリセットされるリセットアニメーションが自動生成される！");
            serializedCore.serializedObject.ApplyModifiedProperties();
        }
#endif
        [MenuItem("Window/Avatar Tinker/VRChat/Reset Clip Generator", false, 60000)]
        public static void CreateWindow()
        {
            CreateInstance<ResetClipGeneratorWindow>().Show();
        }
    }
}