using UnityEngine;
using UnityEditor;

namespace Genesis.Sentience.Synth
{
    [CustomPropertyDrawer(typeof(BoneJointSettingAttribute))]
    public class BoneJointSettingDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position,
                                   SerializedProperty property,
                                   GUIContent label)
        {
            property.serializedObject.Update();
            var rangeL = property.FindPropertyRelative("rangeL");
            var rangeU = property.FindPropertyRelative("rangeU");
            var stiffness = property.FindPropertyRelative("stiffness");
            var damping = property.FindPropertyRelative("damping");
            var gear = property.FindPropertyRelative("gear");

            EditorGUI.BeginProperty(position, label, property);

            EditorGUILayout.BeginHorizontal();
            int minWidth = 50;
            //GUILayout.FlexibleSpace();
            EditorGUILayout.PropertyField(rangeL, GUIContent.none, true, GUILayout.MinWidth(minWidth));
            EditorGUILayout.PropertyField(rangeU, GUIContent.none, true, GUILayout.MinWidth(minWidth));
            EditorGUILayout.PropertyField(stiffness, GUIContent.none, true, GUILayout.MinWidth(minWidth));
            EditorGUILayout.PropertyField(damping, GUIContent.none, true, GUILayout.MinWidth(minWidth));
            EditorGUILayout.PropertyField(gear, GUIContent.none, true, GUILayout.MinWidth(minWidth));
            EditorGUILayout.EndHorizontal();

            EditorGUI.EndProperty();
            property.serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomPropertyDrawer(typeof(BoneJointAttribute))]
    public class BoneJointDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position,
                                   SerializedProperty property,
                                   GUIContent label)
        {
            property.serializedObject.Update();
            var boneName = property.FindPropertyRelative("bone").stringValue;
            label.text = boneName;

            label = EditorGUI.BeginProperty(position, label, property);

            EditorGUILayout.BeginVertical();

            // Bone name + physicsOnly toggle on the same line
            EditorGUILayout.BeginHorizontal();
            EditorGUI.LabelField(position, label.text);
            var physicsOnly = property.FindPropertyRelative("physicsOnly");
            if (physicsOnly != null)
            {
                GUILayout.FlexibleSpace();
                physicsOnly.boolValue = EditorGUILayout.ToggleLeft(
                    new GUIContent("Physics Only", "Bone has joints for simulation but is not voluntarily controllable (excluded from motor control and RL)"),
                    physicsOnly.boolValue, GUILayout.Width(100));
            }
            EditorGUILayout.EndHorizontal();

            var settingsArray = property.FindPropertyRelative("boneJointSettings");

            if (settingsArray != null)
            {
                var settingsX = settingsArray.GetArrayElementAtIndex(0);
                var settingsY = settingsArray.GetArrayElementAtIndex(1);
                var settingsZ = settingsArray.GetArrayElementAtIndex(2);
                int minWidth = 100;

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.PropertyField(settingsX, GUIContent.none, true, GUILayout.MinWidth(minWidth));
                EditorGUILayout.LabelField("X Axis");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.PropertyField(settingsY, GUIContent.none, true, GUILayout.MinWidth(minWidth));
                EditorGUILayout.LabelField("Y Axis");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.PropertyField(settingsZ, GUIContent.none, true, GUILayout.MinWidth(minWidth));
                EditorGUILayout.LabelField("Z Axis");
                EditorGUILayout.EndHorizontal();
            }

            //EditorGUI.indentLevel -= 1;
            EditorGUILayout.EndVertical();
            EditorGUI.EndProperty();
            property.serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(BoneJointsData), true)]
    public class BoneJointsDataEditor: UpdatableDataEditor
    {
        private SerializedProperty humanModelProperty;
        private SerializedProperty boneJointsProperty;
        private bool showJointSettings = true;
        public void OnEnable()
        {
            humanModelProperty = serializedObject.FindProperty("humanModel");
            boneJointsProperty = serializedObject.FindProperty("boneData");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            BoneJointsData data = (BoneJointsData)target;
            EditorGUILayout.PropertyField(humanModelProperty);
            showJointSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showJointSettings, "Synth Joint Settings");
            if (showJointSettings)
            {
                EditorGUI.indentLevel += 1;
                for (int i = 0; i < boneJointsProperty.arraySize; i++)
                {
                    var joint = boneJointsProperty.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(joint);
                }

                EditorGUI.indentLevel -= 1;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(8);

            GUI.enabled = data.humanModel != null;
            if (GUILayout.Button("Bind Human Model"))
            {
                data.BindHumanModel();
                EditorUtility.SetDirty(data);
            }
            GUI.enabled = true;

            EditorGUILayout.Space(50);
            OnUpdateGUI();
            serializedObject.ApplyModifiedProperties();
        }
    }
}