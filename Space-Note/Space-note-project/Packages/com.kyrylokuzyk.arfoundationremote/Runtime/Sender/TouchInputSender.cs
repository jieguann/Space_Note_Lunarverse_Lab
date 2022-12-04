using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;


namespace ARFoundationRemote.Runtime {
    public class TouchInputSender : MonoBehaviour {
        bool needSendEmpty = false;
        
        
        void Update() {
            #if UNITY_2019_3_OR_NEWER && !ENABLE_LEGACY_INPUT_MANAGER
                return;
            #endif
            
            if (!Sender.isConnected) {
                return;
            }
            
            var touches = UnityEngine.Input.touches;
            var touchesPresent = touches.Any();
            if (touchesPresent || needSendEmpty) {
                var payload = touches.Select(TouchSerializable.Create).ToArray();
                new PlayerToEditorMessage { touches = payload }
                    .Send();

                TouchSerializable.log(payload, "sent");
                if (!touchesPresent) {
                    //print("needSendEmpty = false");
                    needSendEmpty = false;
                }
            }

            if (touchesPresent && !needSendEmpty) {
                //print("needSendEmpty = true");
                needSendEmpty = true;
            } 
        }
    }

    [Serializable]
    public class TouchSerializable {
        int fingerId;
        TouchPhase phase;
        Vector2Serializable position;
        float deltaTime;
        TouchType type;
        float radius;
        float pressure;
        int tapCount;
        Vector2Serializable rawPosition;
        float azimuthAngle;
        float altitudeAngle;
        Vector2Serializable deltaPosition;
        float radiusVariance;
        float maximumPossiblePressure;


        public static TouchSerializable CreateDummy() {
            return new TouchSerializable { fingerId = Random.Range(1, 100)};
        }

        public static TouchSerializable Create(Touch t) {
            return new TouchSerializable {
                fingerId = t.fingerId,
                phase = t.phase,
                position = Vector2Serializable.Create(normalizeByScreenSize(t.position)),
                type = t.type,
                radius = t.radius,
                pressure = t.pressure,
                tapCount = t.tapCount,
                deltaTime = t.deltaTime,
                rawPosition = Vector2Serializable.Create(normalizeByScreenSize(t.rawPosition)),
                azimuthAngle = t.azimuthAngle,
                altitudeAngle = t.altitudeAngle,
                deltaPosition = Vector2Serializable.Create(normalizeByScreenSize(t.deltaPosition)),
                radiusVariance = t.radiusVariance,
                maximumPossiblePressure = t.maximumPossiblePressure
            };
        }

        public Touch Value => new Touch {
            fingerId = fingerId,
            phase = phase,
            position = fromNormalizedToScreenPos(position.Value), 
            deltaTime = deltaTime, 
            type = type,
            radius = radius,
            pressure = pressure,
            tapCount = tapCount,
            rawPosition = fromNormalizedToScreenPos(rawPosition.Value),
            azimuthAngle = azimuthAngle,
            altitudeAngle = altitudeAngle,
            deltaPosition = fromNormalizedToScreenPos(deltaPosition.Value),
            radiusVariance = radiusVariance,
            maximumPossiblePressure = maximumPossiblePressure
        };

        public override string ToString() {
            return fingerId.ToString() + phase + position.Value;
        }

        static Vector2 normalizeByScreenSize(Vector2 v) {
            return new Vector2(v.x / Screen.width, v.y / Screen.height);
        }

        static Vector2 fromNormalizedToScreenPos(Vector2 v) {
            return new Vector2(v.x * Screen.width, v.y * Screen.height);            
        }

        public static void log(TouchSerializable[] array, string msg) {
            /*var str = msg + ": " + array.Length + "\n";
            foreach (var touchSerializable in array) {
                str += touchSerializable.phase + "\n";
            }
            
            Debug.Log(str);*/
        }
    }
}
