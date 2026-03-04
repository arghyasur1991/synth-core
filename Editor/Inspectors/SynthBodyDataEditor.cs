using UnityEngine;
using UnityEditor;

namespace Genesis.Sentience.Synth
{
    [CustomEditor(typeof(SynthBodyData), true)]
    public class SynthBodyDataEditor : UpdatableDataEditor
    {
        private SerializedProperty humanModelProperty;
        private SerializedProperty boneJointsProperty;
        private SerializedProperty massProperty;
        private bool showJointSettings = true;
        public void OnEnable()
        {
            humanModelProperty = serializedObject.FindProperty("humanModel");
            massProperty = serializedObject.FindProperty("mass");
            boneJointsProperty = serializedObject.FindProperty("boneData");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SynthBodyData data = (SynthBodyData)target;
            EditorGUILayout.PropertyField(humanModelProperty);
            EditorGUILayout.PropertyField(massProperty);
            showJointSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showJointSettings, "Synth Body Data");
            if (showJointSettings)
            {
                EditorGUI.indentLevel += 1;
                var level = EditorGUI.indentLevel;
                for (int i = 0; i < boneJointsProperty.arraySize; i++)
                {
                    var body = boneJointsProperty.GetArrayElementAtIndex(i);
                    var boneName = body.FindPropertyRelative("bone").stringValue;
                    if (!data.ShowOnEditor(boneName))
                    {
                        continue;
                    }
                    EditorGUILayout.BeginHorizontal();
                    var parentLevel = body.FindPropertyRelative("parents").arraySize;
                    var spacePrefix = "";
                    for (int j = 0; j < parentLevel - 2; j++)
                    {
                        spacePrefix += "-";
                    }
                    var lockedProperty = body.FindPropertyRelative("locked");
                    lockedProperty.boolValue = EditorGUILayout.Toggle(lockedProperty.boolValue, GUILayout.MaxWidth(25));

                    var showChildren = body.FindPropertyRelative("showChildren");
                    showChildren.boolValue = EditorGUILayout.Toggle(showChildren.boolValue, GUILayout.MaxWidth(25));
                    EditorGUILayout.LabelField(spacePrefix + boneName, GUILayout.MaxWidth(150));

                    if (parentLevel == 2 || data.IsLocked(boneName))
                    {
                        GUI.enabled = false;
                    }
                    float massPercent = data.GetMassPercent(boneName);
                    var options = new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.MaxWidth(200) };
                    float newMassPercent = EditorGUILayout.Slider(massPercent, 0, 100, options);
                    float changeThreshold = 0.01f;
                    if (Mathf.Abs(newMassPercent - massPercent) > changeThreshold)
                    {
                        data.AdjustSiblingMasses(boneName, newMassPercent);
                    }
                    if (data.IsLocked(boneName) && parentLevel > 2)
                    {
                        GUI.enabled = true;
                    }
                    if (data.IsSelfMassLocked(boneName))
                    {
                        GUI.enabled = false;
                    }
                    float selfMassPercent = data.GetSelfMassPercent(boneName);
                    float newSelfMassPercent = EditorGUILayout.Slider(selfMassPercent, 0, 100, options);

                    if (Mathf.Abs(newSelfMassPercent - selfMassPercent) > changeThreshold)
                    {
                        data.SetSelfMassPercent(boneName, newSelfMassPercent);
                    }

                    GUI.enabled = true;
                    
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel = level - 1;
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