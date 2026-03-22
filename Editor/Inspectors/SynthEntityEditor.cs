using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mujoco;

namespace Genesis.Sentience.Synth
{
    [CustomEditor(typeof(SynthEntity))]
    public class SynthEntityEditor : Editor
    {
        private struct AxisEntry
        {
            public MjHingeJoint Joint;
            public bool Visualizing;
        }

        private struct BoneEntry
        {
            public string DisplayName;
            public Transform BoneTransform;
            public Quaternion OriginalLocalRotation;
            public AxisEntry[] Axes; // [0]=X, [1]=Y, [2]=Z — may have nulls
            public bool Foldout;
            public int BoneJointIndex; // index into BoneJoints list, -1 if not found
        }

        private List<BoneEntry> _bones = new();
        private bool _showVisualizer;
        private float _speed = 1f;
        private string _filter = "";
        private bool _anyVisualizing;
        private SynthBoneMapper _mapper;
        private SynthEntity _entity;
        private double _startTime;

        private void OnEnable()
        {
            _entity = (SynthEntity)target;
            Rebuild();
        }

        private void OnDisable()
        {
            StopAll(restoreRotations: true);
            UnregisterUpdate();
        }

        private void Rebuild()
        {
            StopAll(restoreRotations: true);
            _bones.Clear();
            _mapper = null;

            if (_entity == null || _entity.synthData == null) return;

            var animator = _entity.GetComponent<Animator>();
            if (animator != null)
                _mapper = SynthBoneMapper.Create(animator);

            var boneJoints = _entity.synthData.BoneJoints;
            var hinges = _entity.GetComponentsInChildren<MjHingeJoint>(true);

            var boneMap = new Dictionary<Transform, AxisEntry[]>();
            foreach (var hinge in hinges)
            {
                var boneTf = hinge.transform.parent;
                if (boneTf == null) continue;

                if (!boneMap.TryGetValue(boneTf, out var axes))
                {
                    axes = new AxisEntry[3];
                    boneMap[boneTf] = axes;
                }

                int axisIdx = ParseAxisIndex(hinge.name);
                if (axisIdx >= 0 && axisIdx < 3)
                    axes[axisIdx].Joint = hinge;
            }

            foreach (var kvp in boneMap)
            {
                var boneTf = kvp.Key;
                int bjIdx = FindBoneJointIndex(boneTf, boneJoints);
                string displayName = bjIdx >= 0
                    ? boneJoints[bjIdx].bone
                    : boneTf.name;

                _bones.Add(new BoneEntry
                {
                    DisplayName = displayName,
                    BoneTransform = boneTf,
                    OriginalLocalRotation = boneTf.localRotation,
                    Axes = kvp.Value,
                    Foldout = false,
                    BoneJointIndex = bjIdx,
                });
            }

            _bones.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(12);
            _showVisualizer = EditorGUILayout.BeginFoldoutHeaderGroup(_showVisualizer, "Joint Range Visualizer");
            if (_showVisualizer)
            {
                if (_entity.synthData == null)
                {
                    EditorGUILayout.HelpBox("Assign SynthData to enable the visualizer.", MessageType.Info);
                }
                else if (_bones.Count == 0)
                {
                    EditorGUILayout.HelpBox("No MjHingeJoint components found. Bind the model first.", MessageType.Info);
                    if (GUILayout.Button("Refresh"))
                        Rebuild();
                }
                else
                {
                    DrawVisualizerUI();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawVisualizerUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter", GUILayout.Width(36));
            _filter = EditorGUILayout.TextField(_filter);
            EditorGUILayout.LabelField("Speed", GUILayout.Width(38));
            _speed = EditorGUILayout.Slider(_speed, 0.1f, 3f, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Stop All", GUILayout.Width(70)))
                StopAll(restoreRotations: true);
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                Rebuild();
            if (GUILayout.Button("Reset All to Defaults", GUILayout.Width(140)))
                ResetAllToDefaults();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            bool filterActive = !string.IsNullOrEmpty(_filter);
            for (int i = 0; i < _bones.Count; i++)
            {
                var bone = _bones[i];
                if (filterActive && bone.DisplayName.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (bone.BoneTransform == null) continue;

                EditorGUILayout.BeginHorizontal();
                bone.Foldout = EditorGUILayout.Foldout(bone.Foldout, bone.DisplayName + "  (" + bone.BoneTransform.name + ")", true);
                if (GUILayout.Button("Reset", GUILayout.Width(50)))
                    ResetBoneToDefaults(ref bone);
                EditorGUILayout.EndHorizontal();

                if (bone.Foldout)
                {
                    EditorGUI.indentLevel++;
                    string[] axisLabels = { "X", "Y", "Z" };
                    for (int a = 0; a < 3; a++)
                    {
                        var axis = bone.Axes[a];
                        if (axis.Joint == null) continue;

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(axisLabels[a], GUILayout.Width(20));

                        EditorGUI.BeginChangeCheck();
                        float newLower = EditorGUILayout.FloatField(axis.Joint.RangeLower, GUILayout.Width(50));
                        EditorGUILayout.LabelField("to", GUILayout.Width(16));
                        float newUpper = EditorGUILayout.FloatField(axis.Joint.RangeUpper, GUILayout.Width(50));
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(axis.Joint, "Edit Joint Range");
                            axis.Joint.RangeLower = newLower;
                            axis.Joint.RangeUpper = newUpper;
                            EditorUtility.SetDirty(axis.Joint);
                        }

                        bool wasViz = axis.Visualizing;
                        bool nowViz = GUILayout.Toggle(wasViz, "Visualize", "Button", GUILayout.Width(65));
                        if (nowViz != wasViz)
                        {
                            if (nowViz)
                            {
                                bool hadOther = false;
                                for (int oa = 0; oa < 3; oa++)
                                {
                                    if (oa != a && bone.Axes[oa].Visualizing)
                                    {
                                        bone.Axes[oa].Visualizing = false;
                                        hadOther = true;
                                    }
                                }

                                if (hadOther)
                                    bone.BoneTransform.localRotation = bone.OriginalLocalRotation;
                                else
                                    bone.OriginalLocalRotation = bone.BoneTransform.localRotation;

                                _startTime = EditorApplication.timeSinceStartup;
                            }
                            else
                            {
                                bone.BoneTransform.localRotation = bone.OriginalLocalRotation;
                            }
                            axis.Visualizing = nowViz;
                        }
                        bone.Axes[a] = axis;

                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUI.indentLevel--;
                }
                _bones[i] = bone;
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Apply All to Asset"))
                ApplyAllToAsset();

            UpdateVisualizationState();
        }

        private void UpdateVisualizationState()
        {
            bool any = false;
            for (int i = 0; i < _bones.Count; i++)
            {
                for (int a = 0; a < 3; a++)
                {
                    if (_bones[i].Axes[a].Visualizing)
                    {
                        any = true;
                        break;
                    }
                }
                if (any) break;
            }

            if (any && !_anyVisualizing)
                RegisterUpdate();
            else if (!any && _anyVisualizing)
                UnregisterUpdate();

            _anyVisualizing = any;
        }

        private void RegisterUpdate()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            _startTime = EditorApplication.timeSinceStartup;
        }

        private void UnregisterUpdate()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                StopAll(restoreRotations: true);
        }

        private void OnEditorUpdate()
        {
            if (_entity == null)
            {
                UnregisterUpdate();
                return;
            }

            float t = (float)(EditorApplication.timeSinceStartup - _startTime) * _speed;
            float blend = (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f;

            bool anyActive = false;
            for (int i = 0; i < _bones.Count; i++)
            {
                var bone = _bones[i];
                if (bone.BoneTransform == null) continue;

                for (int a = 0; a < 3; a++)
                {
                    var axis = bone.Axes[a];
                    if (!axis.Visualizing || axis.Joint == null) continue;

                    anyActive = true;
                    float angle = Mathf.Lerp(axis.Joint.RangeLower, axis.Joint.RangeUpper, blend);
                    Vector3 localAxis = axis.Joint.transform.localRotation * Vector3.right;
                    bone.BoneTransform.localRotation = bone.OriginalLocalRotation * Quaternion.AngleAxis(angle, localAxis);
                }
            }

            if (anyActive)
                SceneView.RepaintAll();
            else
                UnregisterUpdate();
        }

        private void StopAll(bool restoreRotations)
        {
            for (int i = 0; i < _bones.Count; i++)
            {
                var bone = _bones[i];
                bool wasActive = false;
                for (int a = 0; a < 3; a++)
                {
                    if (bone.Axes[a].Visualizing) wasActive = true;
                    bone.Axes[a].Visualizing = false;
                }
                if (restoreRotations && wasActive && bone.BoneTransform != null)
                    bone.BoneTransform.localRotation = bone.OriginalLocalRotation;
                _bones[i] = bone;
            }
            _anyVisualizing = false;
            UnregisterUpdate();
        }

        private void ResetAllToDefaults()
        {
            StopAll(restoreRotations: true);
            int count = 0;
            for (int i = 0; i < _bones.Count; i++)
            {
                var bone = _bones[i];
                if (SynthJointDefaults.Get(bone.DisplayName) != null)
                {
                    ResetBoneToDefaults(ref bone);
                    _bones[i] = bone;
                    count++;
                }
            }
            Debug.Log($"SynthEntityEditor: Reset {count} bones to SynthJointDefaults");
        }

        private void ResetBoneToDefaults(ref BoneEntry bone)
        {
            var defaults = SynthJointDefaults.Get(bone.DisplayName);
            if (defaults == null)
            {
                Debug.LogWarning($"No defaults found for '{bone.DisplayName}'");
                return;
            }

            bool needsFlip = _mapper != null && _mapper.NeedsJointRangeFlip(bone.BoneTransform);

            for (int a = 0; a < 3; a++)
            {
                var axis = bone.Axes[a];
                if (axis.Joint == null) continue;

                Undo.RecordObject(axis.Joint, "Reset Joint Range");
                float rl = defaults[a].rangeL;
                float ru = defaults[a].rangeU;
                if (needsFlip && rl != -ru)
                {
                    axis.Joint.RangeLower = -ru;
                    axis.Joint.RangeUpper = -rl;
                }
                else
                {
                    axis.Joint.RangeLower = rl;
                    axis.Joint.RangeUpper = ru;
                }
                EditorUtility.SetDirty(axis.Joint);
            }
        }

        private void ApplyAllToAsset()
        {
            var so = new SerializedObject(_entity.synthData);
            var boneJointsDataProp = so.FindProperty("boneJointsData");
            if (boneJointsDataProp == null || boneJointsDataProp.objectReferenceValue == null)
            {
                Debug.LogWarning("SynthEntityEditor: Cannot find boneJointsData on SynthData");
                return;
            }

            var asset = (BoneJointsData)boneJointsDataProp.objectReferenceValue;
            var boneJoints = asset.boneData;
            if (boneJoints == null) return;

            Undo.RecordObject(asset, "Apply Joint Ranges to Asset");

            int updated = 0;
            for (int i = 0; i < _bones.Count; i++)
            {
                var bone = _bones[i];
                if (bone.BoneJointIndex < 0 || bone.BoneJointIndex >= boneJoints.Count) continue;

                var bj = boneJoints[bone.BoneJointIndex];
                if (bj.boneJointSettings == null || bj.boneJointSettings.Count < 3) continue;

                bool needsFlip = _mapper != null && _mapper.NeedsJointRangeFlip(bone.BoneTransform);

                for (int a = 0; a < 3; a++)
                {
                    var axis = bone.Axes[a];
                    if (axis.Joint == null) continue;

                    var settings = bj.boneJointSettings[a];
                    float sceneL = axis.Joint.RangeLower;
                    float sceneU = axis.Joint.RangeUpper;

                    if (needsFlip && sceneL != -sceneU)
                    {
                        settings.rangeL = -sceneU;
                        settings.rangeU = -sceneL;
                    }
                    else
                    {
                        settings.rangeL = sceneL;
                        settings.rangeU = sceneU;
                    }
                    bj.boneJointSettings[a] = settings;
                }
                updated++;
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"SynthEntityEditor: Updated {updated} bone entries in {asset.name}");
        }

        private int FindBoneJointIndex(Transform boneTf, List<BoneJoint> boneJoints)
        {
            if (_mapper != null)
            {
                string canonical = _mapper.GetCanonicalName(boneTf);
                if (!string.IsNullOrEmpty(canonical))
                {
                    int idx = boneJoints.FindIndex(b =>
                        b.bone.Equals(canonical, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0) return idx;
                }
            }
            return boneJoints.FindIndex(b => boneTf.name.Contains(b.bone));
        }

        private static int ParseAxisIndex(string jointName)
        {
            if (string.IsNullOrEmpty(jointName)) return -1;
            char last = jointName[jointName.Length - 1];
            return last switch
            {
                'X' => 0,
                'Y' => 1,
                'Z' => 2,
                _ => -1,
            };
        }
    }
}
