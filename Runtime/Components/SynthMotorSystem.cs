using System;
using System.Collections.Generic;
using UnityEngine;
using Mujoco;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Motor system that hooks into MuJoCo's control callback for properly timed
    /// actuator control. The ctrlCallback fires between mj_step1 (forward dynamics)
    /// and mj_step2 (integration), which is the correct time to read sensors and
    /// set actuator controls in MuJoCo's simulation loop.
    /// </summary>
    public class SynthMotorSystem : MonoBehaviour
    {
        [Tooltip("Enable motor control (when disabled, joints are passive)")]
        public bool motorEnabled = false;

        [Tooltip("Apply random forces for testing (overrides RL control)")]
        public bool randomTestMode = false;

        private SynthEntity synthEntity;
        private SynthPhysicalBody synthPhysicalBody;
        private Transform rootBone;
        private List<Transform> controlledBones;
        private bool subscribedToEvents = false;

        // Root body velocity tracking (computed from frame deltas)
        private Vector3 prevRootPosition;
        private Quaternion prevRootRotation;
        private Vector3 rootLinearVelocity;
        private Vector3 rootAngularVelocity;

        // Cached key bone transforms for posture reward computation
        private Transform headBone;
        private Transform chestUpperBone;
        private Transform lFootBone;
        private Transform rFootBone;

        // Reference idle pose (captured at initialization for pose-similarity reward)
        private float[] referencePoseAngles;
        private float referenceHeadHeight;
        private bool referencePoseCaptured = false;

        // Bones marked physicsOnly in BoneJointsData (populated during CacheControlledBones)
        private List<string> physicsOnlyBoneNames = new List<string>();

        // === Unified bone filtering ===
        // Built once after MjScene init from physics-only bones in BoneJointsData.
        // Shared with envs and brain for consistent observation/action dimensions.
        private BoneFilterConfig boneFilter;

        // Pre-allocated observation buffer — avoids GC allocation every decision step.
        // Resized once when boneFilter is built; reused on every GetObservation() call.
        private float[] _obsBuffer;

        // Contact and strain subsystems (initialized after MjScene init)
        private SynthContact _contactSense;
        private StrainComputer _strainComputer;

        // Pending action from brain (applied in ctrlCallback)
        private float[] pendingAction;

        // Per-substep decision callback (set by SynthBrain)
        private int _substepCounter;

        /// <summary>
        /// MuJoCo substep interval between brain decisions. Matches the skill's
        /// frameSkip so the brain decides at the same frequency as training.
        /// Set to 0 to disable per-substep decisions (default).
        /// </summary>
        [System.NonSerialized] public int decisionInterval;

        /// <summary>
        /// Fired every decisionInterval substeps, between mj_step1 and mj_step2.
        /// The brain subscribes to produce new actions at training-matched frequency.
        /// </summary>
        [System.NonSerialized] public System.Action onDecisionNeeded;

        /// <summary>
        /// Bone filter config with included actuator/joint indices.
        /// Built once in OnMjPostInit when MujocoIds are guaranteed valid.
        /// Do NOT lazy-build here — physics-only exclusion requires valid
        /// MujocoIds which are only assigned after MjScene initialization.
        /// </summary>
        public BoneFilterConfig Filter => boneFilter;

        /// <summary>Number of controlled bones (each has 3 joint axes)</summary>
        public int ControlledBoneCount => controlledBones?.Count ?? 0;

        /// <summary>Action dimension = number of included actuators (per-actuator, not per-bone)</summary>
        public int ActionDimension => boneFilter.IsValid ? boneFilter.actDim : 0;

        /// <summary>Physics observation dimension (MuJoCo-level, before task-specific data)</summary>
        public int ObservationDimension => boneFilter.IsValid ? boneFilter.physicsObsDim : 0;

        /// <summary>Current root bone height (y in Unity = up)</summary>
        public float RootHeight => rootBone != null ? rootBone.position.y : 0f;

        /// <summary>Current root bone world position</summary>
        public Vector3 RootPosition => rootBone != null ? rootBone.position : Vector3.zero;

        /// <summary>Current root bone world rotation</summary>
        public Quaternion RootRotation => rootBone != null ? rootBone.rotation : Quaternion.identity;

        /// <summary>Root linear velocity (computed from frame deltas)</summary>
        public Vector3 RootLinearVelocity => rootLinearVelocity;

        /// <summary>Root angular velocity (computed from frame deltas)</summary>
        public Vector3 RootAngularVelocity => rootAngularVelocity;

        // --- Posture properties for reward computation ---

        /// <summary>Head bone height (y). Best indicator of actual standing.</summary>
        public float HeadHeight => headBone != null ? headBone.position.y : 0f;

        /// <summary>Reference head height captured at initialization (standing pose).</summary>
        public float ReferenceHeadHeight => referenceHeadHeight;

        /// <summary>Whether a reference pose has been captured.</summary>
        public bool HasReferencePose => referencePoseCaptured;

        /// <summary>Chest up-vector in world space.</summary>
        public Vector3 ChestUpVector => chestUpperBone != null ? chestUpperBone.up : Vector3.up;

        /// <summary>Head up-vector in world space.</summary>
        public Vector3 HeadUpVector => headBone != null ? headBone.up : Vector3.up;

        /// <summary>Left foot world position.</summary>
        public Vector3 LeftFootPosition => lFootBone != null ? lFootBone.position : Vector3.zero;

        /// <summary>Right foot world position.</summary>
        public Vector3 RightFootPosition => rFootBone != null ? rFootBone.position : Vector3.zero;

        /// <summary>Left foot height (y).</summary>
        public float LeftFootHeight => lFootBone != null ? lFootBone.position.y : 0f;

        /// <summary>Right foot height (y).</summary>
        public float RightFootHeight => rFootBone != null ? rFootBone.position.y : 0f;

        /// <summary>Approximate center-of-mass XZ (uses pelvis position as proxy).</summary>
        public Vector2 CenterOfMassXZ => rootBone != null
            ? new Vector2(rootBone.position.x, rootBone.position.z)
            : Vector2.zero;

        /// <summary>
        /// Midpoint of feet XZ (support base center).
        /// </summary>
        public Vector2 FeetMidpointXZ
        {
            get
            {
                if (lFootBone == null || rFootBone == null) return CenterOfMassXZ;
                return new Vector2(
                    (lFootBone.position.x + rFootBone.position.x) * 0.5f,
                    (lFootBone.position.z + rFootBone.position.z) * 0.5f);
            }
        }

        void Awake()
        {
            // Override inspector-serialized value. motorEnabled must only be set
            // to true by SynthBrain.Enable() or BasePPOTrainer — never from
            // a stale inspector value, which caused the 0.5s ragdoll-on-startup bug.
            motorEnabled = false;

            // Silent first attempt — SynthEntity.Awake may not have run yet
            FindReferences(silent: true);
        }

        void Start()
        {
            // Retry reference lookup in Start (all Awake calls have completed by now)
            if (synthPhysicalBody == null || rootBone == null)
                FindReferences(silent: false);

            SubscribeToMjScene();
        }

        void OnEnable()
        {
            if (synthEntity != null)
                SubscribeToMjScene();
        }

        private void FindReferences(bool silent = false)
        {
            if (synthEntity == null)
            {
                synthEntity = GetComponent<SynthEntity>();
                if (synthEntity == null)
                    synthEntity = GetComponentInParent<SynthEntity>();
                if (synthEntity == null)
                {
                    var found = FindObjectsByType<SynthEntity>(FindObjectsSortMode.None);
                    synthEntity = found.Length > 0 ? found[0] : null;
                }
            }

            if (synthEntity == null)
            {
                if (!silent)
                    Debug.LogWarning("SynthMotorSystem: Cannot find SynthEntity!");
                return;
            }

            synthPhysicalBody = synthEntity.PhysicalBody;
            rootBone = synthEntity.RootBone;

            if (rootBone != null)
            {
                CacheControlledBones();
                prevRootPosition = rootBone.position;
                prevRootRotation = rootBone.rotation;
            }
            else if (!silent)
            {
                Debug.LogWarning("SynthMotorSystem: SynthEntity found but RootBone is null (model not bound yet?)");
            }
        }

        void OnDisable()
        {
            UnsubscribeFromMjScene();
        }

        void OnDestroy()
        {
            UnsubscribeFromMjScene();
        }

        /// <summary>
        /// Called by MjScene between mj_step1 and mj_step2 of EVERY substep.
        /// Order: (1) ask brain for new action if decision is due, (2) apply ctrl.
        /// This ensures observe-decide-act happens at the training-matched frequency.
        /// </summary>
        private bool _loggedFirstApply;

        private void OnMjCtrlCallback(object sender, MjStepArgs args)
        {
            if (!motorEnabled || synthPhysicalBody == null)
                return;

            // Ask the brain for a new action every decisionInterval substeps.
            // This fires BEFORE ctrl is applied, matching training's
            // observe → decide → apply → step2 order.
            if (decisionInterval > 0 && onDecisionNeeded != null &&
                _substepCounter % decisionInterval == 0)
            {
                onDecisionNeeded.Invoke();
            }
            _substepCounter++;

            if (randomTestMode)
            {
                ApplyRandomForces();
                return;
            }

            if (pendingAction != null && boneFilter.IsValid)
            {
                synthPhysicalBody.ApplyActuatorTorques(pendingAction, boneFilter);
                if (!_loggedFirstApply)
                {
                    _loggedFirstApply = true;
                    Debug.Log($"SynthMotorSystem: First ctrl applied — " +
                              $"act[0..2]=[{pendingAction[0]:F4},{pendingAction[1]:F4},{pendingAction[2]:F4}], " +
                              $"actDim={pendingAction.Length}");
                }
            }
        }

        /// <summary>
        /// Called after MuJoCo scene is initialized.
        /// Initializes all Position actuator controls to current joint positions
        /// to prevent the startup "explosion" from PD controllers.
        /// Also builds bone filter config for obs/action dimensions.
        /// </summary>
        private void OnMjPostInit(object sender, MjStepArgs args)
        {
            // Ensure references are fully resolved. OnEnable() may have subscribed
            // before Start() populated physicsOnlyBoneNames — without this,
            // BuildBoneFilter() sees an empty exclusion list and produces wrong dims.
            FindReferences(silent: true);

            Debug.Log("SynthMotorSystem: MuJoCo scene initialized, syncing actuator controls to current positions");
            if (synthPhysicalBody != null)
            {
                synthPhysicalBody.InitializeControlsToCurrentPositions();
            }

            // Capture the initial standing pose as the reference for pose-similarity reward
            CaptureReferencePose();

            // Build bone filter config (shared with envs and brain)
            BuildBoneFilter();
        }

        /// <summary>
        /// Build the BoneFilterConfig from physics-only bones in BoneJointsData.
        /// Also initializes contact sensing and strain computation.
        /// Called once after MjScene initialization.
        /// </summary>
        private unsafe void BuildBoneFilter()
        {
            if (!MjScene.InstanceExists || MjScene.Instance.Model == null) return;

            var model = MjScene.Instance.Model;
            var excludedIds = GetPhysicsOnlyActuatorMujocoIds();
            boneFilter = BoneFilterConfig.Build(model, excludedIds);
            _obsBuffer = new float[boneFilter.physicsObsDim];

            // Initialize per-body contact sensing
            _contactSense = GetComponent<SynthContact>();
            if (_contactSense == null)
                _contactSense = gameObject.AddComponent<SynthContact>();
            _contactSense.Initialize(model, synthEntity?.BoneMapper);

            // Initialize per-joint strain computation
            _strainComputer = new StrainComputer(model, boneFilter);

            Debug.Log($"SynthMotorSystem: BoneFilter built — physicsObsDim={boneFilter.physicsObsDim}, " +
                      $"actDim={boneFilter.actDim}, filteredJoints={boneFilter.filteredJointCount}, " +
                      $"contactDim={boneFilter.contactObsDim}, strainDim={boneFilter.strainObsDim}");
        }

        /// <summary>
        /// Get the MuJoCo-level physics observation vector (zero-allocation hot path).
        /// Fills a pre-allocated buffer via SynthObservations.FillPhysicsObs.
        ///
        /// Includes per-body contact observations and per-joint strain.
        ///
        /// Returns just the physics portion -- consumers (brain, imitation env) append
        /// task-specific data (reference motion, phase) on top.
        /// </summary>
        public unsafe float[] GetObservation()
        {
            if (!boneFilter.IsValid || !MjScene.InstanceExists) return Array.Empty<float>();
            var data = MjScene.Instance.Data;
            if (data == null) return Array.Empty<float>();

            if (_obsBuffer == null || _obsBuffer.Length != boneFilter.physicsObsDim)
                _obsBuffer = new float[boneFilter.physicsObsDim];

            // Compute contact and strain before building the observation vector
            _contactSense?.ComputeContacts(data);
            _strainComputer?.Compute(data);

            float[] contactObs = _contactSense?.GetObservation();
            float[] strainObs = _strainComputer?.StrainBuffer;

            SynthObservations.FillPhysicsObs(_obsBuffer, 0, data->qpos, data->qvel,
                data->qfrc_actuator, contactObs, strainObs, data->xfrc_applied, boneFilter);
            return _obsBuffer;
        }

        /// <summary>
        /// Strain computer for external access (e.g. reward computation).
        /// </summary>
        public StrainComputer Strain => _strainComputer;

        /// <summary>
        /// Apply a per-actuator action vector. Actions are stored and applied
        /// in the ctrl callback (between mj_step1 and mj_step2) via SynthPhysicalBody.
        ///
        /// The action persists until replaced -- it is re-applied every physics step,
        /// which is correct for frame skipping (same action applied over multiple sub-steps).
        /// </summary>
        /// <param name="actions">Per-actuator action array (length = Filter.actDim)</param>
        public void ApplyAction(float[] actions)
        {
            pendingAction = actions;
        }

        /// <summary>
        /// Update root velocity estimates from transform deltas.
        /// Called after MuJoCo's FixedUpdate has synced transforms.
        /// </summary>
        void FixedUpdate()
        {
            UpdateRootVelocities(Time.fixedDeltaTime);
        }

        /// <summary>
        /// Recompute root linear/angular velocity from position/rotation deltas.
        /// Must be called after each MuJoCo StepScene() when using multi-step per frame,
        /// otherwise the velocity observations become stale for steps 2+.
        /// </summary>
        public void UpdateRootVelocities(float dt)
        {
            if (rootBone == null) return;

            if (dt > 0)
            {
                // Linear velocity from position delta
                rootLinearVelocity = (rootBone.position - prevRootPosition) / dt;

                // Angular velocity from rotation delta
                Quaternion deltaRot = rootBone.rotation * Quaternion.Inverse(prevRootRotation);
                deltaRot.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                rootAngularVelocity = axis * (angle * Mathf.Deg2Rad / dt);
            }

            prevRootPosition = rootBone.position;
            prevRootRotation = rootBone.rotation;
        }

        #region Private methods

        private unsafe void SubscribeToMjScene()
        {
            if (subscribedToEvents || !MjScene.InstanceExists)
                return;

            MjScene.Instance.ctrlCallback += OnMjCtrlCallback;
            MjScene.Instance.postInitEvent += OnMjPostInit;
            subscribedToEvents = true;

            if (MjScene.Instance.Model != null && !boneFilter.IsValid)
            {
                OnMjPostInit(this, new MjStepArgs(MjScene.Instance.Model, MjScene.Instance.Data));
            }
        }

        private void UnsubscribeFromMjScene()
        {
            if (!subscribedToEvents || !MjScene.InstanceExists)
                return;

            MjScene.Instance.ctrlCallback -= OnMjCtrlCallback;
            MjScene.Instance.postInitEvent -= OnMjPostInit;
            subscribedToEvents = false;
        }

        private void CacheControlledBones()
        {
            if (rootBone == null) return;

            controlledBones = new List<Transform>();
            physicsOnlyBoneNames = new List<string>();
            CacheBonesRecursive(rootBone);
            CacheKeyBones();

            Debug.Log($"SynthMotorSystem: Cached {controlledBones.Count} controlled bones " +
                                  $"(all non-physics-only), " +
                                  $"physicsOnly={physicsOnlyBoneNames.Count} [{string.Join(", ", physicsOnlyBoneNames)}]");
        }

        /// <summary>
        /// Cache references to key bone transforms used for posture reward computation.
        /// Uses the SynthBoneMapper (Avatar-based) if available; falls back to name search.
        /// </summary>
        private void CacheKeyBones()
        {
            var mapper = synthEntity?.BoneMapper;
            if (mapper != null)
            {
                headBone = mapper.GetTransform(SynthBone.Head);
                chestUpperBone = mapper.GetTransform(SynthBone.UpperChest)
                              ?? mapper.GetTransform(SynthBone.Chest);
                lFootBone = mapper.GetTransform(SynthBone.LeftFoot);
                rFootBone = mapper.GetTransform(SynthBone.RightFoot);
            }
            else
            {
                headBone = FindBoneByName(rootBone, "head");
                chestUpperBone = FindBoneByName(rootBone, "chestUpper");
                lFootBone = FindBoneByName(rootBone, "lFoot");
                rFootBone = FindBoneByName(rootBone, "rFoot");
            }

            int found = 0;
            if (headBone != null) found++;
            if (chestUpperBone != null) found++;
            if (lFootBone != null) found++;
            if (rFootBone != null) found++;
            Debug.Log($"SynthMotorSystem: Cached {found}/4 key posture bones" +
                      (mapper != null ? " (via Avatar)" : " (via name search)"));
        }

        private Transform FindBoneByName(Transform parent, string name)
        {
            if (parent.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindBoneByName(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Capture the current joint angles as the reference idle pose.
        /// Should be called when the humanoid is in its initial standing configuration.
        /// Used by ComputePoseDeviation() for reward shaping (not for action computation).
        /// </summary>
        public void CaptureReferencePose()
        {
            if (controlledBones == null || controlledBones.Count == 0) return;

            int dim = controlledBones.Count * 3;
            referencePoseAngles = new float[dim];
            for (int i = 0; i < controlledBones.Count; i++)
            {
                var state = synthPhysicalBody.GetJointState(controlledBones[i]);
                referencePoseAngles[i * 3] = state.configuration.x;
                referencePoseAngles[i * 3 + 1] = state.configuration.y;
                referencePoseAngles[i * 3 + 2] = state.configuration.z;
            }

            referenceHeadHeight = HeadHeight;
            referencePoseCaptured = true;

            Debug.Log($"SynthMotorSystem: Reference pose captured. " +
                                  $"Head height: {referenceHeadHeight:F3}, " +
                                  $"Joints: {controlledBones.Count}");
        }

        /// <summary>
        /// Compute the mean-squared deviation of current joint angles from the reference pose.
        /// Lower values mean closer to the natural standing pose.
        /// Returns 0 if no reference pose has been captured.
        /// </summary>
        public float ComputePoseDeviation()
        {
            if (!referencePoseCaptured || controlledBones == null) return 0f;

            float sumSq = 0f;
            for (int i = 0; i < controlledBones.Count; i++)
            {
                var state = synthPhysicalBody.GetJointState(controlledBones[i]);
                float dx = state.configuration.x - referencePoseAngles[i * 3];
                float dy = state.configuration.y - referencePoseAngles[i * 3 + 1];
                float dz = state.configuration.z - referencePoseAngles[i * 3 + 2];
                sumSq += dx * dx + dy * dy + dz * dz;
            }
            // Return mean squared deviation per joint axis
            return sumSq / (controlledBones.Count * 3);
        }

        private void CacheBonesRecursive(Transform bone)
        {
            if (synthPhysicalBody.HasJoint(bone))
            {
                if (IsPhysicsOnlyBone(bone.name))
                {
                    physicsOnlyBoneNames.Add(bone.name);
                }
                else
                {
                    // All non-physics-only bones are controlled -- no majorJointsOnly filter.
                    controlledBones.Add(bone);
                }
            }
            for (int i = 0; i < bone.childCount; i++)
            {
                var child = bone.GetChild(i);
                if (synthPhysicalBody.IsBodyBone(child))
                {
                    CacheBonesRecursive(child);
                }
            }
        }

        /// <summary>
        /// Check if a bone is marked as physics-only in BoneJointsData.
        /// Physics-only bones have joints for simulation but are not voluntarily controllable
        /// (e.g. pectorals/breasts). Uses same naming convention as SynthBoneJoints:
        /// bone data stores "Pectoral", actual bone is "lPectoral"/"rPectoral".
        /// </summary>
        private bool IsPhysicsOnlyBone(string boneName)
        {
            if (synthEntity == null || synthEntity.synthData == null) return false;
            var boneJoints = synthEntity.synthData.BoneJoints;
            if (boneJoints == null) return false;
            var entry = boneJoints.Find(b => boneName.IndexOf(b.bone, System.StringComparison.OrdinalIgnoreCase) >= 0);
            return entry != null && entry.physicsOnly;
        }

        /// <summary>
        /// Get MuJoCo actuator indices for physics-only bones. These bones have joints for
        /// physics simulation but should not be part of the motor/RL action space.
        /// Call after MjScene initialization (MujocoId must be valid).
        /// This is the single source of truth -- replaces pattern-matching in the trainer.
        /// </summary>
        public int[] GetPhysicsOnlyActuatorMujocoIds()
        {
            if (physicsOnlyBoneNames == null || physicsOnlyBoneNames.Count == 0)
                return Array.Empty<int>();

            var excluded = new System.Collections.Generic.List<int>();
            var excludedLog = new System.Collections.Generic.List<string>();

            var allActuators = FindObjectsByType<MjActuator>(FindObjectsSortMode.None);
            foreach (var actuator in allActuators)
            {
                string goName = actuator.gameObject.name;
                foreach (var boneName in physicsOnlyBoneNames)
                {
                    if (goName.IndexOf(boneName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int mujocoId = actuator.MujocoId;
                        if (mujocoId >= 0)
                        {
                            excluded.Add(mujocoId);
                            excludedLog.Add($"{goName} (id={mujocoId})");
                        }
                        break;
                    }
                }
            }

            excludedLog.Sort();
            Debug.Log($"SynthMotorSystem: [PhysicsOnly] Excluded {excluded.Count} actuators " +
                                  $"from {physicsOnlyBoneNames.Count} physics-only bones " +
                                  $"[{string.Join(", ", physicsOnlyBoneNames)}]:\n  " +
                                  string.Join("\n  ", excludedLog));

            return excluded.ToArray();
        }

        private void ApplyRandomForces()
        {
            foreach (var bone in controlledBones)
            {
                var randomForce = new Vector3(
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(-1f, 1f)
                );
                synthPhysicalBody.ApplyForceToJoint(bone, randomForce);
            }
        }

        #endregion
    }
}
