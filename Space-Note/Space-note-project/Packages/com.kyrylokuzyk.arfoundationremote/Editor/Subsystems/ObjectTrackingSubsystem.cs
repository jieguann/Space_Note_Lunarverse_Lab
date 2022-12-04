#if ARFOUNDATION_4_0_2_OR_NEWER
using System;
using System.Diagnostics;
using System.Linq;
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;

 
namespace ARFoundationRemote.RuntimeEditor {
    [UsedImplicitly]
    public class ObjectTrackingSubsystem : XRObjectTrackingSubsystem, IReceiver {
        static readonly TrackableChangesReceiver<XRTrackedObjectSerializable, XRTrackedObject> receiver = new TrackableChangesReceiver<XRTrackedObjectSerializable, XRTrackedObject>();

        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.ShouldRegisterSubsystemDescriptor()) {
                return;
            }

            Register<
                #if UNITY_2020_2_OR_NEWER
                    ObjectTrackingSubsystemProvider, 
                #endif
                ObjectTrackingSubsystem>(nameof(ObjectTrackingSubsystem), new XRObjectTrackingSubsystemDescriptor.Capabilities());
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new ObjectTrackingSubsystemProvider();
        #endif

        
        [UsedImplicitly]
        class ObjectTrackingSubsystemProvider : Provider {
            public override TrackableChanges<XRTrackedObject> GetChanges(XRTrackedObject template, Allocator allocator) {
                return receiver.GetChanges(allocator);
            }

            [CanBeNull]
            public override XRReferenceObjectLibrary library {
                set {
                    if (value != null) {
                        if (ObjectTrackingLibraries.Instance.objectLibraries.SingleOrDefault(_ => _.guid == value.guid) == null) {
                            ObjectTrackingSubsystemSender.logMissingObjRefLibError();
                        }
                    }
                    
                    var guid = value != null ? value.guid : (Guid?) null;
                    log($"send XRReferenceObjectLibrary: {(guid != null ? guid.ToString() : "NULL")}");
                    new EditorToPlayerMessage {
                        objectTrackingData = new ObjectTrackingDataEditor {
                            objectLibrary = new ObjectLibraryContainer {
                                guid = guid
                            }
                        }
                    }.Send();
                    base.library = value;
                }
            }

            #if UNITY_2020_2_OR_NEWER
            public override void Start() {
            }

            public override void Stop() {
            }
            #endif

            public override void Destroy() {
                receiver.Reset();
            }
        }
        
        
        public void Receive(PlayerToEditorMessage data) {
            var changes = data.objectTrackingData?.changes;
            if (changes.HasValue) {
                log(changes.ToString());
                receiver.Receive(changes);    
            }
        }
        

        [Conditional("_")]
        static void log(string message) {
            Debug.Log(message);
        }
    }
}
#endif
