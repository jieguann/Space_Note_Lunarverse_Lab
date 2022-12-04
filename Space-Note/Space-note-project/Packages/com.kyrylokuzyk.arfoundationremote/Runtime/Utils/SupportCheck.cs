#if UNITY_EDITOR
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public static class SupportCheck {
        public static bool CheckCameraAndOcclusionSupport([NotNull] Material cameraMaterial, [NotNull] string textureName) {
            if (!cameraMaterial.shader.isSupported) {
                Debug.LogError("Background camera material shader is not supported in Editor.");
                return false;
            }

            if (!cameraMaterial.HasProperty(textureName)) {
                Debug.LogError("Background camera material doesn't contain property with name " + textureName +
                               ". Please ensure AR Companion app is running the same build target and same render pipeline as Editor.");
                return false;
            }

            if (Defines.isUnity2019_2 && Application.platform == RuntimePlatform.WindowsEditor) {
                // Unity Editor crashes at
                // ARTextureInfo.CreateTexture() at Texture2D.CreateExternalTexture()
                Debug.LogError("Camera video is not supported in Windows Unity Editor 2019.2");
                return false;
            }

            if (!checkGraphicsApi()) {
                return false;
            }
            
            return true;
        }
        
        
        static bool checkGraphicsApi() {
            var selectedStandaloneTarget = EditorUserBuildSettings.selectedStandaloneTarget;
            logGraphicsApi($"selectedStandaloneTarget: {selectedStandaloneTarget}");
            var apis = PlayerSettings.GetGraphicsAPIs(selectedStandaloneTarget);
            logGraphicsApi($"apis: {string.Join(", ", apis)}");
            var supportedApis = new[] {UnityEngine.Rendering.GraphicsDeviceType.Metal, UnityEngine.Rendering.GraphicsDeviceType.Direct3D11};
            var editorApi = apis.First();
            logGraphicsApi($"editorApi: {editorApi}");
            if (supportedApis.Contains(editorApi)) {
                return true;
            } else {
                Debug.LogError($"{Constants.packageName}: camera video and occlusion are not supported on current Editor Graphics API: {editorApi}.\n" +
                               "Please set the 'Project Settings/Player/Settings for PC.../Graphics Api for YOUR_PLATFORM' to one of these APIs:\n" +
                               $"{string.Join(", ", supportedApis)}\n");
                return false;
            }
        }

        [Conditional("_")]
        static void logGraphicsApi(string s) {
            Debug.Log(s);
        }

    }
}
#endif
