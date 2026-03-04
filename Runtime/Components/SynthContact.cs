using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Contact/touch sense — reads MuJoCo contact force data.
    ///
    /// Provides information about which body parts are in contact with
    /// the environment and the forces involved. Useful for:
    ///   - Motor skills: ground contact detection, grasp feedback
    ///   - Skill selector: collision avoidance, object manipulation
    ///
    /// Currently a stub — returns empty observations. Will be wired to
    /// mjData.contact when contact-aware skills are implemented.
    ///
    /// Future implementation:
    ///   - Read mjData->ncon (number of active contacts)
    ///   - For each contact: body pair, position, normal, force
    ///   - Encode as per-body contact flag + force magnitude
    ///
    /// Attach to the same GameObject as SynthMotorSystem.
    /// </summary>
    public class SynthContact : MonoBehaviour, ISynthSense
    {
        [Header("Contact")]
        [Tooltip("Maximum number of contacts to report")]
        public int maxContacts = 20;

        [Tooltip("Features per contact: geom1, geom2, force_magnitude, normal_x/y/z")]
        public int featuresPerContact = 6;

        private float[] _obsBuffer;

        public string Name => "Contact";
        public int Dimension => maxContacts * featuresPerContact;
        public bool IsReady => true; // Stub always ready

        /// <summary>
        /// Read the current contact observation.
        /// STUB: returns zeros. Will read from mjData.contact in future.
        /// </summary>
        public float[] GetObservation()
        {
            // TODO: Wire to MuJoCo contact data
            int dim = Dimension;
            if (_obsBuffer == null || _obsBuffer.Length != dim)
                _obsBuffer = new float[dim];
            else
                System.Array.Clear(_obsBuffer, 0, dim);
            return _obsBuffer;
        }
    }
}
