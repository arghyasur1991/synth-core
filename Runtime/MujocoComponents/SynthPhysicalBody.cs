using System.Collections.Generic;
using UnityEngine;
using Mujoco;
namespace Genesis.Sentience.Synth
{
    public class SynthPhysicalBody
    {
        private readonly SynthData synthData;
        private readonly List<BoneMesh> boneMeshes;
        private readonly Transform synthTransform;
        private readonly Transform rootBone;
        private readonly SynthBoneMapper boneMapper;

        private readonly SynthBoneJoints synthBoneJoints;

        /// <summary>
        /// Gravity compensation factor for body parts (0 = full gravity, 1 = weightless).
        /// Useful for training stability - start with partial compensation and reduce over time.
        /// Uses MuJoCo 3.x GravityCompensation on MjBody components.
        /// </summary>
        public float GravityCompensationFactor { get; set; } = 0f;

        public SynthPhysicalBody(Transform synthTransform, Transform skeletonRoot, SynthData synthData,
            SynthBoneMapper mapper = null)
        {
            this.synthData = synthData;
            rootBone = skeletonRoot.GetChild(0);
            this.synthTransform = synthTransform;
            boneMapper = mapper;

            var actuatorRoot = synthTransform.FindInChildren("Actuators");
            synthBoneJoints = new SynthBoneJoints(synthData, actuatorRoot, mapper);
            boneMeshes = synthData.BoneMeshes;
        }

        public bool IsBodyBone(Transform bone)
        {
            return boneMeshes.FindIndex(b => b.bone == bone.name) >= 0;
        }

        public bool HasJoint(Transform bone)
        {
            return synthBoneJoints.HasJoint(bone);
        }

        public void AddPhysicsComponents()
        {
            AddMjComponentsToBone(rootBone, true, true);
        }

        public void RemovePhysicsComponents()
        {
            RemoveMjComponentsFromBone(rootBone);
        }

        public JointState GetJointState(Transform bone)
        {
            return synthBoneJoints.GetJointState(bone);
        }

        public void ApplyForceToJoint(Transform bone, Vector3 force)
        {
            synthBoneJoints.ApplyForceToJoint(bone, force);
        }

        /// <summary>
        /// Returns the joint range (rangeLower, rangeUpper) in degrees for a bone on a given axis index (0=X, 1=Y, 2=Z).
        /// </summary>
        public Vector2 GetJointRangeDegrees(Transform bone, int axisIndex)
        {
            return synthBoneJoints.GetJointRangeDegrees(bone, axisIndex);
        }

        /// <summary>
        /// Apply per-actuator torques to the primary body's MuJoCo data.
        /// Writes directly to MjScene.Instance.Data->ctrl via SynthActions.
        /// Called by SynthMotorSystem during the ctrl callback.
        /// </summary>
        /// <param name="actions">Per-actuator action values (length = filter.actDim)</param>
        /// <param name="filter">Bone filter config with included actuator indices</param>
        /// <param name="clampMin">Minimum action clamp (matches SynthActions and training env defaults)</param>
        /// <param name="clampMax">Maximum action clamp</param>
        public unsafe void ApplyActuatorTorques(float[] actions, BoneFilterConfig filter,
            float clampMin = -0.4f, float clampMax = 0.4f)
        {
            if (!MjScene.InstanceExists || MjScene.Instance.Data == null) return;
            SynthActions.Apply(MjScene.Instance.Data, actions,
                filter.includedActuatorIdx, filter.nu, clampMin, clampMax);
        }

        /// <summary>
        /// Initialize all Position actuator controls to match current joint positions.
        /// Prevents the startup "explosion" from PD controllers trying to drive to ctrl=0.
        /// </summary>
        public void InitializeControlsToCurrentPositions()
        {
            InitControlsRecursive(rootBone);
        }

        private void InitControlsRecursive(Transform bone)
        {
            synthBoneJoints.InitializeControlsToCurrentPositions(bone);
            for (int i = 0; i < bone.childCount; i++)
            {
                var child = bone.GetChild(i);
                if (IsBodyBone(child))
                {
                    InitControlsRecursive(child);
                }
            }
        }

