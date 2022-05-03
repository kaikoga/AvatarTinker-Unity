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
            HelpLabel("1. DynamicBoneは事前にPhysBoneに変換しておく。PrefabはUnpackする。");
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
                    }
                    GUILayout.Label(HumanPhysBoneRole(core.allPhysBoneInfos[i].targetPhysBoneRole, true), GUILayout.Width(24f));

                    if (GUILayout.Button("Select"))
                    {
                        core.SelectTarget(i);
                    }
                }
            }
            HelpLabel("4. ↓のリストに親ボーン候補と（同じ揺れ方をする）子ボーン候補が入る");
            using (new EditorGUI.DisabledScope(_lockGeneratedFields))
            {
                if (core.currentInfo != null)
                {
                    EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("targetPhysBoneIndex"));
                    var currentInfoProperty = serializedCore.FindPropertyRelative("currentInfo");
                    EditorGUILayout.PropertyField(currentInfoProperty.FindPropertyRelative("targetPhysBone"));
                    EditorGUILayout.LabelField(currentInfoProperty.FindPropertyRelative("targetPhysBoneRole").displayName, HumanPhysBoneRole(core.currentInfo.targetPhysBoneRole, false));
                    EditorGUILayout.PropertyField(currentInfoProperty.FindPropertyRelative("parentBone"));
                    EditorGUILayout.PropertyField(currentInfoProperty.FindPropertyRelative("childBones"));
                    EditorGUILayout.PropertyField(currentInfoProperty.FindPropertyRelative("childPhysBones"));
                }
            }

            if (core.currentInfo?.targetPhysBone)
            {
                var targetPhysBone = core.currentInfo.targetPhysBone;
                if (targetPhysBone.GetRootTransform().parent == targetPhysBone.transform && core.CollectHumanoidBones().Contains(targetPhysBone.transform))
                {
                    EditorGUILayout.HelpBox("Humanoidボーンに刺さっていたDynamicBoneが自動変換されたような構造が検知されました。アバターの前髪がJawボーンとして誤検知されていたりしないか確認してください。\nPhysBoneコンポーネントの修正が必要かもしれません。\nアバターによっては正常なので、その場合はこのメッセージを気にしないでください。", MessageType.Warning);
                }
            }

            HelpLabel("5. Child Phys Bonesの中身が２つ以上入っていたらそれらはコピペコンポーネントなので、動かしたい先にDestinationを設定してボタンを押す");
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("destination"));
            EditorGUILayout.PropertyField(serializedCore.FindPropertyRelative("createDummyParent"));
            if (core.createDummyParent)
            {
                EditorGUILayout.HelpBox("CreateDummyParentをオン（推奨値）にすると、共通の揺れ方をするPhysBoneコンポーネントをダミーの親ボーンにまとめます。ボーン構造が変化するため一部のアニメーションクリップは再設定が必要です。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("CreateDummyParentをオフ（非推奨値）にすると、IgnoreTransformを利用してボーン構造を維持しながらPhysBoneをまとめます。ParentBoneがHumanoidボーンの場合は挙動が壊れるため使わないでください。", core.CollectHumanoidBones().Contains(core.currentInfo?.parentBone) ? MessageType.Error : MessageType.Warning);
            }
            using (new EditorGUI.DisabledScope(core.currentInfo == null))
            {
                var currentRole = core.currentInfo?.targetPhysBoneRole ?? PhysBoneCombiner.PhysBoneRole.Unknown;
                using (new EditorGUI.DisabledScope(currentRole != PhysBoneCombiner.PhysBoneRole.Disassembled))
                {
                    if (GUILayout.Button("Assemble Multi Child"))
                    {
                        core.AssembleMultiChild();
                    }
                }
                if (currentRole == PhysBoneCombiner.PhysBoneRole.Independent)
                {
                    EditorGUILayout.HelpBox("ツールの性質上、１本しかないPhysBoneをまとめることはできません。", MessageType.Warning);
                }
                using (new EditorGUI.DisabledScope(currentRole != PhysBoneCombiner.PhysBoneRole.Composed))
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
            EditorGUILayout.HelpBox("（着せ替えでボーン構造を編集する際は分解しておいた方が安全です）", MessageType.Info);
            HelpLabel("6. または、↓のボタンで一括操作する");
            if (GUILayout.Button("Assemble All Multi Child"))
            {
                core.AssembleAllMultiChild();
            }
            if (GUILayout.Button("Disassemble All Multi Child"))
            {
                core.DisassembleAllMultiChild();
            }
            HelpLabel("7. うまくいくと、VRCPhysBoneのコンポーネント数が減ります");
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
                    case PhysBoneCombiner.PhysBoneRole.Independent:
                        return "1";
                    case PhysBoneCombiner.PhysBoneRole.Unknown:
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
                    case PhysBoneCombiner.PhysBoneRole.Independent:
                        return "1 Independent";
                    case PhysBoneCombiner.PhysBoneRole.Unknown:
                    default:
                        return "? Unknown";
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