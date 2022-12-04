using System.Diagnostics;
using ARFoundationRemote.Runtime;
using UnityEditor;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Editor {
    [InitializeOnLoad]
    public static class FixesForEditorSupport {
        static FixesForEditorSupport() {
            Apply();
        }

        public static void Apply(bool force = false) {
            var isAnyFixApplied = false;
            isAnyFixApplied |= ARPointCloudManagerAppendMethodFixer.ApplyIfNeeded();
            isAnyFixApplied |= ARCameraBackgroundFixer.ApplyFixIfNeeded();
            isAnyFixApplied |= ARFaceEditorMemorySafetyErrorFixer.ApplyIfNeeded();
            if (Defines.isURPEnabled || Defines.isLWRPEnabled) {
                isAnyFixApplied |= ARBackgroundRendererFeatureFixer.ApplyFixIfNeeded();
            }

            isAnyFixApplied |= ARMeshManagerFixer.ApplyFixIfNeeded(force) | ARKitBlendShapeVisualizerFixer.ApplyIfNeeded();

            if (isAnyFixApplied) {
                AssetDatabase.Refresh();
            }
        }

        public static void Undo() {
            var isAnyUndone = ARMeshManagerFixer.Undo() | ARKitBlendShapeVisualizerFixer.Undo();
            if (isAnyUndone) {
                AssetDatabase.Refresh();
            }            
        }
        
        [Conditional("_")]
        public static void log(string s) {
            Debug.Log(s);
        }
    }
}