        /// <summary>
        /// Updates gravity compensation on all MjBody components AND writes directly
        /// to the compiled MuJoCo model's body_gravcomp array.
        /// Factor of 0 = full gravity, 1 = fully compensated (weightless).
        /// 
        /// IMPORTANT: Setting MjBody.GravityCompensation alone only affects the C#
        /// property (used during MJCF generation). After the scene is compiled, you
        /// MUST also write to model->body_gravcomp for the change to take effect in
        /// the running simulation.
        /// </summary>
        public void SetGravityCompensation(float factor)
        {
            GravityCompensationFactor = Mathf.Clamp01(factor);

            // Find ALL MjBody components in the hierarchy -- not just "body bones" with meshes.
            var mjBodies = rootBone.GetComponentsInChildren<MjBody>(true);
            foreach (var mjBody in mjBodies)
            {
                // Update C# property (for future MJCF generation)
                mjBody.GravityCompensation = GravityCompensationFactor;

                // Force scene recreation so the new gravcomp value takes effect
                // via MJCF generation (MjBody.GravityCompensation was set above).
                if (MjScene.InstanceExists && mjBody.MujocoId >= 0)
                {
                    MjScene.Instance.SceneRecreationAtLateUpdateRequested = true;
                }
            }
        }

        #region Private methods

        private void AddMjComponentsToBone(Transform bone, bool isRoot, bool addJoint)
        {
            bool isHead = boneMapper != null
                ? boneMapper.GetSynthBone(bone) == SynthBone.Head
                : bone.name.Equals("head", System.StringComparison.OrdinalIgnoreCase);
            var overrideAddJoint = !isHead;

            var bodyChildren = new List<Transform>();
            CollectBodyBoneChildren(bone, bone, bodyChildren);
            foreach (var child in bodyChildren)
            {
                AddMjComponentsToBone(child, false, addJoint && overrideAddJoint);
            }
            if (AddGeomToBone(bone, isRoot) && addJoint)
            {
                synthBoneJoints.AddJointsToBone(bone, isRoot);
            }
        }

