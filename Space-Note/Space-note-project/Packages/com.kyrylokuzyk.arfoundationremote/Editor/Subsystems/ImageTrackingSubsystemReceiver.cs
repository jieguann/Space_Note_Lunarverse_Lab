using ARFoundationRemote.Runtime;
using Unity.Collections;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Editor {
    public partial class ImageTrackingSubsystem : IReceiver {
        static readonly TrackableChangesReceiver<XRTrackedImageSerializable, XRTrackedImage> receiver = new TrackableChangesReceiver<XRTrackedImageSerializable, XRTrackedImage>();


        void IReceiver.Receive(PlayerToEditorMessage data) {
            receiver.Receive(data.trackedImagesData);
        }

        static TrackableChanges<XRTrackedImage> getChanges(Allocator allocator) {
            return receiver.GetChanges(allocator);
        }
    }
}
