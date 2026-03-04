#if UNITY_EDITOR
using UnityEditor;

namespace Genesis.Sentience.Synth.EditorTools
{
    /// <summary>
    /// Fixes MissingReferenceException in Inspector during play mode transitions.
    /// Unity's InspectorWindow holds stale references to edit-mode objects and tries
    /// to redraw them after they're destroyed but before play-mode copies exist.
    /// Clearing the selection before entering play mode prevents this.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeSelectionFix
    {
        static PlayModeSelectionFix()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                Selection.activeObject = null;
            }
        }
    }
}
#endif
