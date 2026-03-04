using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Adapter for Mixamo skeletons.
    /// Mixamo's bone local X is consistently flipped vs Daz, so all X-axis
    /// joints need negation to match the ranges that were tuned for Daz.
    /// Mixamo places Hips directly under the model root (no intermediate node),
    /// and does not use auxiliary end-bones.
    /// </summary>
    public class MixamoAdapter : HumanoidAdapter
    {
        public override SkeletonType Type => SkeletonType.Mixamo;

        public override Vector3 AdjustJointAxis(Transform bone, Vector3 localAxis,
            SynthBone synthBone, SynthBoneMapper mapper)
        {
            if (localAxis != Vector3.right) return localAxis;
            return -localAxis;
        }

        public override void SetupSkeletonRoot(GameObject prefabInstance, Animator animator)
        {
            if (animator == null) return;

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) return;

            var skelRoot = new GameObject("skeletonRoot");
            skelRoot.transform.SetParent(prefabInstance.transform, false);
            skelRoot.transform.SetAsFirstSibling();
            hips.SetParent(skelRoot.transform, false);
            Debug.Log("MixamoAdapter: Created skeletonRoot wrapper and parented Hips");
        }

        /// <summary>
        /// Mixamo skeletons don't have auxiliary end-bones, and bone names like
        /// "LeftForeArm" contain "LeftArm" which would false-positive a string check.
        /// Always returns false — every recognized child is a real body part.
        /// </summary>
        public override bool IsSubBoneOf(Transform child, Transform parent,
            SynthBoneMapper mapper, List<BoneMesh> boneMeshes)
        {
            return false;
        }
    }
}
