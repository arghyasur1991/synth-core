using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// High-level goal produced by the Cortex (L2 planner).
    /// Consumed by the Midbrain (L1 skill selector) to choose skills.
    /// </summary>
    public struct GoalContext
    {
        /// <summary>Human-readable goal description (e.g. "walk to position X").</summary>
        public string goalDescription;

        /// <summary>World-space target position, if applicable.</summary>
        public Vector3 targetPosition;

        /// <summary>Goal urgency / priority (0 = idle, 1 = critical).</summary>
        public float priority;

        /// <summary>Default idle goal — no active objective.</summary>
        public static GoalContext Idle => new GoalContext
        {
            goalDescription = "idle",
            targetPosition = Vector3.zero,
            priority = 0f
        };
    }

    /// <summary>
    /// Skill command produced by the Midbrain (L1 skill selector).
    /// Consumed by the Cerebellum (L0 motor skill) to condition behavior.
    /// </summary>
    public struct SkillContext
    {
        /// <summary>Name of the skill to activate (must match ISynthSkill.Name).</summary>
        public string skillName;

        /// <summary>
        /// Latent conditioning vector for the motor skill.
        /// For universal motor controllers (PHC-style), this encodes the desired motion.
        /// Null for skills that don't use latent conditioning (e.g. ImitationSkill).
        /// </summary>
        public float[] latent;

        /// <summary>Suggested duration to run this skill (seconds). 0 = indefinite.</summary>
        public float duration;

        /// <summary>Default context — no specific skill or conditioning.</summary>
        public static SkillContext Default => new SkillContext
        {
            skillName = null,
            latent = null,
            duration = 0f
        };
    }
}
