using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Auditory sense — spatial audio perception from the Synth's head.
    ///
    /// Wraps Unity's audio system to provide sound-based observations.
    /// Useful for the task planner (L2) to detect and localize sound sources
    /// in the environment (e.g. speech, footsteps, alarms).
    ///
    /// Currently a stub — returns empty observations. Will be implemented
    /// when audio-reactive behaviors are needed.
    ///
    /// Future implementation:
    ///   - AudioListener on the head bone (placed during BindHumanModel)
    ///   - FFT spectrum analysis for frequency features
    ///   - Spatial audio direction estimation
    ///   - Sound event detection (onset, offset, loudness)
    ///
    /// Attach to the same GameObject as SynthEntity.
    /// </summary>
    public class SynthAuditory : MonoBehaviour, ISynthSense
    {
        [Header("Auditory")]
        [Tooltip("Number of FFT frequency bins for spectrum analysis")]
        public int spectrumBins = 64;

        [Tooltip("Additional features: loudness, spatial direction (3D)")]
        public int spatialFeatures = 4;

        private float[] _obsBuffer;

        public string Name => "Auditory";
        public int Dimension => spectrumBins + spatialFeatures;
        public bool IsReady => true; // Stub always ready

        /// <summary>
        /// Read the current auditory observation.
        /// STUB: returns zeros. Will process AudioListener data in future.
        /// </summary>
        public float[] GetObservation()
        {
            // TODO: Wire to Unity AudioListener
            int dim = Dimension;
            if (_obsBuffer == null || _obsBuffer.Length != dim)
                _obsBuffer = new float[dim];
            else
                System.Array.Clear(_obsBuffer, 0, dim);
            return _obsBuffer;
        }
    }
}
