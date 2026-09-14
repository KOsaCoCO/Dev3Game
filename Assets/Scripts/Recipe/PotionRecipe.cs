// Data container describing one customer order: who wants what, which ingredients, and the cauldron heat.

using System.Collections.Generic;
using UnityEngine;


namespace NTGD124
{
    // One line of the order: which ingredient, and what state it needs to be in. A raw request
    // (RequiredArchetype is None) just wants the ingredient as-is, anything else names a specific
    // archetype and time bracket pulled from IngredientOutcomeTable's authored states.
    [System.Serializable]
    public class RequiredIngredientState
    {
        public string IngredientName = "";
        public ToolArchetype RequiredArchetype = ToolArchetype.None;
        public TimeBracket RequiredTimeBracket = TimeBracket.Normal;
        public string RequiredStateName = "";

        // Action method: true when the order just wants the plain ingredient, untouched.
        public bool WantsRaw()
        {
            return RequiredArchetype == ToolArchetype.None;
        }
    }


    [System.Serializable]
    public class PotionRecipe
    {
        ///// Public Variables /////

        [Header("Who Is Asking")]
        public string CustomerName = "Oreon";
        public string PotionType = "health potion";

        [Header("What The Potion Needs")]
        public List<RequiredIngredientState> RequiredIngredients = new List<RequiredIngredientState>();

        [Header("How The Cauldron Must Be Kept")]
        // Either "hot" or "cold". Tools are not listed on purpose, the player may use any tool.
        public string RequiredTemperature = "hot";
        public int RequiredMinutes = 10;


        ///// Custom Methods - Reading /////

        // Action method: returns the headline the player reads at the top of the order.
        public string GetHeadlineText()
        {
            return CustomerName + " wants a " + PotionType;
        }


        // Action method: returns how many separate things the player has to get right.
        public int GetTotalRequirementCount()
        {
            // Every ingredient counts once, and the cauldron temperature counts as one more.
            return RequiredIngredients.Count + 1;
        }


        // Action method: writes the order out as the readable to-do list shown on screen.
        public string GetRecipeAsToDoText()
        {
            string recipeText = "<b>" + GetHeadlineText() + "</b>\n\n";

            recipeText += "<b>Ingredients needed:</b>\n";

            for (int i = 0; i < RequiredIngredients.Count; i++)
            {
                recipeText += (i + 1) + ". " + RequiredIngredients[i].RequiredStateName + "\n";
            }

            recipeText += "\n<b>Cauldron:</b>\n";
            recipeText += "Keep it " + RequiredTemperature.ToUpper() + "\n";
            recipeText += "for " + RequiredMinutes + " minutes\n";

            recipeText += "\n<i>Use whichever tools you like.</i>";

            return recipeText;
        }
    }
}


// Implementation steps:
// 1. This file only holds data, it is not attached to a GameObject.
// 2. RecipeGenerator creates one of these every round, picking a RequiredIngredientState for each
//    ingredient from IngredientOutcomeTable's authored states (or a raw request).
// 3. GameSequenceManager scores each requirement by how close the delivered state is, not just by
//    whether the ingredient showed up at all.
// 4. Tools are deliberately not named directly, RequiredArchetype only implies which kind of tool
//    was needed, so the player is still free to pick between tools of the same archetype.
