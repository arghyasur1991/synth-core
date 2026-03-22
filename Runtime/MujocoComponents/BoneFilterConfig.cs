using System;
using System.Collections.Generic;
using UnityEngine;
using Mujoco;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Shared index arrays for bone filtering. Computed once from the MuJoCo model
    /// and a set of excluded (physics-only) actuator indices.
    ///
    /// Assumes standard humanoid layout: 1 free joint (root, 7 qpos / 6 qvel) + N hinge joints.
    /// Each hinge joint has 1 qpos, 1 qvel (DOF), and 1 actuator.
    ///
    /// Physics observation layout:
    ///   qpos[root]    (5)  +  qpos[joints] (N)  +
    ///   qvel[root]    (6)  +  qvel[joints] (N)  +
    ///   qfrc_actuator (N)  +
    ///   contact       (40) +  strain (N)  +
    ///   xfrc_applied  (nbody × 6)
    ///
    /// Used by:
    ///   - SynthMotorSystem (builds once after MjScene init, exposes as property)
    ///   - SynthImitationEnv (receives from trainer, uses for obs/action/reward)
    ///   - SynthBrain (gets from motor system)
    /// </summary>
    public struct BoneFilterConfig
    {
        public int nq, nv, nu, nbody;

        /// <summary>qpos indices for non-excluded hinge joints (root free joint excluded)</summary>
        public int[] includedQposIdx;

        /// <summary>qvel (DOF) indices for non-excluded hinge joints (root free joint excluded)</summary>
        public int[] includedQvelIdx;

        /// <summary>Actuator (ctrl) indices for non-excluded actuators</summary>
        public int[] includedActuatorIdx;

        /// <summary>Number of included joints = includedActuatorIdx.Length</summary>
        public int filteredJointCount;

        /// <summary>Physics observation dimension (before task-specific data like reference/phase)</summary>
        public int physicsObsDim;

        /// <summary>Contact observation dimension (SynthContact per-body data)</summary>
        public int contactObsDim;

        /// <summary>Per-joint strain observation dimension</summary>
        public int strainObsDim;

        /// <summary>Per-body external force observation dimension (nbody × 6)</summary>
        public int xfrcAppliedObsDim;

        /// <summary>Action dimension = filteredJointCount</summary>
        public int actDim;

        /// <summary>Whether this config has been built (nu > 0 after Build)</summary>
        public bool IsValid => nu > 0;

        /// <summary>
        /// Build a BoneFilterConfig from the MuJoCo model and excluded actuator indices.
        /// </summary>
        public static unsafe BoneFilterConfig Build(MujocoLib.mjModel_* model, int[] excludedActuatorIndices)
        {
            int nq = (int)model->nq;
            int nv = (int)model->nv;
            int nu = (int)model->nu;
            int nbody = (int)model->nbody;

            var excludedSet = excludedActuatorIndices != null
                ? new HashSet<int>(excludedActuatorIndices)
                : new HashSet<int>();

            if (nq != 7 + nu || nv != 6 + nu)
            {
                Debug.LogWarning($"BoneFilterConfig: Non-standard joint layout (nq={nq}, nv={nv}, nu={nu}). " +
                                 $"Expected nq={7 + nu}, nv={6 + nu}.");
            }

            var inclQpos = new List<int>();
            var inclQvel = new List<int>();
            var inclAct = new List<int>();
            var excludedLog = new List<int>();

            for (int a = 0; a < nu; a++)
            {
                if (excludedSet.Contains(a))
                {
                    excludedLog.Add(a);
                    continue;
                }

                inclAct.Add(a);
                inclQpos.Add(7 + a);
                inclQvel.Add(6 + a);
            }

            int nIncQpos = inclQpos.Count;
            int nIncQvel = inclQvel.Count;

            int contactDim = SynthContact.CONTACT_OBS_DIM; // 40
            int strainDim = nIncQvel; // one strain value per included DOF
            int xfrcDim = nbody * 6; // 3 force + 3 torque per body

            // Layout: root_qpos(5) + joint_qpos(N) + root_qvel(6) + joint_qvel(N) +
            //         qfrc_actuator(N) + contact(40) + strain(N) + xfrc_applied(nbody×6)
            int physicsObsDim = 5 + nIncQpos + 6 + nIncQvel + nIncQvel + contactDim + strainDim + xfrcDim;

            var config = new BoneFilterConfig
            {
                nq = nq,
                nv = nv,
                nu = nu,
                nbody = nbody,
                includedQposIdx = inclQpos.ToArray(),
                includedQvelIdx = inclQvel.ToArray(),
                includedActuatorIdx = inclAct.ToArray(),
                filteredJointCount = inclAct.Count,
                physicsObsDim = physicsObsDim,
                contactObsDim = contactDim,
                strainObsDim = strainDim,
                xfrcAppliedObsDim = xfrcDim,
                actDim = inclAct.Count
            };

            Debug.Log($"BoneFilterConfig: nq={nq}, nv={nv}, nu={nu}, nbody={nbody}, " +
                      $"excluded={excludedLog.Count} [{string.Join(", ", excludedLog)}], " +
                      $"included={inclAct.Count}, physicsObsDim={physicsObsDim} " +
                      $"(contact={contactDim}, strain={strainDim}, xfrc={xfrcDim}), actDim={config.actDim}");

            return config;
        }
    }
}
