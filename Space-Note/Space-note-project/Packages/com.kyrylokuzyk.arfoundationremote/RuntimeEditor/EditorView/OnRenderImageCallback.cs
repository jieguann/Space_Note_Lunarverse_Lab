using System;
using UnityEngine;
using UnityEngine.Assertions;


namespace ARFoundationRemote.RuntimeEditor {
    public class OnRenderImageCallback : MonoBehaviour {
        public Action<Camera> callback;
        Camera cam;

        
        void Awake() {
            cam = GetComponent<Camera>();
            Assert.IsNotNull(cam);
            Assert.AreEqual(1, GetComponents<OnRenderImageCallback>().Length);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest) {
            callback(cam);
            Graphics.Blit(src, dest);
        }
    }
}
