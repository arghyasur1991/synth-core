using System.Collections.Generic;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Biomechanically-tuned default joint configurations per canonical bone name.
    /// Derived from the hand-tuned SynthBonesData.asset (Daz Genesis8Female).
    /// Values are ranges (degrees), spring stiffness, spring damping, and motor gear ratio
    /// for each of the three rotation axes [X, Y, Z].
    ///
    /// Canonical names match the SynthBoneCatalog CanonicalName (e.g., "Spine", "UpperArm")
    /// for standard bones, plus common intermediate bone names (e.g., "ShldrTwist",
    /// "chestLower") that appear via StripSidePrefix for models with extra hierarchy.
    /// </summary>
    public static class SynthJointDefaults
    {
        private static readonly Dictionary<string, BoneJointSettings[]> _defaults;

        static SynthJointDefaults()
        {
            _defaults = new Dictionary<string, BoneJointSettings[]>(System.StringComparer.OrdinalIgnoreCase)
            {
                // ── Center chain ──────────────────────────────────────────

                ["Spine"] = new[]
                {
                    new BoneJointSettings { rangeL = -60, rangeU = 25, stiffness = 6, damping = 4, gear = 40 },
                    new BoneJointSettings { rangeL = -15, rangeU = 15, stiffness = 6, damping = 4, gear = 30 },
                    new BoneJointSettings { rangeL = -25, rangeU = 25, stiffness = 6, damping = 4, gear = 40 },
                },
                ["Chest"] = new[]
                {
                    new BoneJointSettings { rangeL = -20, rangeU = 10, stiffness = 8, damping = 5, gear = 35 },
                    new BoneJointSettings { rangeL = -10, rangeU = 10, stiffness = 8, damping = 5, gear = 25 },
                    new BoneJointSettings { rangeL = -18, rangeU = 18, stiffness = 8, damping = 5, gear = 35 },
                },
                ["UpperChest"] = new[]
                {
                    new BoneJointSettings { rangeL = -5, rangeU = 15, stiffness = 10, damping = 5, gear = 25 },
                    new BoneJointSettings { rangeL = -10, rangeU = 10, stiffness = 10, damping = 5, gear = 25 },
                    new BoneJointSettings { rangeL = -8,  rangeU = 8,  stiffness = 10, damping = 5, gear = 25 },
                },
                ["Neck"] = new[]
                {
                    new BoneJointSettings { rangeL = -15, rangeU = 20, stiffness = 5, damping = 3, gear = 10 },
                    new BoneJointSettings { rangeL = -25, rangeU = 25, stiffness = 5, damping = 3, gear = 10 },
                    new BoneJointSettings { rangeL = -15, rangeU = 15, stiffness = 6, damping = 4, gear = 10 },
                },
                ["Head"] = new[]
                {
                    new BoneJointSettings { rangeL = -15, rangeU = 15, stiffness = 8, damping = 5, gear = 10 },
                    new BoneJointSettings { rangeL = -10, rangeU = 10, stiffness = 10, damping = 5, gear = 10 },
                    new BoneJointSettings { rangeL = -8,  rangeU = 8,  stiffness = 10, damping = 5, gear = 10 },
                },

                // ── Arm chain ─────────────────────────────────────────────

                ["Shoulder"] = new[]
                {
                    new BoneJointSettings { rangeL = -10, rangeU = 35, stiffness = 3, damping = 2, gear = 15 },
                    new BoneJointSettings { rangeL = -20, rangeU = 25, stiffness = 3, damping = 2, gear = 15 },
                    new BoneJointSettings { rangeL = -10, rangeU = 10, stiffness = 5, damping = 3, gear = 15 },
                },
                ["UpperArm"] = new[]
                {
                    new BoneJointSettings { rangeL = -40,  rangeU = 150, stiffness = 2,  damping = 1,   gear = 50 },
                    new BoneJointSettings { rangeL = -60,  rangeU = 80,  stiffness = 3,  damping = 2,   gear = 50 },
                    new BoneJointSettings { rangeL = -60,  rangeU = 50,  stiffness = 1,  damping = 0.5f, gear = 50 },
                },
                ["LowerArm"] = new[]
                {
                    new BoneJointSettings { rangeL = -140, rangeU = 2,  stiffness = 1, damping = 0.2f, gear = 30 },
                    new BoneJointSettings { rangeL = -5,   rangeU = 10, stiffness = 3, damping = 1,    gear = 30 },
                    new BoneJointSettings { rangeL = -25,  rangeU = 5,  stiffness = 4, damping = 3,    gear = 30 },
                },
                ["Hand"] = new[]
                {
                    new BoneJointSettings { rangeL = -70, rangeU = 80, stiffness = 3, damping = 2, gear = 15 },
                    new BoneJointSettings { rangeL = -30, rangeU = 20, stiffness = 3, damping = 2, gear = 15 },
                    new BoneJointSettings { rangeL = -10, rangeU = 10, stiffness = 5, damping = 3, gear = 15 },
                },

                // ── Leg chain ─────────────────────────────────────────────

                ["Hips"] = new[]
                {
                    new BoneJointSettings { rangeL = -15, rangeU = 25, stiffness = 5, damping = 3, gear = 40 },
                    new BoneJointSettings { rangeL = -25, rangeU = 25, stiffness = 5, damping = 3, gear = 40 },
                    new BoneJointSettings { rangeL = -20, rangeU = 20, stiffness = 5, damping = 3, gear = 40 },
                },
                ["UpperLeg"] = new[]
                {
                    new BoneJointSettings { rangeL = -30,  rangeU = 120, stiffness = 3, damping = 1, gear = 120 },
                    new BoneJointSettings { rangeL = -40,  rangeU = 45,  stiffness = 4, damping = 2, gear = 40 },
                    new BoneJointSettings { rangeL = -40,  rangeU = 40,  stiffness = 4, damping = 2, gear = 40 },
                },
                ["LowerLeg"] = new[]
                {
                    new BoneJointSettings { rangeL = -135, rangeU = 2, stiffness = 1,  damping = 0.2f, gear = 80 },
                    new BoneJointSettings { rangeL = -5,   rangeU = 5, stiffness = 10, damping = 5,    gear = 10 },
                    new BoneJointSettings { rangeL = -5,   rangeU = 5, stiffness = 10, damping = 5,    gear = 10 },
                },
                ["Foot"] = new[]
                {
                    new BoneJointSettings { rangeL = -30, rangeU = 50, stiffness = 3, damping = 2, gear = 20 },
                    new BoneJointSettings { rangeL = -25, rangeU = 20, stiffness = 4, damping = 3, gear = 15 },
                    new BoneJointSettings { rangeL = -10, rangeU = 10, stiffness = 5, damping = 3, gear = 10 },
                },
                ["Toes"] = new[]
                {
                    new BoneJointSettings { rangeL = -30, rangeU = 50, stiffness = 2, damping = 1, gear = 10 },
                    new BoneJointSettings { rangeL = -5,  rangeU = 5,  stiffness = 5, damping = 3, gear = 10 },
                    new BoneJointSettings { rangeL = -5,  rangeU = 5,  stiffness = 5, damping = 3, gear = 10 },
                },

                // ── Extended / physics-only bones ─────────────────────────

                ["Pectoral"] = new[]
                {
                    new BoneJointSettings { rangeL = -5,  rangeU = 5,  stiffness = 1, damping = 0.2f, gear = 10 },
                    new BoneJointSettings { rangeL = -10, rangeU = 12, stiffness = 1, damping = 0.2f, gear = 10 },
                    new BoneJointSettings { rangeL = -12, rangeU = 10, stiffness = 2, damping = 0.2f, gear = 10 },
                },

                // ── Daz-specific intermediate bones ───────────────────────
                // These only match models with extra hierarchy (Genesis8, etc.).
                // Standard humanoids (Mixamo, UE Mannequin) won't have these bones.

                ["chestLower"] = new[]
                {
                    new BoneJointSettings { rangeL = -5,  rangeU = 5,  stiffness = 10, damping = 5, gear = 25 },
                    new BoneJointSettings { rangeL = -12, rangeU = 12, stiffness = 10, damping = 5, gear = 25 },
                    new BoneJointSettings { rangeL = -10, rangeU = 10, stiffness = 10, damping = 5, gear = 25 },
                },
                ["neckUpper"] = new[]
                {
                    new BoneJointSettings { rangeL = -20, rangeU = 30, stiffness = 4, damping = 3, gear = 10 },
                    new BoneJointSettings { rangeL = -45, rangeU = 45, stiffness = 5, damping = 3, gear = 10 },
                    new BoneJointSettings { rangeL = -20, rangeU = 20, stiffness = 7, damping = 5, gear = 10 },
                },
                ["pelvis"] = new[]
                {
                    new BoneJointSettings { rangeL = -15, rangeU = 25, stiffness = 5, damping = 3, gear = 40 },
                    new BoneJointSettings { rangeL = -25, rangeU = 25, stiffness = 5, damping = 3, gear = 40 },
                    new BoneJointSettings { rangeL = -20, rangeU = 20, stiffness = 5, damping = 3, gear = 40 },
                },

                // ── Twist bones (Daz) ─────────────────────────────────────

                ["ShldrTwist"] = new[]
                {
                    new BoneJointSettings { rangeL = -5,  rangeU = 5,  stiffness = 8, damping = 5, gear = 15 },
                    new BoneJointSettings { rangeL = -15, rangeU = 15, stiffness = 8, damping = 5, gear = 15 },
                    new BoneJointSettings { rangeL = -10, rangeU = 10, stiffness = 8, damping = 5, gear = 15 },
                },
                ["ForearmTwist"] = new[]
                {
                    new BoneJointSettings { rangeL = -5,  rangeU = 5,  stiffness = 5, damping = 3, gear = 15 },
                    new BoneJointSettings { rangeL = -10, rangeU = 10, stiffness = 5, damping = 3, gear = 15 },
                    new BoneJointSettings { rangeL = -5,  rangeU = 5,  stiffness = 5, damping = 3, gear = 15 },
                },
                ["ThighTwist"] = new[]
                {
                    new BoneJointSettings { rangeL = -5, rangeU = 5, stiffness = 8, damping = 5, gear = 15 },
                    new BoneJointSettings { rangeL = -8, rangeU = 8, stiffness = 8, damping = 5, gear = 15 },
                    new BoneJointSettings { rangeL = -5, rangeU = 5, stiffness = 8, damping = 5, gear = 15 },
                },
                ["Metatarsals"] = new[]
                {
                    new BoneJointSettings { rangeL = -25, rangeU = 35, stiffness = 3, damping = 2, gear = 10 },
                    new BoneJointSettings { rangeL = -10, rangeU = 10, stiffness = 5, damping = 3, gear = 10 },
                    new BoneJointSettings { rangeL = -5,  rangeU = 5,  stiffness = 5, damping = 3, gear = 10 },
                },
            };
        }

        /// <summary>
        /// Returns tuned joint settings [X, Y, Z] for a canonical bone name, or null if unknown.
        /// </summary>
        public static BoneJointSettings[] Get(string canonicalBoneName)
        {
            if (string.IsNullOrEmpty(canonicalBoneName)) return null;
            return _defaults.TryGetValue(canonicalBoneName, out var settings) ? settings : null;
        }

        /// <summary>
        /// Returns a single axis setting for a canonical bone, or BoneJointSettings.Default if unknown.
        /// </summary>
        public static BoneJointSettings GetAxis(string canonicalBoneName, int axisIndex)
        {
            var settings = Get(canonicalBoneName);
            if (settings != null && axisIndex >= 0 && axisIndex < settings.Length)
                return settings[axisIndex];
            return BoneJointSettings.Default;
        }
    }
}
