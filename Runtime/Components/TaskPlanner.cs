using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Layer 2 (Cortex) — high-level task planner.
    ///
    /// Operates on the slowest timescale. Receives all sensory input
    /// (vision, audio, proprioception) and produces a GoalContext that
    /// drives the skill selector.
    ///
    /// Current implementation: STUB — always produces GoalContext.Idle.
    /// The planner becomes meaningful when we have:
    ///   - Trained vision encoder (from SynthVision observations)
    ///   - Working memory / world model
    ///   - Goal specification interface (external commands, internal drives)
    ///
    /// Future implementation:
    ///   - Transformer-based world model conditioned on visual + proprioceptive history
    ///   - Goal sampling from a learned goal space
    ///   - Planning via model-predictive control or tree search
    ///
    /// Attach to the same GameObject as SynthBrain.
    /// </summary>
    public class TaskPlanner : MonoBehaviour, ISynthPlanner
    {
        [Header("Planner")]
        [Tooltip("Physics steps between planner ticks (~seconds at 200Hz physics)")]
        public int tickInterval = 200;

        [Tooltip("Whether this planner is functional. Stub defaults to false — SynthBrain skips ticking when not functional.")]
        [SerializeField] private bool isFunctional = false;

        private GoalContext currentGoal = GoalContext.Idle;

        public string Name => "TaskPlanner";
        public bool IsReady => isFunctional;
        public int TickInterval => tickInterval;
        public GoalContext CurrentGoal => currentGoal;

        public bool Initialize()
        {
            currentGoal = GoalContext.Idle;
            if (!isFunctional)
            {
                Debug.Log("TaskPlanner: Present but not functional (stub). Enable isFunctional when a real implementation is ready.");
                return false;
            }
            Debug.Log("TaskPlanner: Initialized");
            return true;
        }

        /// <summary>
        /// Update the goal based on current sensory state.
        /// STUB: always produces GoalContext.Idle.
        /// </summary>
        public void Tick(ISynthSense[] senses)
        {
            if (!isFunctional) return;

            // TODO: Process vision, audio, memory to produce goals
            // For now, maintain idle goal
            currentGoal = GoalContext.Idle;
        }
    }
}
