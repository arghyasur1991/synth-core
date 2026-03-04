#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Genesis.Sentience.Synth.EditorTools
{
    /// <summary>
    /// Automatically rebinds Synth visual meshes and eye cameras after they are lost.
    ///
    /// Visual meshes and eye cameras use HideFlags.DontSave to keep the scene file
    /// small and git-friendly. The tradeoff is these objects don't survive:
    ///   - Play mode → Edit mode transitions (Unity restores only serialized state)
    ///   - Domain reloads (script recompilation in edit mode)
    ///   - Build + deploy (Unity strips DontSave objects during build)
    ///
    /// This script detects the loss and calls BindHumanModel() to restore visuals.
    /// Physics components (MjGeom etc.) are unaffected since they ARE serialized.
    /// </summary>
    [InitializeOnLoad]
    public static class SynthAutoRebind
    {
        static SynthAutoRebind()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.focusChanged += OnEditorFocusChanged;

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RebindIfNeeded;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += RebindIfNeeded;
            }
        }

        private static void OnEditorFocusChanged(bool hasFocus)
        {
            if (hasFocus)
                EditorApplication.delayCall += RebindIfNeeded;
        }

        private static void RebindIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var entities = Object.FindObjectsByType<SynthEntity>(FindObjectsSortMode.None);
            foreach (var entity in entities)
            {
                if (entity.synthData == null) continue;

                bool hasMeshes = entity.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0;
                if (!hasMeshes)
                {
                    Debug.Log($"[SynthAutoRebind] Rebinding '{entity.name}' (visual meshes lost)");
                    entity.BindHumanModel();
                }
            }
        }
    }

    /// <summary>
    /// Post-build callback that rebinds Synth after Unity finishes a build.
    /// The build process strips HideFlags.DontSave objects from the loaded scene.
    /// </summary>
    class SynthPostBuildRebind : IPostprocessBuildWithReport
    {
        public int callbackOrder => 1000;

        public void OnPostprocessBuild(BuildReport report)
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    var entities = Object.FindObjectsByType<SynthEntity>(FindObjectsSortMode.None);
                    foreach (var entity in entities)
                    {
                        if (entity.synthData == null) continue;
                        bool hasMeshes = entity.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0;
                        if (!hasMeshes)
                        {
                            Debug.Log($"[SynthPostBuildRebind] Rebinding '{entity.name}' after build");
                            entity.BindHumanModel();
                        }
                    }
                }
            };
        }
    }
}
#endif
