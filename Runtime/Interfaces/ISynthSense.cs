namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// A sensory channel that provides observations to the brain.
    ///
    /// Each sense produces a flat observation vector. The brain concatenates
    /// what it needs per processing layer:
    ///   - Motor controller: proprioception only
    ///   - Skill selector: proprioception + task-specific
    ///   - Task planner: vision + proprioception + memory
    ///
    /// Implementations should be MonoBehaviour components attached to the Synth
    /// GameObject. The brain discovers them via GetComponent&lt;ISynthSense&gt;()
    /// or GetComponents&lt;ISynthSense&gt;() for multiple senses.
    ///
    /// Current implementations:
    ///   - SynthProprioception: body state from MuJoCo (qpos, qvel, cinert, cvel, cfrc_ext)
    ///
    /// Future implementations:
    ///   - SynthVision: egocentric camera on head bone
    ///   - SynthContact: MuJoCo contact data
    ///   - SynthAuditory: Unity AudioListener on head
    /// </summary>
    public interface ISynthSense
    {
        /// <summary>Human-readable name (e.g. "Proprioception", "Vision").</summary>
        string Name { get; }

        /// <summary>Dimensionality of the observation vector.</summary>
        int Dimension { get; }

        /// <summary>Whether this sense has been initialized and can provide observations.</summary>
        bool IsReady { get; }

        /// <summary>
        /// Read the current observation from this sense.
        /// Returns a flat float array of length <see cref="Dimension"/>.
        /// </summary>
        float[] GetObservation();
    }
}
