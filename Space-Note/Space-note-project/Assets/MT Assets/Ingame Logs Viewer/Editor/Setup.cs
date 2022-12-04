using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System;

namespace MTAssets.IngameLogsViewer.Editor
{
    /*
     * This class is responsible for assisting the installation of Ingame Logs Viewer and its handler.
     */

    [InitializeOnLoad]
    public class Setup : EditorWindow
    {
        //Variables of setup
        private bool setupAutoStarted = false;
        private int rendersCount = 0;
        public static IngameLogsPreferences ilvPreferences;

        //After unity compilation, run setup verification

        static Setup()
        {
            //Run the verifier after unity compiles
            EditorApplication.delayCall += SetupVerifier;
        }

        static void SetupVerifier()
        {
            //Load the preferences 
            LoadThePreferences();

            //If the NAT is not installed, run the setup
            if (ilvPreferences.ilvInstalledInFirstScene == false)
            {
                int confirmation = EditorUtility.DisplayDialogComplex("Ingame Logs Viewer Setup",
                        "Ingame Logs Viewer has detected that \"ILV Debugger\" is not installed in the first scene of your project. This GameObject is responsible for rendering and operating the log viewer console interface within your game.\n\nDo you want to start the installation process now ? ",
                        "Yes",
                        "Don't ask again",
                        "No");
                switch (confirmation)
                {
                    case 0:
                        //Open the window
                        OpenWindow();
                        break;
                    case 1:
                        //Register that installed
                        ilvPreferences.ilvInstalledInFirstScene = true;
                        SaveThePreferences();
                        EditorUtility.DisplayDialog("Tip", "You can always start the ILV installer by going to the \"Tools > Ingame Logs Viewer > Open Setup\" tab.", "Ok");
                        break;
                    case 2:
                        break;
                }
            }
        }

