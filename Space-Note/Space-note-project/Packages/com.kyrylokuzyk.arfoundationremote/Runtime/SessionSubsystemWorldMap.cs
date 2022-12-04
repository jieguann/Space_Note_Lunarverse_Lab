#if UNITY_IOS && UNITY_EDITOR && ARKIT_INSTALLED
using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine.Assertions;
using UnityEngine.XR.ARKit;


namespace ARFoundationRemote.Runtime {
    public partial class SessionSubsystem {
        // todo global issue with receivers
        // after calling activeLoader.Deinitialize(), all subsystems will be recreated
        // but Receiver will pass received data to old subsystems
        // that's why I store all received data in static fields
        // I can't just never destroy subsystems in ARFoundationRemoteLoader.Deinitialize() because Destroy() will not be called
        static ARWorldMappingStatus _worldMappingStatus;


        public ARWorldMapRequest GetARWorldMapAsync() {
            return ARWorldMapRequest.GetARWorldMapAsync();
        }

        /// WARNING: calling <see cref="ARKitSessionSubsystem.GetARWorldMapAsync(Action{ARWorldMapRequestStatus, ARWorldMap})"/> will crash on real devices.
        /// See WorldMapSender.getWorldMapCallback(int) for details about the crash.
        /// The remote plugin uses <see cref="ARKitSessionSubsystem.GetARWorldMapAsync()"/> version to prevent the crash.
        [Obsolete]
        public void GetARWorldMapAsync(Action<ARWorldMapRequestStatus, ARWorldMapRemote> onComplete) {
            DontDestroyOnLoadSingleton.Instance.StartCoroutine(GetARWorldMapAsyncCor(onComplete));
        }

        IEnumerator GetARWorldMapAsyncCor(Action<ARWorldMapRequestStatus, ARWorldMapRemote> onComplete) {
            var request = GetARWorldMapAsync();
            while (!request.status.IsDone()) {
                yield return null;
            }

            var status = request.status;
            onComplete(request.status, status == ARWorldMapRequestStatus.Success ? request.GetWorldMap() : new ARWorldMapRemote());
        }
        
        public void ApplyWorldMap(ARWorldMapRemote worldMap) {
            new EditorToPlayerMessage {
                worldMapData = new WorldMapDataEditor {
                    applyWorldMapNativeHandle = worldMap.nativeHandle
                }
            }.Send();
        }

        public static bool worldMapSupported => true;
        public ARWorldMappingStatus worldMappingStatus => _worldMappingStatus;
        public static bool supportsCollaboration => false;
        public static bool coachingOverlaySupported => false;

        internal static void Send(WorldMapDataEditor worldMapDataEditor) {
            new EditorToPlayerMessage {
                worldMapData = worldMapDataEditor
            }.Send();
        }

        void receiveWorldMap(PlayerToEditorMessage data) {
            var worldMapData = data.worldMapData;
            if (!worldMapData.HasValue) {
                return;
            }
            
            var maybeWorldMapResponse = worldMapData.Value.GetARWorldMapAsyncResponse;
            if (maybeWorldMapResponse.HasValue) {
                var worldMapResponse = maybeWorldMapResponse.Value;
                ARWorldMapRequest.requests.Add(worldMapResponse.requestId, worldMapResponse);
            }

            var mappingStatus = worldMapData.Value.worldMappingStatus;
            if (mappingStatus.HasValue) {
                _worldMappingStatus = mappingStatus.Value;
            }
        }
    }

    
    public readonly struct ARWorldMapRequest : IDisposable {
        static int currentId;
        public static readonly Dictionary<int, GetARWorldMapAsyncResponse> requests = new Dictionary<int, GetARWorldMapAsyncResponse>();
        
        readonly int id;


        ARWorldMapRequest(int id) {
            this.id = id;
        }
        
        public ARWorldMapRequestStatus status => requests.TryGetValue(id, out var request) ? request.status : ARWorldMapRequestStatus.Pending;

        public ARWorldMapRemote GetWorldMap() {
            Assert.AreEqual(ARWorldMapRequestStatus.Success, status, "Check if status is Success before calling GetWorldMap()");
            var request = requests[id];
            Assert.IsTrue(request.nativeHandle.HasValue);
            var serializedBytes = request.serializedBytes;
            Assert.IsNotNull(serializedBytes);
            return new ARWorldMapRemote(request.nativeHandle.Value, serializedBytes, request.isValid);
        }

        public void Dispose() {
        }

        public static ARWorldMapRequest GetARWorldMapAsync() {
            var requestId = currentId;
            var request = new ARWorldMapRequest(requestId);
            currentId++;

            SessionSubsystem.Send(new WorldMapDataEditor {
                getWorldMapRequest = requestId 
            });

            return request;
        }
    }
    
    
    public readonly struct ARWorldMapRemote : IDisposable {
        internal readonly int nativeHandle;
        [NotNull] readonly byte[] serializedBytes;


        internal ARWorldMapRemote(int nativeHandle, [NotNull] byte[] serializedBytes, bool isValid) {
            this.nativeHandle = nativeHandle;
            this.serializedBytes = serializedBytes;
            valid = isValid;
        }
        
        public bool valid { get; }

        public NativeArray<byte> Serialize(Allocator allocator) {
            return new NativeArray<byte>(serializedBytes, allocator);
        }

        public static bool TryDeserialize(NativeArray<byte> serializedWorldMap, out ARWorldMapRemote worldMap) {
            var response = Connection.receiverConnection.BlockUntilReceive(new EditorToPlayerMessage {
                worldMapData = new WorldMapDataEditor {
                    serializedWorldMap = serializedWorldMap.ToArray(),
                }
            });

            Assert.IsTrue(response.worldMapData.HasValue);
            var nativeHandle = response.worldMapData.Value.tryDeserializeMapHandleResponse;
            if (nativeHandle.HasValue) {
                worldMap = new ARWorldMapRemote(nativeHandle.Value, serializedWorldMap.ToArray(), true);
                return true;
            } else {
                worldMap = default;
                return false;
            }
        }

        public void Dispose() {
        }
    }
}
#endif
