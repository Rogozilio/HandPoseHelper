using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts
{
    public abstract class SaveDataTemplate : ScriptableObject
    {
        private bool _isUseDataCollection;
        public virtual List<string> GetAllNames => null;

        protected bool IsUseDataCollection
        {
            set => _isUseDataCollection = value;
        }

        public bool GetIsUseDataCollection => _isUseDataCollection;

        protected virtual void OnEnable()
        {
            
        }

        public virtual HandPoseData Load()
        {
            Debug.LogWarning("Override virtual method Load() in child class");
            return new HandPoseData();
        }

        public virtual void Save(HandPoseData hands, string name)
        {
            Debug.LogWarning("Override virtual method Save() in child class");
        }

        public virtual void SaveElement(HandPoseData hands, string name)
        {
            if(_isUseDataCollection) Debug.LogWarning("Override virtual method SaveElement() in child class");
        }

        public virtual HandPoseData Load(string name)
        {
            Debug.LogWarning("Override virtual method Load(string name) in child class");
            return new HandPoseData();
        }
    }
}