using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public class AnchorSubsystemSender : SubsystemSender {
        [SerializeField] ARAnchorManager manager = null;
        [SerializeField] ARPlaneManager planeManager = null;


        void OnEnable() {
            manager.anchorsChanged += anchorsChanged;
        }

        void OnDisable() {
            manager.anchorsChanged -= anchorsChanged;
        }

        void anchorsChanged(ARAnchorsChangedEventArgs args) {
            LogChangedAnchors(args);
            new PlayerToEditorMessage{anchorSubsystemData = new TrackableChangesData<ARAnchorSerializable> {
                added = toSerializable(args.added),
                updated = toSerializable(args.updated),
                removed = toSerializable(args.removed)
            }}.Send();
        }

        [Conditional("_")]
        public static void LogChangedAnchors(ARAnchorsChangedEventArgs args) {
            var added = args.added;
            if (added.Any()) {
                foreach (var anchor in added) {
                    print($"added {anchor.trackableId}");
                }
            }

            /*var updated = args.updated;
            if (updated.Any()) {
                foreach (var anchor in updated) {
                    print($"updated {anchor.trackableId}");
                }
            }*/

            var removed = args.removed;
            if (removed.Any()) {
                foreach (var anchor in removed) {
                    print($"removed {anchor.trackableId}");
                }
            }
        }

        ARAnchorSerializable[] toSerializable(List<ARAnchor> anchors) {
            return anchors.Select(ARAnchorSerializable.Create).ToArray();
        }

        public override void EditorMessageReceived(EditorToPlayerMessage data) {
            var maybeAnchorsData = data.anchorsData;
            if (maybeAnchorsData.HasValue) {
                var anchorsData = maybeAnchorsData.Value;
                
                if (anchorsData.enableManager.HasValue) {
                    Sender.Instance.SetManagerEnabled(manager, anchorsData.enableManager.Value);
                    return;
                }

                Assert.IsTrue(data.requestGuid.HasValue);
                
                void sendMethodResponse(ARAnchor anchor, bool isSuccess) {
                    new PlayerToEditorMessage {
                        anchorSubsystemMethodsResponse = new AnchorSubsystemMethodsResponse {
                            anchor = ARAnchorSerializable.CreateIfNotNull(anchor),
                            isSuccess = isSuccess
                        },
                        responseGuid = data.requestGuid.Value
                    }.Send();
                }
                
                var tryAddAnchorData = anchorsData.tryAddAnchorData;
                if (tryAddAnchorData.HasValue) {
                    #pragma warning disable 618
                    var anchor = manager.AddAnchor(tryAddAnchorData.Value.pose.Value);
                    #pragma warning restore
                    sendMethodResponse(anchor, anchor != null);
                    return;
                }
                
                var tryAttachAnchorData = anchorsData.tryAttachAnchorData;
                if (tryAttachAnchorData.HasValue) {
                    var attachAnchorData = tryAttachAnchorData.Value;
                    var anchor = tryAttachAnchor(attachAnchorData.trackableToAffix.Value, attachAnchorData.pose.Value);
                    sendMethodResponse(anchor, anchor != null);
                    return;
                }
                
                var tryRemoveAnchorData = anchorsData.tryRemoveAnchorData;
                if (tryRemoveAnchorData.HasValue) {
                    sendMethodResponse(null,tryRemoveAnchor(tryRemoveAnchorData.Value.anchorId.Value));
                    return;
                }

                throw new Exception();
            }
        }

        [CanBeNull]
        ARAnchor tryAttachAnchor(TrackableId planeId, Pose pose) {
            var plane = planeManager.GetPlane(planeId);
            if (plane == null) {
                log($"tryAttachAnchor() plane not found: {planeId}");
                return null;
            }
            
            var anchor = manager.AttachAnchor(plane, pose);
            if (anchor == null) {
                log($"anchor attachment to plane {plane.trackableId} failed");
                return null;
            }
            
            log($"anchor {anchor.trackableId} attached to plane {plane.trackableId}");
            LogAllTrackables(manager);
            return anchor;
        }

        bool tryRemoveAnchor(TrackableId id) {
            log($"tryRemoveAnchor {id}");
            var anchor = manager.GetAnchor(id);
            if (anchor == null) {
                if (!Defines.arSubsystems_4_1_0_preview_11_or_newer) {
                    Debug.LogError($"GetAnchor == null {id}");
                }
                
                LogAllTrackables(manager);
                return false;
            }

            if (Defines.arSubsystems_4_1_0_preview_11_or_newer) {
                Destroy(anchor.gameObject);
                return true;
            } else {
                #pragma warning disable 618
                var removed = manager.RemoveAnchor(anchor);
                #pragma warning restore
                if (!removed) {
                    Debug.LogError($"RemoveAnchor failed {id}");
                } else {
                    log($"anchor removed {anchor.trackableId}");
                    LogAllTrackables(manager);
                }
            
                return removed;    
            }
        }

        [Conditional("_")]
        public static void log(string s) {
            Debug.Log(s);
        }

        [Conditional("_")]
        static void LogAllTrackables(ARAnchorManager m) {
            log("\nALL TRACKABLES ");
            foreach (var trackable in m.trackables) {
                print(trackable.trackableId);
            }
        }
    }
    
    [Serializable]
    public struct ARAnchorSerializable : ISerializableTrackable<XRAnchor> {
        TrackableIdSerializable trackableIdSer;
        PoseSerializable pose;
        TrackingState trackingState;
        Guid sessionId;


        public static ARAnchorSerializable? CreateIfNotNull([CanBeNull] ARAnchor a) {
            if (a != null) {
                return Create(a);
            } else {
                return null;
            }
        }

        public static ARAnchorSerializable Create([NotNull] ARAnchor a) {
            return new ARAnchorSerializable {
                trackableIdSer = TrackableIdSerializable.Create(a.trackableId),
                pose = PoseSerializable.Create(a.transform.LocalPose()),
                trackingState = a.trackingState,
                sessionId = a.sessionId,
            };
        }

        public TrackableId trackableId => trackableIdSer.Value;
        public XRAnchor Value => new XRAnchor(trackableId, pose.Value, trackingState, IntPtr.Zero, sessionId);
    }

    [Serializable]
    public struct AnchorSubsystemMethodsResponse {
        public ARAnchorSerializable? anchor;
        public bool isSuccess;
    }

    [Serializable]
    public struct AnchorDataEditor {
        public TryAddAnchorData? tryAddAnchorData;
        public TryAttachAnchorData? tryAttachAnchorData;
        public TryRemoveAnchorData? tryRemoveAnchorData;
        public bool? enableManager;
    }

    [Serializable]
    public struct TryAddAnchorData {
        public PoseSerializable pose;
    }

    [Serializable]
    public struct TryAttachAnchorData {
        public TrackableIdSerializable trackableToAffix;
        public PoseSerializable pose;
    }

    [Serializable]
    public struct TryRemoveAnchorData {
        public TrackableIdSerializable anchorId;
    }
}
