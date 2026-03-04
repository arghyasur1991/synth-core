using System;
using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Sentience.Synth
{
    public class BoneJointAttribute : PropertyAttribute
    {

    }

    public class BoneJointSettingAttribute : PropertyAttribute
    {

    }

    [Serializable]
    public struct BoneJointSettings
    {
        public float rangeL;
        public float rangeU;
        public float stiffness;
        public float damping;
        /// <summary>
        /// Motor actuator gear ratio for this axis. torque = ctrl[-1,1] × gear.
        /// Higher gear = stronger joint. Standard MuJoCo humanoid uses 40-120 for
        /// load-bearing joints (hips, knees) and 10-25 for secondary joints (arms).
        /// Default 0 means use the DEFAULT_GEAR fallback in SynthBoneJoints.
        /// </summary>
        public float gear;

        public static BoneJointSettings Default => new BoneJointSettings
        {
            rangeL = -5f,
            rangeU = 5f,
            stiffness = 0f,
            damping = 0f,
            gear = 0f,
        };
    }

    [Serializable]
    public class BoneJoint : PerBoneData
    {
        [Tooltip("Physics-only bone — has joints for simulation but is not voluntarily controllable (e.g. pectorals/breasts). Excluded from motor control and RL action space.")]
        public bool physicsOnly;
        [BoneJointSetting] public List<BoneJointSettings> boneJointSettings;
    }

    [CreateAssetMenu()]
    public class BoneJointsData : SynthBonesData<BoneJoint>
    {
        protected override BoneJoint CreatePerBoneData(List<string> parents, string bone, int boneIndex)
        {
            var tuned = SynthJointDefaults.Get(bone);
            var boneJoint = new BoneJoint
            {
                parents = parents,
                bone = bone,
                boneIndex = boneIndex,
                boneJointSettings = new()
            };
            boneJoint.boneJointSettings.Add(tuned != null ? tuned[0] : BoneJointSettings.Default);
            boneJoint.boneJointSettings.Add(tuned != null ? tuned[1] : BoneJointSettings.Default);
            boneJoint.boneJointSettings.Add(tuned != null ? tuned[2] : BoneJointSettings.Default);
            return boneJoint;
        }
    }
}
