#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Runtime {
    public partial class FaceSubsystem : IReceiver {
        static readonly TrackableChangesReceiver<ARFaceSerializable, XRFace> receiver = new TrackableChangesReceiver<ARFaceSerializable, XRFace>();
        bool isVideoChecked;
        static readonly Dictionary<TrackableId, FaceUniqueData> uniqueFacesData = new Dictionary<TrackableId, FaceUniqueData>();
        

        void IReceiver.Receive(PlayerToEditorMessage d) {
            var data = d.faceSubsystemData;
            if (data != null) {
                var uniqueData = data.uniqueData;
                if (uniqueData != null) {
                    foreach (var _ in uniqueData) {
                        uniqueFacesData[_.trackableId.Value] = _; // use [] instead of Add() to support scene change
                    }
                }

                receiver.Receive(data.added, data.updated, data.removed);
                if (data.needLogFaces) {
                    FaceSubsystemSender.log("receive faces\n" + data);
                }

                if (!isVideoChecked) {
                    isVideoChecked = true;
                    if (Defines.isAndroid && !Settings.EnableBackgroundVideo) {
                        Debug.LogError($"{Constants.packageName}: disabling camera video will produce faces with inverted culling. Please use lower video scale instead.");
                    }
                }
            }
        }

        static TrackableChanges<XRFace> getChanges(Allocator allocator) {
            return receiver.GetChanges(allocator);
        }
        
        static void getFaceMesh(TrackableId faceId, Allocator allocator, ref XRFaceMesh faceMesh) {
            if (!receiver.all.TryGetValue(faceId, out var face)) {
                throw new Exception();
            }
            
            if (!uniqueFacesData.TryGetValue(faceId, out var uniqueData)) {
                throw new Exception($"unique face not found {faceId}");
            }
            
            var indices = uniqueData.indices;
            var vertices = face.vertices.Select(_ => _.Value).ToArray();
            if (!vertices.Any()) {
                faceMesh = default;
                return;
            }
            
            var vertexCount = vertices.Length;
            var indicesCount = indices.Length;
            var normals = face.normals.Select(_ => _.Value).ToArray();
            var hasNormals = normals.Any();
            var attrs = XRFaceMesh.Attributes.None;
            var uvs = uniqueData.uvs.Select(_ => _.Value).ToArray();
            var hasUVs = uvs.Any();
            if (hasUVs) {
                attrs |= XRFaceMesh.Attributes.UVs;
            }
            
            if (hasNormals) {
                attrs |= XRFaceMesh.Attributes.Normals;
            }
            
            faceMesh.Resize(vertexCount, indicesCount / 3, attrs, allocator);
            
            var verticesNativeArray = faceMesh.vertices;
            Assert.AreEqual(verticesNativeArray.Length, vertexCount);
            verticesNativeArray.CopyFrom(vertices);

            var indicesNativeArray = faceMesh.indices;
            Assert.AreEqual(indicesNativeArray.Length, indicesCount);
            indicesNativeArray.CopyFrom(indices);

            if (hasUVs) {
                var uvsNativeArray = faceMesh.uvs;
                Assert.AreEqual(uvsNativeArray.Length, uvs.Length);
                uvsNativeArray.CopyFrom(uvs);
            }

            if (hasNormals) {
                var normalsNativeArray = faceMesh.normals;
                Assert.AreEqual(normalsNativeArray.Length, normals.Length);
                normalsNativeArray.CopyFrom(normals);                
            }
        }
    }
}
#endif
