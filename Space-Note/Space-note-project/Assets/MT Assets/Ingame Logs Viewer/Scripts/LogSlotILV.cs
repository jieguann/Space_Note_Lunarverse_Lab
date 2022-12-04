using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTAssets.IngameLogsViewer
{
    /*
     * Class responsible for the operation of the log item.
     */

    [AddComponentMenu("")] //Hide this script in component menu.
    public class LogSlotILV : MonoBehaviour
    {
        public IngameLogsViewer script;
        public Image icon;
        public Text content;
        public Button button;
        public Text counter;

        private void Awake()
        {
            //Store this item in core
            script.logSlots.Add(this);
            this.gameObject.SetActive(false);
        }
    }
}