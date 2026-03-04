using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Creates and manages eye cameras on the Synth's eye bones.
    ///
    /// Follows the same Bind/Unbind pattern as SynthMeshRenderers and SynthPhysicalBody.
    /// Eye bone names come from BoneMesh flags (isLeftEye/isRightEye) via SynthData.
    ///
    /// During Bind():
    ///   1. Finds eye bones by name in the skeleton hierarchy
    ///   2. Destroys any pre-existing camera children (idempotent rebind)
    ///   3. Creates a Camera child on each eye bone, offset forward to the pupil
    ///   4. Creates RenderTextures for each camera to render into
    ///
    /// During Unbind():
    ///   Destroys camera GameObjects and releases RenderTextures.
    ///
    /// Camera optics approximate human eye parameters:
    ///   - FOV 90 degrees (human eye ~120 horizontal, 90 is a practical balance)
    ///   - Near clip 0.008 (8mm — clips through thin eyelid shell)
    ///   - Forward offset ~1.5cm (pushes past pupil surface, combined with near clip to clear eyelids)
    ///   - Culling mask excludes layer 6 (SynthFaceExclude) for future eyelid meshes
    ///
    /// SynthVision reads from the RenderTextures via public accessors.
    /// </summary>
    public class SynthEyeCameras
    {
        /// <summary>
        /// Layer index reserved for face/eyelid meshes that should be excluded
        /// from eye camera rendering. Must match TagManager.asset layer 6.
        /// </summary>
        public const int FACE_EXCLUDE_LAYER = 6;

        /// <summary>
        /// Default forward offset along the eye bone's local Z axis.
        /// Pushes the camera from the eyeball center past the pupil surface,
        /// clearing eyelid geometry. ~1.5cm for Genesis8-scale models.
        /// Combined with a near clip plane of 8mm, this clips through the
        /// thin eyelid shell that curves over the eye.
        /// </summary>
        public static readonly Vector3 DEFAULT_PUPIL_OFFSET = new Vector3(0f, 0f, 0.015f);

        private readonly Transform skeletonRoot;

        private GameObject leftCameraGO;
        private GameObject rightCameraGO;
        private Camera leftCamera;
        private Camera rightCamera;
        private RenderTexture leftRT;
        private RenderTexture rightRT;

        /// <summary>Left eye camera, or null if not bound or eye bone not found.</summary>
        public Camera LeftCamera => leftCamera;

        /// <summary>Right eye camera, or null if not bound or eye bone not found.</summary>
        public Camera RightCamera => rightCamera;

        /// <summary>Left eye render texture, or null if not bound.</summary>
        public RenderTexture LeftRenderTexture => leftRT;

        /// <summary>Right eye render texture, or null if not bound.</summary>
        public RenderTexture RightRenderTexture => rightRT;

        /// <summary>Whether both eye cameras are bound and active.</summary>
        public bool IsBound => leftCamera != null && rightCamera != null;

        public SynthEyeCameras(Transform skeletonRoot)
        {
            this.skeletonRoot = skeletonRoot;
        }

        /// <summary>
        /// Create eye cameras on the specified eye bones.
        /// Idempotent — destroys any pre-existing cameras on the eye bones first.
        /// Call during SynthEntity.BindHumanModel().
        /// </summary>
        /// <param name="leftEyeBoneName">Name of the left eye bone (from SynthData.LeftEyeBoneName).</param>
        /// <param name="rightEyeBoneName">Name of the right eye bone (from SynthData.RightEyeBoneName).</param>
        /// <param name="resolution">Render texture resolution (width and height). Default 256.</param>
        /// <param name="pupilOffset">Local-space offset from eye bone to camera position. Null uses DEFAULT_PUPIL_OFFSET.</param>
        public void Bind(string leftEyeBoneName, string rightEyeBoneName,
                         int resolution = 256, Vector3? pupilOffset = null)
        {
            Unbind();

            if (skeletonRoot == null)
            {
                Debug.LogWarning("SynthEyeCameras: skeletonRoot is null, cannot bind");
                return;
            }

            var offset = pupilOffset ?? DEFAULT_PUPIL_OFFSET;

            if (!string.IsNullOrEmpty(leftEyeBoneName))
            {
                var leftBone = skeletonRoot.FindInChildren(leftEyeBoneName);
                if (leftBone != null)
                    CreateEyeCamera(leftBone, "LeftEyeCamera", resolution, offset,
                                    out leftCameraGO, out leftCamera, out leftRT);
                else
                    Debug.LogWarning($"SynthEyeCameras: Left eye bone '{leftEyeBoneName}' not found");
            }

            if (!string.IsNullOrEmpty(rightEyeBoneName))
            {
                var rightBone = skeletonRoot.FindInChildren(rightEyeBoneName);
                if (rightBone != null)
                    CreateEyeCamera(rightBone, "RightEyeCamera", resolution, offset,
                                    out rightCameraGO, out rightCamera, out rightRT);
                else
                    Debug.LogWarning($"SynthEyeCameras: Right eye bone '{rightEyeBoneName}' not found");
            }

            if (IsBound)
                Debug.Log($"SynthEyeCameras: Bound (L={leftEyeBoneName}, R={rightEyeBoneName}, " +
                          $"{resolution}x{resolution}, offset={offset})");
        }

        /// <summary>
        /// Destroy eye camera GameObjects and release RenderTextures.
        /// Call during SynthEntity.UnbindHumanModel().
        /// </summary>
        public void Unbind()
        {
            DestroyCamera(ref leftCameraGO, ref leftCamera, ref leftRT);
            DestroyCamera(ref rightCameraGO, ref rightCamera, ref rightRT);
        }

        private static void CreateEyeCamera(
            Transform eyeBone, string name, int resolution, Vector3 offset,
            out GameObject go, out Camera cam, out RenderTexture rt)
        {
            // Destroy any pre-existing camera children on this eye bone.
            // This makes Bind() idempotent — safe to call repeatedly without leaking.
            // Same discovery pattern as SynthMeshRenderers.Unbind().
            DestroyExistingCameras(eyeBone);

            // Create RenderTexture
            rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
            rt.name = name + "RT";
            rt.hideFlags = HideFlags.DontSave;
            rt.Create();

            // Create camera GameObject as child of eye bone, offset to pupil position
            go = new GameObject(name);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(eyeBone, false);
            go.transform.localPosition = offset;
            go.transform.localRotation = Quaternion.identity;

            // Configure camera with human-eye-approximated optics
            cam = go.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.fieldOfView = 90f;
            cam.nearClipPlane = 0.008f;
            cam.farClipPlane = 100f;
            cam.depth = -10;
            cam.enabled = true;

            // Exclude face/eyelid layer from eye cameras.
            // Currently no objects on this layer, but the mask is ready for when
            // models with separate eyelid meshes are used.
            cam.cullingMask = ~(1 << FACE_EXCLUDE_LAYER);

            // URP adds UniversalAdditionalCameraData automatically when
            // a Camera component is created. No manual setup needed.
        }

        /// <summary>
        /// Scan an eye bone for any child GameObjects with Camera components
        /// and destroy them. Prevents orphaned cameras from previous binds.
        /// </summary>
        private static void DestroyExistingCameras(Transform eyeBone)
        {
            // Iterate backwards since we're destroying children
            for (int i = eyeBone.childCount - 1; i >= 0; i--)
            {
                var child = eyeBone.GetChild(i);
                if (child.GetComponent<Camera>() != null)
                {
                    if (Application.isPlaying)
                        Object.Destroy(child.gameObject);
                    else
                        Object.DestroyImmediate(child.gameObject, false);
                }
            }
        }

        private static void DestroyCamera(ref GameObject go, ref Camera cam, ref RenderTexture rt)
        {
            if (go != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(go);
                else
                    Object.DestroyImmediate(go, false);
                go = null;
                cam = null;
            }

            if (rt != null)
            {
                rt.Release();
                if (Application.isPlaying)
                    Object.Destroy(rt);
                else
                    Object.DestroyImmediate(rt, false);
                rt = null;
            }
        }
    }
}
