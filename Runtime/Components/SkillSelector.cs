using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Layer 1 (Midbrain) — skill selector / composer.
    ///
    /// Operates on a medium timescale. Receives the current goal from the
    /// planner and sensory data, and produces a SkillContext that conditions
    /// the motor skill (L0).
    ///
    /// Current implementation: STUB — always produces SkillContext.Default
    /// (no specific skill selection or latent conditioning). This means the
    /// motor skill runs unconditionally, which is correct for ImitationSkill
    /// which doesn't use latent conditioning.
    ///
    /// Future implementation:
    ///   - For universal motor controller: map goals to latent vectors (ASE/AMP style)
    ///   - Skill library: maintain a set of available skills, select the best one
    ///   - Transition planning: smooth skill-to-skill transitions
    ///   - Latent space interpolation for novel behaviors
    ///
    /// Attach to the same GameObject as SynthBrain.
    /// </summary>
    public class SkillSelector : MonoBehaviour, ISynthSkillSelector
    {
        [Header("Selector")]
        [Tooltip("Physics steps between selector ticks (~100ms at 200Hz physics)")]
        public int tickInterval = 20;

        [Tooltip("Whether this selector is functional. Stub defaults to false — SynthBrain skips ticking when not functional.")]
        [SerializeField] private bool isFunctional = false;

        private SkillContext currentContext = SkillContext.Default;

        public string Name => "SkillSelector";
        public bool IsReady => isFunctional;
        public int TickInterval => tickInterval;
        public SkillContext CurrentSkillContext => currentContext;

        public bool Initialize()
        {
            currentContext = SkillContext.Default;
            if (!isFunctional)
            {
                Debug.Log("SkillSelector: Present but not functional (stub). Enable isFunctional when a real implementation is ready.");
                return false;
            }
            Debug.Log("SkillSelector: Initialized");
            return true;
        }

        /// <summary>
        /// Update the skill context based on the current goal and senses.
        /// STUB: always produces SkillContext.Default.
        /// </summary>
        public void Tick(GoalContext goal, ISynthSense[] senses)
        {
            if (!isFunctional) return;

            // TODO: Map goal to skill selection and latent conditioning
            // For universal motor: encode goal into latent z via learned encoder
            // For skill library: select skill based on goal + proprioceptive state
            currentContext = SkillContext.Default;
        }
    }
}
