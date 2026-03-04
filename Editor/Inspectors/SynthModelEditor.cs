using UnityEngine;
using UnityEditor;

namespace Genesis.Sentience.Synth
{
    [CustomPropertyDrawer(typeof(SynthSkinAttribute))]
    public class SynthSkinDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position,
                                   SerializedProperty property,
                                   GUIContent label)
        {
            property.serializedObject.Update();
            var renderer = property.FindPropertyRelative("skinnedMeshRenderer");
            label.text = renderer.objectReferenceValue.name;

            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.LabelField(position, label.text);
            EditorGUI.PropertyField(new Rect(position.x + 300, position.y, 20, position.height),
                property.FindPropertyRelative("load"), GUIContent.none);
            EditorGUI.PropertyField(new Rect(position.x + 350, position.y, 20, position.height),
                property.FindPropertyRelative("activateOnBind"), GUIContent.none);
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndProperty();
            property.serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(SynthModel), true)]
    public class SynthModelEditor: UpdatableDataEditor
    {
        private SerializedProperty humanModelProperty;
        private SerializedProperty renderersProperty;
        private bool showSkins = true;
        public void OnEnable()
        {
            humanModelProperty = serializedObject.FindProperty("humanModel");
            renderersProperty = serializedObject.FindProperty("skinnedMeshRenderers");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SynthModel data = (SynthModel)target;
            EditorGUILayout.PropertyField(humanModelProperty);
            showSkins = EditorGUILayout.BeginFoldoutHeaderGroup(showSkins, "Synth Skins");
            if (showSkins)
            {
                EditorGUI.indentLevel += 1;
                //var position = EditorGUILayout.BeginHorizontal();
                //EditorGUI.LabelField(position, "Mesh Name");
                //EditorGUI.LabelField(new Rect(position.x + 300, position.y, 20, position.height), "Load");
                //EditorGUI.LabelField(new Rect(position.x + 350, position.y, 20, position.height), "Activate on Bind");
                //EditorGUILayout.EndHorizontal();
                GUIContent[] meshes = new GUIContent[renderersProperty.arraySize];
                for (int i = 0; i < renderersProperty.arraySize; i++)
                {
                    var skin = renderersProperty.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(skin);
                    meshes[i] = new GUIContent(skin.FindPropertyRelative("skinnedMeshRenderer").objectReferenceValue.name);
                }
                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Mesh for body");
                data.bodyMeshIndex = EditorGUILayout.Popup(data.bodyMeshIndex, meshes);
                EditorGUILayout.EndHorizontal();

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