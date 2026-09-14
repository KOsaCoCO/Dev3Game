// Data container describing a single selectable word and the category it belongs to.

using UnityEngine;


namespace NTGD124
{
    // The category tells the parser how a word should be read inside a command sentence.
    public enum WordCategory
    {
        Verb,        // an action the agent performs, such as "boil" or "stir"
        Quality,     // describes how an action is performed, such as "fast" or "gentle"
        Number,      // a bare amount, such as "10"
        Unit,        // the meaning of a number, such as "minute" or "liters"
        Switch,      // turns something on or off
        Tool,        // a physical tool on the tool stand
        Ingredient   // a physical ingredient on the ingredient stand
    }


    [System.Serializable]
    public class WordDefinition
    {
        ///// Public Variables /////

        [Header("Word Settings")]
        public string WordText = "";
        public WordCategory Category = WordCategory.Verb;


        ///// Custom Methods - Construction /////

        // Empty constructor so Unity can show the class in the Inspector.
        public WordDefinition()
        {
        }


        // Convenience constructor used when the default word lists are built in code.
        public WordDefinition(string wordText, WordCategory category)
        {
            WordText = wordText;
            Category = category;
        }
    }
}


// Implementation steps:
// 1. This file only holds data, it is not attached to a GameObject.
// 2. WordDatabase creates and stores lists of WordDefinition objects.
// 3. The Category value is what CommandParser uses to understand the player sentence.
