using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Proprioceptive sense — the Synth's awareness of its own body state.
    ///
    /// Wraps SynthMotorSystem.GetObservation() as an ISynthSense, providing:
    ///   - Joint positions and velocities (qpos, qvel)
    ///   - Body inertia and velocity (cinert, cvel)
    ///   - Actuator forces (qfrc_actuator)
    ///   - External contact forces (cfrc_ext)
    ///
    /// Also exposes the body schema (BoneFilterConfig) so motor skills can
    /// discover observation/action dimensions without direct motor system access.
    ///
    /// This is the primary sense for motor control — the motor controller
    /// needs nothing else to produce joint torques. Higher-level systems
    /// (skill selectors, planners) augment this with vision, touch, etc.
    ///
    /// Attach to the same GameObject as SynthMotorSystem.
    /// </summary>
    [RequireComponent(typeof(SynthMotorSystem))]
    public class SynthProprioception : MonoBehaviour, ISynthSense
    {
        private SynthMotorSystem motorSystem;

        public string Name => "Proprioception";
        public int Dimension => motorSystem != null ? motorSystem.ObservationDimension : 0;
        public bool IsReady => motorSystem != null && motorSystem.Filter.IsValid;

        /// <summary>
        /// Body schema (bone filter) for skills that need to know the
        /// observation/action space structure. Exposes motor system dimensions
        /// without giving direct motor system access.
        ///
        /// Skills use this to determine:
        ///   - Observation dimension (nq, nv, nbody, filtered joint indices)
        ///   - Action dimension (actDim, filtered actuator indices)
        ///   - Reference observation construction (filter for AppendReferenceObs)
        /// </summary>
        public BoneFilterConfig Filter => motorSystem != null ? motorSystem.Filter : default;

        /// <summary>
        /// Strain computer for external access (e.g. reward computation needs mean strain).
        /// Returns null before initialization.
        /// </summary>
        public StrainComputer Strain => motorSystem?.Strain;

        void Awake()
        {
            motorSystem = GetComponent<SynthMotorSystem>();
        }

        /// <summary>
        /// Read the current proprioceptive observation from MuJoCo state.
        /// Returns the physics observation vector (joint/body state).
        /// </summary>
        public float[] GetObservation()
        {
            if (!IsReady) return System.Array.Empty<float>();
            return motorSystem.GetObservation();
        }
    }
}
