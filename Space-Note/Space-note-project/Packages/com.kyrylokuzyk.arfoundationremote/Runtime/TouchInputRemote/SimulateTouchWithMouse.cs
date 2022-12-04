#if UNITY_EDITOR
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    internal class SimulateTouchWithMouse {
        public static SimulateTouchWithMouse Instance { get; } = new SimulateTouchWithMouse();
        float lastUpdateTime = -1;
        Vector3 prevMousePos;
        Touch? fakeTouch;


        public Touch? FakeTouch {
            get {
                Update();
                return fakeTouch;
            }
        }

        public void Update() {
            if (!Global.IsInitialized) {
                return;
            }
            
            #if UNITY_2019_3_OR_NEWER && !ENABLE_LEGACY_INPUT_MANAGER
                Debug.LogError($"{Constants.packageName}: 'Input System Package (New)' is not supported. {Constants.disableStartupErrorsMessage}");
                return;
            #endif
            
            var currentTime = Time.time;
            if (currentTime != lastUpdateTime) {
                lastUpdateTime = currentTime;

                var curMousePos = UnityEngine.Input.mousePosition;
                var delta = curMousePos - prevMousePos;
                prevMousePos = curMousePos;

                fakeTouch = createTouch(getPhase(delta), delta);
            }
        }

        static TouchPhase? getPhase(Vector3 delta) {
            if (Settings.Instance.inputSimulationType != InputSimulationType.SimulateMouseWithTouches) {
                if (UnityEngine.Input.GetMouseButtonDown(0)) {
                    return TouchPhase.Began;
                } else if (UnityEngine.Input.GetMouseButton(0)) {
                    return delta.sqrMagnitude < 0.01f ? TouchPhase.Stationary : TouchPhase.Moved;
                } else if (UnityEngine.Input.GetMouseButtonUp(0)) {
                    return TouchPhase.Ended;
                }
            }

            return null;
        }

        static Touch? createTouch(TouchPhase? phase, Vector3 delta) {
            if (!phase.HasValue) {
                return null;
            }

            var curMousePos = TouchInputReceiver.Instance.mousePosFromOnGUIEvent;
            var touch = new Touch {
                phase = phase.Value,
                type = TouchType.Indirect,
                position = curMousePos,
                rawPosition = curMousePos,
                fingerId = 0,
                tapCount = 1,
                deltaTime = Time.deltaTime,
                deltaPosition = delta
            };
            LogTouch(touch, "fake");
            return touch;
        }

        [Conditional("_")]
        public static void LogTouch(Touch t, string s) {
            Debug.Log(s + t.fingerId + t.phase + t.position);
        }
    }
}
#endif
