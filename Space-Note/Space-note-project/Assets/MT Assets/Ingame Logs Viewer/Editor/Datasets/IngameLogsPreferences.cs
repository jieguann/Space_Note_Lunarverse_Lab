using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTAssets.IngameLogsViewer.Editor
{
    /*
     * This script is the Dataset of the scriptable object "Preferences". This script saves Ingame Logs Viewer preferences.
     */

    public class IngameLogsPreferences : ScriptableObject
    {
        public string projectName;
        public bool ilvInstalledInFirstScene;
    }
}