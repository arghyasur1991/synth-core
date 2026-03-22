using System.Collections.Generic;
using UnityEngine;
using Mujoco;

namespace Genesis.Sentience.Synth
{
    public struct JointState
    {
        public Vector3 configuration;
        public Vector3 velocity;
        /// <summary>
        /// World-space rotation axes from MuJoCo runtime data (more accurate than transform).
        /// Only populated when MjScene is active.
        /// </summary>
        public Vector3 rotationAxisX;
        public Vector3 rotationAxisY;
        public Vector3 rotationAxisZ;
    }

    public class SynthBoneJoints
    {
        private readonly Transform actuatorRoot;
        private readonly List<BoneJoint> boneJoints;
        private readonly SynthBoneMapper boneMapper;

        // Run 14: Motor (torque) actuators with per-axis gear ratios from SynthBonesData.asset.
        // Analysis of Gymnasium humanoidstandup.xml revealed:
        //   1. Standard uses gear 100-300 with ctrlrange [-0.4, 0.4] → max torques 40-120 N·m
        //   2. Standard HAS joint stiffness (10-20) and damping (1-5) — NOT zero!
        //   3. Standard trains at FULL gravity, no curriculum
        //   4. Different axes of the same joint have different gear ratios (hip_y=300, hip_x/z=100)
        // Gear ratios are now stored per-axis in BoneJointSettings.gear (in SynthBonesData.asset),
        // alongside rangeL/rangeU/stiffness/damping. gear=0 falls back to DEFAULT_GEAR.
        private const float DEFAULT_GEAR = 10f;             // Fallback gear ratio when bone data has gear=0
        private const float DEFAULT_FORCE_RANGE = 150f;     // Force limit (N·m) — generous to not clip

        // Spring scaling: RESTORED to full per-bone values from SynthBonesData.asset.
        // Standard MuJoCo humanoid HAS stiffness=10-20 and damping=1-5 on load-bearing joints.
        // With Motor actuators, springs provide:
        //   - Stiffness: bias toward upright pose (helpful, but not sufficient for balance)
        //   - Damping: slows falls, smooths dynamics, gives agent reaction time
        // This is fundamentally different from Position actuators where springs+PD
        // created redundant passive stability. With Motor, the agent must still actively
        // control torques — springs just make the learning landscape smoother.
        private const float SPRING_STIFFNESS_SCALE = 1.0f;
        private const float SPRING_DAMPING_SCALE = 1.0f;

        public SynthBoneJoints(SynthData synthData, Transform actuatorRoot, SynthBoneMapper mapper = null)
        {
            boneJoints = synthData.BoneJoints;
            this.actuatorRoot = actuatorRoot;
            boneMapper = mapper;
        }

        public void AddJointsToBone(Transform bone, bool isRoot)
        {
            if (isRoot)
            {
                var jointName = bone.name + "Joint";
                var mjJoint = bone.FindInChildren(jointName);
                if (mjJoint == null)
                {
                    var mjJointObj = new GameObject(jointName);
                    mjJointObj.AddComponent<MjFreeJoint>();
                    mjJointObj.transform.SetParent(bone, false);
                }
                return;
            }
            AddJointToBone(bone, Vector3.right); // X axis
            AddJointToBone(bone, Vector3.up);   // Y axis
            AddJointToBone(bone, Vector3.forward);  // Z axis
        }

        public void RemoveJointsFromBone(Transform bone)
        {
            RemoveJointFromBone(bone, bone.name + "Joint"); // For root bone
            RemoveJointFromBone(bone, Vector3.right); // X axis
            RemoveJointFromBone(bone, Vector3.up);   // Y axis
            RemoveJointFromBone(bone, Vector3.forward);  // Z axis
        }

