namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Layer 2 (Cortex) — high-level task planner.
    ///
    /// The planner operates on the slowest timescale (~seconds). It receives
    /// all available sensory data (vision, audio, proprioception, memory) and
    /// produces a GoalContext that tells the skill selector what to achieve.
    ///
    /// Examples of goals: "walk to target", "pick up object", "maintain balance".
    ///
    /// The brain ticks the planner every TickInterval physics steps.
    /// Between ticks, CurrentGoal remains stable — this is by design, as
    /// high-level goals change infrequently compared to motor actions.
    ///
    /// Implementations should be MonoBehaviour components on the Synth GameObject.
    /// The brain discovers them via GetComponent&lt;ISynthPlanner&gt;().
    /// The planner is optional — if absent, the brain operates without goals.
    /// </summary>
    public interface ISynthPlanner
    {
        /// <summary>Human-readable name (e.g. "TaskPlanner").</summary>
        string Name { get; }

        /// <summary>Whether the planner has been initialized and can produce goals.</summary>
        bool IsReady { get; }

        /// <summary>
        /// How many physics steps between planner ticks.
        /// High values (~200) mean the planner runs on a slow timescale (~seconds).
        /// </summary>
        int TickInterval { get; }

        /// <summary>The current goal. Stable between ticks.</summary>
        GoalContext CurrentGoal { get; }

        /// <summary>
        /// Initialize the planner. Called once when the brain enables.
        /// </summary>
        /// <returns>True if initialization succeeded.</returns>
        bool Initialize();

        /// <summary>
        /// Update the goal based on current sensory state.
        /// Called by the brain every TickInterval physics steps.
        /// </summary>
        /// <param name="senses">All available senses on this Synth.</param>
        void Tick(ISynthSense[] senses);
    }
}
