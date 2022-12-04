using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTAssets.IngameLogsViewer
{
    /*
     * This class is responsible for generate fake logs for test.
     */ 

    public class LogGenerator : MonoBehaviour
    {
        float time;
        float time2;

        private void Start()
        {
            time = 5;
        }

        private void Update()
        {
            time += Time.deltaTime;
            time2 += Time.deltaTime;
            if(time >= 5)
            {
                Debug.Log("Test log.");
                Debug.LogWarning("Test warning.");
                Debug.LogError("Test error.");
                time = 0;
            }
            if (time2 >= 5)
            {
                Debug.Log("Test log delayed.");
                Debug.LogWarning("Test warning delayed.");
                Debug.LogError("Test error delayed.");
                time2 = 0;
            }
        }
    }

}