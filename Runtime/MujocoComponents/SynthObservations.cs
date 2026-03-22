using System;
using UnityEngine;
using Mujoco;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Shared pure functions for building MuJoCo-level observation vectors.
    ///
    /// Layout (v3 — with per-body contact, strain, and external forces):
    ///   qpos[2:7]                    (root Z + quat)              = 5
    ///   qpos[filtered hinges]                                     = N
    ///   qvel[0:6]                    (root vel)                   = 6
    ///   qvel[filtered hinges]                                     = N
    ///   qfrc_actuator[filtered DOFs]                              = N
    ///   contact[8 bodies × 5]        (per-body contact sensing)   = 40
    ///   strain[filtered DOFs]         (per-joint strain/pain)     = N
    ///   xfrc_applied[nbody × 6]      (external forces/torques)   = nbody×6
    ///
    /// BREAKING CHANGE from v2: xfrc_applied (nbody×6) appended.
    /// Saved models must be retrained.
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
        /// Fill physics obs directly into a pre-allocated buffer at the given offset.
        /// Zero-allocation hot path for training loops.
        ///
        /// contactObs and strainObs are pre-computed by SynthContact.ComputeContacts()
        /// and StrainComputer.Compute() respectively, and embedded into the physics obs
        /// at the end of the vector.
        /// </summary>
        public static unsafe void FillPhysicsObs(
            float[] obs, int offset,
            double* qpos, double* qvel, double* qfrcActuator,
            float[] contactObs, float[] strainObs, double* xfrcApplied,
            BoneFilterConfig filter)
        {
            int[] inclQpos = filter.includedQposIdx;
            int[] inclQvel = filter.includedQvelIdx;

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

            // Per-body contact observations (40 floats)
            if (contactObs != null && contactObs.Length == SynthContact.CONTACT_OBS_DIM)
            {
                Buffer.BlockCopy(contactObs, 0, obs, idx * sizeof(float),
                    SynthContact.CONTACT_OBS_DIM * sizeof(float));
            }
            idx += SynthContact.CONTACT_OBS_DIM;

            // Per-joint strain observations (N floats)
            int strainDim = filter.strainObsDim;
            if (strainObs != null && strainObs.Length >= strainDim)
            {
                Buffer.BlockCopy(strainObs, 0, obs, idx * sizeof(float),
                    strainDim * sizeof(float));
            }
            idx += strainDim;

            // Per-body external forces/torques (nbody × 6 floats, scaled by 0.001)
            int xfrcDim = filter.xfrcAppliedObsDim;
            if (xfrcApplied != null && xfrcDim > 0)
            {
                for (int i = 0; i < xfrcDim; i++)
                    obs[idx++] = (float)xfrcApplied[i] * 0.001f;
            }
            else idx += xfrcDim;

            // Sanitize
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
