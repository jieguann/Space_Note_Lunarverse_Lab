using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine.Assertions;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public class TrackableChangesReceiverBase<T, V> where T : ISerializableTrackable<V> where V : struct {
        public readonly Dictionary<TrackableId, T>
            added = new Dictionary<TrackableId, T>(),
            updated = new Dictionary<TrackableId, T>(),
            removed = new Dictionary<TrackableId, T>();

        public readonly Dictionary<TrackableId, T> all = new Dictionary<TrackableId, T>();

        readonly bool debug;
        

        public TrackableChangesReceiverBase(bool debug = false) {
            this.debug = debug;
        }
        
        public void Receive(TrackableChangesData<T>? maybeData) {
            if (maybeData.HasValue) {
                var data = maybeData.Value;
                Receive(data.added, data.updated, data.removed);
            }
        }

        public void Receive([NotNull] T[] dataAdded, [NotNull] T[] dataUpdated, [NotNull] T[] dataRemoved) {
            #if UNITY_EDITOR
                Assert.IsTrue(Global.IsInitialized);
            #endif
            
            foreach (var addedItem in dataAdded) {
                var id = addedItem.trackableId;
                if (!all.ContainsKey(id)) {
                    log($"added from added {id}");
                    addToAdded(id, addedItem);
                }
            }

            foreach (var updatedItem in dataUpdated) {
                var id = updatedItem.trackableId;
                if (!all.ContainsKey(id)) {
                    log($"added from updated {id}");
                    addToAdded(id, updatedItem);
                    continue;
                }

                if (!added.ContainsKey(id)) {
                    updated[id] = updatedItem;
                    Assert.IsTrue(all.ContainsKey(id));
                    all[id] = updatedItem; // save the most recent trackable (update plane boundaries, etc.)
                    // log($"updated {id}");
                }
            }

            foreach (var rem in dataRemoved) {
                var id = rem.trackableId;
                var removedFromAdded = added.Remove(id);
                if (removedFromAdded) {
                    log($"removed from added {id}");
                }
                
                var removedFromAll = all.Remove(id);
                if (removedFromAll) {
                    log($"removed from all {id}");
                }
                
                if (updated.Remove(id)) {
                    log($"removed from updated {id}");
                }

                if (removedFromAll && !removedFromAdded) {
                    removed.Add(id, rem);
                    log($"removed {id}");
                }
            }
        }

        void addToAdded(TrackableId id, T item) {
            added.Add(id, item);
            all.Add(id, item);
        }

        [Conditional("_")]
        protected void log(string msg) {
            if (debug) {
                Debug.Log( $"{typeof(V).Name}: " + msg);
            }
        }

        public void Reset() {
            OnAfterGetChanges();
            all.Clear();
        }

        public void OnAfterGetChanges() {
            added.Clear();
            updated.Clear();
            removed.Clear();
        }
    }

    public class TrackableChangesReceiver<T, V> : TrackableChangesReceiverBase<T, V> where V : struct, ITrackable where T : ISerializableTrackable<V> {
        public TrackableChangesReceiver(bool debug = false) : base(debug) {
        }
        
        public TrackableChanges<V> GetChanges(Allocator allocator) {
            // log("GetChanges");
            Assert.IsTrue(updated.Keys.All(all.ContainsKey));
            Assert.IsFalse(removed.Keys.Any(all.ContainsKey));
            var result = TrackableChanges<V>.CopyFrom(
                new NativeArray<V>(added.Values.Select(_ => _.Value).ToArray(), allocator),
                new NativeArray<V>(updated.Values.Select(_ => _.Value).ToArray(), allocator),
                new NativeArray<TrackableId>(removed.Values.Select(_ => _.trackableId).ToArray(), allocator),
                allocator
            );

            OnAfterGetChanges();
            return result;
        }
    }
}
