using UnityEngine;


namespace ARFoundationRemote.Runtime {
    public class CameraPoseSender : MonoBehaviour {
        void Awake() {
            if (Application.isEditor) {
                Debug.LogError(GetType().Name + " is written for running on device, not in Editor");
                enabled = false;
            }
        }

        void LateUpdate() {
            if (Sender.isConnected) {
                new PlayerToEditorMessage {cameraPose = PoseSerializable.Create(transform.LocalPose())}.Send();
            }
        }
    }
}
