using UnityEngine;
using Mujoco;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// The mind. Owns the Synth's decision-making.
    ///
    /// During training: Dormant. The trainer bypasses the brain entirely and
    /// injects actions directly into the training environment.
    ///
    /// During inference: Active. Ticks each brain layer at its own frequency
    /// and applies the resulting motor actions to the motor system. The brain
    /// is a pure coordinator — it has zero skill-specific configuration and
    /// does NOT read from senses directly.
    ///
    /// Hierarchical architecture (three layers, each optional):
    ///   Layer 2 (Cortex):     ISynthPlanner       — task planner (~seconds)
    ///   Layer 1 (Midbrain):   ISynthSkillSelector  — skill selector (~100ms)
    ///   Layer 0 (Cerebellum): ISynthSkill           — motor skill (~10ms)
    ///
    /// Each layer runs at its own tick frequency:
    ///   L2 ticks every planner.TickInterval physics steps (slowest)
    ///   L1 ticks every selector.TickInterval physics steps (medium)
    ///   L0 ticks every skill.FrameSkip physics steps (fastest)
    ///
    /// Data flows top-down:
    ///   Planner.Tick(senses) → GoalContext
    ///   Selector.Tick(goal, senses) → SkillContext
    ///   Skill.Act() → motor actions (skill reads its own senses)
    ///   Brain applies actions to motor system
    ///
    /// Component discovery (all via GetComponent):
    ///   - ISynthPlanner (optional) — if absent, no goal-directed behavior
    ///   - ISynthSkillSelector (optional) — if absent, skill runs unconditionally
    ///   - ISynthSkill (required) — the action producer
    ///   - ISynthSense[] — for management/logging and passing to L1/L2
    ///   - SynthMotorSystem — the only thing the brain writes to
    ///
    /// Physics coupling: The brain does NOT pause or step MjScene. The world runs
    /// continuously via MjScene.FixedUpdate. The brain is just another FixedUpdate
    /// consumer. Between decisions, the motor system repeats the last action via
    /// ctrlCallback. This scales to any number of Synths in the same scene.
    /// </summary>
    [RequireComponent(typeof(SynthMotorSystem))]
    [RequireComponent(typeof(TaskPlanner))]
    [RequireComponent(typeof(SkillSelector))]
    public class SynthBrain : MonoBehaviour
    {
        [Header("State")]
        [Tooltip("Whether the brain is actively controlling the Synth")]
        [SerializeField] private bool isActive = false;

        [Header("References")]
        [SerializeField] private SynthMotorSystem motorSystem;

        // --- Discovered components ---
        private ISynthSkill activeSkill;          // L0: motor skill (required)
        private ISynthSkillSelector selector;     // L1: skill selector (optional)
        private ISynthPlanner planner;            // L2: task planner (optional)
        private ISynthSense[] senses;             // all discovered senses

        // --- Decision timing ---
        private int physicsStepCount;
        private bool enablePending = false;
        private int enableRetryCount = 0;
        private const int MAX_ENABLE_RETRIES = 500; // ~10s at 50Hz FixedUpdate
        private int _decisionCount;
        private float _decisionDt; // physics time per decision = frameSkip * mj_timestep

        // Set by Disable() to prevent Start() from auto-enabling.
        // A trainer calls Disable() in Awake() to claim the brain before Start() runs.
        private bool suppressAutoEnable = false;

        /// <summary>Whether the brain is actively controlling the Synth.</summary>
        public bool IsActive => isActive;

        /// <summary>The currently active motor skill (L0), or null if none.</summary>
        public ISynthSkill ActiveSkill => activeSkill;

        /// <summary>The skill selector (L1), or null if not present.</summary>
        public ISynthSkillSelector Selector => selector;

        /// <summary>The task planner (L2), or null if not present.</summary>
        public ISynthPlanner Planner => planner;

        /// <summary>All discovered senses on this Synth.</summary>
        public ISynthSense[] Senses => senses;

        void Awake()
        {
            // Discover motor system (the only thing the brain writes to)
            if (motorSystem == null)
            {
                motorSystem = GetComponent<SynthMotorSystem>();
                if (motorSystem == null)
                    motorSystem = GetComponentInParent<SynthMotorSystem>();
            }

            // Discover all senses (passed to L1/L2, skills read them directly)
            senses = GetComponents<ISynthSense>();
        }

        void Start()
        {
            // Skip auto-enable if a trainer already called Disable() during Awake().
            // Unity guarantees all Awake() calls run before any Start() calls, so
            // if a trainer is present it will have claimed the brain by now.
            if (suppressAutoEnable)
                return;

            // Auto-enable if a skill component is present.
            // The skill's Initialize() checks prerequisites (trained model,
            // TorchSharp) and returns false gracefully if not ready.
            var skill = GetComponent<ISynthSkill>();
            if (skill != null)
            {
                isActive = false;
                // Try immediately; if MjScene hasn't started yet, Enable() sets
                // enablePending=true and FixedUpdate retries every frame (~0.02s)
                // instead of the old 0.5s Invoke delay. This ensures the brain
                // activates on the very first FixedUpdate after MjScene is ready,
                // preventing the synth from falling as a ragdoll during startup.
                Enable();
            }
        }

        /// <summary>
        /// Activate the brain for autonomous control.
        /// Discovers all layer components, initializes them, and activates.
        /// If the skill can't initialize yet (senses/MjScene not ready), sets
        /// enablePending so FixedUpdate retries every frame (~0.02s).
        /// </summary>
        public void Enable()
        {
            suppressAutoEnable = false;

            // --- Discover L0: Motor Skill (required) ---
            if (activeSkill == null)
            {
                activeSkill = GetComponent<ISynthSkill>();
                if (activeSkill == null)
                    activeSkill = GetComponentInChildren<ISynthSkill>();

                if (activeSkill == null)
                {
                    Debug.LogWarning("SynthBrain: No ISynthSkill component found on this GameObject");
                    return;
                }
            }

            // --- Discover L1: Skill Selector (optional) ---
            if (selector == null)
                selector = GetComponent<ISynthSkillSelector>();

            // --- Discover L2: Task Planner (optional) ---
            if (planner == null)
                planner = GetComponent<ISynthPlanner>();

            // --- Initialize L0 skill ---
            if (!activeSkill.IsReady)
            {
                enableRetryCount++;
                bool success = activeSkill.Initialize();
                if (!success)
                {
                    if (enableRetryCount >= MAX_ENABLE_RETRIES)
                    {
                        Debug.LogWarning($"SynthBrain: Skill '{activeSkill.Name}' failed after {MAX_ENABLE_RETRIES} retries (~{MAX_ENABLE_RETRIES * 0.02f:F1}s) — brain staying dormant.");
                        enablePending = false;
                        return;
                    }
                    // FixedUpdate will retry next frame (no more 0.5s Invoke delay)
                    enablePending = true;
                    return;
                }
            }

            // --- Initialize L1 selector (if present) ---
            if (selector != null && !selector.IsReady)
                selector.Initialize();

            // --- Initialize L2 planner (if present) ---
            if (planner != null && !planner.IsReady)
                planner.Initialize();

            // --- Activate ---
            isActive = true;
            enablePending = false;
            enableRetryCount = 0;
            physicsStepCount = 0;
            _decisionCount = 0;

            // Compute physics dt per decision (must match training's frameSkip * timestep)
            _decisionDt = GetDecisionDt(activeSkill.FrameSkip);

            activeSkill.Reset();

            // Produce the first action immediately so pendingAction is non-null
            // before MjScene's next ctrlCallback fires.
            DecisionStep();

            // Register for per-substep decisions: the motor system will call
            // DecisionStep every frameSkip MuJoCo substeps, matching training's
            // observation-to-action frequency exactly. Without this, the brain
            // only decides once per FixedUpdate (every 10 substeps) while the
            // policy was trained to decide every 2 substeps.
            if (motorSystem != null)
            {
                motorSystem.decisionInterval = activeSkill.FrameSkip;
                motorSystem.onDecisionNeeded = DecisionStep;
                motorSystem.motorEnabled = true;
            }

            int subSteps = GetSubStepsPerFixedUpdate();
            int decisionsPerFrame = subSteps / Mathf.Max(1, activeSkill.FrameSkip);

            string senseNames = senses != null && senses.Length > 0
                ? string.Join(", ", System.Array.ConvertAll(senses, s => s.Name))
                : "none";
            string plannerName = planner != null ? planner.Name : "none";
            string selectorName = selector != null ? selector.Name : "none";
            Debug.Log($"SynthBrain: Enabled (" +
                      $"L2={plannerName}, " +
                      $"L1={selectorName}, " +
                      $"L0='{activeSkill.Name}' frameSkip={activeSkill.FrameSkip}, " +
                      $"motorEnabled={motorSystem?.motorEnabled}, " +
                      $"subSteps/fixedUpdate={subSteps}, " +
                      $"decisions/fixedUpdate={decisionsPerFrame}, " +
                      $"decisionDt={_decisionDt:F6}s, " +
                      $"fixedDt={Time.fixedDeltaTime:F6}, " +
                      $"senses=[{senseNames}])");
        }

        /// <summary>
        /// Deactivate the brain. The motor system will stop receiving commands.
        /// Used when a trainer takes over control.
        /// </summary>
        public void Disable()
        {
            isActive = false;
            enablePending = false;
            suppressAutoEnable = true;
            if (motorSystem != null)
            {
                motorSystem.motorEnabled = false;
                motorSystem.onDecisionNeeded = null;
                motorSystem.decisionInterval = 0;
            }
            Debug.Log("SynthBrain: Disabled (dormant)");
        }

        /// <summary>
        /// Set an externally-created skill as the active skill.
        /// Useful for testing or for higher-level systems that compose skills.
        /// </summary>
        public void SetSkill(ISynthSkill skill)
        {
            activeSkill = skill;
            isActive = false; // Caller must call Enable() after
        }

        /// <summary>
        /// Handles deferred enable and higher-level brain ticks (L2/L1).
        ///
        /// L0 motor decisions do NOT happen here -- they are triggered by
        /// SynthMotorSystem.onDecisionNeeded every frameSkip MuJoCo substeps,
        /// matching the training's observation-to-action frequency exactly.
        /// </summary>
        void FixedUpdate()
        {
            // Deferred enable: retry every FixedUpdate until skill is ready.
            if (enablePending && !isActive)
            {
                Enable();
                return;
            }

            if (!isActive || activeSkill == null || !activeSkill.IsReady || motorSystem == null)
                return;

            physicsStepCount++;

            // L2: Planner — slowest tick (~seconds)
            if (planner != null && planner.IsReady &&
                physicsStepCount % planner.TickInterval == 0)
            {
                planner.Tick(senses);
            }

            // L1: Selector — medium tick (~100ms)
            if (selector != null && selector.IsReady &&
                physicsStepCount % selector.TickInterval == 0)
            {
                var goal = planner != null ? planner.CurrentGoal : GoalContext.Idle;
                selector.Tick(goal, senses);
            }

            // L0: Motor skill decisions are triggered by the motor system's
            // per-substep callback (onDecisionNeeded), not here.
        }

        /// <summary>
        /// One L0 decision step. Called by the motor system's per-substep callback
        /// every frameSkip MuJoCo substeps, matching training's decision frequency.
        ///
        /// Flow: skill reads senses → produces actions → brain writes to motor system
        ///       → advance reference motion by frameSkip * mj_timestep.
        /// </summary>
        private unsafe void DecisionStep()
        {
            if (!MjScene.InstanceExists || MjScene.Instance.Model == null) return;

            var action = activeSkill.Act();
            if (action == null)
            {
                if (_decisionCount == 0)
                    Debug.LogWarning("SynthBrain: First Act() returned null — skill may lack observations");
                return;
            }

            motorSystem.ApplyAction(action);
            _decisionCount++;

            if (_decisionCount == 1)
            {
                float absMax = 0f;
                for (int i = 0; i < action.Length; i++)
                    absMax = Mathf.Max(absMax, Mathf.Abs(action[i]));
                Debug.Log($"SynthBrain: First action produced — " +
                          $"act_dim={action.Length}, |max|={absMax:F4}, " +
                          $"decisionDt={_decisionDt:F4}s");
            }

            // Advance reference motion by the physics time this action covers,
            // NOT by FixedDeltaTime (which would be 5x too fast).
            activeSkill.AdvanceTime(_decisionDt);
        }

        private static unsafe float GetDecisionDt(int frameSkip)
        {
            if (!MjScene.InstanceExists || MjScene.Instance.Model == null) return Time.fixedDeltaTime;
            return (float)(frameSkip * MjScene.Instance.Model->opt.timestep);
        }

        private static unsafe int GetSubStepsPerFixedUpdate()
        {
            if (!MjScene.InstanceExists || MjScene.Instance.Model == null) return 1;
            double ts = MjScene.Instance.Model->opt.timestep;
            return ts > 0 ? Mathf.RoundToInt((float)(Time.fixedDeltaTime / ts)) : 1;
        }
    }
}
