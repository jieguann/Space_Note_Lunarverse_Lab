using System.Collections;
using System.Threading;
using UnityEngine;
using Telepathy;


namespace ARFoundationRemote.Runtime {
    public class TelepathySenderConnection : TelepathyConnection<EditorToPlayerMessage, PlayerToEditorMessage> {
        readonly Server server = new Server();


        /// Sender connection should be persistent to support scene change in Editor
        public static TelepathySenderConnection Create() {
            var gameObject = new GameObject {name = nameof(TelepathySenderConnection)};
            DontDestroyOnLoad(gameObject);
            return gameObject.AddComponent<TelepathySenderConnection>();
        }

        IEnumerator Start() {
            server.MaxMessageSize = maxMessageSize;
            while (true) {
                server.Start(port);
                yield return new WaitForSeconds(1);
                
                if (isActive) {
                    yield break;
                }
            }
        }
        
        protected override Common getCommon() {
            return server;
        }

        protected override bool isConnected_internal => Interlocked.CompareExchange(ref connectionId, 0, 0) != -1;
        
        protected override void send(byte[] payload) {
            if (isConnected) {
                server.Send(connectionId, payload);
            }
        }

        public bool isActive => server.Active;
    }
}
