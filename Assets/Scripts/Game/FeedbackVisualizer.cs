// Colours a panel and a line of text green or red so the player can see how a step went.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace NTGD124
{
    public class FeedbackVisualizer : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)][SerializeField] private string _componentDescription =
            "The only visual stimuli in the prototype. A good step flashes the feedback panel green, a bad " +
            "step flashes it red, and the strength of the colour follows the 0 to 100 roll. Separately, the " +
            "text in the middle of the cauldron turns red while it is hot and blue while it is cold, so " +
            "temperature feedback reads at a glance and is not lost among the per-ingredient feedback. " +
            "Replace this component later when real art goes in.";


        ///// Public Variables /////

        [Header("Singleton Access")]
        public static FeedbackVisualizer Singleton;

        [Header("UI References")]
        public Image FeedbackPanel;
        public TMP_Text FeedbackLabel;

        [Header("Cauldron Temperature Text (sits in the middle of the cauldron)")]
        public TMP_Text CauldronTemperatureLabel;
        public Color HotTemperatureColour = new Color(0.9f, 0.2f, 0.15f, 1f);
        public Color ColdTemperatureColour = new Color(0.25f, 0.55f, 0.95f, 1f);

        [Header("Colours")]
        public Color PositiveColour = new Color(0.25f, 0.75f, 0.3f, 1f);
        public Color NegativeColour = new Color(0.8f, 0.25f, 0.25f, 1f);
        public Color RestingColour = new Color(0.5f, 0.5f, 0.5f, 0.4f);

        [Header("Timing")]
        public float FlashSeconds = 0.9f;


        ///// Private Variables /////

        // The flash that is running right now, kept so a new flash can cut the old one short.
        private Coroutine _runningFlash;


        ///// Unity Methods /////

        private void Awake()
        {
            Singleton = this;
        }


        private void Start()
        {
            ResetToResting();
        }


        ///// Custom Methods - Trigger Methods /////

        // Called by AiAgentBehaviour after every finished step.
        public void ShowFeedback(bool wasPositive, int magnitude)
        {
            if (_runningFlash != null)
            {
                StopCoroutine(_runningFlash);
            }

            _runningFlash = StartCoroutine(FlashFeedback(wasPositive, magnitude));
        }


        // Called by GameSequenceManager when the whole round is won.
        public void ShowWinFeedback(string messageText)
        {
            SetPanelColour(PositiveColour);
            SetLabel(messageText, PositiveColour);
        }


        // Called by GameSequenceManager when the whole round is lost.
        public void ShowLossFeedback(string messageText)
        {
            SetPanelColour(NegativeColour);
            SetLabel(messageText, NegativeColour);
        }


        ///// Coroutines /////

        // Flashes the panel for a moment and then fades it back to its resting grey.
        private IEnumerator FlashFeedback(bool wasPositive, int magnitude)
        {
            Color targetColour = wasPositive ? PositiveColour : NegativeColour;

            // A stronger roll gives a stronger colour, so the player can read the size of the mistake.
            float strength = Mathf.Lerp(0.35f, 1f, magnitude / 100f);
            targetColour.a = strength;

            SetPanelColour(targetColour);
            SetLabel(wasPositive ? "That went well (" + magnitude + "/100)" : "That went badly (" + magnitude + "/100)", targetColour);

            yield return new WaitForSeconds(FlashSeconds);

            ResetToResting();
            _runningFlash = null;
        }


        // Called by AiAgentBehaviour every time the cauldron's temperature is recorded, so the
        // player can see hot versus cold at a glance instead of only reading it in a summary.
        public void ShowCauldronTemperature(string temperature)
        {
            if (CauldronTemperatureLabel == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(temperature))
            {
                CauldronTemperatureLabel.text = "";
                return;
            }

            bool isHot = temperature.ToLower() == "hot";

            CauldronTemperatureLabel.text = temperature.ToUpper();
            CauldronTemperatureLabel.color = isHot ? HotTemperatureColour : ColdTemperatureColour;
        }


        ///// Custom Methods - Action Methods /////

        // Action method: puts the panel back to its neutral look.
        public void ResetToResting()
        {
            SetPanelColour(RestingColour);
            SetLabel("", Color.white);
            ShowCauldronTemperature("");
        }


        // Action method: writes a colour into the feedback panel image.
        private void SetPanelColour(Color panelColour)
        {
            if (FeedbackPanel != null)
            {
                FeedbackPanel.color = panelColour;
            }
        }


        // Action method: writes a message into the feedback label.
        private void SetLabel(string messageText, Color labelColour)
        {
            if (FeedbackLabel != null)
            {
                FeedbackLabel.text = messageText;

                Color solidColour = labelColour;
                solidColour.a = 1f;
                FeedbackLabel.color = solidColour;
            }
        }
    }
}


// Implementation steps:
// 1. Place this component on the GameSystems object.
// 2. Link FeedbackPanel to an Image that sits behind the cauldron and FeedbackLabel to a TMP text.
// 3. Link CauldronTemperatureLabel to a TMP text placed in the middle of the cauldron art.
// 4. AiAgentBehaviour calls ShowFeedback() and ShowCauldronTemperature() by itself, no wiring needed.
