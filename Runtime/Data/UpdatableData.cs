using UnityEngine;

namespace Genesis.Sentience.Synth
{
    public abstract class UpdatableData : ScriptableObject
    {
        public event System.Action OnValuesUpdated;
        public bool autoUpdate;

        protected virtual void OnValidate()
        {
            if (autoUpdate)
            {
                NotifyOnUpdate();
            }
        }

        public virtual void NotifyOnUpdate()
        {
            if (OnValuesUpdated != null)
            {
                OnValuesUpdated();
            }
        }
    }
}
