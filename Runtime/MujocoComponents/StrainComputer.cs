using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Mujoco;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Computes per-joint strain from MuJoCo state, modeling proprioceptive
    /// discomfort/pain. The agent observes strain and learns to avoid
    /// unnatural postures, mimicking biological pain avoidance.
    ///
    /// Strain sources per DOF:
    ///   - limit_proximity: quadratic ramp as joint approaches its range limits
    ///   - passive_strain:  spring/damper reaction (qfrc_passive²)
    ///   - limit_strain:    joint limit constraint forces (qfrc_constraint²)
    ///   - effort_strain:   actuator effort (qfrc_actuator²)
    ///
    /// Sensitivity is scaled inversely by joint range: narrow-range DOFs
    /// (e.g. knee twist ±5°) produce much more strain per degree than
    /// wide-range DOFs (e.g. hip flexion ±75°).
    /// </summary>
    public class StrainComputer
    {
        private const float BASE_SENSITIVITY = 1f;
        private const float W_LIMIT = 0.4f;
        private const float W_PASSIVE = 0.2f;
        private const float W_CONSTRAINT = 0.3f;
        private const float W_EFFORT = 0.1f;
        private const float MIN_HALF_RANGE = 0.01f; // prevent division by zero (~0.6°)

        private readonly int _numIncludedJoints;
        private readonly int[] _includedQvelIdx;

        // Pre-computed per-joint constants (indexed by included joint order)
        private readonly float[] _midRange;
        private readonly float[] _halfRange;
        private readonly float[] _sensitivity;

        // Output buffer
        private readonly float[] _strainBuffer;

        public int Dimension => _numIncludedJoints;
        public float[] StrainBuffer => _strainBuffer;

        /// <summary>
        /// Build from the MuJoCo model and BoneFilterConfig.
        /// Joint ranges come from model->jnt_range for hinge joints.
        /// </summary>
        public unsafe StrainComputer(MujocoLib.mjModel_* model, BoneFilterConfig filter)
        {
            _includedQvelIdx = filter.includedQvelIdx;
            _numIncludedJoints = _includedQvelIdx.Length;
            _strainBuffer = new float[_numIncludedJoints];

            _midRange = new float[_numIncludedJoints];
            _halfRange = new float[_numIncludedJoints];
            _sensitivity = new float[_numIncludedJoints];

            int njnt = (int)model->njnt;

            for (int i = 0; i < _numIncludedJoints; i++)
            {
                int qvelIdx = _includedQvelIdx[i];
                // Map qvel index to joint index: free joint is joint 0 (nv=6),
                // first hinge is joint 1 at qvel[6]. So jntIdx = qvelIdx - 5.
                int jntIdx = qvelIdx - 5;

                float rangeL = 0f, rangeU = 0f;
                if (jntIdx >= 0 && jntIdx < njnt)
                {
                    // model->jnt_range is (njnt x 2) array
                    rangeL = (float)model->jnt_range[jntIdx * 2];
                    rangeU = (float)model->jnt_range[jntIdx * 2 + 1];
                }

                float mid = (rangeL + rangeU) * 0.5f;
                float half = Math.Max(Math.Abs(rangeU - rangeL) * 0.5f, MIN_HALF_RANGE);

                _midRange[i] = mid;
                _halfRange[i] = half;
                _sensitivity[i] = BASE_SENSITIVITY / half;
            }
        }

        /// <summary>
        /// Compute per-joint strain from current simulation state.
        /// Results written to StrainBuffer (length = Dimension).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Compute(MujocoLib.mjData_* data)
        {
            double* qpos = data->qpos;
            double* qfrcPassive = data->qfrc_passive;
            double* qfrcConstraint = data->qfrc_constraint;
            double* qfrcActuator = data->qfrc_actuator;

            for (int i = 0; i < _numIncludedJoints; i++)
            {
                int vi = _includedQvelIdx[i];
                // qpos index for hinge joint: 7 + (vi - 6) = vi + 1
                int qi = vi + 1;

                float pos = (float)qpos[qi];
                float deviation = (pos - _midRange[i]) / _halfRange[i];
                float limitProximity = deviation * deviation; // 0 at center, 1 at limit

                float passive = (float)qfrcPassive[vi];
                float constraint = (float)qfrcConstraint[vi];
                float actuator = (float)qfrcActuator[vi];

                float strain = W_LIMIT * limitProximity
                             + W_PASSIVE * passive * passive
                             + W_CONSTRAINT * constraint * constraint
                             + W_EFFORT * actuator * actuator;

                strain *= _sensitivity[i];

                // Clamp and sanitize
                if (float.IsNaN(strain) || float.IsInfinity(strain)) strain = 0f;
                if (strain > 100f) strain = 100f;

                _strainBuffer[i] = strain;
            }
        }

        /// <summary>
        /// Copy strain observations into a destination buffer at the given offset.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FillObservation(float[] dest, int offset)
        {
            Buffer.BlockCopy(_strainBuffer, 0, dest, offset * sizeof(float),
                _numIncludedJoints * sizeof(float));
        }

        /// <summary>
        /// Mean strain across all included joints. Used for the r_comfort reward.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float MeanStrain()
        {
            if (_numIncludedJoints == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < _numIncludedJoints; i++)
                sum += _strainBuffer[i];
            return sum / _numIncludedJoints;
        }
    }
}
