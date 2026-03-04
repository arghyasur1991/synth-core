using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Adapter for Daz Genesis skeletons (Genesis 3/8/9).
    /// Joint ranges were originally tuned for Daz bone orientations,
    /// so no axis correction is needed. Daz models have an intermediate
    /// node between the prefab root and Hips, and use auxiliary end-bones
    /// (e.g., lShldrBendEnd) for mesh deformation.
    /// </summary>
    public class DazAdapter : HumanoidAdapter
    {
        public override SkeletonType Type => SkeletonType.Daz;

        public override Vector3 AdjustJointAxis(Transform bone, Vector3 localAxis,
            SynthBone synthBone, SynthBoneMapper mapper)
        {
            return localAxis;
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
                string originalName = boneAncestor.name;
                boneAncestor.name = "skeletonRoot";
                Debug.Log($"DazAdapter: Renamed '{originalName}' to 'skeletonRoot'");
            }
            else
            {
                var skelRoot = new GameObject("skeletonRoot");
                skelRoot.transform.SetParent(prefabInstance.transform, false);
                skelRoot.transform.SetAsFirstSibling();
                hips.SetParent(skelRoot.transform, false);
                Debug.Log("DazAdapter: Created skeletonRoot wrapper (unexpected Daz layout)");
            }
        }

        /// <summary>
        /// Daz uses auxiliary end-bones like "lShldrBendEnd" whose name contains
        /// the parent "lShldrBend". These are mesh deformation helpers, not separate bodies.
        /// Recognized SynthBones are never sub-bones.
        /// </summary>
        public override bool IsSubBoneOf(Transform child, Transform parent,
            SynthBoneMapper mapper, List<BoneMesh> boneMeshes)
        {
            if (mapper != null && mapper.IsRecognized(child))
                return false;

            return child.name.Contains(parent.name);
        }
    }
}
