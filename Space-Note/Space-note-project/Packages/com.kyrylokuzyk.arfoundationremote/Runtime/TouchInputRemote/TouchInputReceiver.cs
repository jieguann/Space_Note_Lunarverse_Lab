#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;
#if UNITY_2019_2
    using UnityEngine.Experimental.LowLevel;
#else
    using UnityEngine.LowLevel;
#endif


namespace ARFoundationRemote.Runtime {
    public class TouchInputReceiver: MonoBehaviour, IReceiver {
        public static TouchInputReceiver Instance { get; private set; }
        public Vector2 mousePosFromOnGUIEvent { get; private set; }
        
        static InputSimulationType inputSimulationType => Settings.Instance.inputSimulationType;
        static readonly Queue<Touch[]> touchEventsQueue = new Queue<Touch[]>();
        static Touch[] currentTouches = new Touch[0];
        static float lastUpdateTime = -1;

        public static Touch[] Touches {
            get {
                dequeueTouchesIfNewFrame();
                return currentTouches;
            }
        }

        static void dequeueTouchesIfNewFrame() {
            var currentTime = Time.time;
            if (currentTime != lastUpdateTime) {
                lastUpdateTime = currentTime;
                currentTouches = touchEventsQueue.Any() ? touchEventsQueue.Dequeue() : new Touch[0];
            }
        }

        void Awake() {
            Instance = this;
            setupInputSimulation();
        }

        static bool inputSimulationDidSetup;
        
        static void setupInputSimulation() {
            if (!Global.ShouldRegisterSubsystemDescriptor()) {
                return;
            }
            
            Assert.IsFalse(inputSimulationDidSetup);
            inputSimulationDidSetup = true;
            
            if (inputSimulationType == InputSimulationType.SimulateSingleTouchWithMouseLegacy) {
                return;
            }

            if (inputSimulationType == InputSimulationType.SimulateSingleTouchWithMouse) {
                UnityEngine.Input.simulateMouseWithTouches = false;
            }

            insertTouchSimulationUpdateIntoPlayerLoop();
        }

        static void insertTouchSimulationUpdateIntoPlayerLoop() {
            var loop = PlayerLoop.
                #if UNITY_2019_2
                    GetDefaultPlayerLoop();
                #else
                    GetCurrentPlayerLoop();
                #endif
            logPlayerLoop(loop);
            var index = Array.FindIndex(loop.subSystemList, _ => {
                var typeFullName = _.type.FullName;
                Assert.IsNotNull(typeFullName);
                return typeFullName.Contains(".PlayerLoop.EarlyUpdate");
            });
            Assert.AreNotEqual(-1, index);
            var earlyUpdateLoop = loop.subSystemList[index];
            var list = earlyUpdateLoop.subSystemList.ToList();
            list.Insert(0, new PlayerLoopSystem {type = typeof(TouchSimulationUpdate), updateDelegate = TouchSimulationUpdate.Update});
            earlyUpdateLoop.subSystemList = list.ToArray();
            loop.subSystemList[index] = earlyUpdateLoop;
            PlayerLoop.SetPlayerLoop(loop);
            logPlayerLoop(loop);
        }

        static void logPlayerLoop(PlayerLoopSystem loop) {
            /*Debug.Log("________logPlayerLoop");
            foreach (var loopSystem in loop.subSystemList) {
                Debug.Log(loopSystem.type);
                foreach (var subsystem in loopSystem.subSystemList) {
                    Debug.Log(subsystem.type);
                }
            }*/
        }
        
        void OnGUI() {
            // calling SimulateTouch is overriding Input.mouse position in Windows Unity Editor.
            // so we get mouse position from Event.current
            var mousePosition = Event.current.mousePosition;
            mousePosFromOnGUIEvent = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
        }

        struct TouchSimulationUpdate {
            public static bool enabled = true;
            
            public static void Update() {
                if (!Application.isPlaying) {
                    // PlayerLoop will still contain TouchSimulationUpdate after stopping the scene in Editor
                    return;
                }
                
                if (!enabled) {
                    return;
                }

                if (inputSimulationType == InputSimulationType.SimulateSingleTouchWithMouse && UnityEngine.Input.simulateMouseWithTouches) {
                    Debug.LogError(Constants.packageName + ": UnityEngine.Input.simulateMouseWithTouches is required to be false to be able to simulate single touch with mouse in Editor.");
                    enabled = false;
                    return;
                }
                
                SimulateTouchWithMouse.Instance.Update();
                simulate();
            }
        }

        static void simulate() {
            var touchCount = Input.touchCount;
            for (int i = 0; i < touchCount; i++) {
                var simulateTouchMethod = typeof(UnityEngine.Input).GetMethod("SimulateTouch", BindingFlags.NonPublic | BindingFlags.Static);
                if (simulateTouchMethod == null) {
                    Debug.LogError(Constants.packageName + ": Touch simulation is supported starting from Unity 2019.3. Please set Assets/Plugins/ARFoundationRemoteInstaller/Resources/Settings.InputSimulationType to SimulateSingleTouchWithMouseLegacy and add this line on top of every script that uses UnityEngine.Input:\nusing Input = ARFoundationRemote.Input;\n");
                    TouchSimulationUpdate.enabled = false;
                    return;
                }
                
                var touch = Input.GetTouch(i);
                SimulateTouchWithMouse.LogTouch(touch, "simulate");
                simulateTouchMethod.Invoke(null, new object[] { touch.fingerId, touch.position, touch.phase });
            }
        }
        
        public void Receive(PlayerToEditorMessage data) {
            var serializedTouches = data.touches;
            if (serializedTouches != null) {
                var receivedTouches = serializedTouches.Select(_ => _.Value).ToArray();
                if (touchEventsQueue.Count == 0 || hasSingleFramePhase(receivedTouches) || hasSingleFramePhase(touchEventsQueue.Peek())) {
                    touchEventsQueue.Enqueue(receivedTouches);
                } else {
                    var combined = touchEventsQueue.Dequeue().ToList();
                    foreach (var receivedTouch in receivedTouches) {
                        var i = combined.FindIndex(_ => _.fingerId == receivedTouch.fingerId);
                        if (i != -1) {
                            combined[i] = receivedTouch;
                        } else {
                            combined.Add(receivedTouch);
                        }
                    }
                    
                    touchEventsQueue.Enqueue(combined.ToArray());
                }
            }
        }

        static bool hasSingleFramePhase(IEnumerable<Touch> touches) => touches.Any(_ => isSingleFramePhase(_.phase));

        static bool isSingleFramePhase(TouchPhase phase) => phase == TouchPhase.Canceled || phase == TouchPhase.Ended || phase == TouchPhase.Began;
    }
}
#endif //UNITY_EDITOR
