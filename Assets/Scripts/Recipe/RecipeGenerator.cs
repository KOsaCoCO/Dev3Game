// Builds a random customer order every round and shows it as a scrollable to-do list.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace NTGD124
{
    public class RecipeGenerator : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)][SerializeField] private string _componentDescription =
            "Generates a random order, such as 'Oreon wants a health potion'. The order lists only the " +
            "ingredients and how the cauldron must be kept, hot or cold, and for how long. Tools are left " +
            "out on purpose so the player can use any tool they like.";


        ///// Public Variables /////

        [Header("Singleton Access")]
        public static RecipeGenerator Singleton;

        [Header("UI References")]
        public TMP_Text RecipeLabel;
        public ScrollRect RecipeScrollView;

        [Header("Who Can Walk In")]
        public List<string> CustomerNames = new List<string> { "Oreon", "Mira", "Brannoc", "Selka", "Duvi" };

        [Header("What They Can Ask For")]
        public List<string> PotionTypes = new List<string> { "health potion", "stamina potion", "sleep potion", "courage potion", "clarity potion" };

        [Header("How Many Ingredients An Order Asks For")]
        public int MinimumIngredients = 3;
        public int MaximumIngredients = 5;

        [Header("How Long The Cauldron Must Be Held")]
        // These must be numbers the player can actually click in the action word list.
        public List<int> PossibleMinutes = new List<int> { 1, 2, 3, 4, 5, 10, 20, 30, 60 };

        [Header("How Often An Order Wants An Ingredient Left Raw Instead Of Processed")]
        [Range(0, 100)] public int RawIngredientChancePercent = 50;

        [Header("Current Order (read only at runtime)")]
        public PotionRecipe CurrentRecipe = new PotionRecipe();


        ///// Unity Methods /////

        private void Awake()
        {
            Singleton = this;
        }


        ///// Custom Methods - Trigger Methods /////

        // Called by GameSequenceManager at the start of every round.
        public void GenerateNewRecipe()
        {
            CurrentRecipe = BuildRandomRecipe();
            ShowRecipeOnScreen();

            Debug.Log("[RecipeGenerator] New order: " + CurrentRecipe.GetHeadlineText() +
                      " | ingredients: " + CurrentRecipe.RequiredIngredients.Count +
                      " | cauldron: " + CurrentRecipe.RequiredTemperature +
                      " for " + CurrentRecipe.RequiredMinutes + " minutes");
        }


        ///// Custom Methods - Action Methods /////

        // Action method: picks the random contents of one customer order.
        private PotionRecipe BuildRandomRecipe()
        {
            PotionRecipe newRecipe = new PotionRecipe();

            newRecipe.CustomerName = PickRandomText(CustomerNames, "Oreon");
            newRecipe.PotionType = PickRandomText(PotionTypes, "health potion");

            if (WordDatabase.Singleton == null)
            {
                Debug.LogError("[RecipeGenerator] No WordDatabase in the scene, order will be empty.");
                return newRecipe;
            }

            List<string> allIngredients = GetTextsFromDefinitions(WordDatabase.Singleton.IngredientWords);

            int ingredientCount = PickRandomCount(MinimumIngredients, MaximumIngredients, allIngredients.Count);
            List<string> pickedIngredientNames = PickRandomEntries(allIngredients, ingredientCount);
            newRecipe.RequiredIngredients = BuildRequiredStates(pickedIngredientNames);

            // A coin flip decides whether the cauldron has to run hot or cold.
            newRecipe.RequiredTemperature = Random.Range(0, 2) == 0 ? "hot" : "cold";
            newRecipe.RequiredMinutes = PickRandomMinutes();

            return newRecipe;
        }


        // Action method: returns one random entry of a list, or a fallback when the list is empty.
        private string PickRandomText(List<string> sourceList, string fallbackText)
        {
            if (sourceList == null || sourceList.Count == 0)
            {
                return fallbackText;
            }

            return sourceList[Random.Range(0, sourceList.Count)];
        }


        // Action method: turns a plain list of ingredient names into required states, each either
        // asking for the ingredient raw or for one of its authored processed variants.
        private List<RequiredIngredientState> BuildRequiredStates(List<string> ingredientNames)
        {
            List<RequiredIngredientState> requiredStates = new List<RequiredIngredientState>();

            for (int i = 0; i < ingredientNames.Count; i++)
            {
                requiredStates.Add(BuildOneRequiredState(ingredientNames[i]));
            }

            return requiredStates;
        }


        // Action method: decides whether one ingredient is wanted raw or in one of its known states.
        private RequiredIngredientState BuildOneRequiredState(string ingredientName)
        {
            RequiredIngredientState requiredState = new RequiredIngredientState();
            requiredState.IngredientName = ingredientName;

            List<(ToolArchetype Archetype, TimeBracket Bracket, string StateName)> possibleStates =
                IngredientOutcomeTable.GetPossibleStates(ingredientName);

            bool wantsRaw = possibleStates.Count == 0 || Random.Range(0, 100) < RawIngredientChancePercent;

            if (wantsRaw)
            {
                requiredState.RequiredArchetype = ToolArchetype.None;
                requiredState.RequiredTimeBracket = TimeBracket.Normal;
                requiredState.RequiredStateName = ingredientName;

                return requiredState;
            }

            (ToolArchetype archetype, TimeBracket bracket, string stateName) = possibleStates[Random.Range(0, possibleStates.Count)];

            requiredState.RequiredArchetype = archetype;
            requiredState.RequiredTimeBracket = bracket;
            requiredState.RequiredStateName = stateName;

            return requiredState;
        }


        // Action method: returns one of the times the player is able to click.
        private int PickRandomMinutes()
        {
            if (PossibleMinutes == null || PossibleMinutes.Count == 0)
            {
                return 10;
            }

            return PossibleMinutes[Random.Range(0, PossibleMinutes.Count)];
        }


        // Action method: chooses how many ingredients this order asks for, between the two limits.
        private int PickRandomCount(int smallest, int largest, int howManyExist)
        {
            // A largest of 0 or less means the whole list is allowed.
            int topLimit = largest > 0 ? Mathf.Min(largest, howManyExist) : howManyExist;
            int bottomLimit = Mathf.Clamp(smallest, 0, topLimit);

            return Random.Range(bottomLimit, topLimit + 1);
        }


        // Action method: copies the written text out of a list of word definitions.
        private List<string> GetTextsFromDefinitions(List<WordDefinition> definitionList)
        {
            List<string> resultTexts = new List<string>();

            for (int i = 0; i < definitionList.Count; i++)
            {
                resultTexts.Add(definitionList[i].WordText);
            }

            return resultTexts;
        }


        // Action method: takes a number of random entries out of a list without repeating any of them.
        private List<string> PickRandomEntries(List<string> sourceList, int howMany)
        {
            List<string> remainingEntries = new List<string>(sourceList);
            List<string> pickedEntries = new List<string>();

            // Never ask for more entries than the list actually holds.
            int safeCount = Mathf.Clamp(howMany, 0, remainingEntries.Count);

            for (int i = 0; i < safeCount; i++)
            {
                int randomIndex = Random.Range(0, remainingEntries.Count);
                pickedEntries.Add(remainingEntries[randomIndex]);
                remainingEntries.RemoveAt(randomIndex);
            }

            return pickedEntries;
        }


        // Action method: writes the order into the label and scrolls back to the top.
        private void ShowRecipeOnScreen()
        {
            if (RecipeLabel != null)
            {
                RecipeLabel.text = CurrentRecipe.GetRecipeAsToDoText();

                // The layout group around the label grows it to fit, so the scroll view knows
                // how far it can scroll once the mesh has been rebuilt.
                RecipeLabel.ForceMeshUpdate();
            }

            if (RecipeScrollView != null)
            {
                RecipeScrollView.verticalNormalizedPosition = 1f;
            }
        }
    }
}


// Implementation steps:
// 1. Place this component on the GameSystems object.
// 2. Link RecipeLabel to the TMP text inside RecipeContainer and RecipeScrollView to its ScrollRect.
// 3. GameSequenceManager calls GenerateNewRecipe() when a round starts.
// 4. Edit CustomerNames and PotionTypes in the Inspector to change who walks in and what they want.
// 5. Keep PossibleMinutes to numbers that exist as clickable words, otherwise the order cannot be filled.
// 6. RawIngredientChancePercent controls how often an order just wants an ingredient untouched versus
//    asking for one of its authored processed states (see IngredientOutcomeTable).
