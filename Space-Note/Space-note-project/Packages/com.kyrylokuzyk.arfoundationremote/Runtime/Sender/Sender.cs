using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public class Sender : MonoBehaviour {
        [SerializeField] List<SubsystemSender> serializedSenders = new List<SubsystemSender>();
        [SerializeField] ARSession arSession = null;
        #pragma warning disable 414
        [SerializeField] ARSessionOrigin origin = null;
        #pragma warning restore 414
        [SerializeField] SetupARFoundationVersionSpecificComponents setuper = null;

        const string noARCapabilitiesMessage = "Please run this scene on device with AR capabilities\n" +
                                               "and install AR Provider (ARKit XR Plugin, ARCore XR Plugin, etc)\n" +
                                               "and enable AR Provider in Project Settings -> XR Plug-in Management";
        static readonly string[] logMessagesToIgnore = {
            "ARPoseDriver is already consuming data from", // warning because ARPoseDriver.s_InputTrackingDevice field is static
        };
        
        static readonly string[] logMessagesToLogOnce = {
            "You can only call cameraDepthTarget inside the scope",
        };

        public static Sender Instance { get; private set; }
        readonly List<ISubsystemSender> senders = new List<ISubsystemSender>();
        bool availabilityChecked;
        bool isAlive = true;
        public static bool isConnected { get; private set; }
        ARSessionState? sessionState;


        void Awake() {
            logSceneReload("Sender.Awake()");
            Application.logMessageReceivedThreaded += logMessageReceivedThreaded;

            var xrGeneralSettings = XRGeneralSettings.Instance;
            Assert.IsNotNull(xrGeneralSettings, "xrGeneralSettings != null");
            var settings = xrGeneralSettings.Manager;
            Assert.IsNotNull(settings, "xrManagerSettings != null");
            settings.InitializeLoaderSyncIfNotInitialized();
            
            Assert.IsNull(Instance, "Instance == null");
            Instance = this;
            
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            
            senders.AddRange(serializedSenders);
            
            setupARF4_0_2_Components();
            void setupARF4_0_2_Components() {
                #if ARFOUNDATION_4_0_2_OR_NEWER
                var originGameObject = origin.gameObject;

                void executeOnDisabledOrigin(Action action) {
                    originGameObject.SetActive(false);
                    action();
                    originGameObject.SetActive(true);
                }

                executeOnDisabledOrigin(() => {
                    var descriptors = new List<XRHumanBodySubsystemDescriptor>();
                    SubsystemManager.GetSubsystemDescriptors(descriptors);
                    if (descriptors.Any()) {
                        var manager = originGameObject.AddComponent<ARHumanBodyManager>();
                        manager.pose2DRequested = false;
                        manager.pose3DRequested = false;
                        manager.pose3DScaleEstimationRequested = false;
                        manager.enabled = false;
                        AddSender(new HumanBodySubsystemSender(manager));    
                    }
                });

                AddSender(new ObjectTrackingSubsystemSender(origin));
                
                executeOnDisabledCamera(() => {
                    var manager = origin.camera.gameObject.AddComponent<AROcclusionManager>();
                    manager.enabled = false;
                    manager.requestedHumanStencilMode = HumanSegmentationStencilMode.Disabled;
                    manager.requestedHumanDepthMode = HumanSegmentationDepthMode.Disabled;

                    #if ARFOUNDATION_4_1_OR_NEWER
                        manager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Disabled;
                    #endif

                    Instance.AddSender(new OcclusionSubsystemSender(manager));
                });
                
                void executeOnDisabledCamera(Action action) {
                    var cameraGameObject = origin.camera.gameObject;
                    cameraGameObject.SetActive(false);
                    action();
                    cameraGameObject.SetActive(true);
                }
                #endif
            }

            Assert.IsNotNull(setuper, "setuper != null");
            var cameraManager = setuper.cameraManager;
            var cameraBackground = setuper.cameraBackground;
            Assert.IsNotNull(cameraBackground, "cameraBackground != null");
            AddSender(new CameraSubsystemSender(cameraManager, arSession));
            AddSender(new CpuImageSender(cameraManager));
            #if UNITY_IOS && ARKIT_INSTALLED
                AddSender(new WorldMapSender(arSession));
            #endif

            Assert.IsTrue(origin.camera.transform.parent.lossyScale == Vector3.one);
            if (Application.isEditor) {
                Debug.LogError("Please run this scene on AR capable device");
                enabled = false;
                return;
            }

            setupConnection();
            void setupConnection() {
                var connection = Connection.senderConnection;
                connection.RegisterConnection(onConnectedToEditor);
                connection.RegisterDisconnection(onDisconnectedFromEditor);
                connection.Register(editorMessageReceived);
            }

            AddSender(gameObject.AddComponent<OriginDataReceiver>());

            if (!availabilityChecked) {
                availabilityChecked = true;
                StartCoroutine(checkAvailability());
            }
        }

        void Update() {
            foreach (var _ in senders.OfType<ISubsystemSenderUpdateable>()) {
                _.UpdateSender();
            }
            
            if (isConnected) {
                var currentState = ARSession.state;
                if (sessionState != currentState) {
                    sessionState = currentState;
                    new PlayerToEditorMessage {sessionState = currentState}.Send();
                }
            }
        }

        void OnDestroy() {
            logIfNeeded("Sender.OnDestroy()");
            Instance = null;
            var connection = Connection.senderConnection;
            connection.UnregisterConnection(onConnectedToEditor);
            connection.UnregisterDisconnection(onDisconnectedFromEditor);
            connection.Unregister(editorMessageReceived);
            Application.logMessageReceivedThreaded -= logMessageReceivedThreaded;
        }
        
        void logMessageReceivedThreaded(string message, string stacktrace, LogType type) {
            if (shouldShowLog()) {
                runningErrorMessage += $"{type}: {message}\n{stacktrace}\n";
            }

            bool shouldShowLog() {
                if (type == LogType.Log) {
                    return false;
                }
                
                if (logMessagesToIgnore.Any(message.Contains)) {
                    return false;
                }

                if (logMessagesToLogOnce.Any(message.Contains) && !DebugUtils.IsFirstOccurrence(message)) {
                    return false;
                }

                return true;
            }
        }

        [Conditional("_")]
        static void logSceneReload(string message) {
            Debug.Log(message);
        }

        void AddSender([NotNull] ISubsystemSender subsystemSender) {
            senders.Add(subsystemSender);
        }

        IEnumerator initSessionCor() {
            while (ARSession.state < ARSessionState.Ready) {
                yield return null;
            }
            
            new PlayerToEditorMessage {messageType = PlayerToEditorMessageType.SessionReady}.Send();
        }

        void onConnectedToEditor(int _) {
            isConnected = true;
        }
        
        void onDisconnectedFromEditor(int _) {
            logIfNeeded("onDisconnectedFromEditor");
            isConnected = false;
            tryReloadScene();
        }

        void tryReloadScene([CanBeNull] Action onCompleted = null) {
            if (isAlive) {
                isAlive = false;
                editorMessageReceived(new EditorToPlayerMessage {messageType = EditorToPlayerMessageType.DisconnectedFromEditor});
                stopSession();
                StartCoroutine(reloadSceneCor(onCompleted));
            }
        }

        IEnumerator reloadSceneCor([CanBeNull] Action callback) {
            logSceneReload("reloadSceneCor()");
            var timeStart = Time.time;
            while (DontDestroyOnLoadSingleton.runningCoroutineNames.Count > 0) {
                if (Time.time - timeStart > 5) {
                    Debug.LogError($"reloadSceneCor() while coroutines were running: {string.Join(", ", DontDestroyOnLoadSingleton.runningCoroutineNames)}");
                    break;
                }
                
                yield return null;
            }

            SceneManager.LoadScene("ARCompanion");
            // clear old data to prevent face tracking and image tracking duplications
            Connection.senderConnection.ClearMessages();
            callback?.Invoke();
        }
        
        void editorMessageReceived([NotNull] EditorToPlayerMessage data) {
            var settings = data.settings;
            if (settings != null) {
                Settings.Instance.arCompanionSettings = settings;
                Texture2DSerializable.ClearCache();    
            }

            var editorPackages = data.editorPackages;
            if (editorPackages != null) {
                if (!PackageVersionData.CheckVersions(Settings.Instance.packages, editorPackages)) {
                    new PlayerToEditorMessage {
                        companionAppPackages = Settings.Instance.packages
                    }.Send();
                }
            }
            
            var messageType = data.messageType;
            if (messageType != EditorToPlayerMessageType.None) {
                logIfNeeded("editorMessageReceived type: " + messageType);
            }
            
            switch (messageType) {
                case EditorToPlayerMessageType.Init:
                    DontDestroyOnLoadSingleton.AddCoroutine(initSessionCor(), nameof(initSessionCor));
                    break;
                case EditorToPlayerMessageType.ResumeSession:
                    setSessionEnabled(true);
                    setARComponentsEnabled(true);
                    break;
                case EditorToPlayerMessageType.PauseSession:
                    pauseSession();
                    break;
                case EditorToPlayerMessageType.ResetSession:
                    resetSession();
                    break;
                case EditorToPlayerMessageType.DestroySession:
                    stopSession();
                    break;
                case EditorToPlayerMessageType.InitializeLoader:
                    XRGeneralSettings.Instance.Manager.InitializeLoaderSyncIfNotInitialized();
                    break;
                case EditorToPlayerMessageType.DeinitializeLoader:
                    var manager = XRGeneralSettings.Instance.Manager;
                    if (manager.isInitializationComplete) {
                        logSceneReload("manager.DeinitializeLoader();");
                        manager.DeinitializeLoader();
                    }
                    
                    break;
                case EditorToPlayerMessageType.SceneUnloaded:
                    tryReloadScene(() => {
                        new PlayerToEditorMessage {
                            responseGuid = data.requestGuid
                        }.Send();
                    });
                    break;
            }

            foreach (var _ in senders) {
                _.EditorMessageReceived(data);
            }
        }

        void setSessionEnabled(bool isEnabled) {
            LogObjectTrackingCrash($"setSessionEnabled {isEnabled}");
            arSession.enabled = isEnabled;
        }

        void stopSession() {
            logSceneReload("stopSession()");
            pauseAndResetSession();
            setARComponentsEnabled(false);
            setManagersEnabled(false);
        }

        readonly Dictionary<Behaviour, bool> managers = new Dictionary<Behaviour, bool>();
        
        void setARComponentsEnabled(bool enable) {
            // logSceneReload($"setARComponentsEnabled {enable}");
            var types = new[] {
                typeof(ARInputManager), // disable and enable to prevent native errors
                // typeof(ARCameraBackground) // todo sync ARCameraBackground.enabled with Editor (only when mode == Regular) 
            };
            
            foreach (var _ in types.Select(FindObjectOfType).Cast<MonoBehaviour>()) {
                _.enabled = enable;
            }
        }

        void setManagersEnabled(bool enable) {
            logSceneSpecific($"setManagersEnabled {enable}");
            foreach (var pair in managers) {
                pair.Key.enabled = enable && pair.Value;
            }
        }

        void resetSession() {
            LogObjectTrackingCrash("resetSession()");
            arSession.Reset();    
        }

        public void SetManagerEnabled<T>([NotNull] T manager, bool managerEnabled) where T : Behaviour {
            logSceneSpecific($"{typeof(T)} enabled {managerEnabled}");
            manager.enabled = managerEnabled;
            managers[manager] = managerEnabled;
        }

        void pauseSession() {
            setSessionEnabled(false);
        }

        IEnumerator checkAvailability() {
            yield return ARSession.CheckAvailability();
            while (ARSession.state == ARSessionState.Installing) {
                yield return null;
            }
            
            Assert.IsTrue(isSupported, noARCapabilitiesMessage);

            if (Settings.Instance.debugSettings.printCompanionAppIPsToConsole) {
                while (true) {
                    var ips = getIPAddresses().ToList();
                    if (ips.Any() && Connection.senderConnection.isActive) {
                        Debug.Log(getIPsMessage(ips));
                        break;
                    } else {
                        yield return null;
                    }
                }    
            }
        }

        void pauseAndResetSession() {
            logSceneReload("pauseAndResetSession()");
            pauseSession();
            resetSession();
        }

        static bool isSupported => ARSession.state >= ARSessionState.Ready;

        [Conditional("_")]
        void logIfNeeded(string message) {
            Debug.Log(message);
        }

        void OnGUI() {
            ShowTextAtCenter(getUserMessageAndAppendErrorIfNeeded());
            showPackages();

            void showPackages() {
                if (isSessionCreatedAndRunning) {
                    return;
                }
                
                const int height = 300;
                const int margin = 30;
                var position = new Rect(margin, Screen.height - height - margin, 200, height);
                var text = string.Join("\n", Settings.Instance.packages.Select(_ => _.ToString()));
                var style = new GUIStyle {
                    fontSize = 30,
                    normal = new GUIStyleState {textColor = Color.white},
                    alignment = TextAnchor.LowerLeft
                };

                GUI.Label(position, text, style);
            }

            string getUserMessageAndAppendErrorIfNeeded() {
                if (isSessionCreatedAndRunning) {
                    return runningErrorMessage;
                } else {
                    var result = getWaitingMessage() + "\n\n" + waitingErrorMessage + "\n\n" + runningErrorMessage;
                    if (string.IsNullOrEmpty(runningErrorMessage)) {
                        result += "\n\nPlease leave an honest review on the Asset Store :)";
                    }

                    return result;
                }
            }
        }

        public static string waitingErrorMessage = "";
        static string runningErrorMessage = "";

        string getWaitingMessage() {
            if (!isSupported) {
                return noARCapabilitiesMessage;
            } else {
                var ips = getIPAddresses().ToList();
                if (ips.Any()) {
                    var server = Connection.senderConnection;
                    Assert.IsNotNull(server);
                    if (server.isActive) {
                        return getIPsMessage(ips);
                    } else {
                        return "AR Companion app can't start server.\n" +
                               "Please ensure only one instance of the app is running or restart the app.";
                    }
                } else {
                    return "Can't start sender. Please connect AR device to private network.";
                }
            }
        }

        static string getIPsMessage([NotNull] List<IPAddress> ips) {
            return "Please enter AR Companion app IP in\n" +
                   "Assets/Plugins/ARFoundationRemoteInstaller/Resources/Settings\n" +
                   "and start AR scene in Editor.\n\n" +
                   "Available IP addresses:\n" + String.Join("\n", ips);
        }

        [NotNull]
        static IEnumerable<IPAddress> getIPAddresses() {
            return NetworkInterface.GetAllNetworkInterfaces()
                .SelectMany(_ => _.GetIPProperties().UnicastAddresses)
                .Select(_ => _.Address)
                .Where(_ => _.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(_))
                .Distinct();
        }

        bool isSessionCreatedAndRunning => arSession != null && arSession.enabled;

        [Conditional("_")]
        public static void logSceneSpecific(string msg) {
            Debug.Log(msg);
        }

        public static void ShowTextAtCenter(string text) {
            GUILayout.BeginArea(new Rect(0, 0, Screen.width, Screen.height));
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(text, new GUIStyle {fontSize = 30, normal = new GUIStyleState {textColor = Color.white}});
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        [Conditional("_")]
        public static void LogObjectTrackingCrash(string msg) {
            Debug.Log(msg);
        }

        public static void AddRuntimeErrorOnce(string error) {
            if (DebugUtils.LogOnce(error)) {
                AddRuntimeError(error);
            }
        }

        public static void AddRuntimeError(string error) {
            runningErrorMessage += error + "\n";
        }
    }


    [Serializable]
    public class PlayerToEditorMessage {
        /*public readonly int messageNumber;
        static int staticMessageNumber;
        public PlayerToEditorMessage() {
            messageNumber = staticMessageNumber;
            staticMessageNumber++;
        }*/
        
        public PlayerToEditorMessageType messageType;
        public PoseSerializable? cameraPose;
        [CanBeNull] public PlanesUpdateData planesUpdateData;
        [CanBeNull] public PointCloudData pointCloudData;
        public ARSessionState? sessionState;
        [CanBeNull] public FaceSubsystemData faceSubsystemData;
        public Guid? responseGuid;
        [CanBeNull] public TouchSerializable[] touches;
        public TrackableChangesData<XRTrackedImageSerializable>? trackedImagesData;
        public CameraData? cameraData;
        public TrackableChangesData<ARAnchorSerializable>? anchorSubsystemData;
        public AnchorSubsystemMethodsResponse? anchorSubsystemMethodsResponse;
        public MeshingDataPlayer? meshingData;
        #if (UNITY_IOS || UNITY_EDITOR) && ARKIT_INSTALLED
            public WorldMapData? worldMapData;
        #endif
        #if ARFOUNDATION_4_0_2_OR_NEWER
            [CanBeNull] public OcclusionData occlusionData;
            public HumanBodyData? humanBodyData;
            public ObjectTrackingData? objectTrackingData;
        #endif
        [CanBeNull] public PackageVersionData[] companionAppPackages;

        public void Send() {
            if (Sender.isConnected) {
                Connection.senderConnection.Send(this);
            } else {
                Debug.LogError("skip sending message because !Sender.isConnected");
            }
        }
    }


    public enum PlayerToEditorMessageType {
        None,
        SessionReady
    }


    [Serializable]
    public class EditorToPlayerMessage {
        public EditorToPlayerMessageType messageType;
        public PlaneDetectionMode? planeDetectionMode;
        public Guid? requestGuid;
        [CanBeNull] public ImageLibrarySerializableContainer imageLibrary;
        [CanBeNull] public XRReferenceImageSerializable imageToAdd;
        public SessionOriginData? sessionOriginData;
        public bool? enablePlaneSubsystem;
        public bool? enableDepthSubsystem;
        public bool? enableFaceSubsystem;
        public AnchorDataEditor? anchorsData;
        public int? requestedLightEstimation;
        public MeshingDataEditor? meshingData;
        #if ARFOUNDATION_4_0_OR_NEWER
            public Feature? requestedCamera;
            public Feature? trackingMode;
        #endif
        #if ARFOUNDATION_4_0_2_OR_NEWER
            [CanBeNull] public OcclusionDataEditor occlusionData;
            public HumanBodyDataEditor? humanBodyData;
            public ObjectTrackingDataEditor? objectTrackingData;
        #endif
        [CanBeNull] public ARCompanionSettings settings;
        public CameraDataEditor? cameraData;
        #if (UNITY_IOS || UNITY_EDITOR) && ARKIT_INSTALLED
            public WorldMapDataEditor? worldMapData;
        #endif
        [CanBeNull] public PackageVersionData[] editorPackages;
        public EditorViewData? editorViewData;
    }


    public enum EditorToPlayerMessageType {
        None,
        Init,
        ResumeSession,
        PauseSession,
        ResetSession,
        DestroySession,
        DeinitializeLoader,
        InitializeLoader,
        DisconnectedFromEditor,
        SceneUnloaded
    }


    public static class EditorToPlayerMessageTypeExtensions {
        public static bool IsStop(this EditorToPlayerMessageType _) {
            switch (_) {
                case EditorToPlayerMessageType.DestroySession:
                    return true;
            }

            return false;
        }
    }
    
    
    public interface ISerializableTrackable<out V> {
        TrackableId trackableId { get; }
        V Value { get; }
    }

    
    public interface IReceiver {
        void Receive([NotNull] PlayerToEditorMessage data);
    }

    
    [Serializable]
    public struct TrackableChangesData<T> {
        // removed can be replaced with TrackableID[]
        public T[] added, updated, removed;

        public override string ToString() {
            return $"TrackableChangesData<{nameof(T)}>, added: {added.Length}, updated: {updated.Length}, removed: {removed.Length}";
        }
    }
}
