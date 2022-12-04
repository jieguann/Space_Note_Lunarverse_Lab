using UnityEngine;
using UnityEditor;
using System.IO;

namespace MTAssets.IngameLogsViewer.Editor{

    /*
     * This class is responsible for creating the menu for this asset. 
     */

    public class Menu : MonoBehaviour
    {
        //Menu items

        [MenuItem("Tools/MT Assets/Ingame Logs Viewer/Open Setup", false, 10)]
        static void SetupILV()
        {
            Setup.OpenWindow();
        }

        [MenuItem("Tools/MT Assets/Ingame Logs Viewer/Changelog", false, 10)]
        static void OpenChangeLog()
        {
            string filePath = "Assets/MT Assets/Ingame Logs Viewer/List Of Changes.txt";

            if (File.Exists(filePath) == true)
            {
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath(filePath, typeof(TextAsset)));
            }
            if (File.Exists(filePath) == false)
            {
                EditorUtility.DisplayDialog("Error", "Unable to open file. The file has been deleted, or moved. Please, to correct this problem and avoid future problems with this tool, remove all files from this asset and install it again.", "Ok");
            }
        }

        [MenuItem("Tools/MT Assets/Ingame Logs Viewer/Read Documentation", false, 30)]
        static void ReadDocumentation()
        {
            EditorUtility.DisplayDialog("Read Documentation", "The documentation is located inside the \n\"MT Assets/Ingame Logs Viewer\" folder. Just unzip \"Documentation.zip\" on your desktop and open the \"Documentation.html\" file with your preferred browser.", "Cool!");
        }

        [MenuItem("Tools/MT Assets/Ingame Logs Viewer/More Assets", false, 30)]
        static void MoreAssets()
        {
            Application.OpenURL("https://assetstore.unity.com/publishers/40306");
        }

        [MenuItem("Tools/MT Assets/Ingame Logs Viewer/Support", false, 30)]
        static void GetSupport()
        {
            EditorUtility.DisplayDialog("Support", "If you have any questions, problems or want to contact me, just contact me by email (mtassets@windsoft.xyz).", "Got it!");
        }
    }
}