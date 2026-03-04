using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Static metadata for every SynthBone: Unity Avatar mapping, name-pattern
    /// fallbacks, side, mirror bone, chain-stop flags, and physics-only defaults.
    /// </summary>
    public static class SynthBoneCatalog
    {
        public struct BoneInfo
        {
            public HumanBodyBones? UnityMapping;
            public string[] FallbackNamePatterns;
            public BoneSide Side;
            public SynthBone? MirrorBone;
            public bool IsChainStop;
            public bool IsRequired;
            public bool DefaultPhysicsOnly;
            /// <summary>Side-neutral name used for serialized data (joints, mass).</summary>
            public string CanonicalName;
        }

        private static readonly Dictionary<SynthBone, BoneInfo> _catalog;

        static SynthBoneCatalog()
        {
            _catalog = new Dictionary<SynthBone, BoneInfo>
            {
                // --- Center chain ---
                [SynthBone.Hips] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.Hips,
                    FallbackNamePatterns = new[] { "hip", "pelvis", "Hips" },
                    Side = BoneSide.Center, IsRequired = true,
                    CanonicalName = "Hips"
                },
                [SynthBone.Spine] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.Spine,
                    FallbackNamePatterns = new[] { "spine", "abdomen", "Spine" },
                    Side = BoneSide.Center, IsRequired = true,
                    CanonicalName = "Spine"
                },
                [SynthBone.Chest] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.Chest,
                    FallbackNamePatterns = new[] { "chest", "Chest", "Spine1" },
                    Side = BoneSide.Center, IsRequired = true,
                    CanonicalName = "Chest"
                },
                [SynthBone.UpperChest] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.UpperChest,
                    FallbackNamePatterns = new[] { "chestUpper", "upperChest", "UpperChest", "Spine2" },
                    Side = BoneSide.Center,
                    CanonicalName = "UpperChest"
                },
                [SynthBone.Neck] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.Neck,
                    FallbackNamePatterns = new[] { "neck", "Neck" },
                    Side = BoneSide.Center, IsRequired = true,
                    CanonicalName = "Neck"
                },
                [SynthBone.Head] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.Head,
                    FallbackNamePatterns = new[] { "head", "Head" },
                    Side = BoneSide.Center, IsRequired = true,
                    IsChainStop = true,
                    CanonicalName = "Head"
                },
                [SynthBone.Jaw] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.Jaw,
                    FallbackNamePatterns = new[] { "jaw", "Jaw", "lowerJaw" },
                    Side = BoneSide.Center,
                    CanonicalName = "Jaw"
                },

                // --- Left arm chain ---
                [SynthBone.LeftShoulder] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.LeftShoulder,
                    FallbackNamePatterns = new[] { "lCollar", "LeftShoulder", "l_shoulder", "shoulder_l" },
                    Side = BoneSide.Left, MirrorBone = SynthBone.RightShoulder,
                    CanonicalName = "Shoulder"
                },
                [SynthBone.LeftUpperArm] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.LeftUpperArm,
                    FallbackNamePatterns = new[] { "lShldrBend", "LeftUpperArm", "LeftArm", "l_upperarm", "upperarm_l" },
                    Side = BoneSide.Left, MirrorBone = SynthBone.RightUpperArm,
                    CanonicalName = "UpperArm"
                },
                [SynthBone.LeftLowerArm] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.LeftLowerArm,
                    FallbackNamePatterns = new[] { "lForearmBend", "LeftLowerArm", "LeftForeArm", "l_forearm", "forearm_l" },
                    Side = BoneSide.Left, MirrorBone = SynthBone.RightLowerArm,
                    CanonicalName = "LowerArm"
                },
                [SynthBone.LeftHand] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.LeftHand,
                    FallbackNamePatterns = new[] { "lHand", "LeftHand", "l_hand", "hand_l" },
                    Side = BoneSide.Left, MirrorBone = SynthBone.RightHand,
                    IsChainStop = true,
                    CanonicalName = "Hand"
                },

                // --- Right arm chain ---
                [SynthBone.RightShoulder] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.RightShoulder,
                    FallbackNamePatterns = new[] { "rCollar", "RightShoulder", "r_shoulder", "shoulder_r" },
                    Side = BoneSide.Right, MirrorBone = SynthBone.LeftShoulder,
                    CanonicalName = "Shoulder"
                },
                [SynthBone.RightUpperArm] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.RightUpperArm,
                    FallbackNamePatterns = new[] { "rShldrBend", "RightUpperArm", "RightArm", "r_upperarm", "upperarm_r" },
                    Side = BoneSide.Right, MirrorBone = SynthBone.LeftUpperArm,
                    CanonicalName = "UpperArm"
                },
                [SynthBone.RightLowerArm] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.RightLowerArm,
                    FallbackNamePatterns = new[] { "rForearmBend", "RightLowerArm", "RightForeArm", "r_forearm", "forearm_r" },
                    Side = BoneSide.Right, MirrorBone = SynthBone.LeftLowerArm,
                    CanonicalName = "LowerArm"
                },
                [SynthBone.RightHand] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.RightHand,
                    FallbackNamePatterns = new[] { "rHand", "RightHand", "r_hand", "hand_r" },
                    Side = BoneSide.Right, MirrorBone = SynthBone.LeftHand,
                    IsChainStop = true,
                    CanonicalName = "Hand"
                },

                // --- Left leg chain ---
                [SynthBone.LeftUpperLeg] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.LeftUpperLeg,
                    FallbackNamePatterns = new[] { "lThighBend", "LeftUpperLeg", "LeftUpLeg", "l_thigh", "thigh_l" },
                    Side = BoneSide.Left, MirrorBone = SynthBone.RightUpperLeg,
                    CanonicalName = "UpperLeg"
                },
                [SynthBone.LeftLowerLeg] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.LeftLowerLeg,
                    FallbackNamePatterns = new[] { "lShin", "LeftLowerLeg", "LeftLeg", "l_shin", "shin_l", "l_calf" },
                    Side = BoneSide.Left, MirrorBone = SynthBone.RightLowerLeg,
                    CanonicalName = "LowerLeg"
                },
                [SynthBone.LeftFoot] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.LeftFoot,
                    FallbackNamePatterns = new[] { "lFoot", "LeftFoot", "l_foot", "foot_l" },
                    Side = BoneSide.Left, MirrorBone = SynthBone.RightFoot,
                    IsChainStop = true,
                    CanonicalName = "Foot"
                },
                [SynthBone.LeftToes] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.LeftToes,
                    FallbackNamePatterns = new[] { "lToe", "LeftToes", "LeftToeBase", "l_toe", "toe_l" },
                    Side = BoneSide.Left, MirrorBone = SynthBone.RightToes,
                    CanonicalName = "Toes"
                },

                // --- Right leg chain ---
                [SynthBone.RightUpperLeg] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.RightUpperLeg,
                    FallbackNamePatterns = new[] { "rThighBend", "RightUpperLeg", "RightUpLeg", "r_thigh", "thigh_r" },
                    Side = BoneSide.Right, MirrorBone = SynthBone.LeftUpperLeg,
                    CanonicalName = "UpperLeg"
                },
                [SynthBone.RightLowerLeg] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.RightLowerLeg,
                    FallbackNamePatterns = new[] { "rShin", "RightLowerLeg", "RightLeg", "r_shin", "shin_r", "r_calf" },
                    Side = BoneSide.Right, MirrorBone = SynthBone.LeftLowerLeg,
                    CanonicalName = "LowerLeg"
                },
                [SynthBone.RightFoot] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.RightFoot,
                    FallbackNamePatterns = new[] { "rFoot", "RightFoot", "r_foot", "foot_r" },
                    Side = BoneSide.Right, MirrorBone = SynthBone.LeftFoot,
                    IsChainStop = true,
                    CanonicalName = "Foot"
                },
                [SynthBone.RightToes] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.RightToes,
                    FallbackNamePatterns = new[] { "rToe", "RightToes", "RightToeBase", "r_toe", "toe_r" },
                    Side = BoneSide.Right, MirrorBone = SynthBone.LeftToes,
                    CanonicalName = "Toes"
                },

                // --- Eyes ---
                [SynthBone.LeftEye] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.LeftEye,
                    FallbackNamePatterns = new[] { "lEye", "LeftEye", "l_eye", "eye_l" },
                    Side = BoneSide.Left, MirrorBone = SynthBone.RightEye,
                    CanonicalName = "Eye"
                },
                [SynthBone.RightEye] = new BoneInfo
                {
                    UnityMapping = HumanBodyBones.RightEye,
                    FallbackNamePatterns = new[] { "rEye", "RightEye", "r_eye", "eye_r" },
                    Side = BoneSide.Right, MirrorBone = SynthBone.LeftEye,
                    CanonicalName = "Eye"
                },

                // --- Extended bones (no Unity Avatar mapping) ---
                [SynthBone.LeftPectoral] = new BoneInfo
                {
                    UnityMapping = null,
                    FallbackNamePatterns = new[] { "lPectoral", "LeftPectoral", "l_pectoral", "l_breast", "lBreast" },
                    Side = BoneSide.Left, MirrorBone = SynthBone.RightPectoral,
                    DefaultPhysicsOnly = true,
                    CanonicalName = "Pectoral"
                },
                [SynthBone.RightPectoral] = new BoneInfo
                {
                    UnityMapping = null,
                    FallbackNamePatterns = new[] { "rPectoral", "RightPectoral", "r_pectoral", "r_breast", "rBreast" },
                    Side = BoneSide.Right, MirrorBone = SynthBone.LeftPectoral,
                    DefaultPhysicsOnly = true,
                    CanonicalName = "Pectoral"
                },
                [SynthBone.LeftGluteal] = new BoneInfo
                {
                    UnityMapping = null,
                    FallbackNamePatterns = new[] { "lGluteal", "LeftGluteal", "l_gluteal", "l_butt" },
                    Side = BoneSide.Left, MirrorBone = SynthBone.RightGluteal,
                    DefaultPhysicsOnly = true,
                    CanonicalName = "Gluteal"
                },
                [SynthBone.RightGluteal] = new BoneInfo
                {
                    UnityMapping = null,
                    FallbackNamePatterns = new[] { "rGluteal", "RightGluteal", "r_gluteal", "r_butt" },
                    Side = BoneSide.Right, MirrorBone = SynthBone.LeftGluteal,
                    DefaultPhysicsOnly = true,
                    CanonicalName = "Gluteal"
                },
            };
        }

        public static BoneInfo Get(SynthBone bone)
        {
            return _catalog.TryGetValue(bone, out var info) ? info : default;
        }

        public static string GetCanonicalName(SynthBone bone)
        {
            return Get(bone).CanonicalName;
        }

        public static IEnumerable<KeyValuePair<SynthBone, BoneInfo>> All => _catalog;
    }
}
