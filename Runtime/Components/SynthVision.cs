using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Visual sense — egocentric stereo vision from the Synth's eye cameras.
    ///
    /// Reads pixel data from the left and right eye RenderTextures
    /// (created by SynthEyeCameras during BindHumanModel) and returns
    /// a flattened RGB observation vector.
    ///
    /// The observation is downsampled to obsWidth x obsHeight for efficiency.
    /// Both eyes are concatenated: [left_RGB | right_RGB].
    ///
    /// This sense is used by higher-level brain layers (planner, selector)
    /// for spatial reasoning and navigation. Motor skills (L0) typically
    /// don't need vision — they rely on proprioception.
    ///
    /// Attach to the same GameObject as SynthEntity.
    /// </summary>
    public class SynthVision : MonoBehaviour, ISynthSense
    {
        [Header("Vision")]
        [Tooltip("Width of the downsampled observation image")]
        public int obsWidth = 64;

        [Tooltip("Height of the downsampled observation image")]
        public int obsHeight = 64;

        private SynthEntity synthEntity;
        private Texture2D readbackBuffer;
        private float[] observationCache;
        private RenderTexture downsampleRT;

        public string Name => "Vision";

        /// <summary>
        /// Observation dimension: width * height * 3 channels (RGB) * 2 eyes.
        /// </summary>
        public int Dimension => obsWidth * obsHeight * 3 * 2;

        public bool IsReady =>
            synthEntity != null &&
            synthEntity.EyeCameras != null &&
            synthEntity.EyeCameras.IsBound;

        void Awake()
        {
            synthEntity = GetComponent<SynthEntity>();
            if (synthEntity == null)
                synthEntity = GetComponentInParent<SynthEntity>();
        }

        void OnDestroy()
        {
            if (readbackBuffer != null)
                Destroy(readbackBuffer);
            if (downsampleRT != null)
            {
                downsampleRT.Release();
                Destroy(downsampleRT);
            }
        }

        /// <summary>
        /// Read the current visual observation from both eye cameras.
        /// Returns a flat float array: [left_R, left_G, left_B, ..., right_R, right_G, right_B, ...].
        /// Values are normalized to [0, 1].
        /// </summary>
        public float[] GetObservation()
        {
            if (!IsReady) return System.Array.Empty<float>();

            EnsureBuffers();

            var cams = synthEntity.EyeCameras;
            int pixelsPerEye = obsWidth * obsHeight * 3;

            // Read left eye
            ReadEyePixels(cams.LeftRenderTexture, observationCache, 0);

            // Read right eye
            ReadEyePixels(cams.RightRenderTexture, observationCache, pixelsPerEye);

            return observationCache;
        }

        private void EnsureBuffers()
        {
            if (readbackBuffer == null || readbackBuffer.width != obsWidth || readbackBuffer.height != obsHeight)
            {
                if (readbackBuffer != null) Destroy(readbackBuffer);
                readbackBuffer = new Texture2D(obsWidth, obsHeight, TextureFormat.RGB24, false);
            }

            if (downsampleRT == null || downsampleRT.width != obsWidth || downsampleRT.height != obsHeight)
            {
                if (downsampleRT != null)
                {
                    downsampleRT.Release();
                    Destroy(downsampleRT);
                }
                downsampleRT = new RenderTexture(obsWidth, obsHeight, 0, RenderTextureFormat.ARGB32);
                downsampleRT.Create();
            }

            if (observationCache == null || observationCache.Length != Dimension)
                observationCache = new float[Dimension];
        }

        private void ReadEyePixels(RenderTexture sourceRT, float[] dest, int offset)
        {
            if (sourceRT == null) return;

            // Blit to downsample (GPU-side resize)
            Graphics.Blit(sourceRT, downsampleRT);

            // Read back to CPU
            var prevActive = RenderTexture.active;
            RenderTexture.active = downsampleRT;
            readbackBuffer.ReadPixels(new Rect(0, 0, obsWidth, obsHeight), 0, 0, false);
            readbackBuffer.Apply();
            RenderTexture.active = prevActive;

            // Convert to float [0,1]
            var pixels = readbackBuffer.GetRawTextureData();
            for (int i = 0; i < pixels.Length; i++)
            {
                dest[offset + i] = pixels[i] / 255f;
            }
        }
    }
}