        public unsafe JointState GetJointState(Transform bone)
        {
            var jointX = GetBoneJoint(bone, Vector3.right);
            var jointY = GetBoneJoint(bone, Vector3.up);
            var jointZ = GetBoneJoint(bone, Vector3.forward);
            JointState jointState = new();
            if (jointX != null && jointY != null && jointZ != null)
            {
                jointState.configuration = new Vector3(jointX.Configuration, jointY.Configuration, jointZ.Configuration);
                jointState.velocity = new Vector3(jointX.Velocity, jointY.Velocity, jointZ.Velocity);

                // Use MuJoCo runtime rotation axes for higher accuracy
                // RotationAxis reads from MuJoCo's xaxis data when scene is active
                // Guard against Data being null (scene not yet created)
                if (MjScene.InstanceExists && MjScene.Instance.Data != null)
                {
                    jointState.rotationAxisX = jointX.RotationAxis;
                    jointState.rotationAxisY = jointY.RotationAxis;
                    jointState.rotationAxisZ = jointZ.RotationAxis;
                }
            }
            return jointState;
        }

        public void ApplyForceToJoint(Transform bone, Vector3 force)
        {
            var actuatorX = GetBoneActuator(bone, Vector3.right);
            var actuatorY = GetBoneActuator(bone, Vector3.up);
            var actuatorZ = GetBoneActuator(bone, Vector3.forward);
            if (actuatorX != null && actuatorY != null && actuatorZ != null)
            {
                actuatorX.Control = force.x;
                actuatorY.Control = force.y;
                actuatorZ.Control = force.z;
            }
        }

        /// <summary>
        /// Initialize all actuator controls to zero.
        /// For Motor (torque) actuators, ctrl=0 means no torque applied — the safe default.
        /// (For the old Position actuators, this used to set controls to current joint angles
        /// to prevent PD startup transients. Motor actuators don't have this issue.)
        /// </summary>
        public void InitializeControlsToCurrentPositions(Transform bone)
        {
            var actuatorX = GetBoneActuator(bone, Vector3.right);
            var actuatorY = GetBoneActuator(bone, Vector3.up);
            var actuatorZ = GetBoneActuator(bone, Vector3.forward);

            if (actuatorX != null) actuatorX.Control = 0f;
            if (actuatorY != null) actuatorY.Control = 0f;
            if (actuatorZ != null) actuatorZ.Control = 0f;
        }

        public bool HasJoint(Transform bone)
        {
            var jointX = GetBoneJoint(bone, Vector3.right);
            return jointX != null;
        }

        /// <summary>
        /// Returns the joint range (rangeLower, rangeUpper) in degrees for a bone on a given axis.
        /// Returns (-5, 5) if no bone data found.
        /// </summary>
        public Vector2 GetJointRangeDegrees(Transform bone, int axisIndex)
        {
            var boneSettingIndex = FindBoneJointIndex(bone);
            if (boneSettingIndex >= 0)
            {
                var settings = boneJoints[boneSettingIndex].boneJointSettings;
                if (settings.Count == 3 && axisIndex >= 0 && axisIndex < 3)
                {
                    var s = settings[axisIndex];
                    return new Vector2(s.rangeL, s.rangeU);
                }
            }
            return new Vector2(-5, 5);
        }

        private MjHingeJoint GetBoneJoint(Transform bone, Vector3 axis)
        {
            var boneName = bone.name;
            var axisStr = GetAxisStr(axis);
            var jointName = boneName + "Joint" + axisStr;
            var mjJoint = bone.FindInChildren(jointName);
            if (mjJoint == null)
            {
                return null;
            }
            return mjJoint.GetComponent<MjHingeJoint>();
        }

        private MjActuator GetBoneActuator(Transform bone, Vector3 axis)
        {
            var boneName = bone.name;
            var axisStr = GetAxisStr(axis);
            var jointName = boneName + "Joint" + axisStr;
            var mjActuator = actuatorRoot.FindInChildren(jointName);
            if (mjActuator == null)
            {
                return null;
            }
            return mjActuator.GetComponent<MjActuator>();
        }

        private string GetAxisStr(Vector3 axis)
        {
            var axisStr = "X";
            if (axis == Vector3.up)
            {
                axisStr = "Y";
            }
            else if (axis == Vector3.forward)
            {
                axisStr = "Z";
            }
            return axisStr;
        }

