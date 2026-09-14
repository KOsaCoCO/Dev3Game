// Data container for whatever actually came out of the cauldron, once the player has given it a name.

using System.Collections.Generic;
using UnityEngine;


namespace NTGD124
{
    [System.Serializable]
    public class BrewedPotion
    {
        ///// Public Variables /////

        [Header("What The Player Called It")]
        public string PlayerGivenName = "";

        [Header("What Was Actually Ordered")]
        public string OrderedHeadline = "";

        [Header("What Came Out")]
        public List<string> ContainedIngredients = new List<string>();
        public List<string> UsedTools = new List<string>();

        [Header("How It Turned Out")]
        [Range(0f, 1f)] public float MatchScore = 0f;
        [Range(0f, 1f)] public float QualityScore = 0f;
        public bool WasAccepted = false;
        public bool HasSideEffects = false;


        ///// Custom Methods - Reading /////

        // Action method: writes the brewed potion out as a readable line for the console and the UI.
        public string GetLabelText()
        {
            string statusWord = WasAccepted ? "accepted" : "rejected";

            string labelText = "\"" + PlayerGivenName + "\"\n";
            labelText += "ordered as: " + OrderedHeadline + "\n";
            labelText += "match " + Mathf.RoundToInt(MatchScore * 100f) + "%, ";
            labelText += "quality " + Mathf.RoundToInt(QualityScore * 100f) + "% - " + statusWord;

            if (HasSideEffects)
            {
                labelText += " (with sideeffects)";
            }

            return labelText;
        }
    }
}


// Implementation steps:
// 1. This file only holds data, it is not attached to a GameObject.
// 2. GameSequenceManager creates one of these every time the player names what the cauldron produced.
// 3. The list of brewed potions is kept on GameSequenceManager so the shelf can show them later.
