#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using UnityEngine;
using Telepathy;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;
using EventType = Telepathy.EventType;


namespace ARFoundationRemote.RuntimeEditor {
    public class TelepathyReceiverConnection : TelepathyConnection<PlayerToEditorMessage, EditorToPlayerMessage> {
        readonly Client client = new Client();
        static bool isDestroyed;


        [CanBeNull]
        public static TelepathyReceiverConnection TryCreate() {
            if (isDestroyed) {
                return null;
            }
            
            var gameObject = new GameObject {name = nameof(TelepathyReceiverConnection)};
            DontDestroyOnLoad(gameObject);
            return gameObject.AddComponent<TelepathyReceiverConnection>(); 
        }

        IEnumerator Start() {
            while (!Global.IsInitialized) {
                yield return null;
            }
            
            var ip = Settings.Instance.ARCompanionAppIP;
            if (!IPAddress.TryParse(ip, out _)) {
                Debug.LogError("Please enter correct AR Companion app IP in Assets/Plugins/ARFoundationRemoteInstaller/Resources/Settings");
                yield break;
            }
            
            client.MaxMessageSize = maxMessageSize;
            client.Connect(ip, port);
            
            while (client.Connecting) {
                yield return null;
            }
            
            if (Settings.Instance.logStartupErrors && !isConnected) {
                Debug.LogError($"{Constants.packageName}: connection to AR Companion app failed. Please check that:\n" +
                               "1. Unity Editor and AR Device are on the same Wi-Fi network.\n" +
                               "2. AR Companion is running and device is unlocked.\n" +
                               "3. The IP is correct in Assets/Plugins/ARFoundationRemoteInstaller/Resources/Settings\n\n" +
                               "If the connection is still failing, please try to configure your AR Device's Wi-Fi to have a static IP.\n" +
                               "iOS: https://www.mobi-pos.com/web/guide/settings/static-ip-configuration\n" +
                               "Android: https://service.uoregon.edu/TDClient/2030/Portal/KB/ArticleDet?ID=33742\n");
            }
        }

        protected override Common getCommon() {
            return client;
        }

        protected override bool isConnected_internal => client.Connected;

        protected override void send(byte[] payload) {
            Assert.IsTrue(isConnected);
            send_internal(payload);
        }

        void send_internal(byte[] payload) {
            client.Send(payload);
        }
        
        public PlayerToEditorMessage BlockUntilReceive(EditorToPlayerMessage msg) {
            Assert.IsFalse(msg.requestGuid.HasValue);
            var guid = Guid.NewGuid();
            msg.requestGuid = guid;
            msg.Send();
            return Connection.receiverConnection.BlockUntilReceive(guid);
        }

        PlayerToEditorMessage BlockUntilReceive(Guid guid) {
            var stopwatch = Stopwatch.StartNew();
            while (true) {
                if (!isConnected) {
                    // prevent Unity freeze when blocking method is called every frame
                    throw new Exception($"{Constants.packageName}: please don't call blocking methods while AR Companion is not connected");
                }
                
                const double timeoutInSeconds = 10;
                if (stopwatch.Elapsed > TimeSpan.FromSeconds(timeoutInSeconds)) {
                    throw new Exception($"{Constants.packageName}: {nameof(BlockUntilReceive)} timeout.");
                }

                foreach (var msg in incomingMessages) {
                    if (msg.eventType == EventType.Data) {
                        var playerMessage = msg.message;
                        if (playerMessage.responseGuid == guid) {
                            // Debug.Log($"received, elapsed time: {stopwatch.Elapsed.Milliseconds}");
                            return playerMessage;
                        }
                    }
                }
            }
        }

        protected override void onDestroyInternal() {
            client.Disconnect();
            isDestroyed = true;
        }
        
        public bool Connecting => client.Connecting;
    }
}
#endif
