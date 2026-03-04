using System.Collections.Generic;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Biomechanically-tuned default mass distribution per canonical bone name.
    /// Derived from the hand-tuned SynthBodyData.asset (Daz Genesis8Female).
    ///
    /// Only selfMassPercent is provided — the fraction of a bone's allocated mass
    /// that it keeps (vs passes down to its children). parentMassPercent depends
    /// on sibling count and hierarchy, which varies per model.
    ///
    /// The auto-computed heuristic (selfMass = 0.1 for internal, 1.0 for leaf)
    /// is replaced with these tuned values where available.
    /// </summary>
    public static class SynthMassDefaults
    {
        public struct MassInfo
        {
            public float selfMassPercent;
        }

        private static readonly Dictionary<string, MassInfo> _defaults;

        static SynthMassDefaults()
        {
            _defaults = new Dictionary<string, MassInfo>(System.StringComparer.OrdinalIgnoreCase)
            {
                // ── Center chain ──────────────────────────────────────────
                ["Spine"]      = new MassInfo { selfMassPercent = 0.144f },
                ["Chest"]      = new MassInfo { selfMassPercent = 0.169f },
                ["UpperChest"] = new MassInfo { selfMassPercent = 0.331f },
                ["Neck"]       = new MassInfo { selfMassPercent = 0.05f },
                ["Head"]       = new MassInfo { selfMassPercent = 0.906f },

                // ── Arm chain ─────────────────────────────────────────────
                ["Shoulder"]   = new MassInfo { selfMassPercent = 0.130f },
                ["UpperArm"]   = new MassInfo { selfMassPercent = 0.313f },
                ["LowerArm"]   = new MassInfo { selfMassPercent = 0.392f },
                ["Hand"]       = new MassInfo { selfMassPercent = 0.60f },

                // ── Leg chain ─────────────────────────────────────────────
                ["UpperLeg"]   = new MassInfo { selfMassPercent = 0.389f },
                ["LowerLeg"]   = new MassInfo { selfMassPercent = 0.789f },
                ["Foot"]       = new MassInfo { selfMassPercent = 0.50f },
                ["Toes"]       = new MassInfo { selfMassPercent = 0.90f },

                // ── Extended bones ────────────────────────────────────────
                ["Pectoral"]   = new MassInfo { selfMassPercent = 1.0f },

                // ── Daz-specific intermediate bones ───────────────────────
                ["chestLower"]    = new MassInfo { selfMassPercent = 0.222f },
                ["neckUpper"]     = new MassInfo { selfMassPercent = 0.10f },
                ["pelvis"]        = new MassInfo { selfMassPercent = 0.278f },
                ["ShldrTwist"]    = new MassInfo { selfMassPercent = 0.372f },
                ["ForearmTwist"]  = new MassInfo { selfMassPercent = 0.526f },
                ["ThighTwist"]    = new MassInfo { selfMassPercent = 0.522f },
                ["Metatarsals"]   = new MassInfo { selfMassPercent = 0.70f },
            };
        }

        /// <summary>
        /// Returns tuned mass info for a canonical bone name, or null if unknown.
        /// </summary>
        public static MassInfo? Get(string canonicalBoneName)
        {
            if (string.IsNullOrEmpty(canonicalBoneName)) return null;
            return _defaults.TryGetValue(canonicalBoneName, out var info) ? info : (MassInfo?)null;
        }
    }
}
