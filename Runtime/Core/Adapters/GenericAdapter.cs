using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Fallback adapter for unknown skeleton rigs. Uses geometry-based heuristics
    /// to determine joint axis orientation and sub-bone relationships.
    /// </summary>
    public class GenericAdapter : HumanoidAdapter
    {
        public override SkeletonType Type => SkeletonType.Generic;

        /// <summary>
        /// Geometry-based: compute anatomical axis from bone direction × body forward,
        /// compare with the bone's local X to detect whether it needs flipping.
        /// </summary>
        public override Vector3 AdjustJointAxis(Transform bone, Vector3 localAxis,
            SynthBone synthBone, SynthBoneMapper mapper)
        {
            if (localAxis != Vector3.right || mapper == null)
                return localAxis;

            if (synthBone == SynthBone.Unknown)
                return localAxis;

            Transform childBone = FindNextBoneInChain(bone, mapper);
            if (childBone == null)
                return localAxis;

            Vector3 boneDir = (childBone.position - bone.position).normalized;
            if (boneDir.sqrMagnitude < 0.01f)
                return localAxis;

            Transform synthRoot = bone.root;
            Vector3 bodyForward = synthRoot != null ? synthRoot.forward : Vector3.forward;

            Vector3 anatomicalAxis = Vector3.Cross(boneDir, bodyForward).normalized;
            if (anatomicalAxis.sqrMagnitude < 0.01f)
                anatomicalAxis = Vector3.Cross(boneDir, Vector3.up).normalized;
            if (anatomicalAxis.sqrMagnitude < 0.01f)
                return localAxis;

            var info = SynthBoneCatalog.Get(synthBone);
            if (info.Side == BoneSide.Right)
                anatomicalAxis = -anatomicalAxis;

            Vector3 boneWorldX = bone.TransformDirection(Vector3.right);
            float dot = Vector3.Dot(boneWorldX, anatomicalAxis);

            return dot < -0.1f ? -localAxis : localAxis;
        }

        public override void SetupSkeletonRoot(GameObject prefabInstance, Animator animator)
        {
            if (animator == null) return;

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) return;

            Transform boneAncestor = hips;
            while (boneAncestor.parent != null && boneAncestor.parent != prefabInstance.transform)
                boneAncestor = boneAncestor.parent;

            if (boneAncestor != hips)
            {
                boneAncestor.name = "skeletonRoot";
            }
            else
            {
                var skelRoot = new GameObject("skeletonRoot");
                skelRoot.transform.SetParent(prefabInstance.transform, false);
                skelRoot.transform.SetAsFirstSibling();
                hips.SetParent(skelRoot.transform, false);
            }
        }

        /// <summary>
        /// Uses string containment plus mesh vertex check (same as legacy logic).
        /// </summary>
        public override bool IsSubBoneOf(Transform child, Transform parent,
            SynthBoneMapper mapper, List<BoneMesh> boneMeshes)
        {
            if (mapper != null && mapper.IsRecognized(child))
                return false;

            if (!child.name.Contains(parent.name))
                return false;

            int idx = boneMeshes.FindIndex(b => b.bone == child.name);
            if (idx >= 0 && boneMeshes[idx].meshVertices != null && boneMeshes[idx].meshVertices.Length > 10)
                return false;

            return true;
        }

        private static Transform FindNextBoneInChain(Transform bone, SynthBoneMapper mapper)
        {
            if (mapper != null)
            {
                for (int i = 0; i < bone.childCount; i++)
                {
                    var child = bone.GetChild(i);
                    if (mapper.IsRecognized(child))
                        return child;
                }
            }
            for (int i = 0; i < bone.childCount; i++)
            {
                var child = bone.GetChild(i);
                if (!child.name.Contains("Geom") && !child.name.Contains("Joint"))
                    return child;
            }
            return null;
        }
    }
}
