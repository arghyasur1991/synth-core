using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Sentience.Synth
{
    public enum SkeletonType
    {
        Auto,
        Daz,
        Mixamo,
        Generic
    }

    /// <summary>
    /// Abstract adapter for skeleton-convention-specific behavior.
    /// Concrete implementations (DazAdapter, MixamoAdapter, GenericAdapter) handle
    /// differences in bone orientation, hierarchy layout, naming, and sub-bone detection.
    /// Joint ranges are tuned for Daz conventions — adapters compensate other rigs.
    /// </summary>
    public abstract class HumanoidAdapter
    {
        public abstract SkeletonType Type { get; }

        /// <summary>
        /// Adjust the local axis used for an MjHingeJoint so asymmetric ranges
        /// (e.g., knee [-135, 2]) produce correct flexion for this skeleton convention.
        /// Called once per joint during physics body creation.
        /// </summary>
        public abstract Vector3 AdjustJointAxis(Transform bone, Vector3 localAxis,
            SynthBone synthBone, SynthBoneMapper mapper);

        /// <summary>
        /// Ensure the prefab has a proper skeletonRoot node.
        /// SynthEntity expects: prefabRoot > skeletonRoot > Hips > ...
        /// Different rigs have different hierarchy layouts that need normalizing.
        /// </summary>
        public abstract void SetupSkeletonRoot(GameObject prefabInstance, Animator animator);

        /// <summary>
        /// True if the child bone is an auxiliary end-bone of the parent
        /// (same rigid body segment) and should NOT get its own MjBody.
        /// </summary>
        public abstract bool IsSubBoneOf(Transform child, Transform parent,
            SynthBoneMapper mapper, List<BoneMesh> boneMeshes);

        /// <summary>
        /// Resolve the skeleton root name from the model's hierarchy.
        /// Used by SynthData.SkeletonRootName and SynthEntity binding.
        /// </summary>
        public virtual string GetSkeletonRootName(Transform modelRoot, Animator animator)
        {
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                {
                    var ancestor = hips;
                    while (ancestor.parent != null && ancestor.parent != modelRoot)
                        ancestor = ancestor.parent;

                    if (ancestor == hips)
                        return "skeletonRoot";

                    return ancestor.name;
                }
            }

            foreach (Transform child in modelRoot)
            {
                if (child.GetComponent<SkinnedMeshRenderer>() == null && child.childCount > 0)
                    return child.name;
            }

            var transforms = modelRoot.GetComponentsInChildren<Transform>();
            return transforms.Length > 1 ? transforms[1].name : "skeletonRoot";
        }

        /// <summary>
        /// Create the correct adapter for a skeleton type enum value.
        /// SkeletonType.Auto uses DetectType to auto-detect from the model.
        /// </summary>
        public static HumanoidAdapter Create(SkeletonType type, Transform modelRoot = null)
        {
            if (type == SkeletonType.Auto && modelRoot != null)
                type = DetectType(modelRoot);

            return type switch
            {
                SkeletonType.Daz => new DazAdapter(),
                SkeletonType.Mixamo => new MixamoAdapter(),
                _ => new GenericAdapter()
            };
        }

        /// <summary>
        /// Auto-detect skeleton type from bone names in the hierarchy.
        /// </summary>
        public static SkeletonType DetectType(Transform root)
        {
            if (root == null) return SkeletonType.Generic;

            var transforms = root.GetComponentsInChildren<Transform>(true);
            bool hasDazMarker = false;
            int mixamoScore = 0;

            foreach (var t in transforms)
            {
                string name = t.name;

                if (name == "lShldrBend" || name == "lThighBend" || name == "rShldrBend" ||
                    name == "rThighBend" || name.StartsWith("Genesis"))
                {
                    hasDazMarker = true;
                    break;
                }

                if (name.StartsWith("mixamorig"))
                {
                    int idx = "mixamorig".Length;
                    if (idx < name.Length && (name[idx] == ':' || char.IsDigit(name[idx])))
                    {
                        mixamoScore += 10;
                        continue;
                    }
                }

                // Distinctive Mixamo bone names (not used by Unity standard or Daz):
                //   LeftForeArm (vs LeftLowerArm), LeftUpLeg (vs LeftUpperLeg),
                //   LeftLeg (vs LeftLowerLeg), Spine1/Spine2 (vs Spine/Chest)
                switch (name)
                {
                    case "LeftForeArm": case "RightForeArm":
                    case "LeftUpLeg": case "RightUpLeg":
                    case "LeftLeg": case "RightLeg":
                    case "LeftArm": case "RightArm":
                        mixamoScore += 2;
                        break;
                    case "Spine1": case "Spine2":
                    case "LeftToeBase": case "RightToeBase":
                        mixamoScore++;
                        break;
                }
            }

            if (hasDazMarker) return SkeletonType.Daz;
            if (mixamoScore >= 3) return SkeletonType.Mixamo;
            return SkeletonType.Generic;
        }
    }
}
