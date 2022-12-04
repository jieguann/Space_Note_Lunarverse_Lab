using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.Assertions;


namespace ARFoundationRemote {
    public static class Input {
        #if UNITY_EDITOR
            static bool hasMouseTouch => mouseTouch.HasValue;
            static Touch? mouseTouch => SimulateTouchWithMouse.Instance.FakeTouch;
        #endif

        public static bool GetButton(string buttonName) {
            return UnityEngine.Input.GetButton(buttonName);
        }

        public static bool GetButtonDown(string buttonName) {
            return UnityEngine.Input.GetButtonDown(buttonName);
        }

        public static bool GetButtonUp(string buttonName) {
            return UnityEngine.Input.GetButtonUp(buttonName);
        }

        public static bool GetMouseButton(int button) {
            return UnityEngine.Input.GetMouseButton(button);
        }

        public static bool GetMouseButtonDown(int button) {
            return UnityEngine.Input.GetMouseButtonDown(button);
        }

        public static bool GetMouseButtonUp(int button) {
            return UnityEngine.Input.GetMouseButtonUp(button);
        }

        public static Vector3 mousePosition => UnityEngine.Input.mousePosition;

        public static int touchCount {
            get {
                #if UNITY_EDITOR
                    if (UnityEditor.EditorApplication.isRemoteConnected) {
                        return UnityEngine.Input.touchCount;
                    } else if (hasMouseTouch) {
                        return mouseTouch.HasValue ? 1 : 0;
                    } else {
                        return TouchInputReceiver.Touches.Length;
                    }
                #else 
                    return UnityEngine.Input.touchCount;
                #endif
            }
        }


        public static Touch GetTouch(int index) {
            #if UNITY_EDITOR
                if (UnityEditor.EditorApplication.isRemoteConnected) {
                    return UnityEngine.Input.GetTouch(index);                
                } else if (hasMouseTouch) {
                    Assert.IsTrue(index == 0);
                    return mouseTouch.Value;
                } else {
                    return TouchInputReceiver.Touches[index];
                } 
            #else 
                return UnityEngine.Input.GetTouch(index);                
            #endif
        }

        public static Touch[] touches {
            get {
                #if UNITY_EDITOR
                    if (UnityEditor.EditorApplication.isRemoteConnected) {
                        return UnityEngine.Input.touches;
                    } else if (hasMouseTouch) {
                        return new[] {mouseTouch.Value};
                    } else {
                        return TouchInputReceiver.Touches;
                    }
                #else 
                    return UnityEngine.Input.touches;
                #endif
            }
        }
    }
}
