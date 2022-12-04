using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;


namespace ARFoundationRemote.Editor {
    /// todo add gamma to linear space conversion to ARCoreBackgroundEditor.shader?
    public partial class CameraSubsystem : IReceiver {
        [CanBeNull] static ARCameraFrameEventArgsSerializable? receivedCameraFrame { get; set; }
        [CanBeNull] static TextureAndDescriptor[] textures { get; set; }


        void IReceiver.Receive(PlayerToEditorMessage data) {
            var maybeCameraData = data.cameraData;
            if (!maybeCameraData.HasValue) {
                return;
            }

            var cameraData = maybeCameraData.Value;
            var maybeRemoteFrame = cameraData.cameraFrame;
            if (maybeRemoteFrame.HasValue) {
                var remoteFrame = maybeRemoteFrame.Value;
                receivedCameraFrame = remoteFrame;

                var receivedTextures = remoteFrame.textures;
                var count = receivedTextures.Length;
                if (textures == null) {
                    textures = new TextureAndDescriptor[count];
                    for (int i = 0; i < count; i++) {
                        textures[i] = new TextureAndDescriptor();
                    }
                }

                Assert.AreEqual(receivedTextures.Length, textures.Length);
                for (int i = 0; i < count; i++) {
                    var tex = receivedTextures[i];
                    if (tex.texture.HasValue) {
                        textures[i].Update(tex.texture.Value, tex.propName);
                    } else if (!textures[i].descriptor.HasValue) {
                        var dummyTexture = new UnityEngine.Texture2D(2, 2);
                        var color = UnityEngine.Color.clear;
                        for (int x = 0; x < dummyTexture.width; x++) {
                            for (int y = 0; y < dummyTexture.height; y++) {
                                dummyTexture.SetPixel(x,y,color);
                            }
                        }

                        dummyTexture.Apply();
                        textures[i] = new TextureAndDescriptor(dummyTexture, tex.propName);
                    }
                }
            }

            receiveCpuImages(data);

            var screenOrientation = cameraData.screenOrientation;
            if (screenOrientation.HasValue) {
                CameraSubsystemSender.logScreenOrientation($"receive orientation {screenOrientation.Value}");
                ARFoundationRemoteUtils.ScreenOrientation = screenOrientation.Value;
            }

            var maybeResolution = cameraData.screenResolution;
            if (maybeResolution.HasValue) {
                var playerResolution = maybeResolution.Value.Deserialize();
                var editorResolution = new Vector2Int(Screen.width, Screen.height);
                if (playerResolution != editorResolution) {
                    Debug.LogWarning($"{Constants.packageName}: please set Editor View resolution to match AR device's resolution: {playerResolution}. Otherwise, UI and other screen-size dependent features may be displayed incorrectly. Current Editor View resolution: {editorResolution}");
                }
            }

            if (cameraData.colorSpace.HasValue) {
                var companionColorSpace = cameraData.colorSpace.Value;
                var editorColorSpace = QualitySettings.activeColorSpace;
                if (companionColorSpace != editorColorSpace) {
                    Debug.LogError($"{Constants.packageName}: please use the the same Color Space in the AR Companion app (currently {companionColorSpace}) and in Unity Editor (currently {editorColorSpace}).");
                }
            }
        }
    }
}
