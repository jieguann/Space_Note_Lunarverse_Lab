#if ARFOUNDATION_4_0_2_OR_NEWER
using System;
using System.Collections;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Linq;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Runtime {
    public class OcclusionSubsystemSender : ISubsystemSender {
        readonly AROcclusionManager manager;
        bool isSending;
        static bool isSupportChecked;


        public OcclusionSubsystemSender(AROcclusionManager _manager) {
            manager = _manager;
            manager.frameReceived += args => {
                if (!isSending && canSend() && args.textures.Any() && CameraSubsystemSender.Instance.canRunAsyncConversion && Settings.Instance.arCompanionSettings.mode == Mode.Regular) {
                    // AR Companion will crash on scene reload if CpuImageSerializer is running
                    // DontDestroyOnLoadSingleton.AddCoroutine ensures that all coroutines are finished before calling scene reload
                    CameraSubsystemSender.Instance.AddAsyncConversionCoroutine(sendTextures(args), nameof(sendTextures));

                    if (!isSupportChecked) {
                        isSupportChecked = true;
                        if (!isSupported()) {
                            Sender.AddRuntimeErrorOnce("- Occlusion is not supported on this device");
                        }
                    }
                }
            };
        }

        IEnumerator sendTextures(AROcclusionFrameEventArgs args) {
            isSending = true;

            var descriptor = manager.descriptor;
            var resolutionScale = Settings.occlusionSettings.resolutionScale;
            var humanStencilSerializer = new CpuImageSerializer(descriptor.supportsHumanSegmentationStencilImage, () => manager.humanStencilTexture, manager.TryAcquireHumanStencilCpuImage, args, resolutionScale);
            var humanDepthSerializer = new CpuImageSerializer(descriptor.supportsHumanSegmentationDepthImage, () => manager.humanDepthTexture, manager.TryAcquireHumanDepthCpuImage, args, resolutionScale);

            #if ARFOUNDATION_4_1_OR_NEWER
                var depthSerializer = new CpuImageSerializer(descriptor.supportsEnvironmentDepthImage, () => manager.environmentDepthTexture, manager.TryAcquireEnvironmentDepthCpuImage, args, resolutionScale);
                var environmentDepthConfidenceSerializer = new CpuImageSerializer(descriptor.supportsEnvironmentDepthConfidenceImage, () => manager.environmentDepthConfidenceTexture, manager.TryAcquireEnvironmentDepthConfidenceCpuImage, args, resolutionScale);
            #endif
            
            var serializers = new[] {
                humanStencilSerializer, 
                humanDepthSerializer, 
                #if ARFOUNDATION_4_1_OR_NEWER
                    depthSerializer, 
                    environmentDepthConfidenceSerializer
                #endif
            };
            
            while (serializers.Any(_ => !_.IsDone)) {
                yield return null;
            }

            if (Sender.isConnected) {
                new PlayerToEditorMessage {
                    occlusionData = new OcclusionData {
                        humanStencil = humanStencilSerializer.result,
                        humanDepth = humanDepthSerializer.result,
                        #if ARFOUNDATION_4_1_OR_NEWER
                            environmentDepth = depthSerializer.result,
                            environmentDepthConfidence = environmentDepthConfidenceSerializer.result,
                        #endif
                    }
                }.Send();
            }

            isSending = false;
        }

        float lastSendTime;
        
        bool canSend() {
            if (!Connection.senderConnection.CanSendNonCriticalMessage) {
                return false;
            }
            
            var curTime = Time.time;
            if (curTime - lastSendTime > 1f / Settings.occlusionSettings.maxFPS) {
                lastSendTime = curTime;
                return true;
            } else {
                return false;
            }
        }

        [CanBeNull]
        public static string findPropName([NotNull] Texture2D tex, AROcclusionFrameEventArgs args) {
            var i = args.textures.FindIndex(_ => _.GetNativeTexturePtr() == tex.GetNativeTexturePtr());
            if (i != -1 && CameraSubsystemSender.Instance.PropIdToName(args.propertyNameIds[i], out var result)) {
                return result;
            } else {
                return null;
            }
        }

        void ISubsystemSender.EditorMessageReceived(EditorToPlayerMessage data) {
            var occlusionData = data.occlusionData;
            if (occlusionData == null) {
                return;
            }
            
            if (occlusionData.requestedHumanDepthMode.HasValue) {
                manager.requestedHumanDepthMode = occlusionData.requestedHumanDepthMode.Value;
            }
            
            if (occlusionData.requestedHumanStencilMode.HasValue) {
                manager.requestedHumanStencilMode = occlusionData.requestedHumanStencilMode.Value;
            }

            if (occlusionData.enableOcclusion.HasValue) {
                Sender.Instance.SetManagerEnabled(manager, occlusionData.enableOcclusion.Value);
            }

            #if ARFOUNDATION_4_1_OR_NEWER
            if (occlusionData.requestedEnvironmentDepthMode.HasValue) {
                manager.requestedEnvironmentDepthMode = occlusionData.requestedEnvironmentDepthMode.Value;
            }

            if (occlusionData.requestedOcclusionPreferenceMode.HasValue) {
                manager.requestedOcclusionPreferenceMode = occlusionData.requestedOcclusionPreferenceMode.Value;
            }
            #endif
        }

        bool isSupported() {
            var descriptor = manager.descriptor;
            if (descriptor == null) {
                return false;
            }
            
            #if ARFOUNDATION_4_1_OR_NEWER
            // correct values are reported only after first AROcclusionManager.frameReceived event
            // Debug.LogWarning($"supportsEnvironmentDepthImage: {descriptor.supportsEnvironmentDepthImage}, supportsEnvironmentDepthConfidenceImage: {descriptor.supportsEnvironmentDepthConfidenceImage}");
            if (descriptor.supportsEnvironmentDepthImage || descriptor.supportsEnvironmentDepthConfidenceImage) {
                return true;
            }
            #endif

            return descriptor.supportsHumanSegmentationDepthImage || descriptor.supportsHumanSegmentationStencilImage;
        }
    }


    [Serializable]
    public class OcclusionData {
        public SerializedTextureAndPropId? humanStencil;
        public SerializedTextureAndPropId? humanDepth;
        #if ARFOUNDATION_4_1_OR_NEWER
            public SerializedTextureAndPropId? environmentDepth;
            public SerializedTextureAndPropId? environmentDepthConfidence;
        #endif
    }


    [Serializable]
    public class OcclusionDataEditor {
        public HumanSegmentationDepthMode? requestedHumanDepthMode;
        public HumanSegmentationStencilMode? requestedHumanStencilMode;
        public bool? enableOcclusion;
        #if ARFOUNDATION_4_1_OR_NEWER
        public EnvironmentDepthMode? requestedEnvironmentDepthMode;
        public OcclusionPreferenceMode? requestedOcclusionPreferenceMode;
        #endif
    }
}
#endif
