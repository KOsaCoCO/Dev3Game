// Data container describing one instruction the agent pulled out of the player's word sentence.

using System.Collections.Generic;
using UnityEngine;


namespace NTGD124
{
    [System.Serializable]
    public class CommandStep
    {
        ///// Public Variables /////

        [Header("What The Step Says")]
        public string Verb = "";

        // One action can carry several ingredients, because each word can only be clicked once.
        // Saying "put / salt / herbs / liver" is one step that puts all three things in.
        public List<string> Ingredients = new List<string>();

        public string Tool = "";
        public string Quality = "";
        public string Unit = "";
        public string SwitchState = "";

        // A value below zero means the player never said a number.
        public int Amount = -1;

        [Header("What The Step Is Missing")]
        public bool IsAmbiguous = false;
        public string AmbiguityReason = "";


        ///// Custom Methods - Reading /////

        // Action method: returns true when the player attached a usable number to this step.
        public bool HasAmount()
        {
            return Amount >= 0;
        }


        // Action method: returns true when at least one ingredient was named in this step.
        public bool HasIngredient()
        {
            return Ingredients.Count > 0;
        }


        // Action method: writes the ingredients out as one readable piece of text.
        public string GetIngredientsText()
        {
            if (Ingredients.Count == 0)
            {
                return "";
            }

            return string.Join(", ", Ingredients);
        }


        // Action method: writes the step out in a short readable line for the console and the UI.
        public string GetReadableText()
        {
            string readableText = Verb;

            if (HasIngredient())
            {
                readableText += " the " + GetIngredientsText();
            }

            if (!string.IsNullOrEmpty(Tool))
            {
                readableText += " with the " + Tool;
            }

            if (HasAmount())
            {
                readableText += " " + Amount;

                if (!string.IsNullOrEmpty(Unit))
                {
                    readableText += " " + Unit;
                }
            }

            if (!string.IsNullOrEmpty(Quality))
            {
                readableText += " (" + Quality + ")";
            }

            if (!string.IsNullOrEmpty(SwitchState))
            {
                readableText += " [" + SwitchState + "]";
            }

            return readableText.Trim();
        }
    }
}


// Implementation steps:
// 1. This file only holds data, it is not attached to a GameObject.
// 2. CommandParser fills a list of these from the player's chosen words.
// 3. AiAgentBehaviour walks that list one step at a time.
// 4. Ingredients is a list, so one verb can carry as many ingredients as the player names after it.