        /// <summary>
        /// Collect body-bone children reachable from a parent, searching through
        /// intermediate non-body transforms. Handles models with intermediate bones
        /// (twist/roll) that aren't in the BodyData mesh list.
        /// Sub-bones (auxiliary end-bones) are skipped along with their subtrees.
        /// </summary>
        private void CollectBodyBoneChildren(Transform parent, Transform bodyBoneAncestor,
            List<Transform> result)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (IsBodyBone(child))
                {
                    if (!IsSubBoneOf(child, bodyBoneAncestor))
                        result.Add(child);
                }
                else
                {
                    CollectBodyBoneChildren(child, bodyBoneAncestor, result);
                }
            }
        }

        private void RemoveMjComponentsFromBone(Transform bone)
        {
            for (int i = 0; i < bone.childCount; i++)
            {
                var boneChild = bone.GetChild(i);
                if (!IsSubBoneOf(boneChild, bone))
                {
                    RemoveMjComponentsFromBone(boneChild);
                }
            }
            RemoveGeomFromBone(bone);
            synthBoneJoints.RemoveJointsFromBone(bone);
        }

        private bool IsSubBoneOf(Transform child, Transform parent)
        {
            var adapter = boneMapper?.Adapter;
            if (adapter != null)
                return adapter.IsSubBoneOf(child, parent, boneMapper, boneMeshes);

            if (boneMapper != null && boneMapper.IsRecognized(child))
                return false;

            if (!child.name.Contains(parent.name))
                return false;

            int idx = boneMeshes.FindIndex(b => b.bone == child.name);
            if (idx >= 0 && boneMeshes[idx].meshVertices != null && boneMeshes[idx].meshVertices.Length > 10)
                return false;

            return true;
        }

        private bool AddGeomToBone(Transform bone, bool isRoot)
        {
            if (bone.gameObject.GetComponent<MjBody>() != null)
            {
                return false;
            }
            var boneName = bone.name;
            var mjGeom = bone.FindInChildren(boneName + "Geom");
            if (mjGeom == null)
            {
                // Root bone: try to use actual mesh data from BodyData first,
                // falling back to MjInertial only when no mesh is available.
                if (isRoot)
                {
                    int rootMeshIdx = boneMeshes.FindIndex(b => b.bone == bone.name);
                    if (rootMeshIdx >= 0 && boneMeshes[rootMeshIdx].meshVertices != null
                        && boneMeshes[rootMeshIdx].meshVertices.Length > 10)
                    {
                        var mjGeomObj = new GameObject(boneName + "Geom");
                        var geom = mjGeomObj.AddComponent<MjGeom>();
                        geom.ShapeType = MjShapeComponent.ShapeTypes.Mesh;
                        var mesh = new Mesh
                        {
                            vertices = boneMeshes[rootMeshIdx].meshVertices,
                            triangles = boneMeshes[rootMeshIdx].triangles.ToArray()
                        };
                        mesh.RecalculateBounds();
                        mesh.RecalculateNormals();
                        geom.Mesh.Mesh = mesh;
                        float rootMass = synthData.GetMass(boneName);
                        geom.Mass = rootMass > 0f ? rootMass : 0.5f;
                        mjGeomObj.transform.SetPositionAndRotation(synthTransform.position, synthTransform.rotation);
                        mjGeomObj.transform.SetParent(bone, true);
                        var mjBodyRoot = bone.gameObject.AddComponent<MjBody>();
                        mjBodyRoot.GravityCompensation = GravityCompensationFactor;
                        return true;
                    }

                    var mjGeomFallback = new GameObject(boneName + "Geom");
                    var geomFallback = mjGeomFallback.AddComponent<MjInertial>();
                    geomFallback.Mass = 0.5f;
                    mjGeomFallback.transform.SetPositionAndRotation(synthTransform.position, synthTransform.rotation);
                    mjGeomFallback.transform.SetParent(bone, true);
                    var mjBodyFallback = bone.gameObject.AddComponent<MjBody>();
                    mjBodyFallback.GravityCompensation = GravityCompensationFactor;
                    return true;
                }
                int boneMeshIndex = boneMeshes.FindIndex(b => b.bone == bone.name);
                if (boneMeshIndex >= 0)
                {
                    var meshVertices = boneMeshes[boneMeshIndex].meshVertices;
                    if (meshVertices != null && meshVertices.Length > 10)
                    {
                        var mjGeomObj = new GameObject(boneName + "Geom");
                        var geom = mjGeomObj.AddComponent<MjGeom>();
                        geom.ShapeType = MjShapeComponent.ShapeTypes.Mesh;
                        var mesh = new Mesh
                        {
                            vertices = meshVertices,
                            triangles = boneMeshes[boneMeshIndex].triangles.ToArray()
                        };
                        mesh.RecalculateBounds();
                        mesh.RecalculateNormals();
                        geom.Mesh.Mesh = mesh;
                        geom.Mass = synthData.GetMass(boneName);
                        mjGeomObj.transform.SetPositionAndRotation(synthTransform.position, synthTransform.rotation);
                        mjGeomObj.transform.SetParent(bone, true);

                        var mjBodyMesh = bone.gameObject.AddComponent<MjBody>();
                        mjBodyMesh.GravityCompensation = GravityCompensationFactor;
                        return true;
                    }
                    else if (boneMeshes[boneMeshIndex].selfMassPercent > 0)
                    {
                        var mjGeomObj = new GameObject(boneName + "Geom");
                        var geom = mjGeomObj.AddComponent<MjInertial>();
                        geom.Mass = synthData.GetMass(boneName);
                        mjGeomObj.transform.SetPositionAndRotation(synthTransform.position, synthTransform.rotation);
                        mjGeomObj.transform.SetParent(bone, true);
                        var mjBodyInertial = bone.gameObject.AddComponent<MjBody>();
                        mjBodyInertial.GravityCompensation = GravityCompensationFactor;
                        return true;
                    }
                }
            }
            return false;
        }

        private void RemoveGeomFromBone(Transform bone)
        {
            if (bone.GetComponent<MjBody>() != null)
            {
                Object.DestroyImmediate(bone.gameObject.GetComponent<MjBody>());
            }
            var boneName = bone.name;

            var mjGeom = bone.FindInChildren(boneName + "Geom");
            if (mjGeom != null)
            {
                Object.DestroyImmediate(mjGeom.gameObject);
            }
        }

        #endregion
    }
}