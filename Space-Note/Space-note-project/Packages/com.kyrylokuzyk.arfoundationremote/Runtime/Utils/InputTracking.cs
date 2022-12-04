using System.Collections.Generic;
using UnityEngine.XR;


namespace ARFoundationRemote.Runtime {
    public static class InputTracking {
        static XRNodeState centerEyePoseState;
        
        
        public static void SetCenterEyeNodeState(XRNodeState state) {
            centerEyePoseState = state;
        }

        public static void GetNodeStates(List<XRNodeState> states) {
            #if UNITY_EDITOR
                states.Clear();
                states.Add(centerEyePoseState);
                return;
            #endif
            
            #pragma warning disable 162
                UnityEngine.XR.InputTracking.GetNodeStates(states);
            #pragma warning restore
        }
    }
}
