#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ARFoundationRemote.Editor;
using ARFoundationRemote.Runtime;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.RuntimeEditor {
    [InitializeOnLoad]
    public class Receiver: MonoBehaviour {
        ARemoteReceiverState state;
        static List<IReceiver> receivers { get; } = new List<IReceiver>();
        static Receiver instance;


        static Receiver() {
            logDestruction("static Receiver()");
            // Session subsystem starts last in Unity 2019.2 so we create Receiver after all other subsystems 
            SessionSubsystem.SetOnSessionStartDelegate(CreateInstanceIfNone);
            
            // XRMeshSubsystemRemote.SetDelegate before ARMeshManager.OnEnable()
            Assert.IsFalse(receivers.OfType<MeshSubsystemReceiver>().Any());
            var meshReceiver = new MeshSubsystemReceiver();
            XRMeshSubsystemRemote.SetDelegate(meshReceiver);
            receivers.Add(meshReceiver);
        }

        static void CreateInstanceIfNone() {
            if (instance == null) {
                logDestruction("Receiver.CreateInstanceIfNone()");
                Assert.IsTrue(FindObjectsOfType<Receiver>().Length == 0);
                Assert.IsTrue(Application.isPlaying);
                if (isQuitting) {
                    throw new Exception($"{Constants.packageName}: please disable the 'Project Settings/Editor/Enter Play Mode Options (Experimental)' or enable the 'Reload Domain' setting.\n");
                }
                
                var type = typeof(Receiver);
                var go = new GameObject {
                    name = type.Namespace + "." + type.Name,
                    tag = "EditorOnly"
                };

                DontDestroyOnLoad(go);
                instance = go.AddComponent<Receiver>();
            }
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void afterSceneLoad() {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += scene => {
                var receiverConnection = Connection.receiverConnection;
                if (receiverConnection == null || !receiverConnection.isConnected) {
                    return;
                }
            
                // scene reload is needed to fix these bugs in AR Foundation (can be reproduced in AR Foundation Samples repo):
                // after scaling the ARSesstionOrigin (Scale scene), ARAnchorManager will throw errors in OnTrackablesParentTransformChanged (trackable == null)
                logDestruction($"SceneManager.sceneUnloaded {scene.name}");
                new EditorToPlayerMessage {
                    messageType = EditorToPlayerMessageType.SceneUnloaded
                }.BlockUntilReceive();
            };
        }
        
        void Awake() {
            logDestruction("Receiver.Awake()");
            Assert.IsTrue(Application.isPlaying);
            Assert.IsFalse(isQuitting);
            Assert.AreEqual("EditorOnly", gameObject.tag);
            addSubsystemToReceivers<XRPlaneSubsystem>();
            receivers.Add(new CameraPoseReceiver());
            addSubsystemToReceivers<XRDepthSubsystem>();
            XRGeneralSettings.Instance.Manager.InitializeLoaderSyncIfNotInitialized();
            addSubsystemToReceivers<XRFaceSubsystem>();
            if (TouchInputReceiver.Instance == null) {
                var touchInputReceiver = new GameObject(nameof(TouchInputReceiver)).AddComponent<TouchInputReceiver>();
                DontDestroyOnLoad(touchInputReceiver.gameObject);
                receivers.Add(touchInputReceiver);
            }
            addSubsystemToReceivers<XRImageTrackingSubsystem>();
            addSubsystemToReceivers<XRCameraSubsystem>();
            createAndAddReceiver<OriginDataSender>();
            addSubsystemToReceivers<XRDepthSubsystem>();
            addSubsystemToReceivers<XRSessionSubsystem>();
            #if ARFOUNDATION_4_0_OR_NEWER
                addSubsystemToReceivers<XROcclusionSubsystem>();
                addSubsystemToReceivers<XRHumanBodySubsystem>();
                addSubsystemToReceivers<XRObjectTrackingSubsystem>();
            #endif
            addSubsystemToReceivers<XRAnchorSubsystem>();
            receivers.Add(gameObject.AddComponent<CaptureDepthAndColorTexture>());
            Connection.receiverConnection.Register(playerMessageReceived);
            Connection.receiverConnection.RegisterDisconnection(onPlayerDisconnected);
            StartCoroutine(initCor());
            StartCoroutine(sendPackages());
        }

        void addSubsystemToReceivers<T>() where T : class, ISubsystem {
            var generalSettings = XRGeneralSettings.Instance;
            Assert.IsNotNull(generalSettings);
            var manager = generalSettings.Manager;
            Assert.IsNotNull(manager);
            var activeLoader = manager.activeLoader;
            if (Defines.isUnity2019_2 && activeLoader == null) {
                throw new System.Exception($"{Constants.packageName}: please install \"com.unity.xr.management\": \"3.0.4-preview.1\"");
            }

            Assert.IsNotNull(activeLoader);
            var subsystem = activeLoader.GetLoadedSubsystem<T>();
            Assert.IsNotNull(subsystem);
            var receiver = subsystem as IReceiver;
            Assert.IsNotNull(receiver);
            receivers.Add(receiver);
        }
       
        void OnApplicationQuit() {
            logDestruction("OnApplicationQuit; isQuitting = true;");
            isQuitting = true;
        }
        
        [Conditional("_")]
        static void logDestruction(string s) {
            Debug.Log(s);
        }
        
        void createAndAddReceiver<T>() where T : Component, IReceiver {
            var existing = FindObjectOfType<T>();
            Assert.IsNull(existing);
            var receiver = gameObject.AddComponent<T>();
            receivers.Add(receiver);
        }

        IEnumerator initCor() {
            setState(ARemoteReceiverState.WaitingForConnectedPlayer);
            while (!Connection.receiverConnection.isConnected) {
                yield return null;
            }

            setState(ARemoteReceiverState.WaitingForPlayerResponse);
            SendMessageToRemote(EditorToPlayerMessageType.Init);
        }
        
        IEnumerator sendPackages() {
            var listRequest = Client.List(true, true);
            while (!listRequest.IsCompleted) {
                yield return null;
            }
                
            Assert.AreEqual(StatusCode.Success, listRequest.Status);
            new EditorToPlayerMessage {
                editorPackages = PackageVersionData.Create(listRequest.Result)
            }.Send();
        }

        public static bool isQuitting { get; private set; }

        void OnDestroy() {
            logDestruction($"{nameof(Receiver)} OnDestroy()");
            var receiverConnection = Connection.receiverConnection;
            if (receiverConnection != null) {
                receiverConnection.Unregister(playerMessageReceived);
                receiverConnection.UnregisterDisconnection(onPlayerDisconnected);    
            }
        }

        static void SendMessageToRemote(EditorToPlayerMessageType messageType) {
            new EditorToPlayerMessage {messageType = messageType}.Send();
        }

        void onPlayerDisconnected(int _) {
            if (state >= ARemoteReceiverState.WaitingForPlayerResponse) {
                Debug.LogError($"{Constants.packageName}: Editor lost connection with AR Companion app. Please restart Editor scene.");
            }
        }

        void setState(ARemoteReceiverState _state) {
            state = _state;
        }

        void playerMessageReceived(PlayerToEditorMessage data) {
            Assert.IsNotNull(instance);
            
            if (data.messageType == PlayerToEditorMessageType.SessionReady) {
                setState(ARemoteReceiverState.Running);
                ReviewRequest.RecordUsage();
            }

            if (Global.IsInitialized) {
                foreach (var receiver in receivers) {
                    receiver.Receive(data);
                }
            }

            if (data.companionAppPackages != null) {
                PackageVersionData.CheckVersions(data.companionAppPackages, Settings.Instance.packages);
            }
        }
    }
}
#endif