        private void RemoveJointFromBone(Transform bone, Vector3 axis)
        {
            var boneName = bone.name;
            var axisStr = GetAxisStr(axis);
            var jointName = boneName + "Joint" + axisStr;
            RemoveJointFromBone(bone, jointName);
            var mjActuator = actuatorRoot.FindInChildren(jointName);
            if (mjActuator != null)
            {
                Object.DestroyImmediate(mjActuator.gameObject);
            }
        }

        private void RemoveJointFromBone(Transform bone, string jointName)
        {
            var mjJoint = bone.FindInChildren(jointName);
            if (mjJoint != null)
            {
                Object.DestroyImmediate(mjJoint.gameObject);
            }
        }

        /// <summary>
        /// Creates a Motor (torque) actuator. The control signal [-1,1] is multiplied
        /// by the per-axis gear ratio to produce actual torque (N·m). No PD controller —
        /// the RL agent directly controls joint torques and must learn active balance.
        /// This matches the standard MuJoCo humanoid approach (Humanoid-v4).
        /// Gear ratios are read from BoneJointSettings in SynthBonesData.asset.
        /// </summary>
        private void AddActuator(MjHingeJoint joint, int axisIndex)
        {
            var actuatorName = joint.name;
            var mjActuator = actuatorRoot.FindInChildren(actuatorName);
            if (mjActuator == null)
            {
                var actuatorObj = new GameObject(actuatorName);
                var actuator = actuatorObj.AddComponent<MjActuator>();
                actuator.Joint = joint;

                // Motor actuator: control signal is normalized torque [-1, 1]
                actuator.Type = MjActuator.ActuatorType.Motor;

                // Per-axis gear ratio from bone data. torque = ctrl × gear.
                // Standard MuJoCo humanoid uses different gear per axis
                // (e.g., hip_y=300 but hip_x=100 with ctrlrange [-0.4, 0.4]).
                float gear = GetGearRatio(joint.name, axisIndex);
                actuator.CommonParams.Gear = new List<float>() { gear };

                // Control range: normalized [-1, 1] (policy output range)
                actuator.CommonParams.CtrlLimited = true;
                actuator.CommonParams.CtrlRange = new Vector2(-1f, 1f);

                // Force limiting for safety
                actuator.CommonParams.ForceLimited = true;
                actuator.CommonParams.ForceRange = new Vector2(-DEFAULT_FORCE_RANGE, DEFAULT_FORCE_RANGE);

                actuatorObj.transform.SetParent(actuatorRoot);
            }
        }

        /// <summary>
        /// Returns the per-axis gear ratio from bone data (SynthBonesData.asset).
        /// Reads BoneJointSettings.gear for the matching bone and axis.
        /// Falls back to DEFAULT_GEAR if bone not found or gear=0 (not configured).
        /// </summary>
        private float GetGearRatio(string jointName, int axisIndex)
        {
            var boneSettingIndex = FindBoneJointIndexByJointName(jointName);
            if (boneSettingIndex >= 0)
            {
                var settings = boneJoints[boneSettingIndex].boneJointSettings;
                if (settings.Count == 3 && axisIndex >= 0 && axisIndex < 3)
                {
                    float gear = settings[axisIndex].gear;
                    if (gear > 0f) return gear;
                }
            }
            return DEFAULT_GEAR;
        }

