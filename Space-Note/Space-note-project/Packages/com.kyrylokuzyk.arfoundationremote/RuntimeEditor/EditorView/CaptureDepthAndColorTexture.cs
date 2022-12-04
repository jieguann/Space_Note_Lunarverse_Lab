#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.RuntimeEditor {
    public class CaptureDepthAndColorTexture : MonoBehaviour, IReceiver {
        [SerializeField] bool debug = false;
        [SerializeField] [CanBeNull] Texture2D depthTexture = null;
        [SerializeField] [CanBeNull] Texture2D colorTexture = null;
        
        [CanBeNull] RenderTexture depthRt;
        [CanBeNull] RenderTexture colorRt;
        Material renderDepthMaterial;
        readonly int prevDepthTexPropId = Shader.PropertyToID("_PrevDepthTex");
        Camera[] allCameras;
        Throttler throttler;
        bool canSend;
        Vector2? prevClipPlanes;
        ARSessionOrigin origin;
        [CanBeNull] Camera arCamera;
        bool? currentInvertCulling;

        static bool optimizeForPerformance => Settings.Instance.arCompanionSettings.mode == Mode.OptimizeForPerformance;


        IEnumerator Start() {
            throttler = new Throttler(Settings.Instance.editorGameViewSettings.maxEditorViewFps);
            renderDepthMaterial = new Material(Shader.Find("ARFoundationRemote/RenderDepth"));
            Assert.IsTrue(renderDepthMaterial.shader.isSupported);
            Assert.IsNull(GetComponent<Camera>());
            Assert.AreEqual(1, FindObjectsOfType<CaptureDepthAndColorTexture>().Length);

            if (optimizeForPerformance) {
                StartCoroutine(checkTransparentMaterials());
                StartCoroutine(disableCameraBackground());
            }
            
            var waitForEndOfFrame = new WaitForEndOfFrame();
            while (true) {
                while (!canSend) {
                    log("!canSend");
                    yield return null;
                }
                
                yield return waitForEndOfFrame;
                log("yield return waitForEndOfFrame");
                var w = Screen.width;
                var h = Screen.height;

                var isTransparent = optimizeForPerformance;
                var tex = Texture2DSerializable.GetCachedTexture(w, h, isTransparent ? TextureFormat.ARGB32 : TextureFormat.RGB24);
                tex.ReadPixels(new Rect(0,0,w,h),0,0);
                tex.Apply();
                Assert.IsNull(RenderTexture.active, "RenderTexture.active == null");
                Assert.IsNotNull(colorRt);
                Graphics.Blit(tex, colorRt);
                Assert.IsNotNull(colorRt);
                var serializedColorTex = isTransparent ? Texture2DSerializable.SerializeToPNG(colorRt) : Texture2DSerializable.SerializeToJPG(colorRt);
                if (debug) {
                    serializedColorTex.ResizeIfNeededAndDeserializeInto(ref colorTexture);
                }
        
                new EditorToPlayerMessage {
                    editorViewData = new EditorViewData {
                        colorTexture = serializedColorTex,
                        depthTexture = trySerializedDepthTexAndClear()
                    }
                }.Send();
                
                yield return null; // wait for updated canSend 
            }
        }

        IEnumerator checkTransparentMaterials() {
            var wait = new WaitForSeconds(2);
            while (true) {
                var transparentMaterials = FindObjectsOfType<Renderer>().Select(_ => _.sharedMaterial)
                    .Concat(FindObjectsOfType<Graphic>().Select(_ => _.materialForRendering))
                    .Concat(FindObjectsOfType<Projector>().Select(_ => _.material))
                    .Where(_ => _ != null && _.renderQueue >= (int) RenderQueue.AlphaTest)
                    .ToArray();

                if (transparentMaterials.Any()) {
                    Debug.LogWarning($"{Constants.packageName}: {Mode.OptimizeForPerformance} mode may display transparent materials incorrectly because of blending. Found transparent materials: {string.Join(", ", transparentMaterials.Select(_ => _.name))}");
                    yield break;
                }    
                
                yield return wait;
            }
        }

        [CanBeNull]
        Texture2DSerializable? trySerializedDepthTexAndClear() {
            if (shouldSendDepthTexture() && depthRt != null) {
                var prev = RenderTexture.active;
                RenderTexture.active = depthRt;
                var serializedDepthTex = Texture2DSerializable.SerializeToJPG(depthRt, 95);
                clearDepthTexture();
                prevClipPlanes = null;
                RenderTexture.active = prev;
                if (debug) {
                    serializedDepthTex.ResizeIfNeededAndDeserializeInto(ref depthTexture);
                }
                
                return serializedDepthTex;
            } else {
                return null;
            }
        }

        bool shouldSendDepthTexture() {
            if (optimizeForPerformance && arCamera != null) {
                #if ARFOUNDATION_4_0_2_OR_NEWER
                    var manager = arCamera.GetComponent<AROcclusionManager>();
                    return manager != null && manager.enabled;
                #endif
            }
            
            return false;
        }
        
        void OnDestroy() {
            foreach (var _ in new UnityEngine.Object[] {depthTexture, colorTexture, depthRt}) {
                if (_ != null) {
                    Destroy(_);
                }
            }
        }

        void Update() {
            canSend = throttler.CanSendNonCriticalMessage;
            updateArCameraReference();
            interceptOnCameraRenderEventsFromAllCameras();
            updateRenderTextures();
            setCameraClearFlagsAndBgColor();
        }

        void updateArCameraReference() {
            if (origin == null) {
                origin = FindObjectOfType<ARSessionOrigin>();
            }

            if (origin != null) {
                arCamera = origin.camera;
            }
        }

        void setCameraClearFlagsAndBgColor() {
            if (optimizeForPerformance && arCamera != null) {
                if (arCamera.clearFlags != CameraClearFlags.SolidColor) {
                    Debug.LogWarning($"{Constants.packageName}: camera clear flags should be {CameraClearFlags.SolidColor} in mode {Mode.OptimizeForPerformance}");
                    arCamera.clearFlags = CameraClearFlags.SolidColor;
                }

                var bgColor = arCamera.backgroundColor;
                if (bgColor != Color.clear) {
                    Debug.LogWarning($"{Constants.packageName}: camera background color should be Color.clear in mode {Mode.OptimizeForPerformance}");
                    arCamera.backgroundColor = Color.clear;
                }
            }
        }

        IEnumerator disableCameraBackground() {
            var waitForEndOfFrame = new WaitForEndOfFrame();
            while (true) {
                if (arCamera != null) {
                    var bg = arCamera.GetComponent<ARCameraBackground>();
                    if (bg != null && bg.enabled && bg.backgroundRenderingEnabled) {
                        yield return waitForEndOfFrame; // wait for invertCulling to be applied
                        // todo camera bg is not disabled if enable it manually in the Inspector
                        bg.enabled = false;
                        Debug.LogWarning($"{Constants.packageName}: {nameof(ARCameraBackground)} should be disabled in mode {Mode.OptimizeForPerformance}");
                    }
                }
                
                yield return null;
            }
        }

        void interceptOnCameraRenderEventsFromAllCameras() {
            if (!optimizeForPerformance) {
                return;
            }
            
            if (allCameras == null || allCameras.Length < Camera.allCamerasCount) {
                allCameras = new Camera[Camera.allCamerasCount];
            }

            Camera.GetAllCameras(allCameras);
            
            foreach (var _ in allCameras) {
                if (_ == null) {
                    continue;
                }
                
                _.depthTextureMode |= DepthTextureMode.Depth;
                if (_.GetComponent<OnRenderImageCallback>() == null) {
                    _.gameObject.AddComponent<OnRenderImageCallback>().callback = captureDepthTexture;
                }
            }
        }

        void captureDepthTexture([NotNull] Camera cam) {
            Assert.IsTrue(optimizeForPerformance);
            if (canSend && shouldSendDepthTexture()) {
                log($"onCameraRenderImage {cam.name}");
                checkCameraDepth(cam);
                checkClippingPlanes(cam);
                // todo captureDepthTexture can be called after Destroy() (only in URP?)
                renderDepthMaterial.SetTexture(prevDepthTexPropId, depthRt);
                Graphics.Blit(null, depthRt, renderDepthMaterial);
            }
        }

        void checkCameraDepth(Camera cam) {
            if (arCamera != null && cam.depth < arCamera.depth) {
                Debug.LogError("arCamera != null && cam.depth < arCamera.depth");
            }
        }

        void checkClippingPlanes(Camera cam) {
            var curClipPlanes = new Vector2(cam.nearClipPlane, cam.farClipPlane);
            if (prevClipPlanes.HasValue && prevClipPlanes.Value != curClipPlanes) {
                Debug.LogError($"{Constants.packageName}: please set near/far clipping planes on all cameras to the same values for occlusion to be displayed correctly in companion app. Current camera: {curClipPlanes}, previous camera: {prevClipPlanes.Value}");
            }

            prevClipPlanes = curClipPlanes;
        }

        void updateRenderTextures() {
            var downscaled = getDownscaledResolution();
            var w = downscaled.x;
            var h = downscaled.y;
            updateRt(ref depthRt, RenderTextureFormat.R8);
            updateRt(ref colorRt, RenderTextureFormat.ARGB32);

            void updateRt(ref RenderTexture rt, RenderTextureFormat format) {
                if (rt != null && rt.width == w && rt.height == h) {
                    return;
                }

                if (rt != null) {
                    Destroy(rt);
                }

                rt = new RenderTexture(w, h, 0, format);
            }
        }

        static Vector2Int getDownscaledResolution() {
            var scale = Settings.Instance.editorGameViewSettings.resolutionScale;
            var downscaledWidth = Mathf.RoundToInt(Screen.width * scale);
            var downscaledHeight = Mathf.RoundToInt(Screen.height * scale);
            return new Vector2Int(downscaledWidth, downscaledHeight);
        }

        [ContextMenu("clearDepthTexture")]
        void clearDepthTexture() {
            log("clearDepthTexture");
            setActiveRtAndRestore(depthRt, () => {
                GL.Clear(false, true, new Color(1, 0, 0));
            });
        }

        void setActiveRtAndRestore(RenderTexture rt, Action action) {
            PreserveActiveRenderTexture(() => {
                RenderTexture.active = rt;
                action();
            });
        }

        static void PreserveActiveRenderTexture(Action action) {
            var prev = RenderTexture.active;
            action();
            RenderTexture.active = prev;
        }

        public void Receive(PlayerToEditorMessage data) {
            if (optimizeForPerformance && arCamera != null) {
                var projMatrix = data.cameraData?.cameraFrame?.frame.Value.projectionMatrix;
                if (projMatrix.HasValue) {
                    arCamera.projectionMatrix = projMatrix.Value;
                }  
                    
                var invertCulling = data.cameraData?.cameraFrame?.invertCulling;
                if (invertCulling.HasValue) {
                    var newInvertCulling = invertCulling.Value;
                    if (currentInvertCulling != newInvertCulling) {
                        currentInvertCulling = newInvertCulling;
                        var bg = arCamera.GetComponent<ARCameraBackground>();
                        if (bg != null) {
                            // enable ARCameraBackground to apply new invertCulling
                            bg.enabled = true;
                        }
                    }
                }    
            }
        }

        [Conditional("_")]
        void log(string s) {
            Debug.Log(s);
        }
    }
}
#endif
