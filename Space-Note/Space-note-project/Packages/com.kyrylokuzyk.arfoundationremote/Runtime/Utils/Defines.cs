
namespace ARFoundationRemote.Runtime {
    public static class Defines {
        public static bool isIOS {
            get {
                return
                #if UNITY_IOS
                    true;
                #else
                    false;
                #endif
            }
        }

        public static bool isAndroid {
            get {
                return
                #if UNITY_ANDROID
                    true;
                #else
                    false;
                #endif
            }
        }

        public static bool isArkitFaceTrackingPluginInstalled {
            get {
                return
                #if ARFOUNDATION_REMOTE_ENABLE_IOS_BLENDSHAPES
                    true;
                #else
                    false;
                #endif
            }
        }

        public static bool isARFoundation4_0_OrNewer {
            get {
                return
                #if ARFOUNDATION_4_0_OR_NEWER
                    true;
                #else
                    false;
                #endif
            }
        }
    
        public static bool isARFoundation4_1_OrNewer {
            get {
                return
                #if ARFOUNDATION_4_1_OR_NEWER
                    true;
                #else
                    false;
                #endif
            }
        }
    
        public static bool isURPEnabled {
            get {
                return
                #if MODULE_URP_ENABLED
                    true;
                #else
                    false;
                #endif
            }
        }
    
        public static bool isLWRPEnabled {
            get {
                return
                #if MODULE_LWRP_ENABLED
                    true;
                #else
                    false;
                #endif
            }
        }
    
        public static bool isUnity2019_2 {
            get {
                return
                #if UNITY_2019_2
                    true;
                #else
                    false;
                #endif
            }
        }
       
        public static bool isCanvasGUIInstalled {
            get {
                return
                #if UGUI_INSTALLED
                    true;
                #else
                    false;
                #endif
            }
        }

        public static bool isARCompanionDefine {
            get {
                #if AR_COMPANION
                    return true;
                #else
                    return false;
                #endif
            }
        }
    
        public static bool arSubsystems_4_1_0_preview_11_or_newer {
            get {
                #if AR_SUBSYSTEMS_4_1_0_PREVIEW_11_OR_NEWER
                    return true;
                #else
                    return false;
                #endif
            }
        }
    }
}
