#if UNITY_EDITOR
using ARFoundationRemote.Runtime;


public static class EditorToPlayerMessageExtensions {
    /// Can't send in ARFoundationRemoteLoader.Initialize() because in Unity 2019.2 it's called after subsystems start
    static bool didSendSettings;

    
    public static void Send(this EditorToPlayerMessage msg) {
            var receiverConnection = Connection.receiverConnection;
            if (receiverConnection != null) {
                if (!didSendSettings) {
                    didSendSettings = true;
                    new EditorToPlayerMessage {
                        settings = Settings.Instance.arCompanionSettings
                    }.Send();
                }
                
                receiverConnection.Send(msg);
            }
    }

    public static void BlockUntilReceive(this EditorToPlayerMessage msg) {
        Connection.receiverConnection.BlockUntilReceive(msg);
    }
}
#endif
