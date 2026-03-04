using System;
using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Persisted wizard configuration for repeatable Synth creation.
    /// Saved alongside the source model asset so that one-click quick-create
    /// can reuse previous wizard customizations instead of falling back to defaults.
    /// </summary>
    public class SynthCreationConfig : ScriptableObject
    {
        public string synthName;
        public string outputFolder;
        public float totalMass = 55f;
        public SkeletonType skeletonType = SkeletonType.Auto;

        [Serializable]
        public struct MeshEntry
        {
            public string meshName;
            public bool load;
            public bool activateOnBind;
            public bool isBody;
        }

        public List<MeshEntry> meshEntries = new();

        [Serializable]
        public struct BoneEntry
        {
            public string synthBoneName;
            public bool include;
            public bool physicsOnly;
        }

        public List<BoneEntry> boneEntries = new();

        /// <summary>
        /// Resolve the asset path where a config should be stored for a given model.
        /// Convention: same folder as the model, named {ModelName}.SynthConfig.asset
        /// </summary>
        public static string GetConfigPath(string modelAssetPath)
        {
            string dir = System.IO.Path.GetDirectoryName(modelAssetPath).Replace('\\', '/');
            string modelName = System.IO.Path.GetFileNameWithoutExtension(modelAssetPath);
            return $"{dir}/{modelName}.SynthConfig.asset";
        }
    }
}
