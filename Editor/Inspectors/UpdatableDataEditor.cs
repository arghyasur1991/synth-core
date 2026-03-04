using UnityEngine;
using UnityEditor;

namespace Genesis.Sentience.Synth
{
    [CustomEditor(typeof(UpdatableData), true)]
    public abstract class UpdatableDataEditor: Editor
    {
        protected void OnUpdateGUI()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("autoUpdate"));
            UpdatableData data = (UpdatableData)target;
            if (GUILayout.Button("Update"))
            {
                data.NotifyOnUpdate();
            }
        }
    }
}