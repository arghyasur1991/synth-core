using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// The root identity component. Declares "this GameObject is a Synth"
    /// and wires sub-systems together. Does not control behavior -- that is
    /// SynthBrain's job.
    /// 
    /// Renamed from SynthController to avoid confusion with SynthBrain.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class SynthEntity : MonoBehaviour
    {
        private static readonly string DEFAULT_SKELETON_ROOT_NAME = "skeletonRoot";
        public SynthData synthData;

        [SerializeField] string skeletonRootName = DEFAULT_SKELETON_ROOT_NAME;

        private Transform skeletonRoot;
        private Animator animator;
        private SynthMeshRenderers synthMeshRenderers;
        private SynthPhysicalBody synthPhysicalBody;
        private SynthEyeCameras synthEyeCameras;
        private SynthBoneMapper boneMapper;

        void Awake()
        {
            animator = GetComponent<Animator>();
            InitObjectsAndRefs();
            if (!HumanBound)
            {
                BindHumanModel();
            }
        }

        bool HumanBound
        {
            get
            {
                return synthData != null && GetComponentsInChildren<SkinnedMeshRenderer>().Length != 0;
            }
        }

        [ContextMenu("BindHumanModel()")]
        public void BindHumanModel()
        {
            if (synthData == null)
            {
                return;
            }
            UnbindHumanModel();
            Debug.Log("Binding Human Model: " + synthData.ModelName);
            InitObjectsAndRefs();
            if (skeletonRoot == null)
            {
                Debug.LogWarning("SynthEntity: skeletonRoot not found, cannot bind model");
                return;
            }

            skeletonRoot.name = synthData.SkeletonRootName;
            skeletonRootName = synthData.SkeletonRootName;
            synthMeshRenderers.Bind();
            synthEyeCameras?.Bind(synthData.LeftEyeBoneName, synthData.RightEyeBoneName);

            if (Application.isPlaying)
            {
                if (animator.isActiveAndEnabled)
                {
                    animator.Rebind();
                }
            }
            else
            {
                synthPhysicalBody.AddPhysicsComponents();
            }
        }

        public SynthPhysicalBody PhysicalBody
        {
            get
            {
                return synthPhysicalBody;
            }
        }

        /// <summary>Eye cameras created during BindHumanModel. Used by SynthVision.</summary>
        public SynthEyeCameras EyeCameras => synthEyeCameras;

        /// <summary>Bone mapper for this model. Created from the Animator's Humanoid Avatar.</summary>
        public SynthBoneMapper BoneMapper => boneMapper;

        public Transform RootBone
        {
            get
            {
                if (skeletonRoot != null)
                {
                    return skeletonRoot.GetChild(0);
                }
                return null;
            }
        }

        [Header("Gravity Compensation (Test)")]
        [Range(0f, 1f)]
        [SerializeField] private float testGravityComp = 0f;

        [ContextMenu("Apply Test Gravity Compensation")]
        public void ApplyTestGravityCompensation()
        {
            if (synthPhysicalBody == null)
                InitObjectsAndRefs();
            if (synthPhysicalBody != null)
            {
                synthPhysicalBody.SetGravityCompensation(testGravityComp);
                Debug.Log($"SynthEntity: Applied gravity comp = {testGravityComp:F3}");
            }
            else
            {
                Debug.LogWarning("SynthEntity: PhysicalBody is null, cannot set gravity comp");
            }
        }

        [ContextMenu("UnbindHumanModel()")]
        public void UnbindHumanModel()
        {
            InitObjectsAndRefs();
            if (skeletonRoot != null)
            {
                skeletonRoot.name = DEFAULT_SKELETON_ROOT_NAME;
            }
            skeletonRootName = DEFAULT_SKELETON_ROOT_NAME;
            if (!Application.isPlaying && synthPhysicalBody != null)
            {
                synthPhysicalBody.RemovePhysicsComponents();
            }
            synthEyeCameras?.Unbind();
            synthMeshRenderers?.Unbind();
        }


        #region Private methods

        private void InitObjectsAndRefs()
        {
            skeletonRoot = transform.FindInChildren(skeletonRootName);

            // Fallback: if the skeleton root was already renamed by a previous bind
            // (e.g. from old SynthController), try finding by synthData's name
            if (skeletonRoot == null && synthData != null
                && !string.IsNullOrEmpty(synthData.SkeletonRootName))
            {
                skeletonRoot = transform.FindInChildren(synthData.SkeletonRootName);
                if (skeletonRoot != null)
                    skeletonRootName = synthData.SkeletonRootName;
            }

            if (skeletonRoot == null || synthData == null)
                return;

            if (animator == null) animator = GetComponent<Animator>();
            boneMapper = SynthBoneMapper.Create(animator);

            synthPhysicalBody = new SynthPhysicalBody(transform, skeletonRoot, synthData, boneMapper);
            synthMeshRenderers = new SynthMeshRenderers(transform, skeletonRoot, synthData);
            synthEyeCameras = new SynthEyeCameras(skeletonRoot);
        }

        #endregion
    }
}
