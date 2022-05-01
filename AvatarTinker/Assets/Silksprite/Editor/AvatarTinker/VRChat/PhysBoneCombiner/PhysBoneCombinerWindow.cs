using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Silksprite.AvatarTinker.VRChat.PhysBoneCombiner
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
        [SerializeField] PhysBoneCombiner core;

        public void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            void HelpLabel(string message)
            {
                GUILayout.Label(message.Replace(" ", " "), new GUIStyle{wordWrap = true});
            }

            var serializedCore = new SerializedObject(this).FindProperty("core");
            GUILayout.Label("PhysBone合成ツール", new GUIStyle{fontStyle = FontStyle.Bold});
            GUILayout.Space(4f);
            EditorGUILayout.HelpBox("同じ階層に存在する同じ設定のVRCPhysBoneを、MultiChildType=IgnoreとExcludeを駆使してまとめます。".Replace(" ", " "), MessageType.Info);
            GUILayout.Space(4f);
            _lockGeneratedFields = EditorGUILayout.ToggleLeft("出力フィールドを保護する（デバッグ用）", _lockGeneratedFields);
            GUILayout.Space(4f);
            HelpLabel("1. DynamicBoneは事前にPhysBoneに変換しておく。");
            GUILayout.Space(4f);
            HelpLabel("2. アバターを↓に入れてCollect PhysBonesボタンを押すと、使用されているPhysBoneコンポーネントがリストアップされる");
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("avatarRoot"));
            using (new EditorGUI.DisabledScope(core.avatarRoot == null))
            {
                if (GUILayout.Button("Collect PhysBones"))
                {
                    core.CollectPhysBones();
                }
            }

            HelpLabel("3. 操作対象のPhysBoneコンポーネントを選んでSelectボタンを押す");
            if (core.allPhysBones.Count == 0)
            {
                GUILayout.Label("No VRCPhysBone components");
            }
            for (var i = 0; i < core.allPhysBones.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_lockGeneratedFields))
                    {
                        EditorGUILayout.ObjectField($"Element {i}", core.allPhysBones[i], PhysBoneCombiner.PhysBoneType, false);
                        GUILayout.Label(HumanPhysBoneRole(core.allPhysBonesRole[i], true), GUILayout.Width(24f));
                    }

                    if (GUILayout.Button("Select"))
                    {
                        core.SelectTarget(i, core.allPhysBones[i]);
                    }
                }
            }
            HelpLabel("4. ↓のリストに親ボーン候補と（同じ揺れ方をする）子ボーン候補が入る");
            using (new EditorGUI.DisabledScope(_lockGeneratedFields))
            {
                EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("targetPhysBoneIndex"));
                EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("targetPhysBone"));
                EditorGUILayout.LabelField(serializedCore.FindPropertyRelative("targetPhysBoneRole").displayName, HumanPhysBoneRole(core.targetPhysBoneRole, false));
                EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("parentBone"));
                EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("childBones"));
                EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("childPhysBones"));
            }

            if (core.targetPhysBone)
            {
                if (core.targetPhysBone.GetRootTransform().parent == core.targetPhysBone.transform && PhysBoneCombiner.CollectHumanoidBones(core.avatarRoot).Contains(core.targetPhysBone.transform))
                {
                    EditorGUILayout.HelpBox("Humanoidボーンに刺さっていたDynamicBoneが自動変換されたような構造が検知されました。アバターの前髪がJawボーンとして誤検知されていたりしないか確認してください。\nPhysBoneコンポーネントの修正が必要かもしれません。\nアバターによっては正常なので、その場合はこのメッセージを気にしないでください。", MessageType.Warning);
                }
            }

            HelpLabel("5. Child Phys Bonesの中身が２つ以上入っていたらそれらはコピペコンポーネントなので、動かしたい先にDestinationを設定してボタンを押す");
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("destination"));
            using (new EditorGUI.DisabledScope(core.targetPhysBone == null || core.parentBone == null || core.childBones == null || core.childBones.Count == 0))
            {
                using (new EditorGUI.DisabledScope(core.targetPhysBoneRole != PhysBoneCombiner.PhysBoneRole.Disassembled || core.childBones == null || core.childBones.Count <= 1))
                {
                    if (GUILayout.Button("Assemble Multi Child"))
                    {
                        core.AssembleMultiChild();
                    }
                    if (core.childBones != null && core.childBones.Count == 1)
                    {
                        EditorGUILayout.HelpBox("ツールの性質上、１本しかないPhysBoneをまとめることはできません。", MessageType.Warning);
                    }
                }
                using (new EditorGUI.DisabledScope(core.targetPhysBoneRole != PhysBoneCombiner.PhysBoneRole.Composed))
                {
                    if (GUILayout.Button("Disassemble Multi Child"))
                    {
                        core.DisassembleMultiChild();
                    }
                }
                if (GUILayout.Button("Move PhysBone"))
                {
                    core.MovePhysBone();
                }
            }
            HelpLabel("6. うまくいくと、VRCPhysBoneのコンポーネント数が減ります");
            EditorGUILayout.HelpBox("PhysBoneを動かすことも、分解することもできます。\n（着せ替えでボーン構造を編集する際は分解しておいた方が安全です）", MessageType.Info);
            serializedCore.serializedObject.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();
        }

        static string HumanPhysBoneRole(PhysBoneCombiner.PhysBoneRole role, bool isLetter)
        {
            if (isLetter)
            {
                switch (role)
                {
                    case PhysBoneCombiner.PhysBoneRole.Composed:
                        return "+";
                    case PhysBoneCombiner.PhysBoneRole.Disassembled:
                        return "-";
                    default:
                        return "?";
                }
            }
            else
            {
                switch (role)
                {
                    case PhysBoneCombiner.PhysBoneRole.Composed:
                        return "+ Composed";
                    case PhysBoneCombiner.PhysBoneRole.Disassembled:
                        return "- Disassembled";
                    default:
                        return "? 不明";
                }
            }
        }
#endif

        [MenuItem("Window/Avatar Tinker/VRChat/Phys Bone Combiner", false, 60000)]
        public static void CreateWindow()
        {
            CreateInstance<PhysBoneCombinerWindow>().Show();
        }
    }
}