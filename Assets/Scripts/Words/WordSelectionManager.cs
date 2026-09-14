// Spawns every word into one shared panel, switches between the three word pages, and builds the sentence.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


namespace NTGD124
{
    public class WordSelectionManager : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)][SerializeField] private string _componentDescription =
            "Fills one shared panel with every clickable word container. The three numbered buttons on the " +
            "right of that panel switch which page is showing: 1 is action words, 2 is tools, 3 is " +
            "ingredients. Clicking a word greys it out and copies it into the output panel, building the " +
            "clunky sentence the player sends to the agent.";


        ///// Public Variables /////

        [Header("Singleton Access")]
        public static WordSelectionManager Singleton;

        [Header("Panel That Holds Every Word Container")]
        public RectTransform WordListParent;

        [Header("The Three Numbered Page Buttons")]
        public Button ActionsPageButton;
        public Button ToolsPageButton;
        public Button IngredientsPageButton;

        [Header("Page Button Colours")]
        public Color ActivePageColour = new Color(0.95f, 0.8f, 0.35f, 1f);
        public Color RestingPageColour = new Color(0.55f, 0.55f, 0.6f, 1f);

        [Header("Panel That Shows The Built Sentence")]
        public RectTransform OutputWordsParent;
        public TMP_Text OutputSentenceLabel;

        [Header("Prototype Container Look")]
        public Vector2 WordContainerSize = new Vector2(130f, 34f);
        public float WordFontSize = 18f;

        [Header("Which Page Opens First")]
        public int StartingPageNumber = 1;

        [Header("Rotational Selection")]
        public int ActionsPerRound = 3;
        public int ToolsPerRound = 3;

        [Header("Events")]
        public UnityEvent OnSentenceChanged = new UnityEvent();


        ///// Private Variables /////

        // Every container that was spawned, kept so they can all be reset and paged.
        private List<WordButtonBehaviour> _spawnedWordButtons = new List<WordButtonBehaviour>();

        // The words the player has picked, in the order they were picked.
        private List<WordButtonBehaviour> _selectedWordButtons = new List<WordButtonBehaviour>();

        // The output copies, kept so they can be destroyed when the sentence is cleared.
        private List<GameObject> _outputWordObjects = new List<GameObject>();

        // Which page is on screen right now: 1 actions, 2 tools, 3 ingredients.
        private int _currentPageNumber = 1;

        // Tracks which actions and tools are currently shown (for rotation).
        private List<WordButtonBehaviour> _currentlyShownActionButtons = new List<WordButtonBehaviour>();
        private List<WordButtonBehaviour> _currentlyShownToolButtons = new List<WordButtonBehaviour>();

        // Tracks which ingredients are required for the current potion.
        private List<string> _requiredIngredientTexts = new List<string>();


        ///// Unity Methods /////

        private void Awake()
        {
            Singleton = this;
        }


        private void Start()
        {
            SpawnAllWordContainers();
            RotateActionsAndTools();

            // If no recipe has been set yet (game start), show all ingredients as available
            if (_requiredIngredientTexts.Count == 0)
            {
                SetAllIngredientsAsRequired();
            }

            ShowPage(StartingPageNumber);
        }


        // Action method: marks all ingredients as required (used at game start).
        private void SetAllIngredientsAsRequired()
        {
            _requiredIngredientTexts.Clear();

            for (int i = 0; i < _spawnedWordButtons.Count; i++)
            {
                if (_spawnedWordButtons[i] != null && _spawnedWordButtons[i].PageNumber == 3)
                {
                    _requiredIngredientTexts.Add(_spawnedWordButtons[i].WordText);
                }
            }
        }


        ///// Custom Methods - Spawning /////

        // Action method: fills the shared panel with every word in the game.
        public void SpawnAllWordContainers()
        {
            ClearSpawnedContainers();

            if (WordDatabase.Singleton == null)
            {
                Debug.LogError("[WordSelectionManager] No WordDatabase in the scene, cannot spawn words.");
                return;
            }

            if (WordListParent == null)
            {
                Debug.LogError("[WordSelectionManager] WordListParent is not linked, cannot spawn words.");
                return;
            }

            // Page 1 is the action words, page 2 the tools, page 3 the ingredients.
            SpawnWordList(WordDatabase.Singleton.ActionWords, 1);
            SpawnWordList(WordDatabase.Singleton.ToolWords, 2);
            SpawnWordList(WordDatabase.Singleton.IngredientWords, 3);

            Debug.Log("[WordSelectionManager] Spawned " + _spawnedWordButtons.Count + " word containers.");
        }


        // Action method: spawns one list of words and marks them all as belonging to one page.
        private void SpawnWordList(List<WordDefinition> wordList, int pageNumber)
        {
            for (int i = 0; i < wordList.Count; i++)
            {
                WordButtonBehaviour spawnedButton = CreateWordContainer(wordList[i], pageNumber);
                _spawnedWordButtons.Add(spawnedButton);
            }
        }


        // Action method: builds one clickable container from plain UI parts, no prefab needed.
        private WordButtonBehaviour CreateWordContainer(WordDefinition wordDefinition, int pageNumber)
        {
            GameObject containerObject = new GameObject("Word_" + wordDefinition.WordText, typeof(RectTransform));
            containerObject.transform.SetParent(WordListParent, false);

            RectTransform containerRect = containerObject.GetComponent<RectTransform>();
            containerRect.sizeDelta = WordContainerSize;

            Image backgroundImage = containerObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.85f, 0.85f, 0.85f, 1f);

            Button wordButton = containerObject.AddComponent<Button>();
            wordButton.targetGraphic = backgroundImage;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(containerObject.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI wordLabel = labelObject.AddComponent<TextMeshProUGUI>();
            wordLabel.text = wordDefinition.WordText;
            wordLabel.fontSize = WordFontSize;
            wordLabel.color = Color.black;
            wordLabel.alignment = TextAlignmentOptions.Center;
            wordLabel.enableAutoSizing = true;
            wordLabel.fontSizeMin = 8f;
            wordLabel.fontSizeMax = WordFontSize;

            // The label must not swallow the click, otherwise the container underneath never hears it.
            wordLabel.raycastTarget = false;

            WordButtonBehaviour wordBehaviour = containerObject.AddComponent<WordButtonBehaviour>();
            wordBehaviour.WordButton = wordButton;
            wordBehaviour.BackgroundImage = backgroundImage;
            wordBehaviour.WordLabel = wordLabel;
            wordBehaviour.SetupWord(wordDefinition, this, pageNumber);

            return wordBehaviour;
        }


        // Action method: destroys every spawned container so the panel can be rebuilt.
        private void ClearSpawnedContainers()
        {
            for (int i = 0; i < _spawnedWordButtons.Count; i++)
            {
                if (_spawnedWordButtons[i] != null)
                {
                    Destroy(_spawnedWordButtons[i].gameObject);
                }
            }

            _spawnedWordButtons.Clear();
        }


        ///// Custom Methods - Trigger Methods /////

        // Input-based trigger: wired to the button numbered 1.
        public void ShowActionWords()
        {
            ShowPage(1);
        }


        // Input-based trigger: wired to the button numbered 2.
        public void ShowToolWords()
        {
            ShowPage(2);
        }


        // Input-based trigger: wired to the button numbered 3.
        public void ShowIngredientWords()
        {
            ShowPage(3);
        }


        // Input-based trigger: called by a WordButtonBehaviour when the player clicks it.
        // Clicking a word adds it; clicking it again removes it (toggle behavior).
        public void AddWordToOutput(WordButtonBehaviour clickedWordButton)
        {
            // Check if word is already selected
            int existingIndex = _selectedWordButtons.IndexOf(clickedWordButton);

            if (existingIndex >= 0)
            {
                // Word is already selected, so deselect it
                RemoveWordFromOutput(clickedWordButton, existingIndex);
            }
            else
            {
                // Word is not selected, so select it
                clickedWordButton.SetUsedState(true);
                _selectedWordButtons.Add(clickedWordButton);

                CreateOutputCopy(clickedWordButton);
                RefreshSentenceLabel();

                Debug.Log("[WordSelectionManager] Sentence is now: " + GetSentenceText());
                OnSentenceChanged.Invoke();
            }
        }


        // Action method: removes a word from the selected list (for deselection).
        private void RemoveWordFromOutput(WordButtonBehaviour wordButton, int indexInSelectedList)
        {
            wordButton.SetUsedState(false);
            _selectedWordButtons.RemoveAt(indexInSelectedList);

            // Find and destroy the output copy
            for (int i = 0; i < _outputWordObjects.Count; i++)
            {
                if (_outputWordObjects[i] != null &&
                    _outputWordObjects[i].name == "Chosen_" + wordButton.WordText)
                {
                    Destroy(_outputWordObjects[i]);
                    _outputWordObjects.RemoveAt(i);
                    break;
                }
            }

            RefreshSentenceLabel();

            Debug.Log("[WordSelectionManager] Sentence is now: " + GetSentenceText());
            OnSentenceChanged.Invoke();
        }


        // Input-based trigger: wired to the Clear button so the player can start the sentence again.
        public void ClearSentence()
        {
            for (int i = 0; i < _selectedWordButtons.Count; i++)
            {
                if (_selectedWordButtons[i] != null)
                {
                    _selectedWordButtons[i].SetUsedState(false);
                }
            }

            _selectedWordButtons.Clear();

            for (int i = 0; i < _outputWordObjects.Count; i++)
            {
                if (_outputWordObjects[i] != null)
                {
                    Destroy(_outputWordObjects[i]);
                }
            }

            _outputWordObjects.Clear();
            RefreshSentenceLabel();

            Debug.Log("[WordSelectionManager] Sentence cleared.");
            OnSentenceChanged.Invoke();
        }


        ///// Custom Methods - Action Methods /////

        // Action method: shows only the containers of one page and hides all the others.
        private void ShowPage(int pageNumber)
        {
            _currentPageNumber = pageNumber;

            for (int i = 0; i < _spawnedWordButtons.Count; i++)
            {
                if (_spawnedWordButtons[i] == null)
                {
                    continue;
                }

                bool shouldShow = false;

                // Page 1: Actions - show only randomly selected ones
                if (pageNumber == 1 && _spawnedWordButtons[i].PageNumber == 1)
                {
                    shouldShow = _currentlyShownActionButtons.Contains(_spawnedWordButtons[i]);
                }
                // Page 2: Tools - show only randomly selected ones
                else if (pageNumber == 2 && _spawnedWordButtons[i].PageNumber == 2)
                {
                    shouldShow = _currentlyShownToolButtons.Contains(_spawnedWordButtons[i]);
                }
                // Page 3: Ingredients - show only required ones
                else if (pageNumber == 3 && _spawnedWordButtons[i].PageNumber == 3)
                {
                    shouldShow = _requiredIngredientTexts.Contains(_spawnedWordButtons[i].WordText);
                }

                _spawnedWordButtons[i].gameObject.SetActive(shouldShow);
            }

            RefreshPageButtonColours();

            Debug.Log("[WordSelectionManager] Showing word page " + pageNumber + ".");
        }


        // Action method: lights up the numbered button of the page that is open.
        private void RefreshPageButtonColours()
        {
            PaintPageButton(ActionsPageButton, _currentPageNumber == 1);
            PaintPageButton(ToolsPageButton, _currentPageNumber == 2);
            PaintPageButton(IngredientsPageButton, _currentPageNumber == 3);
        }


        // Action method: paints one numbered button either active or resting.
        private void PaintPageButton(Button pageButton, bool isActivePage)
        {
            if (pageButton == null)
            {
                return;
            }

            Image buttonImage = pageButton.GetComponent<Image>();

            if (buttonImage != null)
            {
                buttonImage.color = isActivePage ? ActivePageColour : RestingPageColour;
            }
        }


        // Action method: puts a small read-only copy of the chosen word into the output panel.
        private void CreateOutputCopy(WordButtonBehaviour sourceWordButton)
        {
            if (OutputWordsParent == null)
            {
                return;
            }

            GameObject copyObject = new GameObject("Chosen_" + sourceWordButton.WordText, typeof(RectTransform));
            copyObject.transform.SetParent(OutputWordsParent, false);

            RectTransform copyRect = copyObject.GetComponent<RectTransform>();
            copyRect.sizeDelta = WordContainerSize;

            Image copyBackground = copyObject.AddComponent<Image>();
            copyBackground.color = new Color(0.95f, 0.9f, 0.55f, 1f);
            copyBackground.raycastTarget = false;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(copyObject.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI copyLabel = labelObject.AddComponent<TextMeshProUGUI>();
            copyLabel.text = sourceWordButton.WordText;
            copyLabel.fontSize = WordFontSize;
            copyLabel.color = Color.black;
            copyLabel.alignment = TextAlignmentOptions.Center;
            copyLabel.enableAutoSizing = true;
            copyLabel.fontSizeMin = 8f;
            copyLabel.fontSizeMax = WordFontSize;
            copyLabel.raycastTarget = false;

            _outputWordObjects.Add(copyObject);
        }


        // Action method: writes the current sentence into the label under the output panel.
        private void RefreshSentenceLabel()
        {
            if (OutputSentenceLabel != null)
            {
                OutputSentenceLabel.text = GetSentenceText();
            }
        }


        // Action method: returns the chosen words joined with slashes, the way the player reads them.
        public string GetSentenceText()
        {
            List<string> wordTexts = GetSelectedWordTexts();

            if (wordTexts.Count == 0)
            {
                return "(no words chosen)";
            }

            return string.Join(" / ", wordTexts);
        }


        // Action method: returns the chosen words as a plain list for the parser.
        public List<string> GetSelectedWordTexts()
        {
            List<string> wordTexts = new List<string>();

            for (int i = 0; i < _selectedWordButtons.Count; i++)
            {
                wordTexts.Add(_selectedWordButtons[i].WordText);
            }

            return wordTexts;
        }


        // Action method: returns how many words the player has chosen so far.
        public int GetSelectedWordCount()
        {
            return _selectedWordButtons.Count;
        }


        // Action method: picks a word by its written text, exactly as if the player had clicked it.
        // This allows keyboard or voice input to be added later without touching the buttons.
        public bool SelectWordByText(string wordText)
        {
            for (int i = 0; i < _spawnedWordButtons.Count; i++)
            {
                WordButtonBehaviour checkedButton = _spawnedWordButtons[i];

                if (checkedButton == null || checkedButton.IsUsed())
                {
                    continue;
                }

                if (checkedButton.WordText.ToLower() == wordText.ToLower().Trim())
                {
                    AddWordToOutput(checkedButton);
                    return true;
                }
            }

            Debug.LogWarning("[WordSelectionManager] No free container found for the word: " + wordText);
            return false;
        }


        // Action method: rotates the displayed actions and tools to a random subset.
        public void RotateActionsAndTools()
        {
            RotateActionButtons();
            RotateToolButtons();
        }


        // Action method: randomly selects a subset of action buttons to show.
        private void RotateActionButtons()
        {
            _currentlyShownActionButtons.Clear();

            List<WordButtonBehaviour> allActions = new List<WordButtonBehaviour>();

            for (int i = 0; i < _spawnedWordButtons.Count; i++)
            {
                if (_spawnedWordButtons[i] != null && _spawnedWordButtons[i].PageNumber == 1)
                {
                    allActions.Add(_spawnedWordButtons[i]);
                }
            }

            // Shuffle and pick the first ActionsPerRound items
            ShuffleList(allActions);

            for (int i = 0; i < allActions.Count && i < ActionsPerRound; i++)
            {
                _currentlyShownActionButtons.Add(allActions[i]);
            }

            Debug.Log("[WordSelectionManager] Rotated action buttons to: " + string.Join(", ", _currentlyShownActionButtons.ConvertAll(x => x.WordText)));
        }


        // Action method: randomly selects a subset of tool buttons to show.
        private void RotateToolButtons()
        {
            _currentlyShownToolButtons.Clear();

            List<WordButtonBehaviour> allTools = new List<WordButtonBehaviour>();

            for (int i = 0; i < _spawnedWordButtons.Count; i++)
            {
                if (_spawnedWordButtons[i] != null && _spawnedWordButtons[i].PageNumber == 2)
                {
                    allTools.Add(_spawnedWordButtons[i]);
                }
            }

            // Shuffle and pick the first ToolsPerRound items
            ShuffleList(allTools);

            for (int i = 0; i < allTools.Count && i < ToolsPerRound; i++)
            {
                _currentlyShownToolButtons.Add(allTools[i]);
            }

            Debug.Log("[WordSelectionManager] Rotated tool buttons to: " + string.Join(", ", _currentlyShownToolButtons.ConvertAll(x => x.WordText)));
        }


        // Action method: sets which ingredients are required for the current potion.
        public void SetRequiredIngredients(List<string> requiredIngredients)
        {
            _requiredIngredientTexts = new List<string>(requiredIngredients);
            Debug.Log("[WordSelectionManager] Required ingredients are now: " + string.Join(", ", _requiredIngredientTexts));
        }


        // Action method: permanently locks the ingredient button for one word, so it can never be
        // picked again this round. Called once its raw ingredient is accepted in a processed form.
        public void LockIngredientButton(string ingredientName)
        {
            for (int i = 0; i < _spawnedWordButtons.Count; i++)
            {
                WordButtonBehaviour checkedButton = _spawnedWordButtons[i];

                if (checkedButton != null && checkedButton.PageNumber == 3 &&
                    checkedButton.WordText.ToLower() == ingredientName.ToLower())
                {
                    checkedButton.SetLocked(true);
                    return;
                }
            }
        }


        // Action method: clears every ingredient lock, called at the start of a new round.
        public void UnlockAllIngredientButtons()
        {
            for (int i = 0; i < _spawnedWordButtons.Count; i++)
            {
                if (_spawnedWordButtons[i] != null && _spawnedWordButtons[i].PageNumber == 3)
                {
                    _spawnedWordButtons[i].SetLocked(false);
                }
            }
        }


        // Action method: shuffles a list in place using Fisher-Yates.
        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);

                T temp = list[i];
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }
    }
}


// Implementation steps:
// 1. Place this component on the GameSystems object.
// 2. Link WordListParent to the scrolling content inside WordSelectionContainer.
// 3. Link the three numbered buttons, and hook their onClick to ShowActionWords, ShowToolWords
//    and ShowIngredientWords in that order.
// 4. Link OutputWordsParent to the grid inside WordContainerOutput.
// 5. Hook the Clear button's onClick to ClearSentence().
// 6. Set ActionsPerRound and ToolsPerRound to the number of options you want visible at a time.
// 7. GameSequenceManager will automatically call RotateActionsAndTools and SetRequiredIngredients
//    at the start of each round.
