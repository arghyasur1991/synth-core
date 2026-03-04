namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// A self-contained action generator — a "skill" that the brain can invoke.
    ///
    /// Skills encapsulate everything needed to produce motor actions:
    ///   - Their own neural network / policy
    ///   - Any normalizer state
    ///   - Internal state tracking (e.g. reference motion time)
    ///   - Decision frequency (FrameSkip — must match training)
    ///   - Which senses they read from (discovered via GetComponent)
    ///
    /// Skills discover their own senses — the brain does NOT pre-select or pass
    /// observations. Each skill decides which ISynthSense components it needs
    /// (e.g. proprioception for motor skills, vision for navigation skills) and
    /// reads from them directly in Act(). This keeps the brain completely
    /// skill-agnostic and allows different skills to use different sense combinations.
    ///
    /// Implementations should be MonoBehaviour components attached to the Synth
    /// GameObject. The brain discovers them via GetComponent&lt;ISynthSkill&gt;().
    /// Each skill owns its own inspector config (model path, clip, network sizes,
    /// frameSkip, etc.) — the brain has zero skill-specific configuration.
    ///
    /// Current implementations:
    ///   - ImitationSkill: DeepMimic-style motion imitation policy
    ///
    /// Future implementations:
    ///   - UniversalMotorSkill: PHC-style universal tracker conditioned on latent z
    ///   - WalkingSkill: task-specific walking policy
    /// </summary>
    public interface ISynthSkill
    {
        /// <summary>Human-readable name (e.g. "Imitation", "Walking").</summary>
        string Name { get; }

        /// <summary>Whether the skill has been initialized and can produce actions.</summary>
        bool IsReady { get; }

        /// <summary>
        /// Number of physics sub-steps per decision. The brain uses this to
        /// determine how often to call Act(). Must match the value used
        /// during training — different skills may have different decision frequencies.
        /// </summary>
        int FrameSkip { get; }

        /// <summary>
        /// Initialize the skill. Called once when the skill is first activated.
        /// The skill discovers its own senses via GetComponent (e.g. SynthProprioception)
        /// and loads model weights, extracts reference data, etc.
        /// No parameters — the skill is self-contained.
        /// </summary>
        /// <returns>True if initialization succeeded, false if it should be retried later.</returns>
        bool Initialize();

        /// <summary>
        /// Produce motor actions by reading from the skill's own senses.
        ///
        /// The skill is responsible for:
        ///   1. Reading observations from its discovered senses (e.g. proprioception)
        ///   2. Augmenting with its own internal state if needed (reference motion, latents)
        ///   3. Normalizing observations
        ///   4. Running the policy forward pass
        ///   5. Returning raw actions for the motor system
        ///
        /// The brain does NOT pass observations — the skill reads what it needs.
        /// </summary>
        /// <returns>Action array for the brain to send to the motor system, or null if not ready.</returns>
        float[] Act();

        /// <summary>
        /// Advance the skill's internal time by the given delta (in seconds).
        /// Called every decision step to keep the skill's state in sync with physics.
        /// </summary>
        void AdvanceTime(float dt);

        /// <summary>
        /// Reset the skill's internal state (e.g. motion time, episode counters).
        /// Called when the brain re-enables or the Synth is respawned.
        /// </summary>
        void Reset();
    }
}
