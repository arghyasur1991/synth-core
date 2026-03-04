using System;
using System.Collections.Generic;
using UnityEngine;
using Mujoco;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Per-body contact sensing from MuJoCo's collision detection.
    ///
    /// Reads mjData->contact after mj_step1 (when contacts are valid) and
    /// accumulates per-body force data for a set of key body parts. Replaces
    /// the old cfrc_ext summation with spatially-resolved contact information.
    ///
    /// Observation layout per key body (5 floats):
    ///   [0]   contact_flag   — 1.0 if body is in contact, 0.0 otherwise
    ///   [1]   force_magnitude — total normal force magnitude (Newtons)
    ///   [2:4] normal_xyz     — contact normal direction (body frame avg)
    ///
    /// Total dimension = KEY_BODY_COUNT * FEATURES_PER_BODY = 8 * 5 = 40
    ///
    /// Key bodies: left foot, right foot, left hand, right hand,
    ///             pelvis/hips, left knee, right knee, head.
    /// </summary>
    public class SynthContact : MonoBehaviour, ISynthSense
    {
        public const int KEY_BODY_COUNT = 8;
        public const int FEATURES_PER_BODY = 5;
        public const int CONTACT_OBS_DIM = KEY_BODY_COUNT * FEATURES_PER_BODY;

        public const int SLOT_LEFT_FOOT = 0;
        public const int SLOT_RIGHT_FOOT = 1;
        public const int SLOT_LEFT_HAND = 2;
        public const int SLOT_RIGHT_HAND = 3;
        public const int SLOT_HIPS = 4;
        public const int SLOT_LEFT_KNEE = 5;
        public const int SLOT_RIGHT_KNEE = 6;
        public const int SLOT_HEAD = 7;

        private float[] _obsBuffer;
        private bool _initialized;

        // geom index -> MuJoCo body index (built from model->geom_bodyid)
        private int[] _geomToBody;

        // MuJoCo body index -> key body slot index (0-7), or -1 if not a key body
        private int[] _bodyToSlot;

        // Reverse: slot index -> MuJoCo body index (-1 if unmapped)
        private int[] _slotToBodyId;

        // Per-key-body accumulators (reset each frame)
        private float[] _forceMag;
        private float[] _normalX, _normalY, _normalZ;
        private bool[] _inContact;

        // Cached pointers
        private unsafe MujocoLib.mjModel_* _model;

        public string Name => "Contact";
        public int Dimension => CONTACT_OBS_DIM;
        public bool IsReady => _initialized;

        private static readonly SynthBone[] KeyBones = new SynthBone[]
        {
            SynthBone.LeftFoot,
            SynthBone.RightFoot,
            SynthBone.LeftHand,
            SynthBone.RightHand,
            SynthBone.Hips,
            SynthBone.LeftLowerLeg,
            SynthBone.RightLowerLeg,
            SynthBone.Head,
        };

        /// <summary>Raw force magnitude (Newtons) for a key body slot.</summary>
        public float GetForceMagnitude(int slot) =>
            _forceMag != null && slot >= 0 && slot < KEY_BODY_COUNT ? _forceMag[slot] : 0f;

        /// <summary>
        /// Normalized contact normal Z component for a key body slot.
        /// Positive = force points upward (e.g., ground pushing up on foot).
        /// </summary>
        public float GetNormalZ(int slot) =>
            _normalZ != null && slot >= 0 && slot < KEY_BODY_COUNT && _forceMag[slot] > 1e-6f
                ? _normalZ[slot] / _forceMag[slot] : 0f;

        /// <summary>Whether this key body slot has any active contact.</summary>
        public bool IsInContact(int slot) =>
            _inContact != null && slot >= 0 && slot < KEY_BODY_COUNT && _inContact[slot];

        /// <summary>
        /// World-frame Z position (height) of a key body.
        /// Used for proximity-based rewards — creates gradient before contact occurs.
        /// </summary>
        public unsafe float GetBodyWorldZ(int slot, MujocoLib.mjData_* data)
        {
            if (_slotToBodyId == null || slot < 0 || slot >= KEY_BODY_COUNT) return 0f;
            int bodyId = _slotToBodyId[slot];
            if (bodyId < 0 || data == null) return 0f;
            return (float)data->xpos[bodyId * 3 + 2];
        }

        /// <summary>
        /// Downward support force for a slot: the vertical component of
        /// contact force where the normal points upward (ground reaction).
        /// </summary>
        public float GetSupportForce(int slot)
        {
            if (_forceMag == null || slot < 0 || slot >= KEY_BODY_COUNT) return 0f;
            float fm = _forceMag[slot];
            if (fm < 1e-6f) return 0f;
            float nz = _normalZ[slot] / fm;
            return nz > 0f ? fm * nz : 0f;
        }

        /// <summary>
        /// Initialize contact sensing after MuJoCo scene is ready.
        /// Must be called after MjScene.postInitEvent when MujocoIds are valid.
        /// </summary>
        public unsafe void Initialize(MujocoLib.mjModel_* model, SynthBoneMapper boneMapper)
        {
            _model = model;
            _obsBuffer = new float[CONTACT_OBS_DIM];

            int ngeom = (int)model->ngeom;
            int nbody = (int)model->nbody;

            _geomToBody = new int[ngeom];
            for (int g = 0; g < ngeom; g++)
                _geomToBody[g] = model->geom_bodyid[g];

            _bodyToSlot = new int[nbody];
            for (int b = 0; b < nbody; b++)
                _bodyToSlot[b] = -1;

            _slotToBodyId = new int[KEY_BODY_COUNT];
            for (int i = 0; i < KEY_BODY_COUNT; i++)
                _slotToBodyId[i] = -1;

            int mapped = 0;
            if (boneMapper != null)
            {
                for (int slot = 0; slot < KeyBones.Length; slot++)
                {
                    var t = boneMapper.GetTransform(KeyBones[slot]);
                    if (t == null) continue;

                    var mjBody = t.GetComponent<MjBody>();
                    if (mjBody == null) continue;

                    int bodyId = mjBody.MujocoId;
                    if (bodyId >= 0 && bodyId < nbody)
                    {
                        _bodyToSlot[bodyId] = slot;
                        _slotToBodyId[slot] = bodyId;
                        mapped++;
                    }
                }
            }

            _forceMag = new float[KEY_BODY_COUNT];
            _normalX = new float[KEY_BODY_COUNT];
            _normalY = new float[KEY_BODY_COUNT];
            _normalZ = new float[KEY_BODY_COUNT];
            _inContact = new bool[KEY_BODY_COUNT];

            _initialized = true;
            Debug.Log($"SynthContact: Initialized — mapped {mapped}/{KEY_BODY_COUNT} key bodies, " +
                      $"ngeom={ngeom}, nbody={nbody}");
        }

        /// <summary>
        /// Compute per-body contact observations from current mjData.
        /// Call between mj_step1 and mj_step2 (contacts are valid after collision detection).
        /// </summary>
        public unsafe void ComputeContacts(MujocoLib.mjData_* data)
        {
            if (!_initialized) return;

            Array.Clear(_forceMag, 0, KEY_BODY_COUNT);
            Array.Clear(_normalX, 0, KEY_BODY_COUNT);
            Array.Clear(_normalY, 0, KEY_BODY_COUNT);
            Array.Clear(_normalZ, 0, KEY_BODY_COUNT);
            Array.Clear(_inContact, 0, KEY_BODY_COUNT);

            int ncon = data->ncon;
            if (ncon <= 0) goto writeObs;

            var forceResult = stackalloc double[6];

            for (int c = 0; c < ncon; c++)
            {
                var contact = &data->contact[c];
                int g1 = contact->geom1;
                int g2 = contact->geom2;

                if (g1 < 0 || g1 >= _geomToBody.Length ||
                    g2 < 0 || g2 >= _geomToBody.Length)
                    continue;

                int body1 = _geomToBody[g1];
                int body2 = _geomToBody[g2];

                int slot1 = (body1 >= 0 && body1 < _bodyToSlot.Length) ? _bodyToSlot[body1] : -1;
                int slot2 = (body2 >= 0 && body2 < _bodyToSlot.Length) ? _bodyToSlot[body2] : -1;

                if (slot1 < 0 && slot2 < 0) continue;

                MujocoLib.mj_contactForce(_model, data, c, forceResult);
                float fx = (float)forceResult[0];
                float fy = (float)forceResult[1];
                float fz = (float)forceResult[2];
                float mag = (float)Math.Sqrt(fx * fx + fy * fy + fz * fz);

                // Contact normal from the contact frame (first 3 elements of the 3x3 frame matrix)
                float nx = (float)contact->frame[0];
                float ny = (float)contact->frame[1];
                float nz = (float)contact->frame[2];

                if (slot1 >= 0)
                {
                    _inContact[slot1] = true;
                    _forceMag[slot1] += mag;
                    _normalX[slot1] += nx * mag;
                    _normalY[slot1] += ny * mag;
                    _normalZ[slot1] += nz * mag;
                }

                if (slot2 >= 0)
                {
                    _inContact[slot2] = true;
                    _forceMag[slot2] += mag;
                    _normalX[slot2] -= nx * mag;
                    _normalY[slot2] -= ny * mag;
                    _normalZ[slot2] -= nz * mag;
                }
            }

            writeObs:
            for (int s = 0; s < KEY_BODY_COUNT; s++)
            {
                int off = s * FEATURES_PER_BODY;
                _obsBuffer[off + 0] = _inContact[s] ? 1f : 0f;

                float fm = _forceMag[s];
                _obsBuffer[off + 1] = fm * 0.001f; // scale to ~[0,10] range for typical contacts

                if (fm > 1e-6f)
                {
                    float invMag = 1f / fm;
                    _obsBuffer[off + 2] = _normalX[s] * invMag;
                    _obsBuffer[off + 3] = _normalY[s] * invMag;
                    _obsBuffer[off + 4] = _normalZ[s] * invMag;
                }
                else
                {
                    _obsBuffer[off + 2] = 0f;
                    _obsBuffer[off + 3] = 0f;
                    _obsBuffer[off + 4] = 0f;
                }
            }
        }

        /// <summary>
        /// Return pre-computed contact observations.
        /// Call ComputeContacts() first to update the buffer.
        /// </summary>
        public float[] GetObservation()
        {
            if (!_initialized)
            {
                if (_obsBuffer == null || _obsBuffer.Length != CONTACT_OBS_DIM)
                    _obsBuffer = new float[CONTACT_OBS_DIM];
                return _obsBuffer;
            }
            return _obsBuffer;
        }

        /// <summary>
        /// Copy contact observations into a destination buffer at the given offset.
        /// Zero-allocation path for embedding in a larger observation vector.
        /// </summary>
        public void FillObservation(float[] dest, int offset)
        {
            if (_obsBuffer == null || _obsBuffer.Length != CONTACT_OBS_DIM)
            {
                Array.Clear(dest, offset, CONTACT_OBS_DIM);
                return;
            }
            Buffer.BlockCopy(_obsBuffer, 0, dest, offset * sizeof(float), CONTACT_OBS_DIM * sizeof(float));
        }
    }
}
