using System;
using UnityEngine;
using Mujoco;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Shared pure functions for building MuJoCo-level observation vectors.
    ///
    /// The field order matches the trained imitation model EXACTLY.
    /// Do NOT change the layout without retraining.
    /// </summary>
    public static class SynthObservations
    {
        public static int GetPhysicsDim(BoneFilterConfig filter)
        {
            return filter.physicsObsDim;
        }

        public static int GetImitationObsDim(BoneFilterConfig filter)
        {
            int refDim = 5 + filter.includedQposIdx.Length + 6 + filter.includedQvelIdx.Length;
            int phaseDim = 2;
            return filter.physicsObsDim + refDim + phaseDim;
        }

        /// <summary>
        /// Build the physics observation vector from MuJoCo data pointers.
        ///
        /// Lean layout + contact summary:
        ///   qpos[2:7]                    (root Z + quat)              = 5
        ///   qpos[filtered hinges]                                     = N
        ///   qvel[0:6]                    (root vel)                   = 6
        ///   qvel[filtered hinges]                                     = N
        ///   qfrc_actuator[filtered DOFs]                              = N
        ///   totalCfrcExt                 (sum of all body contact)    = 6
        /// </summary>
        public static unsafe float[] BuildPhysicsObs(MujocoLib.mjData_* data, BoneFilterConfig filter)
        {
            var obs = new float[filter.physicsObsDim];
            FillPhysicsObs(obs, 0, data->qpos, data->qvel,
                data->qfrc_actuator, data->cfrc_ext, filter);
            return obs;
        }

        /// <summary>
        /// Fill physics obs directly into a pre-allocated buffer at the given offset.
        /// Zero-allocation hot path for training loops.
        /// </summary>
        public static unsafe void FillPhysicsObs(
            float[] obs, int offset,
            double* qpos, double* qvel, double* qfrcActuator, double* cfrcExt,
            BoneFilterConfig filter)
        {
            int[] inclQpos = filter.includedQposIdx;
            int[] inclQvel = filter.includedQvelIdx;
            int nbody = filter.nbody;

            int idx = offset;

            if (qpos != null)
            {
                obs[idx++] = (float)qpos[2];
                for (int i = 3; i < 7; i++) obs[idx++] = (float)qpos[i];
                for (int i = 0; i < inclQpos.Length; i++)
                    obs[idx++] = (float)qpos[inclQpos[i]];
            }
            else idx += 5 + inclQpos.Length;

            if (qvel != null)
            {
                for (int i = 0; i < 6; i++) obs[idx++] = (float)qvel[i];
                for (int i = 0; i < inclQvel.Length; i++)
                    obs[idx++] = (float)qvel[inclQvel[i]];
            }
            else idx += 6 + inclQvel.Length;

            if (qfrcActuator != null)
            {
                for (int i = 0; i < inclQvel.Length; i++)
                    obs[idx++] = (float)qfrcActuator[inclQvel[i]];
            }
            else idx += inclQvel.Length;

            if (cfrcExt != null)
            {
                float t0 = 0f, t1 = 0f, t2 = 0f, t3 = 0f, t4 = 0f, t5 = 0f;
                for (int b = 1; b < nbody; b++)
                {
                    double* src = cfrcExt + b * 6;
                    t0 += (float)src[0]; t1 += (float)src[1]; t2 += (float)src[2];
                    t3 += (float)src[3]; t4 += (float)src[4]; t5 += (float)src[5];
                }
                obs[idx++] = t0; obs[idx++] = t1; obs[idx++] = t2;
                obs[idx++] = t3; obs[idx++] = t4; obs[idx++] = t5;
            }
            else idx += 6;

            for (int i = offset; i < idx; i++)
            {
                float v = obs[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) obs[i] = 0f;
                else if (v > 1e6f) obs[i] = 1e6f;
                else if (v < -1e6f) obs[i] = -1e6f;
            }
        }

        /// <summary>
        /// Append reference motion state and phase to an existing observation array.
        /// </summary>
        public static void AppendReferenceObs(
            float[] obs, ref int idx,
            double[] refQpos, double[] refQvel,
            float phase, BoneFilterConfig filter)
        {
            obs[idx++] = (float)refQpos[2];
            for (int i = 3; i < 7; i++) obs[idx++] = (float)refQpos[i];

            for (int i = 0; i < filter.includedQposIdx.Length; i++)
                obs[idx++] = (float)refQpos[filter.includedQposIdx[i]];

            for (int i = 0; i < 6; i++) obs[idx++] = (float)refQvel[i];

            for (int i = 0; i < filter.includedQvelIdx.Length; i++)
                obs[idx++] = (float)refQvel[filter.includedQvelIdx[i]];

            obs[idx++] = Mathf.Sin(2f * Mathf.PI * phase);
            obs[idx++] = Mathf.Cos(2f * Mathf.PI * phase);
        }
    }
}
