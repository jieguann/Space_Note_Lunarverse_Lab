using ARFoundationRemote.Runtime;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Editor {
    public sealed partial class PlaneSubsystem: XRPlaneSubsystem, IReceiver {
        static readonly TrackableChangesReceiver<BoundedPlaneSerializable, BoundedPlane> receiver = new TrackableChangesReceiver<BoundedPlaneSerializable, BoundedPlane>();

        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.ShouldRegisterSubsystemDescriptor()) {
                return;
            }

            //Debug.Log("RegisterDescriptor ARemotePlaneSubsystem");
            var thisType = typeof(PlaneSubsystem);
            XRPlaneSubsystemDescriptor.Create(new XRPlaneSubsystemDescriptor.Cinfo {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(ARemotePlaneSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    subsystemImplementationType = thisType,
                #endif
                supportsHorizontalPlaneDetection = true,
                supportsVerticalPlaneDetection = true,
                supportsBoundaryVertices = true,
                supportsClassification = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS,
                supportsArbitraryPlaneDetection = false
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new ARemotePlaneSubsystemProvider();
        #endif
        
        void IReceiver.Receive(PlayerToEditorMessage data) {
            var planesData = data.planesUpdateData;
            if (planesData != null) {
                receiver.Receive(planesData.added, planesData.updated, planesData.removed);
            }
        }
        
        class ARemotePlaneSubsystemProvider: Provider {
            public override void GetBoundary(TrackableId trackableId, Allocator allocator, ref NativeArray<Vector2> boundary) {
                var points = receiver.all[trackableId].boundary;
                var length = points.Length;
                CreateOrResizeNativeArrayIfNecessary(length, allocator, ref boundary);
                for (int i = 0; i < length; i++) {
                    boundary[i] = points[i].Value;
                }
            }

            public override TrackableChanges<BoundedPlane> GetChanges(BoundedPlane defaultPlane, Allocator allocator) {
                return receiver.GetChanges(allocator);
            }
            
            public override void Start() {
                setRemoteSubsystemEnabled(true);
            }

            public override void Stop() {
                setRemoteSubsystemEnabled(false);
            }
            
            public override void Destroy() {
                receiver.Reset();
            }

            void setRemoteSubsystemEnabled(bool isEnabled) {
                Sender.logSceneSpecific("send " + GetType().Name + " " + isEnabled);
                new EditorToPlayerMessage {enablePlaneSubsystem = isEnabled}.Send();
            }

            #if !ARFOUNDATION_4_0_OR_NEWER
                public override PlaneDetectionMode planeDetectionMode {
                    set => new EditorToPlayerMessage {planeDetectionMode = value}.Send();
                }
            #else
                PlaneDetectionMode detectionMode = PlaneDetectionMode.None;
    
                public override PlaneDetectionMode requestedPlaneDetectionMode {
                    get => detectionMode;
                    set {
                        if (detectionMode != value) {
                            detectionMode = value;
                            new EditorToPlayerMessage { planeDetectionMode = value }.Send();                    
                        }
                    }
                }
    
                public override PlaneDetectionMode currentPlaneDetectionMode => detectionMode;
            #endif
        }
    }
}
