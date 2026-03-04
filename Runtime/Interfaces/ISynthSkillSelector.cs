namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Layer 1 (Midbrain) — skill selector / composer.
    ///
    /// The skill selector operates on a medium timescale (~100ms). It receives
    /// the current goal from the planner and sensory data, and produces a
    /// SkillContext that tells the motor skill (L0) how to behave.
    ///
    /// For a universal motor controller, the SkillContext.latent encodes the
    /// desired motion (e.g. from a motion prior or skill embedding).
    /// For simple skills like ImitationSkill, the latent is null/ignored.
    ///
    /// The brain ticks the selector every TickInterval physics steps.
    /// Between ticks, CurrentSkillContext remains stable.
    ///
    /// Implementations should be MonoBehaviour components on the Synth GameObject.
    /// The brain discovers them via GetComponent&lt;ISynthSkillSelector&gt;().
    /// The selector is optional — if absent, the skill runs unconditionally.
    /// </summary>
    public interface ISynthSkillSelector
    {
        /// <summary>Human-readable name (e.g. "SkillSelector").</summary>
        string Name { get; }

        /// <summary>Whether the selector has been initialized.</summary>
        bool IsReady { get; }

        /// <summary>
        /// How many physics steps between selector ticks.
        /// Medium values (~20) mean the selector runs on a ~100ms timescale.
        /// </summary>
        int TickInterval { get; }

        /// <summary>The current skill command. Stable between ticks.</summary>
        SkillContext CurrentSkillContext { get; }

        /// <summary>
        /// Initialize the selector. Called once when the brain enables.
        /// </summary>
        /// <returns>True if initialization succeeded.</returns>
        bool Initialize();

        /// <summary>
        /// Update the skill context based on the current goal and senses.
        /// Called by the brain every TickInterval physics steps.
        /// </summary>
        /// <param name="goal">Current goal from the planner (or GoalContext.Idle if no planner).</param>
        /// <param name="senses">All available senses on this Synth.</param>
        void Tick(GoalContext goal, ISynthSense[] senses);
    }
}
