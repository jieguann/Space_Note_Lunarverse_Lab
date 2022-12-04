using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;


namespace ARFoundationRemote.Runtime {
    public class EditorViewReceiver : SubsystemSender {
        [SerializeField] RawImage rawImage = null;
        [SerializeField] Material opaqueMaterial = null, 
            transparentMaterial = null;

        [CanBeNull] Texture2D depthTexture;
        [CanBeNull] Texture2D colorTexture;
        readonly int colorTexPropId = Shader.PropertyToID("_MainTex");
        readonly int depthTexPropId = Shader.PropertyToID("_DepthTex");


        void Awake() {
            rawImage.enabled = false;
        }

        public override void EditorMessageReceived(EditorToPlayerMessage data) {
            if (data.messageType.IsStop()) {
                rawImage.enabled = false;
            }

            var maybeEditorViewData = data.editorViewData;
            if (maybeEditorViewData.HasValue) {
                // disable raw image, then enable it back to support resizing
                rawImage.enabled = false;
                var editorViewData = maybeEditorViewData.Value;
                editorViewData.colorTexture.ResizeIfNeededAndDeserializeInto(ref colorTexture);
                
                var material = Settings.Instance.arCompanionSettings.mode == Mode.OptimizeForPerformance ? transparentMaterial : opaqueMaterial;
                Assert.IsNotNull(material, "material != null");
                rawImage.material = material;
                Assert.AreEqual(rawImage.materialForRendering, material);
                material.SetTexture(colorTexPropId, colorTexture);

                var depth = editorViewData.depthTexture;
                if (depth.HasValue) {
                    Assert.IsTrue(material.HasProperty(depthTexPropId), "material.HasProperty(depthTexPropId)");
                    depth.Value.ResizeIfNeededAndDeserializeInto(ref depthTexture);
                    material.SetTexture(depthTexPropId, depthTexture);
                } else {
                    material.SetTexture(depthTexPropId, null);
                }
                
                rawImage.enabled = true;
            }
        }

        void OnDestroy() {
            if (depthTexture != null) {
                Destroy(depthTexture);
            }

            if (colorTexture != null) {
                Destroy(colorTexture);
            }
        }
    }


    [Serializable]
    public struct EditorViewData {
        public Texture2DSerializable colorTexture;
        public Texture2DSerializable? depthTexture;
    }
}
