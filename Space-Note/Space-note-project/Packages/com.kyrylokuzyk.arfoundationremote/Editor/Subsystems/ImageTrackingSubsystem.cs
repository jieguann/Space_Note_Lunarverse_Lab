using System;
using System.Collections.Generic;
using System.Linq;
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Editor {
    public partial class ImageTrackingSubsystem: XRImageTrackingSubsystem {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.ShouldRegisterSubsystemDescriptor()) {
                return;
            }

            var thisType = typeof(ImageTrackingSubsystem);
            XRImageTrackingSubsystemDescriptor.Create(new XRImageTrackingSubsystemDescriptor.Cinfo {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(ImageTrackingSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    subsystemImplementationType = thisType,
                #endif
                supportsMovingImages = true,
                supportsMutableLibrary = true,
                requiresPhysicalImageDimensions = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS
            });
        }
        
        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new ImageTrackingSubsystemProvider();
        #endif

        class ImageTrackingSubsystemProvider: Provider {
            public override TrackableChanges<XRTrackedImage> GetChanges(XRTrackedImage defaultTrackedImage, Allocator allocator) {
                return getChanges(allocator);
            }

            public override RuntimeReferenceImageLibrary CreateRuntimeLibrary([CanBeNull] XRReferenceImageLibrary serializedLibrary) {
                ImageTrackingSubsystemSender.log("RuntimeReferenceImageLibrary CreateRuntimeLibrary");
                return new ImageLibrary(serializedLibrary);
            }

            [CanBeNull]
            public override RuntimeReferenceImageLibrary imageLibrary {
                set {
                    ImageTrackingSubsystemSender.log("send library " + (value != null ? value.count.ToString() : "NULL"));
                    new EditorToPlayerMessage {
                        imageLibrary = new ImageLibrarySerializableContainer {
                            library = value != null ? toSerializable(value) : null
                        }
                    }.Send();
                }
            }
                  
            static ImageLibrarySerializable toSerializable([NotNull] IReferenceImageLibrary library) {
                var images = new List<XRReferenceImageSerializable>();
                for (int i = 0; i < library.count; i++) {
                    var referenceImage = library[i];
                    Texture2D textureOverride = null;
                    if (referenceImage.texture == null) {
                        // will be null if Keep Texture at Runtime == false
                        textureOverride = loadTextureFromAssetDatabase(referenceImage.textureGuid);
                    }
                    
                    images.Add(XRReferenceImageSerializable.Create(referenceImage, textureOverride));
                }

                return new ImageLibrarySerializable(images);
            }

            static Texture2D loadTextureFromAssetDatabase(Guid guid) {
                var path = AssetDatabase.GUIDToAssetPath(guid.ToString("N"));
                Assert.IsTrue(path.Any());
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(tex);
                // Debug.Log("image texture loaded from Asset Database " + guid);
                return tex;
            }
            
            #if !ARFOUNDATION_4_0_OR_NEWER
                public override int maxNumberOfMovingImages {
                    set => ImageTrackingSubsystemSender.log("maxNumberOfMovingImages setter not implemented");
                }
            #else
                public override int currentMaxNumberOfMovingImages => requestedMaxNumberOfMovingImages;
                public override int requestedMaxNumberOfMovingImages { get; set; }
            #endif

            #if UNITY_2020_2_OR_NEWER
            public override void Start() {
            }

            public override void Stop() {
            }
            #endif
            
            public override void Destroy() {
                receiver.Reset();
            }
        }

        class ImageLibrary : MutableRuntimeReferenceImageLibrary {
            readonly List<XRReferenceImage> images = new List<XRReferenceImage>();


            public ImageLibrary([CanBeNull] XRReferenceImageLibrary lib) {
                if (lib != null) {
                    for (int i = 0; i < lib.count; i++) {
                        add(lib[i]);
                    }
                }
            }

            void add(XRReferenceImage xrReferenceImage) {
                images.Add(xrReferenceImage);
            }

            protected override XRReferenceImage GetReferenceImageAt(int index) {
                return images[index];
            }

            public override int count => images.Count;

            protected override JobHandle ScheduleAddImageJobImpl(NativeSlice<byte> imageBytes, Vector2Int sizeInPixels, TextureFormat format, XRReferenceImage referenceImage, JobHandle inputDeps) {
                add(referenceImage);

                var manager = UnityEngine.Object.FindObjectOfType<UnityEngine.XR.ARFoundation.ARTrackedImageManager>();
                if (manager != null && manager.referenceLibrary == this) {
                    ImageTrackingSubsystemSender.log($"send ScheduleAddImageJobImpl {referenceImage.name}, {referenceImage.guid}, size: {sizeInPixels}");
                    new EditorToPlayerMessage { imageToAdd = XRReferenceImageSerializable.Create(referenceImage, null) }.Send();    
                } else {
                    ImageTrackingSubsystemSender.log("skipping send ScheduleAddImageJobImpl because manager.referenceLibrary != this");
                }
                
                return inputDeps;
            }

            protected override TextureFormat GetSupportedTextureFormatAtImpl(int index) {
                return supportedFormats[index];
            }

            public override int supportedTextureFormatCount => supportedFormats.Length;

            [NotNull] static readonly TextureFormat[] supportedFormats = Enum.GetValues(typeof(TextureFormat)) as TextureFormat[] ?? throw new Exception();
        }
    }
}