        /// <summary>
        /// Find the BoneJoint entry for a bone Transform.
        /// Uses mapper canonical names if available; falls back to Contains matching.
        /// </summary>
        private int FindBoneJointIndex(Transform bone)
        {
            if (boneMapper != null)
            {
                string canonical = boneMapper.GetCanonicalName(bone);
                if (!string.IsNullOrEmpty(canonical))
                {
                    int idx = boneJoints.FindIndex(b =>
                        b.bone.Equals(canonical, System.StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0) return idx;
                }
            }
            return boneJoints.FindIndex(b => bone.name.Contains(b.bone));
        }

        /// <summary>
        /// Find the BoneJoint entry by joint name string (for actuator creation).
        /// Joint names are boneName + "Joint" + axis, so we match against the bone portion.
        /// </summary>
        private int FindBoneJointIndexByJointName(string jointName)
        {
            if (boneMapper != null)
            {
                foreach (var kvp in boneMapper.BoneMap)
                {
                    if (jointName.Contains(kvp.Value.name))
                    {
                        string canonical = SynthBoneCatalog.GetCanonicalName(kvp.Key);
                        int idx = boneJoints.FindIndex(b =>
                            b.bone.Equals(canonical, System.StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0) return idx;
                    }
                }
            }
            return boneJoints.FindIndex(b => jointName.Contains(b.bone));
        }

        private void AddJointToBone(Transform bone, Vector3 axis)
        {
            var boneName = bone.name;
            var axisStr = GetAxisStr(axis);
            var jointName = boneName + "Joint" + axisStr;
            var mjJoint = bone.FindInChildren(jointName);
            if (mjJoint == null)
            {
                var mjJointObj = new GameObject(jointName);
                var joint = mjJointObj.AddComponent<MjHingeJoint>();
                int axisIdx = AxisIndex(axis);
                UpdateJointSettings(joint, axisIdx);
                mjJointObj.transform.SetParent(bone, false);

                var synthBone = boneMapper != null ? boneMapper.GetSynthBone(bone) : SynthBone.Unknown;
                var adapter = boneMapper?.Adapter;
                Vector3 effectiveAxis = adapter != null
                    ? adapter.AdjustJointAxis(bone, axis, synthBone, boneMapper)
                    : axis;
                mjJointObj.transform.localRotation = Quaternion.FromToRotation(Vector3.right, effectiveAxis);

                AddActuator(joint, axisIdx);
            }
        }

        private int AxisIndex(Vector3 axis)
        {
            if (axis == Vector3.right) return 0;
            if (axis == Vector3.up) return 1;
            if (axis == Vector3.forward) return 2;
            return -1;
        }

        private void UpdateJointSettings(MjHingeJoint joint, int axisIndex)
        {
            const float DEFAULT_RANGE_L = -5f;
            const float DEFAULT_RANGE_U = 5f;

            joint.Settings.Solver.Limited = true;
            joint.Settings.Armature = 0.01f;
            joint.RangeLower = DEFAULT_RANGE_L;
            joint.RangeUpper = DEFAULT_RANGE_U;
            joint.Settings.Spring.Stiffness = 10 * SPRING_STIFFNESS_SCALE;
            joint.Settings.Spring.Damping = 5 * SPRING_DAMPING_SCALE;

            var boneSettingIndex = FindBoneJointIndexByJointName(joint.name);
            if (boneSettingIndex >= 0)
            {
                var boneSettingsList = boneJoints[boneSettingIndex].boneJointSettings;
                if (boneSettingsList.Count == 3)
                {
                    var boneSettingsAxis = boneSettingsList[axisIndex];

                    float rangeL = boneSettingsAxis.rangeL;
                    float rangeU = boneSettingsAxis.rangeU;

                    // No range flip needed: left/right bones share the same local joint
                    // axis (DazAdapter returns axis unchanged), and the skeleton hierarchy
                    // naturally mirrors world-space orientation. The same canonical range
                    // produces correct mirrored motion on both sides.

                    // MuJoCo requires range[0] < range[1] when limited=true.
                    // Fall back to defaults if the stored range is degenerate (unconfigured).
                    if (rangeL < rangeU)
                    {
                        joint.RangeLower = rangeL;
                        joint.RangeUpper = rangeU;
                    }

                    joint.Settings.Spring.Stiffness = boneSettingsAxis.stiffness * SPRING_STIFFNESS_SCALE;
                    joint.Settings.Spring.Damping = boneSettingsAxis.damping * SPRING_DAMPING_SCALE;
                }
            }
        }
    }
}
