#if UNITY_EDITOR
using System.Linq;
using UnityEngine.XR.Management;


namespace ARFoundationRemote.Runtime {
    public static class Global {
        /// Dirty hack to fix face and image tracking duplications.
        /// Correct fix: remove all static fields from subsystems.
        public static bool IsInitialized;

        public static bool ShouldRegisterSubsystemDescriptor() {
            if (Defines.isUnity2019_2) {
                return true; // IsPluginEnabled() is not working in Unity 2019.2
            }

            return IsPluginEnabled();
        }
        
        public static bool IsPluginEnabled() {
            var settings = XRGeneralSettings.Instance;
            if (settings != null) {
                var manager = settings.Manager;
                if (manager != null) {
                    var loaders = manager.loaders;
                    // Debug.Log($"active loader: {xrManagerSettings.activeLoader}", xrManagerSettings.activeLoader);
                    // Debug.Log($"loaders: {string.Join(", ", loaders)}");
                    if (loaders != null) {
                        return loaders.Any(_ => _.GetType().Name == "ARFoundationRemoteLoader");
                    }
                }
            }

            return false;
        }
    }
}
#endif
