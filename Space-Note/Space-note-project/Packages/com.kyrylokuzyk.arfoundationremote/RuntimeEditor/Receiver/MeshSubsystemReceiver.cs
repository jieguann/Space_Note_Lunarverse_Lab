#if UNITY_EDITOR
#if UNITY_2019_3_OR_NEWER
    using MeshId = UnityEngine.XR.MeshId;
#else
    using MeshGenerationResult = UnityEngine.Experimental.XR.MeshGenerationResult;
    using MeshId = UnityEngine.Experimental.XR.TrackableId;
    using XRMeshSubsystem = UnityEngine.Experimental.XR.XRMeshSubsystem;
    using MeshInfo = UnityEngine.Experimental.XR.MeshInfo;
    using MeshVertexAttributes = UnityEngine.Experimental.XR.MeshVertexAttributes;
    using MeshChangeState = UnityEngine.Experimental.XR.MeshChangeState;
    using MeshGenerationStatus = UnityEngine.Experimental.XR.MeshGenerationStatus;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using Object = UnityEngine.Object;


namespace ARFoundationRemote.RuntimeEditor {
    public class MeshSubsystemReceiver : IXRMeshSubsystem, IReceiver {
        readonly Dictionary<MeshId, GenerateMeshAsyncReceiverData> meshGenerationRequests = new Dictionary<MeshId, GenerateMeshAsyncReceiverData>();
        readonly List<MeshInfo> meshInfos = new List<MeshInfo>();
        
        [CanBeNull] ARMeshManager _manager;
        
        [CanBeNull]
        ARMeshManager manager {
            get {
                if (_manager == null) {
                    _manager = Object.FindObjectOfType<ARMeshManager>();
                }

                return _manager;
            }
        }


        void ISubsystem.Start() {
            enableRemoteManager(true);
            running = true;
        }

        void ISubsystem.Stop() {
            meshGenerationRequests.Clear();
            enableRemoteManager(false);
            running = false;
        }

        static void enableRemoteManager(bool enableArMeshManager) {
            MeshSubsystemSender.log("send ARMeshManager enabled " + enableArMeshManager);
            new EditorToPlayerMessage {
                meshingData = new MeshingDataEditor {
                    enableARMeshManager = enableArMeshManager
                }
            }.Send();
        }

        void ISubsystem.Destroy() {
        }

        public bool running { get; private set; }

        bool IXRMeshSubsystem.TryGetMeshInfos(List<MeshInfo> meshInfosOut) {
            meshInfosOut.Clear();
            if (meshInfos.Any()) {
                meshInfosOut.AddRange(meshInfos);
                meshInfos.Clear();
                return true;
            } else {
                return false;
            }
        }

        void IXRMeshSubsystem.GenerateMeshAsync(MeshId meshId, Mesh mesh, MeshCollider meshCollider, MeshVertexAttributes attributes,
            Action<MeshGenerationResult> onMeshGenerationComplete) {
            meshGenerationRequests.Add(meshId, new GenerateMeshAsyncReceiverData {
                mesh = mesh,
                meshCollider = meshCollider,
                onMeshGenerationComplete = onMeshGenerationComplete,
                attributes = attributes
            });

            MeshSubsystemSender.log("Receiver generateMeshAsyncRequest " + meshId);
            new EditorToPlayerMessage {
                meshingData = new MeshingDataEditor {
                    generateMeshAsyncRequest = new GenerateMeshAsyncRequest {
                        meshId = MeshIdSerializable.Create(meshId),
                        attributes = attributes
                    }
                }
            }.Send();
        }
        
        bool IXRMeshSubsystem.SetBoundingVolume(Vector3 origin, Vector3 extents) {
            MeshSubsystemSender.log($"send SetBoundingVolume {origin}, {extents}");
            new EditorToPlayerMessage {
                meshingData = new MeshingDataEditor {
                    boundingVolumeData = new SetBoundingVolumeCallData {
                        origin = Vector3Serializable.Create(origin),
                        extents = Vector3Serializable.Create(extents)
                    }
                }
            }.Send();

            return true;
        }

        float _meshDensity;

        float IXRMeshSubsystem.meshDensity {
            get => _meshDensity;
            set {
                if (_meshDensity != value) {
                    MeshSubsystemSender.log($"send meshDensity {value}");
                    _meshDensity = value;
                    new EditorToPlayerMessage {
                        meshingData = new MeshingDataEditor {
                            meshDensity = value
                        }
                    }.Send();
                }
            }
        }

        public void Receive(PlayerToEditorMessage data) {
            var maybeMeshingData = data.meshingData;
            if (!maybeMeshingData.HasValue) {
                return;
            }
            
            if (manager == null) {
                return;
            }    
                
            var meshingData = maybeMeshingData.Value;
            var remoteMeshInfos = meshingData.meshInfos;
            if (remoteMeshInfos != null) {
                MeshSubsystemSender.log("receive meshInfos " + remoteMeshInfos.Count);
                meshInfos.AddRange(remoteMeshInfos.Select(_ => _.Value));
            }

            var maybeGenerateMeshAsyncResponse = meshingData.generateMeshAsyncResponse;
            if (!maybeGenerateMeshAsyncResponse.HasValue) {
                return;
            }
            
            var response = maybeGenerateMeshAsyncResponse.Value;
            var meshId = response.meshId.Value;
            var request = meshGenerationRequests[meshId];
            var removed = meshGenerationRequests.Remove(meshId);
            Assert.IsTrue(removed);

            var mesh = request.mesh;
            mesh.MarkDynamic();
            var meshCollider = request.meshCollider;
            var status = response.Status;
            if (status == MeshGenerationStatus.Success) {
                Assert.IsFalse(response.indices.All(_ => _ == 0));
                Assert.AreNotEqual(0, response.vertices.Length);
                        
                mesh.Clear();
                mesh.SetVertices(response.vertices.ToNonSerializableList());
                mesh.SetIndices(response.indices, MeshTopology.Triangles, 0);

                var normals = response.normals;
                if (normals != null) {
                    mesh.SetNormals(normals.ToNonSerializableList());
                }

                var tangents = response.tangents;
                if (tangents != null) {
                    mesh.SetTangents(tangents.ToNonSerializableList());
                }

                var uvs = response.uvs;
                if (uvs != null) {
                    mesh.SetUVs(0, uvs.ToNonSerializableList());
                }

                var colors = response.colors;
                if (colors != null) {
                    mesh.SetColors(colors.ToNonSerializableList());
                }
                    
                mesh.RecalculateBounds();

                #if UNITY_IOS && UNITY_EDITOR && ARKIT_INSTALLED && ARFOUNDATION_4_0_OR_NEWER
                    var faceClassifications = response.faceClassifications;
                    if (faceClassifications != null) {
                        ARKitMeshingExtensions.faceClassifications[meshId.trackableId()] = faceClassifications;
                    }
                #endif

                if (meshCollider != null) {
                    meshCollider.sharedMesh = mesh;
                }
            }
                      
            if (mesh.bounds.extents == Vector3.zero) {
                MeshSubsystemSender.errorOccursSometimes("mesh.bounds.extents == Vector3.zero");
            }
                    
            MeshSubsystemSender.log("Receiver: onMeshGenerationComplete " + meshId);
            request.onMeshGenerationComplete(new MeshGenerationResultWrapper {
                MeshId = meshId,
                Mesh = mesh,
                MeshCollider = meshCollider,
                Status = status,
                Attributes = request.attributes
            }.Value);
        }
    }
}
#endif
