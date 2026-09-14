// Holds the master registry of every word the player can use and answers lookups about them.

using System.Collections.Generic;
using UnityEngine;


namespace NTGD124
{
    public class WordDatabase : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)][SerializeField] private string _componentDescription =
            "Master list of all action words, tools and ingredients. Other scripts ask this component " +
            "which category a word belongs to and which words exist. Press 'Reset To Default Words' " +
            "in the context menu to rebuild the lists.";


        ///// Public Variables /////

        [Header("Singleton Access")]
        public static WordDatabase Singleton;

        [Header("Action Words (verbs, qualities, numbers, units, switches)")]
        public List<WordDefinition> ActionWords = new List<WordDefinition>();

        [Header("Tool Words")]
        public List<WordDefinition> ToolWords = new List<WordDefinition>();

        [Header("Ingredient Words")]
        public List<WordDefinition> IngredientWords = new List<WordDefinition>();


        ///// Private Variables /////

        // Fast lookup from the written word to its full definition.
        private Dictionary<string, WordDefinition> _wordLookup = new Dictionary<string, WordDefinition>();


        ///// Unity Methods /////

        private void Awake()
        {
            Singleton = this;

            // If nobody filled the lists in the Inspector we build the default ones.
            if (ActionWords.Count == 0 || ToolWords.Count == 0 || IngredientWords.Count == 0)
            {
                BuildDefaultWordLists();
            }

            BuildWordLookup();
        }


        ///// Custom Methods - Word List Building /////

        // Action method: fills the three public lists with the words agreed for the prototype.
        [ContextMenu("Reset To Default Words")]
        public void BuildDefaultWordLists()
        {
            ActionWords.Clear();
            ToolWords.Clear();
            IngredientWords.Clear();

            // Verbs describe what the agent should physically do.
            AddActionWord("cook", WordCategory.Verb);
            AddActionWord("pull", WordCategory.Verb);
            AddActionWord("put", WordCategory.Verb);
            AddActionWord("cut", WordCategory.Verb);
            AddActionWord("peel", WordCategory.Verb);
            AddActionWord("stir", WordCategory.Verb);
            AddActionWord("pour", WordCategory.Verb);
            AddActionWord("mix", WordCategory.Verb);
            AddActionWord("wait", WordCategory.Verb);
            AddActionWord("blend", WordCategory.Verb);
            AddActionWord("heat", WordCategory.Verb);

            // A deliberate no-op: paired with an ingredient it just leaves it raw, no tool or time
            // ever gets guessed for it. See IngredientProcessor.IsTriadEligible.
            AddActionWord("do nothing", WordCategory.Verb);

            // Qualities describe how well or how strongly an action is performed.
            AddActionWord("hard", WordCategory.Quality);
            AddActionWord("fast", WordCategory.Quality);
            AddActionWord("slow", WordCategory.Quality);
            AddActionWord("gentle", WordCategory.Quality);
            AddActionWord("long", WordCategory.Quality);
            AddActionWord("short", WordCategory.Quality);
            AddActionWord("small", WordCategory.Quality);
            AddActionWord("big", WordCategory.Quality);
            AddActionWord("hot", WordCategory.Quality);
            AddActionWord("cool", WordCategory.Quality);
            AddActionWord("cold", WordCategory.Quality);

            // Numbers are meaningless on their own, they need a unit next to them.
            AddActionWord("1", WordCategory.Number);
            AddActionWord("2", WordCategory.Number);
            AddActionWord("3", WordCategory.Number);
            AddActionWord("4", WordCategory.Number);
            AddActionWord("5", WordCategory.Number);
            AddActionWord("10", WordCategory.Number);
            AddActionWord("20", WordCategory.Number);
            AddActionWord("30", WordCategory.Number);
            AddActionWord("60", WordCategory.Number);

            // Units give a number its meaning.
            AddActionWord("time", WordCategory.Unit);
            AddActionWord("minute", WordCategory.Unit);
            AddActionWord("hour", WordCategory.Unit);
            AddActionWord("liters", WordCategory.Unit);
            AddActionWord("centimeters", WordCategory.Unit);
            AddActionWord("millimeters", WordCategory.Unit);

            // Switches turn the heat source on or off.
            AddActionWord("on", WordCategory.Switch);
            AddActionWord("off", WordCategory.Switch);

            // Tools live on the tool stand.
            AddToolWord("cauldron");
            AddToolWord("blender");
            AddToolWord("knife");
            AddToolWord("peeler");
            AddToolWord("scissors");
            AddToolWord("chopping board");
            AddToolWord("hammer");
            AddToolWord("spoon");
            AddToolWord("ladel");

            // Ingredients live on the ingredient stand.
            AddIngredientWord("apple");
            AddIngredientWord("carrot");
            AddIngredientWord("watermelon");
            AddIngredientWord("tree leaves");
            AddIngredientWord("herbs");
            AddIngredientWord("fruits");
            AddIngredientWord("cow tongue");
            AddIngredientWord("chicken leg");
            AddIngredientWord("liver");
            AddIngredientWord("milk");
            AddIngredientWord("honey");
            AddIngredientWord("tree root");
            AddIngredientWord("acid flower");
            AddIngredientWord("crystal");
            AddIngredientWord("salt");

            Debug.Log("[WordDatabase] Default word lists built. Actions: " + ActionWords.Count +
                      " Tools: " + ToolWords.Count + " Ingredients: " + IngredientWords.Count);
        }


        // Action method: adds one action word to the action list.
        private void AddActionWord(string wordText, WordCategory category)
        {
            ActionWords.Add(new WordDefinition(wordText, category));
        }


        // Action method: adds one tool word to the tool list.
        private void AddToolWord(string wordText)
        {
            ToolWords.Add(new WordDefinition(wordText, WordCategory.Tool));
        }


        // Action method: adds one ingredient word to the ingredient list.
        private void AddIngredientWord(string wordText)
        {
            IngredientWords.Add(new WordDefinition(wordText, WordCategory.Ingredient));
        }


        // Action method: rebuilds the fast lookup dictionary from the three public lists.
        private void BuildWordLookup()
        {
            _wordLookup.Clear();

            AddListToLookup(ActionWords);
            AddListToLookup(ToolWords);
            AddListToLookup(IngredientWords);
        }


        // Action method: copies one list into the lookup dictionary.
        private void AddListToLookup(List<WordDefinition> wordList)
        {
            for (int i = 0; i < wordList.Count; i++)
            {
                string keyText = wordList[i].WordText.ToLower();

                if (!_wordLookup.ContainsKey(keyText))
                {
                    _wordLookup.Add(keyText, wordList[i]);
                }
            }
        }


        ///// Custom Methods - Lookup /////

        // Action method: returns the definition of a written word, or null when the word is unknown.
        public WordDefinition GetWordDefinition(string wordText)
        {
            if (_wordLookup.Count == 0)
            {
                BuildWordLookup();
            }

            string keyText = wordText.ToLower().Trim();

            if (_wordLookup.ContainsKey(keyText))
            {
                return _wordLookup[keyText];
            }

            return null;
        }


        // Action method: returns true when the written word exists in the registry.
        public bool IsKnownWord(string wordText)
        {
            return GetWordDefinition(wordText) != null;
        }


        // Action method: returns every word of one category as plain text.
        public List<string> GetWordTextsOfCategory(WordCategory category)
        {
            List<string> resultTexts = new List<string>();

            AddMatchingTexts(ActionWords, category, resultTexts);
            AddMatchingTexts(ToolWords, category, resultTexts);
            AddMatchingTexts(IngredientWords, category, resultTexts);

            return resultTexts;
        }


        // Action method: helper that copies matching words of one list into the result list.
        private void AddMatchingTexts(List<WordDefinition> sourceList, WordCategory category, List<string> resultTexts)
        {
            for (int i = 0; i < sourceList.Count; i++)
            {
                if (sourceList[i].Category == category)
                {
                    resultTexts.Add(sourceList[i].WordText);
                }
            }
        }
    }
}


// Implementation steps:
// 1. Put this component on a manager GameObject in the scene (the scene builder calls it "GameSystems").
// 2. On Awake the three lists fill themselves with the default prototype words.
// 3. Edit the lists in the Inspector to add or remove words, or use the context menu to reset them.
// 4. Other scripts reach this component through WordDatabase.Singleton.
