using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public interface ISingletonData
    {
        void OnAfterDeserialize();
        void OnBeforeSerialize();
    }

    // TODO: Move all singletons over to this ..
    public class SingletonManager<DataType, SingletonInstanceType> : ScriptableObject, ISerializationCallbackReceiver
        where DataType				: ISingletonData, new()
        where SingletonInstanceType : SingletonManager<DataType,SingletonInstanceType>
    {
        #region Instance
        static SingletonManager<DataType, SingletonInstanceType> s_Instance;
        public static SingletonInstanceType Instance
        {
            get
            {
                if (s_Instance)
                    return s_Instance as SingletonInstanceType;
                
                s_Instance = ScriptableObject.CreateInstance<SingletonInstanceType>();
                s_Instance.hideFlags = HideFlags.HideAndDontSave;
                return s_Instance as SingletonInstanceType;  
            }
        }
        #endregion
        

        public DataType data = new DataType();

        public static DataType Data { get { return Instance.data; } }


        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            // This helps survive domain reloads
            if (s_Instance == null) s_Instance = this;
            var instance = s_Instance ? s_Instance : this;
            instance.data.OnAfterDeserialize();
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            // This helps survive domain reloads
            if (s_Instance == null) s_Instance = this;
            var instance = s_Instance ? s_Instance : this;
            instance.data.OnBeforeSerialize();
        }

        void OnEnable()
        {
            // This helps survive domain reloads
            if (s_Instance == null) s_Instance = this;
            Initialize();
        }

        protected virtual void Initialize() { }

        void OnDestroy()
        {
            // This helps survive domain reloads
            if (s_Instance == this) s_Instance = null;
            Shutdown();
        }

        protected virtual void Shutdown() { }

        protected static void RecordUndo(string name)
        {
            Undo.RecordObject(Instance, name);
        }
    }

}
