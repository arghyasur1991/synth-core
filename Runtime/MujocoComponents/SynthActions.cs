using System;
using UnityEngine;
using Mujoco;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Shared internal utility for applying per-actuator actions to MuJoCo ctrl.
    /// </summary>
    public static class SynthActions
    {
        /// <summary>
        /// Zero all ctrl, then apply clamped actions to included actuators.
        /// Excluded actuators receive zero torque (passive/physics-only).
        /// </summary>
        public static unsafe void Apply(
            MujocoLib.mjData_* data,
            float[] actions,
            int[] includedActuatorIdx,
            int totalActuators,
            float clampMin = -0.4f,
            float clampMax = 0.4f)
        {
            double* p = data->ctrl;
            for (int i = 0; i < totalActuators; i++)
                p[i] = 0;

            int count = Math.Min(actions.Length, includedActuatorIdx.Length);
            for (int i = 0; i < count; i++)
                p[includedActuatorIdx[i]] = Mathf.Clamp(actions[i], clampMin, clampMax);
        }
    }
}
