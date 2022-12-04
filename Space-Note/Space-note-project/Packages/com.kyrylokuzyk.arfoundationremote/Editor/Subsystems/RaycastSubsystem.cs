using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Editor {
    public class RaycastSubsystem : XRRaycastSubsystem {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.ShouldRegisterSubsystemDescriptor()) {
                return;
            }

            var thisType = typeof(RaycastSubsystem);
            XRRaycastSubsystemDescriptor.RegisterDescriptor(new XRRaycastSubsystemDescriptor.Cinfo {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(RaycastSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    subsystemImplementationType = thisType,
                #endif
                supportedTrackableTypes =
                    TrackableType.Planes |
                    TrackableType.FeaturePoint
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new RaycastSubsystemProvider();
        #endif

        class RaycastSubsystemProvider: Provider {
            public override void Start() {
                base.Start();
                if (Object.FindObjectOfType<ARRaycastManager>()) {
                    if (Object.FindObjectOfType<ARPlaneManager>() == null && Object.FindObjectOfType<ARPointCloudManager>() == null) {
                        Debug.LogWarning($"{Constants.packageName}: ARRaycastManager found in scene but no ARPlaneManager or ARPointCloudManager is present.");
                        Debug.LogWarning($"{Constants.packageName}: please add ARPlaneManager to raycast against detected planes.");
                        Debug.LogWarning($"{Constants.packageName}: please add ARPointCloudManager to raycast against detected cloud points.");
                    }
                }
            }
        }
    }
}
