using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Genesis.Synth.Editor
{
    /// <summary>
    /// Editor wizard: Synth > Create Synth from Humanoid.
    /// Walks through model selection, bone discovery, joint config, mass
    /// distribution, and generates SynthData + prefab ready for simulation.
    /// </summary>
    public class SynthSetupWizard : EditorWindow
    {
        private enum WizardStep
        {
            SelectModel,
            BoneDiscovery,
            JointConfig,
            MassDistribution,
            Generate
        }

        [MenuItem("Synth/Create Synth from Humanoid")]
        public static void ShowWindow()
        {
            var win = GetWindow<SynthSetupWizard>("Synth Setup Wizard");
            win.Reset();
            win.minSize = new Vector2(520, 640);
        }

        private void Reset()
        {
            step = WizardStep.SelectModel;
            sourceModel = null;
            animator = null;
            boneMapper = null;
            discoveredBones.Clear();
            bodyMeshIndex = 0;
            meshRenderers = null;
            meshLoad = null;
            meshActivateOnBind = null;
            outputFolder = "Assets/Synth/NewSynth";
            synthName = "NewSynth";
            totalMass = 55f;
            loadedConfig = null;
            skeletonType = Genesis.Sentience.Synth.SkeletonType.Auto;
            detectedType = Genesis.Sentience.Synth.SkeletonType.Generic;
            scrollPos = Vector2.zero;
        }

        // --- State ---
        private WizardStep step = WizardStep.SelectModel;
        private GameObject sourceModel;
        private Animator animator;
        private Genesis.Sentience.Synth.SynthBoneMapper boneMapper;

        // Bone discovery results
        private List<DiscoveredBone> discoveredBones = new();
        private int bodyMeshIndex;
        private SkinnedMeshRenderer[] meshRenderers;
        private bool[] meshLoad;
        private bool[] meshActivateOnBind;

        // Generation
        private string outputFolder = "Assets/Synth/NewSynth";
        private string synthName = "NewSynth";
        private float totalMass = 55f;

        // Loaded config (if one exists next to the model)
        private Genesis.Sentience.Synth.SynthCreationConfig loadedConfig;

        // Skeleton type (auto-detected or user-overridden)
        private Genesis.Sentience.Synth.SkeletonType skeletonType = Genesis.Sentience.Synth.SkeletonType.Auto;
        private Genesis.Sentience.Synth.SkeletonType detectedType = Genesis.Sentience.Synth.SkeletonType.Generic;

        private Vector2 scrollPos;

        private struct DiscoveredBone
        {
            public Genesis.Sentience.Synth.SynthBone bone;
            public Transform transform;
            public string displayName;
            public bool detected; // true = Avatar, false = pattern
            public bool include;
            public bool physicsOnly;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            DrawStepHeader();
            EditorGUILayout.Space(4);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            switch (step)
            {
                case WizardStep.SelectModel: DrawSelectModel(); break;
                case WizardStep.BoneDiscovery: DrawBoneDiscovery(); break;
                case WizardStep.JointConfig: DrawJointConfig(); break;
                case WizardStep.MassDistribution: DrawMassDistribution(); break;
                case WizardStep.Generate: DrawGenerate(); break;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            DrawNavigation();
        }

        // --- Step Header ---
        private void DrawStepHeader()
        {
            string[] labels = { "1. Select Model", "2. Bone Discovery", "3. Joint Config", "4. Mass", "5. Generate" };
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < labels.Length; i++)
            {
                bool isCurrent = (int)step == i;
                var style = isCurrent ? EditorStyles.boldLabel : EditorStyles.miniLabel;
                var color = GUI.color;
                if (isCurrent) GUI.color = new Color(0.3f, 0.8f, 1f);
                GUILayout.Label(labels[i], style, GUILayout.Width(100));
                GUI.color = color;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        // --- Step 1: Select Model ---
        private void DrawSelectModel()
        {
            EditorGUILayout.LabelField("Select a Humanoid model to create a Synth from.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(8);

            var newModel = (GameObject)EditorGUILayout.ObjectField("Humanoid Model", sourceModel, typeof(GameObject), true);
            if (newModel != sourceModel)
            {
                sourceModel = newModel;
                ValidateModel();
            }

            if (sourceModel == null)
            {
                EditorGUILayout.HelpBox("Drag a GameObject with an Animator + Humanoid Avatar.", MessageType.Info);
                return;
            }

            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                EditorGUILayout.HelpBox("The selected model does not have a valid Humanoid Avatar. " +
                    "Please configure the model's Rig as Humanoid in the import settings.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Avatar", animator.avatar.name);
            EditorGUILayout.LabelField("Status", "Valid Humanoid Avatar", EditorStyles.boldLabel);

            EditorGUILayout.Space(4);
            skeletonType = (Genesis.Sentience.Synth.SkeletonType)EditorGUILayout.EnumPopup(
                "Skeleton Type", skeletonType);
            if (skeletonType == Genesis.Sentience.Synth.SkeletonType.Auto)
                EditorGUILayout.LabelField("  Detected:", detectedType.ToString(), EditorStyles.miniLabel);

            if (loadedConfig != null)
                EditorGUILayout.HelpBox("Loaded saved configuration. Settings below reflect your previous wizard run.", MessageType.Info);

            // Mesh selection
            if (meshRenderers != null && meshRenderers.Length > 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Skinned Meshes", EditorStyles.boldLabel);

                // Header row
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                EditorGUILayout.LabelField("Mesh", EditorStyles.miniLabel, GUILayout.MinWidth(140));
                EditorGUILayout.LabelField("Verts", EditorStyles.miniLabel, GUILayout.Width(50));
                EditorGUILayout.LabelField("Load", EditorStyles.miniLabel, GUILayout.Width(35));
                EditorGUILayout.LabelField("Show", EditorStyles.miniLabel, GUILayout.Width(35));
                EditorGUILayout.LabelField("Body", EditorStyles.miniLabel, GUILayout.Width(35));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < meshRenderers.Length; i++)
                {
                    var r = meshRenderers[i];
                    int verts = r.sharedMesh?.vertexCount ?? 0;
                    bool isBody = (i == bodyMeshIndex);

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField(r.name, GUILayout.MinWidth(140));
                    EditorGUILayout.LabelField(verts.ToString(), GUILayout.Width(50));
                    meshLoad[i] = EditorGUILayout.Toggle(meshLoad[i], GUILayout.Width(35));
                    meshActivateOnBind[i] = EditorGUILayout.Toggle(meshActivateOnBind[i], GUILayout.Width(35));

                    bool newIsBody = EditorGUILayout.Toggle(isBody, GUILayout.Width(35));
                    if (newIsBody && !isBody) bodyMeshIndex = i;
                    EditorGUILayout.EndHorizontal();
                }

                // Body mesh must be loaded
                meshLoad[bodyMeshIndex] = true;
                meshActivateOnBind[bodyMeshIndex] = true;
            }

            EditorGUILayout.Space(8);
            synthName = EditorGUILayout.TextField("Synth Name", synthName);
            totalMass = EditorGUILayout.FloatField("Total Mass (kg)", totalMass);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        }

        private void ValidateModel()
        {
            animator = null;
            boneMapper = null;
            loadedConfig = null;
            discoveredBones.Clear();

            if (sourceModel == null) return;
            animator = sourceModel.GetComponent<Animator>();
            if (animator == null) animator = sourceModel.GetComponentInChildren<Animator>();

            meshRenderers = sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>();
            meshLoad = new bool[meshRenderers.Length];
            meshActivateOnBind = new bool[meshRenderers.Length];
            if (meshRenderers.Length > 0)
            {
                int maxVerts = 0;
                for (int i = 0; i < meshRenderers.Length; i++)
                {
                    meshLoad[i] = true;
                    meshActivateOnBind[i] = true;
                    int verts = meshRenderers[i].sharedMesh?.vertexCount ?? 0;
                    if (verts > maxVerts) { maxVerts = verts; bodyMeshIndex = i; }
                }
            }

            if (!string.IsNullOrEmpty(sourceModel.name))
            {
                synthName = sourceModel.name + "Synth";
                outputFolder = $"Assets/Synth/{sourceModel.name}";
            }

            detectedType = Genesis.Sentience.Synth.HumanoidAdapter.DetectType(sourceModel.transform);

            // Try loading an existing config saved next to the model asset
            var modelAsset = sourceModel;
            if (!EditorUtility.IsPersistent(sourceModel))
                modelAsset = PrefabUtility.GetCorrespondingObjectFromOriginalSource(sourceModel);

            if (modelAsset != null)
            {
                string modelPath = AssetDatabase.GetAssetPath(modelAsset);
                if (!string.IsNullOrEmpty(modelPath))
                {
                    string configPath = Genesis.Sentience.Synth.SynthCreationConfig.GetConfigPath(modelPath);
                    loadedConfig = AssetDatabase.LoadAssetAtPath<Genesis.Sentience.Synth.SynthCreationConfig>(configPath);

                    if (loadedConfig != null)
                    {
                        Debug.Log($"SynthSetupWizard: Loaded existing config from {configPath}");
                        synthName = loadedConfig.synthName;
                        totalMass = loadedConfig.totalMass;
                        skeletonType = loadedConfig.skeletonType;

                        if (!string.IsNullOrEmpty(loadedConfig.outputFolder))
                            outputFolder = loadedConfig.outputFolder;

                        foreach (var me in loadedConfig.meshEntries)
                        {
                            for (int i = 0; i < meshRenderers.Length; i++)
                            {
                                if (meshRenderers[i].name == me.meshName)
                                {
                                    meshLoad[i] = me.load;
                                    meshActivateOnBind[i] = me.activateOnBind;
                                    if (me.isBody) bodyMeshIndex = i;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        // --- Step 2: Bone Discovery ---
        private void DrawBoneDiscovery()
        {
            if (boneMapper == null)
                RunBoneDiscovery();

            int includedUnique = discoveredBones.Count(b => b.include);
            int includedPaired = discoveredBones.Count(b => b.include && IsPairedBone(b.bone));
            int totalBones = includedUnique + includedPaired;
            EditorGUILayout.LabelField($"Discovered {totalBones} bones " +
                                       $"({includedUnique} unique, {includedPaired} mirrored L/R). " +
                                       $"({discoveredBones.Count(b => b.detected && b.include)} via Avatar, " +
                                       $"{discoveredBones.Count(b => !b.detected && b.include)} via patterns).",
                                       EditorStyles.wordWrappedLabel);
            EditorGUILayout.HelpBox("Left-side bones are shown once and mirrored to the right side automatically.", MessageType.Info);
            EditorGUILayout.Space(8);

            // Group by category
            DrawBoneGroup("Spine", b => IsSpineBone(b.bone));
            DrawBoneGroup("Left Arm", b => IsLeftArmBone(b.bone));
            DrawBoneGroup("Left Leg", b => IsLeftLegBone(b.bone));
            DrawBoneGroup("Extended", b => IsExtendedBone(b.bone));
            DrawBoneGroup("Other", b => !IsSpineBone(b.bone) && !IsLeftArmBone(b.bone) &&
                                        !IsLeftLegBone(b.bone) && !IsExtendedBone(b.bone));
        }

        private void RunBoneDiscovery()
        {
            boneMapper = Genesis.Sentience.Synth.SynthBoneMapper.Create(animator);
            if (boneMapper != null && skeletonType != Genesis.Sentience.Synth.SkeletonType.Auto)
                boneMapper.SetAdapter(skeletonType);
            discoveredBones.Clear();

            // Build lookup from saved config for restoring bone customizations
            Dictionary<string, Genesis.Sentience.Synth.SynthCreationConfig.BoneEntry> savedBones = null;
            if (loadedConfig != null && loadedConfig.boneEntries.Count > 0)
            {
                savedBones = new Dictionary<string, Genesis.Sentience.Synth.SynthCreationConfig.BoneEntry>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var be in loadedConfig.boneEntries)
                    savedBones[be.synthBoneName] = be;
            }

            foreach (var kvp in boneMapper.BoneMap)
            {
                var info = Genesis.Sentience.Synth.SynthBoneCatalog.Get(kvp.Key);
                if (info.Side == Genesis.Sentience.Synth.BoneSide.Right) continue;

                bool include = true;
                bool physicsOnly = info.DefaultPhysicsOnly;

                if (savedBones != null && savedBones.TryGetValue(kvp.Key.ToString(), out var saved))
                {
                    include = saved.include;
                    physicsOnly = saved.physicsOnly;
                }

                discoveredBones.Add(new DiscoveredBone
                {
                    bone = kvp.Key,
                    transform = kvp.Value,
                    displayName = info.CanonicalName ?? kvp.Key.ToString(),
                    detected = info.UnityMapping.HasValue,
                    include = include,
                    physicsOnly = physicsOnly
                });
            }

            discoveredBones.Sort((a, b) => ((int)a.bone).CompareTo((int)b.bone));
        }

        private void DrawBoneGroup(string groupName, Func<DiscoveredBone, bool> filter)
        {
            var bones = discoveredBones.Where(filter).ToList();
            if (bones.Count == 0) return;

            EditorGUILayout.LabelField(groupName, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < discoveredBones.Count; i++)
            {
                if (!filter(discoveredBones[i])) continue;
                var b = discoveredBones[i];
                EditorGUILayout.BeginHorizontal();

                var color = b.detected ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.9f, 0.3f);
                var prevColor = GUI.color;
                GUI.color = color;
                GUILayout.Label(b.detected ? "\u2713" : "\u25CB", GUILayout.Width(16));
                GUI.color = prevColor;

                b.include = EditorGUILayout.ToggleLeft(b.displayName + " (" + (b.transform != null ? b.transform.name : "?") + ")",
                    b.include, GUILayout.Width(280));
                b.physicsOnly = EditorGUILayout.ToggleLeft("Physics Only", b.physicsOnly, GUILayout.Width(100));

                discoveredBones[i] = b;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // --- Step 3: Joint Config ---
        private void DrawJointConfig()
        {
            EditorGUILayout.LabelField("Joint limits are auto-generated from Avatar muscle definitions. " +
                "Fine-tune after generation in the BoneJointsData asset.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(8);

            EditorGUILayout.HelpBox("Default joint limits will be applied based on standard humanoid ranges. " +
                "You can adjust them in the generated BoneJointsData asset after creation.", MessageType.Info);

            foreach (var b in discoveredBones.Where(b => b.include && !b.physicsOnly))
            {
                EditorGUILayout.LabelField($"  {b.displayName}: standard limits", EditorStyles.miniLabel);
            }
        }

        // --- Step 4: Mass Distribution ---
        private void DrawMassDistribution()
        {
            EditorGUILayout.LabelField("Mass will be automatically distributed based on mesh vertex weights. " +
                "Absent optional bones redistribute mass to their parent.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(8);

            totalMass = EditorGUILayout.FloatField("Total Mass (kg)", totalMass);
            if (totalMass < 1f) totalMass = 1f;

            EditorGUILayout.HelpBox("Mass distribution is computed from the body mesh's bone weights. " +
                "Fine-tune in the generated SynthBodyData asset.", MessageType.Info);
        }

        // --- Step 5: Generate ---
        private void DrawGenerate()
        {
            EditorGUILayout.LabelField("Ready to generate Synth assets and prefab.", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Model", sourceModel?.name ?? "None");
            EditorGUILayout.LabelField("Synth Name", synthName);
            EditorGUILayout.LabelField("Output Folder", outputFolder);

            if (meshRenderers != null)
            {
                int loadedCount = meshLoad.Count(l => l);
                string bodyName = meshRenderers[bodyMeshIndex].name;
                EditorGUILayout.LabelField("Meshes", $"{loadedCount}/{meshRenderers.Length} loaded, body = {bodyName}");
            }

            int uniqueBones = discoveredBones.Count(b => b.include);
            int pairedBones = discoveredBones.Count(b => b.include && IsPairedBone(b.bone));
            EditorGUILayout.LabelField("Included Bones", $"{uniqueBones + pairedBones} ({uniqueBones} unique + {pairedBones} mirrored)");
            EditorGUILayout.LabelField("Physics-Only Bones", discoveredBones.Count(b => b.include && b.physicsOnly).ToString());
            EditorGUILayout.LabelField("Total Mass", $"{totalMass:F1} kg");
            EditorGUILayout.Space(16);

            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Generate Synth", GUILayout.Height(40)))
            {
                EditorApplication.delayCall += GenerateSynth;
            }
            GUI.backgroundColor = Color.white;
        }

        // --- Navigation ---
        private void DrawNavigation()
        {
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = step > WizardStep.SelectModel;
            if (GUILayout.Button("< Back", GUILayout.Width(80)))
                step--;
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            bool canAdvance = step switch
            {
                WizardStep.SelectModel => sourceModel != null && animator != null && animator.avatar != null && animator.avatar.isHuman,
                WizardStep.Generate => false,
                _ => true
            };
            GUI.enabled = canAdvance;
            if (GUILayout.Button("Next >", GUILayout.Width(80)))
                step++;
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        // --- Generation ---
        private void GenerateSynth()
        {
            try
            {
                // Resolve scene instance to its source prefab/FBX asset.
                var modelAsset = sourceModel;
                if (!EditorUtility.IsPersistent(sourceModel))
                {
                    modelAsset = PrefabUtility.GetCorrespondingObjectFromOriginalSource(sourceModel);
                    if (modelAsset == null)
                    {
                        EditorUtility.DisplayDialog("Error",
                            "The selected model is a scene-only object with no source prefab or FBX.\n" +
                            "Please use a prefab or imported model (FBX/glTF) as the source.",
                            "OK");
                        return;
                    }
                }

                // Build a raw bone name → SynthBone lookup for physics-only marking
                var physicsOnlyBones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (boneMapper != null)
                {
                    foreach (var b in discoveredBones)
                    {
                        if (!b.include || !b.physicsOnly) continue;
                        var t = boneMapper.GetTransform(b.bone);
                        if (t != null) physicsOnlyBones.Add(t.name);
                        // Also add the right-side mirror
                        var info = Genesis.Sentience.Synth.SynthBoneCatalog.Get(b.bone);
                        if (info.MirrorBone.HasValue)
                        {
                            var mt = boneMapper.GetTransform(info.MirrorBone.Value);
                            if (mt != null) physicsOnlyBones.Add(mt.name);
                        }
                    }
                }

                GenerateSynthAssets(modelAsset, sourceModel, animator, synthName,
                    outputFolder, totalMass, bodyMeshIndex, meshLoad, meshActivateOnBind,
                    boneMapper, physicsOnlyBones);

                SaveConfig(modelAsset);

                int boneCount = discoveredBones.Count(b => b.include);
                Debug.Log($"SynthSetupWizard: Generated Synth '{synthName}' with " +
                          $"{boneCount} bones at {outputFolder}");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to create Synth: {e.Message}", "OK");
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Persist the current wizard configuration as a SynthCreationConfig asset
        /// next to the source model so quick-create can reuse it.
        /// </summary>
        private void SaveConfig(GameObject modelAsset)
        {
            string modelPath = AssetDatabase.GetAssetPath(modelAsset);
            if (string.IsNullOrEmpty(modelPath)) return;

            string configPath = Genesis.Sentience.Synth.SynthCreationConfig.GetConfigPath(modelPath);
            var config = AssetDatabase.LoadAssetAtPath<Genesis.Sentience.Synth.SynthCreationConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<Genesis.Sentience.Synth.SynthCreationConfig>();
                AssetDatabase.CreateAsset(config, configPath);
            }

            config.synthName = synthName;
            config.outputFolder = outputFolder;
            config.totalMass = totalMass;
            config.skeletonType = skeletonType;

            config.meshEntries.Clear();
            if (meshRenderers != null)
            {
                for (int i = 0; i < meshRenderers.Length; i++)
                {
                    config.meshEntries.Add(new Genesis.Sentience.Synth.SynthCreationConfig.MeshEntry
                    {
                        meshName = meshRenderers[i].name,
                        load = meshLoad[i],
                        activateOnBind = meshActivateOnBind[i],
                        isBody = (i == bodyMeshIndex)
                    });
                }
            }

            config.boneEntries.Clear();
            foreach (var b in discoveredBones)
            {
                config.boneEntries.Add(new Genesis.Sentience.Synth.SynthCreationConfig.BoneEntry
                {
                    synthBoneName = b.bone.ToString(),
                    include = b.include,
                    physicsOnly = b.physicsOnly
                });
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"SynthSetupWizard: Saved creation config to {configPath}");
        }

        // ---------- One-click creation from Project window ----------

        [MenuItem("Assets/Create/Synth from Humanoid", false, 90)]
        private static void QuickCreateSynth()
        {
            CreateSynthWithDefaults(Selection.activeGameObject);
        }

        [MenuItem("Assets/Create/Synth from Humanoid", true)]
        private static bool QuickCreateSynthValidate()
        {
            var selected = Selection.activeGameObject;
            if (selected == null || !EditorUtility.IsPersistent(selected)) return false;
            var anim = selected.GetComponent<Animator>();
            if (anim == null) anim = selected.GetComponentInChildren<Animator>();
            return anim != null && anim.avatar != null && anim.avatar.isHuman;
        }

        /// <summary>
        /// Create a Synth from a humanoid model. Looks for a SynthCreationConfig
        /// asset next to the model (saved by a previous wizard run) and uses those
        /// settings. Falls back to sensible defaults if no config exists.
        /// </summary>
        public static void CreateSynthWithDefaults(GameObject model)
        {
            if (model == null) return;

            var modelAsset = model;
            if (!EditorUtility.IsPersistent(model))
            {
                modelAsset = PrefabUtility.GetCorrespondingObjectFromOriginalSource(model);
                if (modelAsset == null)
                {
                    EditorUtility.DisplayDialog("Error",
                        "Selected model is a scene-only object with no source prefab or FBX.", "OK");
                    return;
                }
            }

            var anim = modelAsset.GetComponent<Animator>();
            if (anim == null) anim = modelAsset.GetComponentInChildren<Animator>();
            if (anim == null || anim.avatar == null || !anim.avatar.isHuman)
            {
                EditorUtility.DisplayDialog("Error",
                    "Model does not have a valid Humanoid Avatar.\n" +
                    "Configure the model's Rig as Humanoid in import settings.", "OK");
                return;
            }

            // Try loading saved config
            string modelPath = AssetDatabase.GetAssetPath(modelAsset);
            Genesis.Sentience.Synth.SynthCreationConfig config = null;
            if (!string.IsNullOrEmpty(modelPath))
            {
                string configPath = Genesis.Sentience.Synth.SynthCreationConfig.GetConfigPath(modelPath);
                config = AssetDatabase.LoadAssetAtPath<Genesis.Sentience.Synth.SynthCreationConfig>(configPath);
                if (config != null)
                    Debug.Log($"Quick Create: Using saved config from {configPath}");
            }

            // --- Resolve settings from config or defaults ---
            string synthName = config != null ? config.synthName : modelAsset.name + "Synth";
            string folder = config != null && !string.IsNullOrEmpty(config.outputFolder)
                ? config.outputFolder
                : $"Assets/Synth/{modelAsset.name}";
            float mass = config != null ? config.totalMass : 55f;

            var meshRenderers = modelAsset.GetComponentsInChildren<SkinnedMeshRenderer>();
            int bodyIdx = 0;
            var load = new bool[meshRenderers.Length];
            var activate = new bool[meshRenderers.Length];

            Dictionary<string, Genesis.Sentience.Synth.SynthCreationConfig.MeshEntry> meshLookup = null;
            if (config != null && config.meshEntries.Count > 0)
            {
                meshLookup = new Dictionary<string, Genesis.Sentience.Synth.SynthCreationConfig.MeshEntry>();
                foreach (var me in config.meshEntries)
                    meshLookup[me.meshName] = me;
            }

            bool bodyResolved = false;
            int maxVerts = 0;
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                if (meshLookup != null && meshLookup.TryGetValue(meshRenderers[i].name, out var me))
                {
                    load[i] = me.load;
                    activate[i] = me.activateOnBind;
                    if (me.isBody) { bodyIdx = i; bodyResolved = true; }
                }
                else
                {
                    load[i] = true;
                    activate[i] = true;
                }
                if (!bodyResolved)
                {
                    int verts = meshRenderers[i].sharedMesh?.vertexCount ?? 0;
                    if (verts > maxVerts) { maxVerts = verts; bodyIdx = i; }
                }
            }

            var mapper = Genesis.Sentience.Synth.SynthBoneMapper.Create(anim);

            // Apply skeleton type from config if user overrode it
            if (config != null && config.skeletonType != Genesis.Sentience.Synth.SkeletonType.Auto && mapper != null)
                mapper.SetAdapter(config.skeletonType);

            // Resolve physics-only bones from config or catalog defaults
            var physicsOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (config != null && config.boneEntries.Count > 0 && mapper != null)
            {
                foreach (var be in config.boneEntries)
                {
                    if (!be.physicsOnly) continue;
                    if (Enum.TryParse<Genesis.Sentience.Synth.SynthBone>(be.synthBoneName, out var synthBone))
                    {
                        var t = mapper.GetTransform(synthBone);
                        if (t != null) physicsOnly.Add(t.name);
                        var info = Genesis.Sentience.Synth.SynthBoneCatalog.Get(synthBone);
                        if (info.MirrorBone.HasValue)
                        {
                            var mt = mapper.GetTransform(info.MirrorBone.Value);
                            if (mt != null) physicsOnly.Add(mt.name);
                        }
                    }
                }
            }
            else if (mapper != null)
            {
                foreach (var kvp in mapper.BoneMap)
                {
                    var info = Genesis.Sentience.Synth.SynthBoneCatalog.Get(kvp.Key);
                    if (info.DefaultPhysicsOnly && kvp.Value != null)
                        physicsOnly.Add(kvp.Value.name);
                }
            }

            try
            {
                GenerateSynthAssets(modelAsset, modelAsset, anim, synthName, folder,
                    mass, bodyIdx, load, activate, mapper, physicsOnly);
                Debug.Log($"Quick Create: Generated Synth '{synthName}' at {folder}");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to create Synth: {e.Message}", "OK");
                Debug.LogException(e);
            }
        }

        // ---------- Core generation (shared by wizard and quick-create) ----------

        /// <summary>
        /// Creates all Synth assets (SynthModel, BoneJointsData, SynthBodyData,
        /// SynthData) and the prefab. Ensures the output folder exists and
        /// overwrites any existing assets at the same paths.
        /// </summary>
        private static void GenerateSynthAssets(
            GameObject modelAsset, GameObject sourceForInstantiate, Animator anim,
            string synthName, string outputFolder, float totalMass,
            int bodyMeshIndex, bool[] meshLoad, bool[] meshActivateOnBind,
            Genesis.Sentience.Synth.SynthBoneMapper boneMapper,
            HashSet<string> physicsOnlyBoneNames)
        {
            EnsureFolderExists(outputFolder);

            // 1. SynthModel
            var synthModel = ScriptableObject.CreateInstance<Genesis.Sentience.Synth.SynthModel>();
            synthModel.humanModel = modelAsset;
            synthModel.bodyMeshIndex = bodyMeshIndex;
            AssetDatabase.CreateAsset(synthModel, $"{outputFolder}/{synthName}Model.asset");
            synthModel.BindHumanModel();

            if (synthModel.skinnedMeshRenderers == null || synthModel.skinnedMeshRenderers.Count == 0)
            {
                EditorUtility.DisplayDialog("Error",
                    "Could not detect skinned mesh renderers on the model.\n" +
                    "Make sure the source model has SkinnedMeshRenderer components.", "OK");
                return;
            }

            for (int i = 0; i < synthModel.skinnedMeshRenderers.Count && i < meshLoad.Length; i++)
            {
                var entry = synthModel.skinnedMeshRenderers[i];
                entry.load = meshLoad[i];
                entry.activateOnBind = meshActivateOnBind[i];
                synthModel.skinnedMeshRenderers[i] = entry;
            }
            EditorUtility.SetDirty(synthModel);
            AssetDatabase.SaveAssets();

            // 2. BoneJointsData
            var boneJointsData = ScriptableObject.CreateInstance<Genesis.Sentience.Synth.BoneJointsData>();
            boneJointsData.humanModel = synthModel;
            AssetDatabase.CreateAsset(boneJointsData, $"{outputFolder}/{synthName}BoneJoints.asset");
            boneJointsData.BindHumanModel();

            if (boneJointsData.boneData != null && physicsOnlyBoneNames != null)
            {
                foreach (var bj in boneJointsData.boneData)
                {
                    if (physicsOnlyBoneNames.Contains(bj.bone))
                        bj.physicsOnly = true;
                }
            }
            EditorUtility.SetDirty(boneJointsData);
            AssetDatabase.SaveAssets();

            // 3. SynthBodyData
            var bodyData = ScriptableObject.CreateInstance<Genesis.Sentience.Synth.SynthBodyData>();
            bodyData.humanModel = synthModel;
            bodyData.mass = totalMass;
            AssetDatabase.CreateAsset(bodyData, $"{outputFolder}/{synthName}BodyData.asset");
            bodyData.BindHumanModel();

            ApplyMassDefaults(bodyData, synthModel, anim);
            FlagEyeBones(bodyData, boneMapper);

            EditorUtility.SetDirty(bodyData);
            AssetDatabase.SaveAssets();

            // 4. SynthData — wire references then persist
            var synthData = ScriptableObject.CreateInstance<Genesis.Sentience.Synth.SynthData>();
            var so = new SerializedObject(synthData);
            so.FindProperty("humanModel").objectReferenceValue = synthModel;
            so.FindProperty("boneJointsData").objectReferenceValue = boneJointsData;
            so.FindProperty("synthBodyData").objectReferenceValue = bodyData;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(synthData, $"{outputFolder}/{synthName}Data.asset");

            // 5. Prefab
            var prefabInstance = Instantiate(sourceForInstantiate);
            prefabInstance.name = synthName;

            var skinnedRenderers = prefabInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = skinnedRenderers.Length - 1; i >= 0; i--)
                DestroyImmediate(skinnedRenderers[i].gameObject);

            var prefabAnimator = prefabInstance.GetComponent<Animator>();
            if (prefabAnimator != null)
            {
                var adapter = boneMapper?.Adapter
                    ?? Genesis.Sentience.Synth.HumanoidAdapter.Create(
                        Genesis.Sentience.Synth.SkeletonType.Auto, prefabAnimator.transform);
                adapter.SetupSkeletonRoot(prefabInstance, prefabAnimator);
                prefabAnimator.enabled = false;
            }

            if (prefabInstance.GetComponent<Genesis.Sentience.Synth.SynthEntity>() == null)
                prefabInstance.AddComponent<Genesis.Sentience.Synth.SynthEntity>();
            if (prefabInstance.GetComponent<Genesis.Sentience.Synth.SynthBrain>() == null)
                prefabInstance.AddComponent<Genesis.Sentience.Synth.SynthBrain>();
            if (prefabInstance.GetComponent<Genesis.Sentience.Synth.SynthMotorSystem>() == null)
                prefabInstance.AddComponent<Genesis.Sentience.Synth.SynthMotorSystem>();
            if (prefabInstance.GetComponent<Genesis.Sentience.Synth.SynthProprioception>() == null)
                prefabInstance.AddComponent<Genesis.Sentience.Synth.SynthProprioception>();
            if (prefabInstance.GetComponent<Genesis.Sentience.Synth.SynthVision>() == null)
                prefabInstance.AddComponent<Genesis.Sentience.Synth.SynthVision>();
            if (prefabInstance.GetComponent<Genesis.Sentience.Synth.SynthAuditory>() == null)
                prefabInstance.AddComponent<Genesis.Sentience.Synth.SynthAuditory>();
            if (prefabInstance.GetComponent<Genesis.Sentience.Synth.SynthContact>() == null)
                prefabInstance.AddComponent<Genesis.Sentience.Synth.SynthContact>();

            var modelRoot = new GameObject("modelRoot");
            modelRoot.transform.SetParent(prefabInstance.transform, false);

            var actuators = new GameObject("Actuators");
            actuators.transform.SetParent(prefabInstance.transform, false);

            string prefabPath = $"{outputFolder}/{synthName}.prefab";
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
            DestroyImmediate(prefabInstance);

            // Set synthData on the saved prefab asset directly — setting it on
            // the temp scene instance before SaveAsPrefabAsset loses the reference.
            var prefabEntity = savedPrefab.GetComponent<Genesis.Sentience.Synth.SynthEntity>();
            if (prefabEntity != null)
            {
                prefabEntity.synthData = synthData;
                EditorUtility.SetDirty(savedPrefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Synth Created",
                $"Successfully created Synth '{synthName}' at {outputFolder}\n\n" +
                "Generated assets:\n" +
                $"  - {synthName}Model.asset\n" +
                $"  - {synthName}BoneJoints.asset\n" +
                $"  - {synthName}BodyData.asset\n" +
                $"  - {synthName}Data.asset\n" +
                $"  - {synthName}.prefab\n\n" +
                "Next steps:\n" +
                "1. Add the prefab to your scene\n" +
                "2. Right-click SynthEntity > BindHumanModel()\n" +
                "3. Fine-tune joints in BoneJointsData, mass in BodyData",
                "OK");

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        /// <summary>Recursively ensures all segments of a Unity asset folder path exist.</summary>
        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string parent = System.IO.Path.GetDirectoryName(folderPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolderExists(parent);

            string folderName = System.IO.Path.GetFileName(folderPath);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        /// <summary>
        /// Set left/right eye bone names in SynthBodyData so that SynthEyeCameras
        /// can find them during BindHumanModel. Uses the mapper to locate eye bones
        /// via SynthBone.LeftEye / RightEye from the Humanoid Avatar.
        /// Eye bones are typically NOT in the body mesh's bone list, so we store
        /// them as explicit name fields rather than relying on BoneMesh flags.
        /// </summary>
        private static void FlagEyeBones(Genesis.Sentience.Synth.SynthBodyData bodyData,
            Genesis.Sentience.Synth.SynthBoneMapper mapper)
        {
            if (mapper == null) return;

            var leftEyeTransform = mapper.GetTransform(Genesis.Sentience.Synth.SynthBone.LeftEye);
            var rightEyeTransform = mapper.GetTransform(Genesis.Sentience.Synth.SynthBone.RightEye);

            bodyData.leftEyeBone = leftEyeTransform != null ? leftEyeTransform.name : null;
            bodyData.rightEyeBone = rightEyeTransform != null ? rightEyeTransform.name : null;

            // Also flag BoneMesh entries if they happen to exist (backward compat)
            if (bodyData.boneData != null)
            {
                if (leftEyeTransform != null)
                {
                    var bd = bodyData.boneData.Find(b => b.bone == leftEyeTransform.name);
                    if (bd != null) bd.isLeftEye = true;
                }
                if (rightEyeTransform != null)
                {
                    var bd = bodyData.boneData.Find(b => b.bone == rightEyeTransform.name);
                    if (bd != null) bd.isRightEye = true;
                }
            }

            if (leftEyeTransform != null || rightEyeTransform != null)
                Debug.Log($"SynthSetupWizard: Eye bones set " +
                          $"(L={bodyData.leftEyeBone ?? "none"}, R={bodyData.rightEyeBone ?? "none"})");
            else
                Debug.LogWarning("SynthSetupWizard: No eye bones found in Avatar — " +
                                 "SynthVision will not work until eye bones are manually set in SynthBodyData");
        }

        /// <summary>
        /// Apply tuned selfMassPercent defaults from SynthMassDefaults to body data entries.
        /// Uses the mapper to translate raw bone names → canonical names for lookup.
        /// </summary>
        private static void ApplyMassDefaults(Genesis.Sentience.Synth.SynthBodyData bodyData,
            Genesis.Sentience.Synth.SynthModel synthModel, Animator anim)
        {
            if (bodyData.boneData == null || bodyData.boneData.Count == 0) return;
            if (anim == null) return;

            var mapper = Genesis.Sentience.Synth.SynthBoneMapper.Create(anim);
            if (mapper == null) return;

            // Build raw bone name → canonical name lookup from the model's skinned mesh
            if (synthModel.skinnedMeshRenderers == null ||
                synthModel.bodyMeshIndex < 0 ||
                synthModel.bodyMeshIndex >= synthModel.skinnedMeshRenderers.Count)
                return;

            var smr = synthModel.skinnedMeshRenderers[synthModel.bodyMeshIndex].skinnedMeshRenderer;
            if (smr == null) return;

            var rawToCanonical = new Dictionary<string, string>();
            foreach (var bone in smr.bones)
            {
                if (bone == null) continue;
                string canonical = mapper.GetCanonicalName(bone) ?? bone.name;
                rawToCanonical[bone.name] = canonical;
            }

            int applied = 0;
            foreach (var bd in bodyData.boneData)
            {
                if (rawToCanonical.TryGetValue(bd.bone, out var canonical))
                {
                    var massInfo = Genesis.Sentience.Synth.SynthMassDefaults.Get(canonical);
                    if (massInfo.HasValue)
                    {
                        bd.selfMassPercent = massInfo.Value.selfMassPercent;
                        applied++;
                    }
                }
            }

            Debug.Log($"SynthSetupWizard: Applied tuned mass defaults to {applied}/{bodyData.boneData.Count} bones");
        }

        // --- Bone Category Helpers ---
        private static bool IsSpineBone(Genesis.Sentience.Synth.SynthBone b) =>
            b is Genesis.Sentience.Synth.SynthBone.Hips or Genesis.Sentience.Synth.SynthBone.Spine
            or Genesis.Sentience.Synth.SynthBone.Chest or Genesis.Sentience.Synth.SynthBone.UpperChest
            or Genesis.Sentience.Synth.SynthBone.Neck or Genesis.Sentience.Synth.SynthBone.Head
            or Genesis.Sentience.Synth.SynthBone.Jaw;

        private static bool IsLeftArmBone(Genesis.Sentience.Synth.SynthBone b) =>
            b is Genesis.Sentience.Synth.SynthBone.LeftShoulder or Genesis.Sentience.Synth.SynthBone.LeftUpperArm
            or Genesis.Sentience.Synth.SynthBone.LeftLowerArm or Genesis.Sentience.Synth.SynthBone.LeftHand;

        private static bool IsLeftLegBone(Genesis.Sentience.Synth.SynthBone b) =>
            b is Genesis.Sentience.Synth.SynthBone.LeftUpperLeg or Genesis.Sentience.Synth.SynthBone.LeftLowerLeg
            or Genesis.Sentience.Synth.SynthBone.LeftFoot or Genesis.Sentience.Synth.SynthBone.LeftToes;

        private static bool IsExtendedBone(Genesis.Sentience.Synth.SynthBone b) =>
            b is Genesis.Sentience.Synth.SynthBone.LeftPectoral or Genesis.Sentience.Synth.SynthBone.RightPectoral
            or Genesis.Sentience.Synth.SynthBone.LeftGluteal or Genesis.Sentience.Synth.SynthBone.RightGluteal;

        private static bool IsPairedBone(Genesis.Sentience.Synth.SynthBone b)
        {
            var info = Genesis.Sentience.Synth.SynthBoneCatalog.Get(b);
            return info.Side == Genesis.Sentience.Synth.BoneSide.Left;
        }
    }
}