        static void LoadThePreferences()
        {
            //Create the default directory, if not exists
            if (!AssetDatabase.IsValidFolder("Assets/MT Assets/_AssetsData"))
                AssetDatabase.CreateFolder("Assets/MT Assets", "_AssetsData");
            if (!AssetDatabase.IsValidFolder("Assets/MT Assets/_AssetsData/Preferences"))
                AssetDatabase.CreateFolder("Assets/MT Assets/_AssetsData", "Preferences");

            //Try to load the preferences file
            ilvPreferences = (IngameLogsPreferences)AssetDatabase.LoadAssetAtPath("Assets/MT Assets/_AssetsData/Preferences/IngameLogsViewer.asset", typeof(IngameLogsPreferences));
            //Validate the preference file. if this preference file is of another project, delete then
            if (ilvPreferences != null)
            {
                if (ilvPreferences.projectName != Application.productName)
                {
                    AssetDatabase.DeleteAsset("Assets/MT Assets/_AssetsData/Preferences/IngameLogsViewer.asset");
                    ilvPreferences = null;
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
            //If null, create and save a preferences file
            if (ilvPreferences == null)
            {
                ilvPreferences = ScriptableObject.CreateInstance<IngameLogsPreferences>();
                ilvPreferences.projectName = Application.productName;
                AssetDatabase.CreateAsset(ilvPreferences, "Assets/MT Assets/_AssetsData/Preferences/IngameLogsViewer.asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        static void SaveThePreferences()
        {
            //Save the preferences in Prefs.asset
            ilvPreferences.projectName = Application.productName;

            EditorUtility.SetDirty(ilvPreferences);
            AssetDatabase.SaveAssets();
        }

        //Setup window code

        public static void OpenWindow()
        {
            //Method to open the Window
            var window = GetWindow<Setup>("Setup");
            window.minSize = new Vector2(360, 220);
            window.maxSize = new Vector2(360, 220);
            var position = window.position;
            position.center = new Rect(0f, 0f, Screen.currentResolution.width, Screen.currentResolution.height).center;
            window.position = position;
            window.Show();
        }

        void OnGUI()
        {
            //Start the undo event support, draw default inspector and monitor of changes
            EditorGUI.BeginChangeCheck();

            //Try to load needed assets
            Texture iconOfUi = (Texture)AssetDatabase.LoadAssetAtPath("Assets/MT Assets/Ingame Logs Viewer/Editor/Images/Icon.png", typeof(Texture));
            //If fails on load needed assets, locks ui
            if (iconOfUi == null)
            {
                EditorGUILayout.HelpBox("Unable to load required files. Please reinstall Ingame Logs Viewer to correct this problem.", MessageType.Error);
                return;
            }

            //Run install wizard, if is not installed (after rendered the window) 
            if (ilvPreferences.ilvInstalledInFirstScene == false && setupAutoStarted == false)
            {
                rendersCount += 1;
                if (rendersCount >= 5)
                {
                    setupAutoStarted = true;
                    RunInstallWizard();
                }
            }

            GUILayout.BeginVertical("box");

            GUIStyle tituloBox = new GUIStyle();
            tituloBox.fontStyle = FontStyle.Bold;
            tituloBox.alignment = TextAnchor.MiddleCenter;

            //Topbar
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUILayout.BeginVertical();
            GUILayout.Space(8);
            GUIStyle estiloIcone = new GUIStyle();
            estiloIcone.border = new RectOffset(0, 0, 0, 0);
            estiloIcone.margin = new RectOffset(4, 0, 4, 0);
            GUILayout.Box(iconOfUi, estiloIcone, GUILayout.Width(48), GUILayout.Height(44));
            GUILayout.Space(6);
            GUILayout.EndVertical();
            GUILayout.Space(8);
            GUILayout.Space(-55);
            GUILayout.BeginVertical();
            GUILayout.Space(20);
            GUIStyle titulo = new GUIStyle();
            titulo.fontSize = 25;
            titulo.normal.textColor = Color.black;
            titulo.alignment = TextAnchor.MiddleLeft;
            EditorGUILayout.LabelField("Ingame Logs Viewer", titulo);
            GUILayout.Space(4);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            //Setup 
            EditorGUILayout.LabelField("Setup of Asset", tituloBox);
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("Setup will install \"ILV Debugger\" in the main scene of your game. The GameObject \"ILV Debugger\" will persist in all subsequent scenes and is responsible for rendering the interface and log console and making it work. Click the button below to start the installation.", MessageType.Info);
            GUILayout.Space(10);
            if (GUILayout.Button("Start Installation", GUILayout.Height(40)))
            {
                RunInstallWizard();
            }
            GUILayout.Space(3);

            GUILayout.EndVertical();

            //Apply changes on script, case is not playing in editor
            if (GUI.changed == true && Application.isPlaying == false)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            if (EditorGUI.EndChangeCheck() == true)
            {

            }
        }

        void RunInstallWizard()
        {
            //Start the install
            EditorUtility.DisplayDialog("Installation Wizard", "The installation wizard will assist you in the process of installing GameObject \"ILV Debugger\" in the primary scene of your game.\n\nThis GameObject is responsible for displaying the logs console so that you can access it from anywhere in the app, and make it work. This GameObject must be installed correctly. This GameObject must remain in the first scene of your game (the scene your game plays when it is opened), as this GameObject is indestructible and will persist even if another scene is loaded.\n\nIf your game does not have a first scene registered on the \"Build Settings\" screen, you can install this GameObject on the first scene your game plays when it opens.", "Next");

            //Get data of scenes
            int registeredScenes = SceneManager.sceneCountInBuildSettings;
            int activeSceneId = SceneManager.GetActiveScene().buildIndex;

            //Set registered scenes to zero, if the main is deleted
            if (registeredScenes > 0 && AssetDatabase.LoadAssetAtPath(SceneUtility.GetScenePathByBuildIndex(0), typeof(object)) == null)
            {
                registeredScenes = 0;
            }

            //If not exists registered scene
            if (registeredScenes == 0)
            {
                int confirmation = EditorUtility.DisplayDialogComplex("Installation Wizard",
                    "Could not find a main scene registered in \"Build Settings\". You can install \"ILV Debugger\" on the current scene, but the log console will only appear when the scene in question is executed.\n\nDo you want to proceed with the installation?",
                    "Install In This Scene",
                    "Open Build Settings",
                    "Cancel");
                switch (confirmation)
                {
                    case 0:
                        //Create data handler
                        EditorUtility.DisplayDialog("Installation Wizard",
                        "Debugger was successfully installed on your scene!",
                        "Finish");
                        CreateDataHandler();
                        this.Close();
                        EditorUtility.DisplayDialog("Installation Wizard",
                        "The installation was completed successfully. You can always repeat installation by going to \"Tools > MT Assets > Ingame Logs Viewer > Open Setup\" tab.",
                        "Ok");
                        break;
                    case 1:
                        //Open build settings
                        EditorWindow.GetWindow(Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));
                        break;
                    case 2:
                        break;
                }
            }

            //If exists registered scene
            if (registeredScenes > 0)
            {
                EditorUtility.DisplayDialog("Installation Wizard",
                    "The main scene of your game was found in \"Build Settings\". Click next to proceed with the installation.",
                    "Next");

                //Load the first scene
                string firstScene = SceneUtility.GetScenePathByBuildIndex(0);
                EditorSceneManager.OpenScene(firstScene);

                EditorUtility.DisplayDialog("Installation Wizard",
                        "Debugger was successfully installed on your scene!",
                        "Finish");

                CreateDataHandler();

                this.Close();

                EditorUtility.DisplayDialog("Installation Wizard",
                        "The installation was completed successfully. You can always repeat installation by going to \"Tools > MT Assets > Ingame Logs Viewer > Open Setup\" tab.",
                        "Ok");
            }
        }

        void CreateDataHandler()
        {
            //Save the prefs of install
            ilvPreferences.ilvInstalledInFirstScene = true;
            SaveThePreferences();

            //Create the data handler in the scene
            GameObject debuggerFind = GameObject.Find("ILV Debugger");
            if (debuggerFind == null)
            {
                GameObject debugger = GameObject.Instantiate((GameObject)AssetDatabase.LoadAssetAtPath("Assets/MT Assets/Ingame Logs Viewer/Editor/Prefabs/ILV Debugger.prefab", typeof(GameObject)), new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0));
                debugger.name = "ILV Debugger";
                debugger.transform.SetAsFirstSibling();
                EditorGUIUtility.PingObject(debugger);
                Selection.objects = new UnityEngine.Object[] { debugger };
            }
            if (debuggerFind != null)
            {
                EditorGUIUtility.PingObject(debuggerFind);
                Selection.objects = new UnityEngine.Object[] { debuggerFind };
            }
        }
    }
} 