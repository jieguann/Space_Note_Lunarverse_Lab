using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;


namespace ARFoundationRemote.Runtime {
    public static class Connection {
        [CanBeNull] static TelepathySenderConnection _senderConnection;
        public static TelepathySenderConnection senderConnection {
            get {
                #if UNITY_EDITOR
                    throw new Exception("senderConnection getter in Editor!");
                #endif

                #pragma warning disable 162
                if (_senderConnection == null) {
                    _senderConnection = TelepathySenderConnection.Create();
                }

                return _senderConnection;
                #pragma warning restore
            }
        }

        #if UNITY_EDITOR
            [CanBeNull] static RuntimeEditor.TelepathyReceiverConnection _receiverConnection;
            public static RuntimeEditor.TelepathyReceiverConnection receiverConnection {
                get {
                    if (_receiverConnection == null && Application.isPlaying) {
                        _receiverConnection = RuntimeEditor.TelepathyReceiverConnection.TryCreate();
                    }
                
                    return _receiverConnection;
                }
            }
        #endif

        public static bool CanSendNonCriticalMessage {
            get {
                #if UNITY_EDITOR
                    return receiverConnection.CanSendNonCriticalMessage;
                #endif
                
                #pragma warning disable 162
                return senderConnection.CanSendNonCriticalMessage;
                #pragma warning restore 162
            }
        }
    }

    public interface IConnection<IncomingMessageType, OutgoingMessageType>{
        bool isConnected { get; }
        void Register(Action<IncomingMessageType> callback);
        void Send([NotNull] OutgoingMessageType msg);
        void RegisterDisconnection(UnityAction<int> callback);
        void UnregisterDisconnection(UnityAction<int> callback);
    }
}
