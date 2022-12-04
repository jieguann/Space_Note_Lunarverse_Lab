#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MTAssets.IngameLogsViewer
{
    /*
     This class is responsible for the functioning of the "Ingame Logs Viewer"
    */
    /*
     * The Ingame Logs Viewer was developed by Marcos Tomaz in 2019.
     * Need help? Contact me (mtassets@windsoft.xyz)
     */

    [AddComponentMenu("")] //Hide this script in component menu.
    public class IngameLogsViewer : MonoBehaviour
    {
        //Private variables
        private Dictionary<LogType, List<LogContent>> storageOfLogs = new Dictionary<LogType, List<LogContent>>();
        private long lastSizeOfHeap;
        private int minutes;
        private float seconds;

        //Public variables
        [HideInInspector]
        public List<LogSlotILV> logSlots = new List<LogSlotILV>();
        [HideInInspector]
        public List<LogSlotILV> logSlotsAssert = new List<LogSlotILV>();
        [HideInInspector]
        public List<LogSlotILV> logSlotsError = new List<LogSlotILV>();
        [HideInInspector]
        public List<LogSlotILV> logSlotsException = new List<LogSlotILV>();
        [HideInInspector]
        public List<LogSlotILV> logSlotsLog = new List<LogSlotILV>();
        [HideInInspector]
        public List<LogSlotILV> logSlotsWarning = new List<LogSlotILV>();

        [Header("Configuration")]
        public bool startOpened;
        public bool hideTouchButton;
        public KeyCode keyForOpen;
        public KeyCode keyForClose;

        [Header("Icons")]
        public Sprite errorIcon;
        public Sprite warnIcon;
        public Sprite logIcon;

        [Header("UI Elements")]
        public GameObject openButton;
        public GameObject closeButton;
        public GameObject console;
        public Image showLogs;
        public Image showWarns;
        public Image showErrors;
        public Text logCounter;
        public Text errorCounter;
        public Text warnCounter;
        public Text heapMeter;
        public Text timeMeter;
        public GameObject strackTrace;
        public Text strackView;

        [Header("Colors")]
        public Color colorEnabled;
        public Color colorHidded;

        //The UI of this component
#if UNITY_EDITOR
        //Public variables of Interface
        private bool gizmosOfThisComponentIsDisabled = false;

        #region INTERFACE_CODE
        [UnityEditor.CustomEditor(typeof(IngameLogsViewer))]
        public class CustomInspector : UnityEditor.Editor
        {
            public bool DisableGizmosInSceneView(string scriptClassNameToDisable, bool isGizmosDisabled)
            {
                /*
                *  This method disables Gizmos in scene view, for this component
                */

                if (isGizmosDisabled == true)
                    return true;

                //Try to disable
                try
                {
                    //Get all data of Unity Gizmos manager window
                    var Annotation = System.Type.GetType("UnityEditor.Annotation, UnityEditor");
                    var ClassId = Annotation.GetField("classID");
                    var ScriptClass = Annotation.GetField("scriptClass");
                    var Flags = Annotation.GetField("flags");
                    var IconEnabled = Annotation.GetField("iconEnabled");

                    System.Type AnnotationUtility = System.Type.GetType("UnityEditor.AnnotationUtility, UnityEditor");
                    var GetAnnotations = AnnotationUtility.GetMethod("GetAnnotations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var SetIconEnabled = AnnotationUtility.GetMethod("SetIconEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                    //Scann all Gizmos of Unity, of this project
                    System.Array annotations = (System.Array)GetAnnotations.Invoke(null, null);
                    foreach (var a in annotations)
                    {
                        int classId = (int)ClassId.GetValue(a);
                        string scriptClass = (string)ScriptClass.GetValue(a);
                        int flags = (int)Flags.GetValue(a);
                        int iconEnabled = (int)IconEnabled.GetValue(a);

                        // this is done to ignore any built in types
                        if (string.IsNullOrEmpty(scriptClass))
                        {
                            continue;
                        }

                        const int HasIcon = 1;
                        bool hasIconFlag = (flags & HasIcon) == HasIcon;

                        //If the current gizmo is of the class desired, disable the gizmo in scene
                        if (scriptClass == scriptClassNameToDisable)
                        {
                            if (hasIconFlag && (iconEnabled != 0))
                            {
                                /*UnityEngine.Debug.LogWarning(string.Format("Script:'{0}' is not ment to show its icon in the scene view and will auto hide now. " +
                                    "Icon auto hide is checked on script recompile, if you'd like to change this please remove it from the config", scriptClass));*/
                                SetIconEnabled.Invoke(null, new object[] { classId, scriptClass, 0 });
                            }
                        }
                    }

                    return true;
                }
                //Catch any error
                catch (System.Exception exception)
                {
                    string exceptionOcurred = "";
                    exceptionOcurred = exception.Message;
                    if (exceptionOcurred != null)
                        exceptionOcurred = "";
                    return false;
                }
            }

            public Rect GetInspectorWindowSize()
            {
                //Returns the current size of inspector window
                return EditorGUILayout.GetControlRect(true, 0f);
            }

            public override void OnInspectorGUI()
            {
                //Start the undo event support, draw default inspector and monitor of changes
                IngameLogsViewer script = (IngameLogsViewer)target;
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(target, "Undo Event");
                script.gizmosOfThisComponentIsDisabled = DisableGizmosInSceneView("IngameLogsViewer", script.gizmosOfThisComponentIsDisabled);

                //Support reminder
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("Remember to read the Ingame Logs Viewer documentation to understand how to use it.\nGet support at: mtassets@windsoft.xyz", MessageType.None);

                GUILayout.Space(10);

                script.openButton.SetActive(!script.hideTouchButton);

                DrawDefaultInspector();

                //Apply changes on script, case is not playing in editor
                if (GUI.changed == true && Application.isPlaying == false)
                {
                    EditorUtility.SetDirty(script);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(script.gameObject.scene);
                }
                if (EditorGUI.EndChangeCheck() == true)
                {

                }
            }
        }
        #endregion
#endif

        private void Awake()
        {
            //Inicialize
            console.SetActive(true);

            //Start heap meter
            StartCoroutine(HeapMonitor());
            StartCoroutine(TimeMonitor());

            //Delete others Debuggers, to avoid duplicates
            GameObject[] debuggers = GameObject.FindGameObjectsWithTag("ILV Debugger");
            if (debuggers != null && debuggers.Length >= 2)
            {
                for (int i = 0; i < debuggers.Length; i++)
                {
                    if (i == 0)
                    {
                        continue;
                    }
                    if (debuggers[i] != null)
                    {
                        DestroyImmediate(debuggers[i], true);
                        Debug.Log("A duplicate of the ILV Debugger was found in this scene. It has already been removed.");
                        return;
                    }
                }
            }

            //Set correct name
            this.gameObject.name = "ILV Debugger";

            //Configures to not destroy
            DontDestroyOnLoad(this.gameObject);

            //Register the log receiver
            Application.logMessageReceivedThreaded += OnLogReceiver;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            //Inicialize
            openButton.SetActive(true);
            closeButton.SetActive(false);
            console.SetActive(false);

            if (startOpened == true)
            {
                openButton.SetActive(false);
                closeButton.SetActive(true);
                console.SetActive(true);
            }

            //Separate the slots
            for (int i = 0; i < 99; i++)
            {
                logSlotsAssert.Add(logSlots[i]);
            }
            for (int i = 99; i < 199; i++)
            {
                logSlotsError.Add(logSlots[i]);
            }
            for (int i = 199; i < 299; i++)
            {
                logSlotsException.Add(logSlots[i]);
            }
            for (int i = 299; i < 399; i++)
            {
                logSlotsLog.Add(logSlots[i]);
            }
            for (int i = 399; i < 499; i++)
            {
                logSlotsWarning.Add(logSlots[i]);
            }

            //Change the state of filters
            if (PlayerPrefs.HasKey("showLogs") == true)
            {
                if (PlayerPrefs.GetInt("showLogs") == 1)
                {
                    ShowLogs();
                }
            }
            if (PlayerPrefs.HasKey("showWarns") == true)
            {
                if (PlayerPrefs.GetInt("showWarns") == 1)
                {
                    ShowWarns();
                }
            }
            if (PlayerPrefs.HasKey("showErrors") == true)
            {
                if (PlayerPrefs.GetInt("showErrors") == 1)
                {
                    ShowErrors();
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(SceneLoadedDelayedWarn("[ILV] - Loaded scene: \"" + scene.name + " (" + scene.buildIndex.ToString() + ")" + "\""));
        }

        private void OnLogReceiver(string condiction, string strackTrace, LogType logType)
        {
            //Find a empty slot
            LogSlotILV slot = null;

            if (logType == LogType.Assert)
            {
                for (int i = 0; i < logSlotsAssert.Count; i++)
                {
                    if (logSlotsAssert[i].gameObject.activeSelf == false)
                    {
                        slot = logSlotsAssert[i];
                        break;
                    }
                    if (i == logSlotsAssert.Count - 1)
                    {
                        foreach (LogSlotILV s in logSlotsAssert)
                        {
                            s.gameObject.SetActive(false);
                        }
                        slot = logSlotsAssert[0];
                    }
                }
            }
            if (logType == LogType.Error)
            {
                for (int i = 0; i < logSlotsError.Count; i++)
                {
                    if (logSlotsError[i].gameObject.activeSelf == false)
                    {
                        slot = logSlotsError[i];
                        break;
                    }
                    if (i == logSlotsError.Count - 1)
                    {
                        foreach (LogSlotILV s in logSlotsError)
                        {
                            s.gameObject.SetActive(false);
                        }
                        slot = logSlotsError[0];
                    }
                }
            }
            if (logType == LogType.Exception)
            {
                for (int i = 0; i < logSlotsException.Count; i++)
                {
                    if (logSlotsException[i].gameObject.activeSelf == false)
                    {
                        slot = logSlotsException[i];
                        break;
                    }
                    if (i == logSlotsException.Count - 1)
                    {
                        foreach (LogSlotILV s in logSlotsException)
                        {
                            s.gameObject.SetActive(false);
                        }
                        slot = logSlotsException[0];
                    }
                }
            }
            if (logType == LogType.Log)
            {
                for (int i = 0; i < logSlotsLog.Count; i++)
                {
                    if (logSlotsLog[i].gameObject.activeSelf == false)
                    {
                        slot = logSlotsLog[i];
                        break;
                    }
                    if (i == logSlotsLog.Count - 1)
                    {
                        foreach (LogSlotILV s in logSlotsLog)
                        {
                            s.gameObject.SetActive(false);
                        }
                        slot = logSlotsLog[0];
                    }
                }
            }
            if (logType == LogType.Warning)
            {
                for (int i = 0; i < logSlotsWarning.Count; i++)
                {
                    if (logSlotsWarning[i].gameObject.activeSelf == false)
                    {
                        slot = logSlotsWarning[i];
                        break;
                    }
                    if (i == logSlotsWarning.Count - 1)
                    {
                        foreach (LogSlotILV s in logSlotsWarning)
                        {
                            s.gameObject.SetActive(false);
                        }
                        slot = logSlotsWarning[0];
                    }
                }
            }

            //Avoid null slot
            if (slot == null)
            {
                return;
            }

            //Manipulates the log by storing it in the dictionary.
            if (storageOfLogs.ContainsKey(logType) == true)
            {
                LogSlotILV slotToEdit = null;
                bool notFound = true;
                foreach (LogContent log in storageOfLogs[logType])
                {
                    if (log.text == condiction)
                    {
                        log.count += 1;
                        slotToEdit = log.slotResponsible;
                        slotToEdit.counter.text = log.count.ToString();
                        notFound = false;
                        break;
                    }
                }
                if (notFound == true)
                {
                    storageOfLogs[logType].Add(new LogContent(condiction, strackTrace, slot));
                    slotToEdit = slot;
                    slotToEdit.content.text = storageOfLogs[logType][storageOfLogs[logType].Count - 1].text;
                    slotToEdit.counter.text = storageOfLogs[logType][storageOfLogs[logType].Count - 1].count.ToString();
                    if (logType == LogType.Assert || logType == LogType.Error || logType == LogType.Exception)
                    {
                        slotToEdit.icon.sprite = errorIcon;
                    }
                    if (logType == LogType.Log)
                    {
                        slotToEdit.icon.sprite = logIcon;
                    }
                    if (logType == LogType.Warning)
                    {
                        slotToEdit.icon.sprite = warnIcon;
                    }
                    slotToEdit.button.onClick.RemoveAllListeners();
                    slotToEdit.button.onClick.AddListener(() => ShowStrackTrace(storageOfLogs[logType][storageOfLogs[logType].Count - 1].strackTrace));
                }
                slotToEdit.gameObject.SetActive(true);
            }
            if (storageOfLogs.ContainsKey(logType) == false)
            {
                storageOfLogs.Add(logType, new List<LogContent>() { new LogContent(condiction, strackTrace, slot) });
                slot.content.text = storageOfLogs[logType][0].text;
                slot.counter.text = "1";
                if (logType == LogType.Assert || logType == LogType.Error || logType == LogType.Exception)
                {
                    slot.icon.sprite = errorIcon;
                }
                if (logType == LogType.Log)
                {
                    slot.icon.sprite = logIcon;
                }
                if (logType == LogType.Warning)
                {
                    slot.icon.sprite = warnIcon;
                }
                slot.button.onClick.RemoveAllListeners();
                slot.button.onClick.AddListener(() => ShowStrackTrace(storageOfLogs[logType][0].strackTrace));
                slot.gameObject.SetActive(true);
            }

            UpdateScreen();
        }

        public void UpdateScreen()
        {
            //Update the counters
            if (console.activeSelf == true)
            {
                int logs = 0;
                int errors = 0;
                int warns = 0;

                if (storageOfLogs.ContainsKey(LogType.Assert) == true) { errors = storageOfLogs[LogType.Assert].Count; };
                if (storageOfLogs.ContainsKey(LogType.Error) == true) { errors = storageOfLogs[LogType.Error].Count; };
                if (storageOfLogs.ContainsKey(LogType.Exception) == true) { errors = storageOfLogs[LogType.Exception].Count; };
                if (storageOfLogs.ContainsKey(LogType.Log) == true) { logs = storageOfLogs[LogType.Log].Count; };
                if (storageOfLogs.ContainsKey(LogType.Warning) == true) { warns = storageOfLogs[LogType.Warning].Count; };

                logCounter.text = logs.ToString();
                errorCounter.text = errors.ToString();
                warnCounter.text = warns.ToString();

                //Update the logs to hide with filters. Call the button to hide double time, for hidde the new logs
                if (showingLogs == false)
                {
                    ShowLogs();
                    ShowLogs();
                }
                if (showingWarn == false)
                {
                    ShowWarns();
                    ShowWarns();
                }
                if (showingErrors == false)
                {
                    ShowErrors();
                    ShowErrors();
                }
            }
        }

        private void Update()
        {
            //Controls
            if (Input.GetKeyDown(keyForOpen) == true)
            {
                OpenConsole();
            }
            if (Input.GetKeyDown(keyForClose) == true)
            {
                CloseConsole();
            }

            //Control of time
            seconds += Time.deltaTime;
            if (seconds >= 59)
            {
                minutes += 1;
                seconds = 0;
            }
        }

        private bool showingLogs = true;
        public void ShowLogs()
        {
            //Show the logs
            if (showingLogs == false)
            {
                showLogs.color = colorEnabled;
                if (storageOfLogs.ContainsKey(LogType.Log) == true)
                {
                    foreach (LogContent log in storageOfLogs[LogType.Log])
                    {
                        LogSlotILV slot = null;
                        for (int i = 0; i < logSlotsLog.Count; i++)
                        {
                            if (logSlotsLog[i].gameObject.activeSelf == false)
                            {
                                slot = logSlotsLog[i];
                                break;
                            }
                            if (i == logSlotsLog.Count - 1)
                            {
                                foreach (LogSlotILV s in logSlotsLog)
                                {
                                    s.gameObject.SetActive(false);
                                }
                                slot = logSlotsLog[0];
                            }
                        }
                        slot.content.text = log.text;
                        slot.counter.text = log.count.ToString();
                        slot.icon.sprite = logIcon;
                        slot.button.onClick.RemoveAllListeners();
                        slot.button.onClick.AddListener(() => ShowStrackTrace(log.strackTrace));
                        slot.gameObject.SetActive(true);
                    }
                }
                PlayerPrefs.SetInt("showLogs", 0);
                PlayerPrefs.Save();
                showingLogs = true;
                return;
            }
            //Hide the logs
            if (showingLogs == true)
            {
                showLogs.color = colorHidded;
                foreach (LogSlotILV slot in logSlotsLog)
                {
                    slot.gameObject.SetActive(false);
                }
                PlayerPrefs.SetInt("showLogs", 1);
                PlayerPrefs.Save();
                showingLogs = false;
                return;
            }
        }

        private bool showingWarn = true;
        public void ShowWarns()
        {
            //Show the Warns
            if (showingWarn == false)
            {
                showWarns.color = colorEnabled;
                if (storageOfLogs.ContainsKey(LogType.Warning) == true)
                {
                    foreach (LogContent log in storageOfLogs[LogType.Warning])
                    {
                        LogSlotILV slot = null;
                        for (int i = 0; i < logSlotsWarning.Count; i++)
                        {
                            if (logSlotsWarning[i].gameObject.activeSelf == false)
                            {
                                slot = logSlotsWarning[i];
                                break;
                            }
                            if (i == logSlotsWarning.Count - 1)
                            {
                                foreach (LogSlotILV s in logSlotsWarning)
                                {
                                    s.gameObject.SetActive(false);
                                }
                                slot = logSlotsWarning[0];
                            }
                        }
                        slot.content.text = log.text;
                        slot.counter.text = log.count.ToString();
                        slot.icon.sprite = warnIcon;
                        slot.button.onClick.RemoveAllListeners();
                        slot.button.onClick.AddListener(() => ShowStrackTrace(log.strackTrace));
                        slot.gameObject.SetActive(true);
                    }
                }
                PlayerPrefs.SetInt("showWarns", 0);
                PlayerPrefs.Save();
                showingWarn = true;
                return;
            }
            //Hide the logs
            if (showingWarn == true)
            {
                showWarns.color = colorHidded;
                foreach (LogSlotILV slot in logSlotsWarning)
                {
                    slot.gameObject.SetActive(false);
                }
                PlayerPrefs.SetInt("showWarns", 1);
                PlayerPrefs.Save();
                showingWarn = false;
                return;
            }
        }

        private bool showingErrors = true;
        public void ShowErrors()
        {
            //Show the Errors
            if (showingErrors == false)
            {
                showErrors.color = colorEnabled;
                if (storageOfLogs.ContainsKey(LogType.Assert) == true)
                {
                    foreach (LogContent log in storageOfLogs[LogType.Assert])
                    {
                        LogSlotILV slot = null;
                        for (int i = 0; i < logSlotsAssert.Count; i++)
                        {
                            if (logSlotsAssert[i].gameObject.activeSelf == false)
                            {
                                slot = logSlotsAssert[i];
                                break;
                            }
                            if (i == logSlotsAssert.Count - 1)
                            {
                                foreach (LogSlotILV s in logSlotsAssert)
                                {
                                    s.gameObject.SetActive(false);
                                }
                                slot = logSlotsAssert[0];
                            }
                        }
                        slot.content.text = log.text;
                        slot.counter.text = log.count.ToString();
                        slot.icon.sprite = errorIcon;
                        slot.button.onClick.RemoveAllListeners();
                        slot.button.onClick.AddListener(() => ShowStrackTrace(log.strackTrace));
                        slot.gameObject.SetActive(true);
                    }
                }
                if (storageOfLogs.ContainsKey(LogType.Error) == true)
                {
                    foreach (LogContent log in storageOfLogs[LogType.Error])
                    {
                        LogSlotILV slot = null;
                        for (int i = 0; i < logSlotsError.Count; i++)
                        {
                            if (logSlotsError[i].gameObject.activeSelf == false)
                            {
                                slot = logSlotsError[i];
                                break;
                            }
                            if (i == logSlotsError.Count - 1)
                            {
                                foreach (LogSlotILV s in logSlotsError)
                                {
                                    s.gameObject.SetActive(false);
                                }
                                slot = logSlotsError[0];
                            }
                        }
                        slot.content.text = log.text;
                        slot.counter.text = log.count.ToString();
                        slot.icon.sprite = errorIcon;
                        slot.button.onClick.RemoveAllListeners();
                        slot.button.onClick.AddListener(() => ShowStrackTrace(log.strackTrace));
                        slot.gameObject.SetActive(true);
                    }
                }
                if (storageOfLogs.ContainsKey(LogType.Exception) == true)
                {
                    foreach (LogContent log in storageOfLogs[LogType.Exception])
                    {
                        LogSlotILV slot = null;
                        for (int i = 0; i < logSlotsException.Count; i++)
                        {
                            if (logSlotsException[i].gameObject.activeSelf == false)
                            {
                                slot = logSlotsException[i];
                                break;
                            }
                            if (i == logSlotsException.Count - 1)
                            {
                                foreach (LogSlotILV s in logSlotsException)
                                {
                                    s.gameObject.SetActive(false);
                                }
                                slot = logSlotsException[0];
                            }
                        }
                        slot.content.text = log.text;
                        slot.counter.text = log.count.ToString();
                        slot.icon.sprite = errorIcon;
                        slot.button.onClick.RemoveAllListeners();
                        slot.button.onClick.AddListener(() => ShowStrackTrace(log.strackTrace));
                        slot.gameObject.SetActive(true);
                    }
                }
                PlayerPrefs.SetInt("showErrors", 0);
                PlayerPrefs.Save();
                showingErrors = true;
                return;
            }
            //Hide the logs
            if (showingErrors == true)
            {
                showErrors.color = colorHidded;
                foreach (LogSlotILV slot in logSlotsError)
                {
                    slot.gameObject.SetActive(false);
                }
                foreach (LogSlotILV slot in logSlotsAssert)
                {
                    slot.gameObject.SetActive(false);
                }
                foreach (LogSlotILV slot in logSlotsException)
                {
                    slot.gameObject.SetActive(false);
                }
                PlayerPrefs.SetInt("showErrors", 1);
                PlayerPrefs.Save();
                showingErrors = false;
                return;
            }
        }

        public void ShowStrackTrace(string strack)
        {
            //Show strack trace
            strackView.text = strack;
            strackTrace.SetActive(true);
        }

        public void CloseStrackTrace()
        {
            //Hide strack trace
            strackTrace.SetActive(false);
        }

        public void Clear()
        {
            //Clear the debugger
            foreach (LogSlotILV slot in logSlots)
            {
                slot.gameObject.SetActive(false);
            }
            storageOfLogs.Clear();
            logCounter.text = "0";
            errorCounter.text = "0";
            warnCounter.text = "0";
        }

        public void OpenConsole()
        {
            //Open the console
            console.SetActive(true);
            openButton.SetActive(false);
            closeButton.SetActive(true);
            UpdateScreen();
        }

        public void CloseConsole()
        {
            //Close console
            console.SetActive(false);
            openButton.SetActive(true);
            closeButton.SetActive(false);
        }

        public void RunGargbageCollector()
        {
            //Run garbage collector
            System.GC.Collect();
            Resources.UnloadUnusedAssets();
        }

        private IEnumerator HeapMonitor()
        {
            WaitForSeconds wait = new WaitForSeconds(1f);

            while (enabled)
            {
                yield return wait;

                if (console.activeSelf == true)
                {
                    if (lastSizeOfHeap >= System.GC.GetTotalMemory(false))
                    {
                        Debug.Log("[ILV] - The heap had its garbage collected, or increased allocation size.");
                    }
                    lastSizeOfHeap = System.GC.GetTotalMemory(false);
                    double totalMemory = (double)lastSizeOfHeap / (double)1000000;
                    heapMeter.text = totalMemory.ToString("F1");
                }
            }
        }

        private IEnumerator TimeMonitor()
        {
            WaitForSeconds wait = new WaitForSeconds(1f);

            while (enabled)
            {
                yield return wait;

                if (console.activeSelf == true)
                {
                    string minute = "";
                    string second = "";
                    if (minutes < 10)
                    {
                        minute = "0" + minutes.ToString("F0");
                    }
                    if (minutes >= 10)
                    {
                        minute = minutes.ToString("F0");
                    }
                    if (seconds < 10)
                    {
                        second = "0" + seconds.ToString("F0");
                    }
                    if (seconds >= 10)
                    {
                        second = seconds.ToString("F0");
                    }
                    timeMeter.text = minute + ":" + second;
                }
            }
        }

        private IEnumerator SceneLoadedDelayedWarn(string text)
        {
            WaitForSeconds wait = new WaitForSeconds(0.2f);
            yield return wait;
            Debug.Log(text);
        }
    }

    public class LogContent
    {
        /*
         * Class responsible for store a log
         */

        public string text = "";
        public string strackTrace = "";
        public int count = 0;
        public LogSlotILV slotResponsible;

        public LogContent(string text, string strackTrace, LogSlotILV logSlot)
        {
            //Iniciate the objetct
            this.text = text;
            this.strackTrace = strackTrace;
            this.count = 1;
            this.slotResponsible = logSlot;
        }
    }
}