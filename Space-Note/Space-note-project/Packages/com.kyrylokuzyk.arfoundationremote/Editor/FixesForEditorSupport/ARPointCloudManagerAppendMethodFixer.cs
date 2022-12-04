using System.IO;
using System.Reflection;
using ARFoundationRemote.Runtime;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;


namespace ARFoundationRemote.Editor {
    public static class ARPointCloudManagerAppendMethodFixer {
        public static bool ApplyIfNeeded() {
            return applyFix();
        }

        /// <summary>
        /// Method <see cref="ARPointCloudManager.Append(NativeArray{T},NativeArray{T},int,Allocator)"/> causes Editor to throw exception
        /// To fix it change this line:
        /// NativeArray<T>.Copy(currentArray, dstArray);
        /// to this:
        /// NativeArray<T>.Copy(currentArray, dstArray, currentArray.Length);
        /// </summary>
        static bool IsFixApplied() {
            var array1 = new NativeArray<int>(0, Allocator.Temp);
            var array2 = new NativeArray<int>(new int[42], Allocator.Temp);
            
            var method = typeof(ARPointCloudManager).GetMethod("Append", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            var genericMethod = method.MakeGenericMethod(typeof(int));

            try {
                genericMethod.Invoke(null, new object[] {array1, array2, array2.Length, Allocator.Temp});
                return true;
            } catch {
                return false;
            } finally {
                array1.Dispose();
                array2.Dispose();
            }
        }
        
        static bool applyFix() {
            var path = "Packages/com.unity.xr.arfoundation/Runtime/AR/ARPointCloudManager.cs";
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            var text = script.text;
            //Debug.Log(text);
            var lineWithError = "NativeArray<T>.Copy(currentArray, dstArray);";
            if (!text.Contains(lineWithError)) {
                FixesForEditorSupport.log("ARPointCloudManagerAppendMethodFixer already applied");
                // Debug.LogError("ARPointCloudManager.cs fix can't be applied. For unknown reason, Unity 2019.2 caches this script file and doesn't see modified version.");
                // Debug.LogError("Please UnInstall the AR Foundation Editor Remote plugin, install needed XR Plugins, then install the plugin again.");
                // Debug.Log(text);
                return false;
            }

            FixesForEditorSupport.log("ARPointCloudManagerAppendMethodFixer");
            var withFix = text.Replace(lineWithError, "NativeArray<T>.Copy(currentArray, dstArray, currentArray.Length);");
            File.WriteAllText(AssetDatabase.GetAssetPath(script), withFix);
            return true;
        }
    }
}
