#if ARFOUNDATION_4_0_2_OR_NEWER
using System;
using JetBrains.Annotations;
using UnityEngine.XR.ARFoundation;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Assertions;
using System.Collections;


namespace ARFoundationRemote.Runtime {
    class CpuImageSerializer {
        public bool IsDone { get; private set; }
        public SerializedTextureAndPropId? result { get; private set; }

        readonly bool isSupported;
        readonly Func<Texture2D> getTexture;
        readonly TryAcquireCpuImageDelegate<XRCpuImage> tryAcquireCpuImage;
        readonly float resolutionScale;
        readonly TextureFormat? formatOverride;


        public CpuImageSerializer(bool isSupported, [NotNull] Func<Texture2D> getTexture, TryAcquireCpuImageDelegate<XRCpuImage> tryAcquireCpuImage, AROcclusionFrameEventArgs args, float resolutionScale, TextureFormat? formatOverride = null) {
            this.isSupported = isSupported;
            this.getTexture = getTexture;
            this.tryAcquireCpuImage = tryAcquireCpuImage;
            Assert.IsTrue(0f < resolutionScale && resolutionScale <= 1f);
            this.resolutionScale = resolutionScale;
            this.formatOverride = formatOverride;
            
            if (!trySerializeCpuImage(args)) {
                IsDone = true;
            }
        }

        bool trySerializeCpuImage(AROcclusionFrameEventArgs args) {
            if (!isSupported) {
                return false;
            }

            var tex = getTexture();
            if (tex == null) {
                return false;
            }
            
            var propName = OcclusionSubsystemSender.findPropName(tex, args);
            if (propName == null) {
                return false;
            }
        
            if (tryAcquireCpuImage(out var image) && image.valid) {
                DontDestroyOnLoadSingleton.AddCoroutine(serializeCpuImage(image, propName), nameof(serializeCpuImage));
                return true;
            } else {
                return false;
            }
        }

        IEnumerator serializeCpuImage(XRCpuImage image, string propName) {
            var origWidth = image.width;
            var origHeight = image.height;
            var destWidth = Mathf.RoundToInt(origWidth * resolutionScale);
            var destHeight = Mathf.RoundToInt(origHeight * resolutionScale);

            var format = formatOverride ?? image.format.AsTextureFormat();
            Assert.AreNotEqual(0, (int) format, "AreNotEqual(0, (int) format)");
            var conversionParams = new XRCpuImage.ConversionParams {
                outputDimensions = new Vector2Int(destWidth, destHeight),
                inputRect = new RectInt(0, 0, origWidth, origHeight),
                transformation = XRCpuImage.Transformation.None,
                outputFormat = format
            };

            using (image) {
                // callback version of ConvertAsync() will never finish after ARSession pause
                using (var conversion = image.ConvertAsync(conversionParams)) {
                    while (!conversion.status.IsDone()) {
                        yield return null;
                    }
                    
                    if (conversion.status == XRCpuImage.AsyncConversionStatus.Ready) {
                        var data = conversion.GetData<byte>();
                        if (data.IsCreated) {
                            result = new SerializedTextureAndPropId {
                                texture = new Texture2DSerializable(
                                    data.ToArray(),
                                    destWidth,
                                    destHeight,
                                    format,
                                    false
                                ),
                                propName = propName
                            };    
                        }
                    }

                    IsDone = true;
                }
            }
        }
    }

    public delegate bool TryAcquireCpuImageDelegate<TResult>(out TResult res);
}
#endif
