using ARFoundationRemote.Runtime;
using ARFoundationRemote.RuntimeEditor;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Editor {
    public class AnchorSubsystem : XRAnchorSubsystem, IReceiver {
        static readonly TrackableChangesReceiver<ARAnchorSerializable, XRAnchor> receiver = new TrackableChangesReceiver<ARAnchorSerializable,XRAnchor>();

        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.ShouldRegisterSubsystemDescriptor()) {
                return;
            }
            
            var thisType = typeof(AnchorSubsystem);
            XRAnchorSubsystemDescriptor.Create(new XRAnchorSubsystemDescriptor.Cinfo {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(AnchorSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    subsystemImplementationType = thisType,
                #endif
                supportsTrackableAttachments = true
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new AnchorSubsystemProvider();
        #endif
        
        void IReceiver.Receive(PlayerToEditorMessage data) {
            receiver.Receive(data.anchorSubsystemData);
        }
        
        public override TrackableChanges<XRAnchor> GetChanges(Allocator allocator) {
            if (!running) {
                // silent exception when closing the scene
                return new TrackableChanges<XRAnchor>();
            }

            return base.GetChanges(allocator);
        }

        class AnchorSubsystemProvider : Provider {
            public override TrackableChanges<XRAnchor> GetChanges(XRAnchor defaultAnchor, Allocator allocator) {
                return receiver.GetChanges(allocator);
            }

            public override bool TryAddAnchor(Pose pose, out XRAnchor anchor) {
                var response = sendBlocking(new AnchorDataEditor {
                    tryAddAnchorData = new TryAddAnchorData {
                        pose = PoseSerializable.Create(pose)
                    }
                });

                var responseAnchor = response.anchor;
                Assert.IsTrue(responseAnchor.HasValue);
                anchor = responseAnchor.Value.Value;
                return response.isSuccess;
            }

            public override bool TryAttachAnchor(TrackableId trackableToAffix, Pose pose, out XRAnchor anchor) {
                var response = sendBlocking(new AnchorDataEditor {
                    tryAttachAnchorData = new TryAttachAnchorData {
                        trackableToAffix = TrackableIdSerializable.Create(trackableToAffix),
                        pose = PoseSerializable.Create(pose)
                    }
                });

                var responseAnchor = response.anchor;
                Assert.IsTrue(responseAnchor.HasValue);
                anchor = responseAnchor.Value.Value;
                return response.isSuccess;
            }

            public override bool TryRemoveAnchor(TrackableId anchorId) {
                if (Defines.arSubsystems_4_1_0_preview_11_or_newer) {
                    var receiverConnection = Connection.receiverConnection;
                    if (Receiver.isQuitting || receiverConnection == null || !receiverConnection.isConnected) {
                        // AR Foundation 4.1.x destroys ARAnchor after disconnect
                        return false;
                    }
                }
                
                AnchorSubsystemSender.log($"remote subsystem TryRemoveAnchor {anchorId}");
                return sendBlocking(new AnchorDataEditor {
                    tryRemoveAnchorData = new TryRemoveAnchorData {
                        anchorId = TrackableIdSerializable.Create(anchorId)
                    }
                }).isSuccess;
            }

            static AnchorSubsystemMethodsResponse sendBlocking(AnchorDataEditor anchorSubsystemMethodsData) {
                var response = Connection.receiverConnection.BlockUntilReceive(new EditorToPlayerMessage {anchorsData = anchorSubsystemMethodsData}).anchorSubsystemMethodsResponse;
                Assert.IsTrue(response.HasValue);
                return response.Value;
            }
            
            public override void Start() {
                enableRemoteManager(true);
            }

            public override void Stop() {
                enableRemoteManager(false);
            }
            
            void enableRemoteManager(bool enable) {
                new EditorToPlayerMessage {
                    anchorsData = new AnchorDataEditor {
                        enableManager = enable
                    } 
                }.Send();
            }

            public override void Destroy() {
                receiver.Reset();
            }
        }
    }
}
