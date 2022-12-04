#if UNITY_EDITOR
using System.Collections;
using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.XR.ARFoundation;


namespace ARFoundationRemote.RuntimeEditor {
    public class OriginDataSender : MonoBehaviour, IReceiver {
        IEnumerator Start() {
            ARSessionOrigin origin = null;
            SessionOriginData oldData = new SessionOriginData();

            while (true) {
                while (origin == null) {
                    origin = FindObjectOfType<ARSessionOrigin>();
                    yield return null;
                }

                var newData = SessionOriginData.Create(origin);
                if (!newData.Equals(oldData)) {
                    oldData = newData;
                    // Debug.Log("send origin data\n" + newData);
                    new EditorToPlayerMessage {sessionOriginData = newData}.Send();
                }

                yield return null;
            }
        }

        public void Receive(PlayerToEditorMessage data) {
        }
    }
}
#endif
