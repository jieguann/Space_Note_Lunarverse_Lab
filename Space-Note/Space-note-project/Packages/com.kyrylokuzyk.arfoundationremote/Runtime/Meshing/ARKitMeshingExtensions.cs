#if UNITY_IOS && UNITY_EDITOR && ARKIT_INSTALLED && ARFOUNDATION_4_0_OR_NEWER
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.Assertions;
using UnityEngine.XR.ARKit;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Runtime {
    public static class ARKitMeshingExtensions {
        public static readonly Dictionary<TrackableId, ARMeshClassification[]> faceClassifications = new Dictionary<TrackableId, ARMeshClassification[]>();
        
        public static NativeArray<ARMeshClassification> GetFaceClassifications(this IXRMeshSubsystem subsystem, TrackableId meshId, Allocator allocator) {
            if (faceClassifications.TryGetValue(meshId, out var result)) {
                return new NativeArray<ARMeshClassification>(result, allocator);
            } else {
                return new NativeArray<ARMeshClassification>(0, allocator);
            }
        }

        public static void SetClassificationEnabled(this IXRMeshSubsystem subsystem, bool enabled) {
            new EditorToPlayerMessage {
                meshingData = new MeshingDataEditor {
                    setClassificationEnabled = enabled
                }
            }.Send();
        }

        public static bool GetClassificationEnabled(this IXRMeshSubsystem subsystem) {
            var meshingData = Connection.receiverConnection.BlockUntilReceive(new EditorToPlayerMessage {
                meshingData = new MeshingDataEditor {
                    getClassificationEnabled = true
                }
            }).meshingData;
            Assert.IsTrue(meshingData.HasValue);
            var classificationEnabled = meshingData.Value.classificationEnabled;
            Assert.IsTrue(classificationEnabled.HasValue);
            return classificationEnabled.Value;
        }
    }
}
#endif
