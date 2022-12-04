#if UNITY_EDITOR
using System;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Runtime {
    public partial class FaceSubsystem: XRFaceSubsystem {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.ShouldRegisterSubsystemDescriptor()) {
                return;
            }

            var thisType = typeof(FaceSubsystem);
            XRFaceSubsystemDescriptor.Create(new FaceSubsystemParams {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(FaceSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    subsystemImplementationType = thisType,
                #endif
                supportsFacePose = true,
                supportsEyeTracking = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS,
                supportsFaceMeshVerticesAndIndices = true,
                supportsFaceMeshUVs = true,
                supportsFaceMeshNormals = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new FaceSubsystemProvider();
        #endif
        
        #if (UNITY_IOS || UNITY_EDITOR) && ARFOUNDATION_REMOTE_ENABLE_IOS_BLENDSHAPES
        public NativeArray<UnityEngine.XR.ARKit.ARKitBlendShapeCoefficient> GetBlendShapeCoefficients(TrackableId trackableId, Allocator allocator) {
            var blendShapeCoefficients = receiver.all[trackableId].blendShapeCoefficients;
            if (blendShapeCoefficients != null) {
                var result = blendShapeCoefficients.Select(_ => _.Value).ToArray();
                return new NativeArray<UnityEngine.XR.ARKit.ARKitBlendShapeCoefficient>(result, allocator);                
            } else {
                return new NativeArray<UnityEngine.XR.ARKit.ARKitBlendShapeCoefficient>(0, allocator);
            }
        }
        #endif
        
        class FaceSubsystemProvider: Provider {
            public override void GetFaceMesh(TrackableId faceId, Allocator allocator, ref XRFaceMesh faceMesh) {
                getFaceMesh(faceId, allocator, ref faceMesh);
            }

            public override TrackableChanges<XRFace> GetChanges(XRFace defaultFace, Allocator allocator) {
                return getChanges(allocator);
            }

            public override int supportedFaceCount => Int32.MaxValue;
            #if ARFOUNDATION_4_0_OR_NEWER
                public override int currentMaximumFaceCount => Int32.MaxValue;
                public override int requestedMaximumFaceCount { get => Int32.MaxValue; set {} }
            #endif

            public override void Start() {
                setRemoteFaceSubsystemEnabled(true);
            }

            public override void Stop() {
                setRemoteFaceSubsystemEnabled(false);
            }
            
            public override void Destroy() {
                receiver.Reset();
            }

            void setRemoteFaceSubsystemEnabled(bool isEnabled) {
                Sender.logSceneSpecific("send " + GetType().Name + " " + isEnabled);
                new EditorToPlayerMessage {enableFaceSubsystem = isEnabled}.Send();
            }
        }
    }
}
#endif
