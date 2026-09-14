// Editor tool that builds the whole prototype UI inside the placement map, in one click.

using LLMUnity;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


namespace NTGD124
{
    public class PotionGameSceneBuilder : EditorWindow
    {
        ///// Private Variables /////

        // The canvas that holds the placement map drawn by hand.
        private Canvas _placementCanvas;

        // The object that ends up holding every manager component.
        private GameObject _gameSystemsObject;

        // Components created during the build, kept so they can be linked to each other at the end.
        private WordDatabase _wordDatabase;
        private WordSelectionManager _wordSelectionManager;
        private CommandParser _commandParser;
        private UncertaintyRandomizer _uncertaintyRandomizer;
        private IngredientProcessor _ingredientProcessor;
        private LlmCommandAdvisor _llmCommandAdvisor;
        private AiAgentBehaviour _aiAgent;
        private RecipeGenerator _recipeGenerator;
        private FeedbackVisualizer _feedbackVisualizer;
        private GameSequenceManager _gameSequenceManager;
        private LeaderboardManager _leaderboardManager;
        private LeaderboardUIManager _leaderboardUiManager;

        // Panels built during the run, linked into the sequence manager at the end.
        private GameObject _namingPanel;
        private GameObject _resultPanel;
        private GameObject _menuPanel;

        // Every object this tool creates gets this prefix so a rebuild can clean up after itself.
        private const string BuiltObjectPrefix = "Built_";


        ///// Unity Methods /////

        [MenuItem("NTGD124/Build Potion Game Scene")]
        public static void ShowBuilderWindow()
        {
            PotionGameSceneBuilder window = GetWindow<PotionGameSceneBuilder>("Potion Game Builder");
            window.minSize = new Vector2(360f, 220f);
        }


        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Potion Game Scene Builder", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "This builds every panel, button and text inside the UI-PlacementObj canvas and links all " +
                "the manager components together.\n\nRunning it again wipes what it built last time and " +
                "builds it fresh. Your own placement objects are never deleted.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Build Scene Now", GUILayout.Height(36f)))
            {
                BuildWholeScene();
            }

            EditorGUILayout.Space(6);

            if (GUILayout.Button("Clear What The Builder Made", GUILayout.Height(24f)))
            {
                ClearPreviousBuild();
            }
        }


        ///// Custom Methods - Trigger Methods /////

        // Editor-based trigger: called by the Build Scene Now button.
        private void BuildWholeScene()
        {
            _placementCanvas = FindPlacementCanvas();

            if (_placementCanvas == null)
            {
                EditorUtility.DisplayDialog("Builder",
                    "No object called UI-PlacementObj with a Canvas was found in the open scene.", "OK");
                return;
            }

            ClearPreviousBuild();

            CreateGameSystemsObject();
            CreateEventSystemIfMissing();

            BuildRecipePanel();
            BuildCauldronPanel();
            BuildWordPanels();
            BuildAgentPanel();
            BuildOutputPanel();
            BuildMenuPanel();
            BuildNamingPanel();
            BuildResultPanel();
            BuildLanguageModelObjects();

            LinkEverythingTogether();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[PotionGameSceneBuilder] Build finished. Save the scene to keep it.");

            EditorUtility.DisplayDialog("Builder",
                "The scene was built.\n\nNext: select the LLM object and download a small model from its " +
                "Inspector, then press Play.", "OK");
        }


        // Editor-based trigger: called by the Clear button, and at the start of every build.
        private void ClearPreviousBuild()
        {
            if (_placementCanvas == null)
            {
                _placementCanvas = FindPlacementCanvas();
            }

            if (_placementCanvas == null)
            {
                return;
            }

            // Everything the builder made carries the prefix, so only those objects are removed.
            Transform[] allChildren = _placementCanvas.GetComponentsInChildren<Transform>(true);

            for (int i = allChildren.Length - 1; i >= 0; i--)
            {
                if (allChildren[i] != null && allChildren[i].name.StartsWith(BuiltObjectPrefix))
                {
                    DestroyImmediate(allChildren[i].gameObject);
                }
            }

            GameObject oldSystems = GameObject.Find(BuiltObjectPrefix + "GameSystems");
            if (oldSystems != null)
            {
                DestroyImmediate(oldSystems);
            }

            GameObject oldModel = GameObject.Find(BuiltObjectPrefix + "LLM");
            if (oldModel != null)
            {
                DestroyImmediate(oldModel);
            }
        }


        ///// Custom Methods - Finding Existing Objects /////

        // Action method: finds the hand-made placement canvas in the open scene.
        private Canvas FindPlacementCanvas()
        {
            GameObject placementObject = GameObject.Find("UI-PlacementObj");

            if (placementObject == null)
            {
                return null;
            }

            return placementObject.GetComponent<Canvas>();
        }


        // Action method: finds one of the hand-placed panels by name.
        private RectTransform FindPlacementPanel(string panelName)
        {
            Transform[] allChildren = _placementCanvas.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < allChildren.Length; i++)
            {
                if (allChildren[i].name == panelName)
                {
                    return allChildren[i] as RectTransform;
                }
            }

            Debug.LogWarning("[PotionGameSceneBuilder] Could not find a placement panel called " + panelName);
            return null;
        }


        ///// Custom Methods - Building The Managers /////

        // Action method: creates the object that carries every manager component.
        private void CreateGameSystemsObject()
        {
            _gameSystemsObject = new GameObject(BuiltObjectPrefix + "GameSystems");

            _wordDatabase = _gameSystemsObject.AddComponent<WordDatabase>();
            _wordDatabase.BuildDefaultWordLists();

            _commandParser = _gameSystemsObject.AddComponent<CommandParser>();
            _uncertaintyRandomizer = _gameSystemsObject.AddComponent<UncertaintyRandomizer>();
            _ingredientProcessor = _gameSystemsObject.AddComponent<IngredientProcessor>();
            _wordSelectionManager = _gameSystemsObject.AddComponent<WordSelectionManager>();
            _recipeGenerator = _gameSystemsObject.AddComponent<RecipeGenerator>();
            _feedbackVisualizer = _gameSystemsObject.AddComponent<FeedbackVisualizer>();
            _leaderboardManager = _gameSystemsObject.AddComponent<LeaderboardManager>();
            _leaderboardUiManager = _gameSystemsObject.AddComponent<LeaderboardUIManager>();
            _gameSequenceManager = _gameSystemsObject.AddComponent<GameSequenceManager>();
        }


        // Action method: makes sure the scene has an EventSystem, otherwise no button can be clicked.
        private void CreateEventSystemIfMissing()
        {
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }


        ///// Custom Methods - Building The Panels /////

        // Action method: turns RecipeContainer into a scrollable to-do list.
        private void BuildRecipePanel()
        {
            RectTransform panel = FindPlacementPanel("RecipeContainer");

            if (panel == null)
            {
                return;
            }

            AddPanelTitle(panel, "TODAY'S ORDER");

            ScrollRect scrollView = CreateScrollView(panel, "RecipeScroll", out RectTransform contentRect);

            VerticalLayoutGroup contentLayout = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.childForceExpandHeight = false;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.padding = new RectOffset(8, 8, 8, 8);

            ContentSizeFitter contentFitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text recipeLabel = CreateLabel(contentRect, "RecipeText", "", 20f, TextAlignmentOptions.TopLeft, Color.black);

            _recipeGenerator.RecipeLabel = recipeLabel;
            _recipeGenerator.RecipeScrollView = scrollView;
        }


        // Action method: turns Cauldron into the feedback panel that flashes green or red.
        private void BuildCauldronPanel()
        {
            RectTransform panel = FindPlacementPanel("Cauldron");

            if (panel == null)
            {
                return;
            }

            AddPanelTitle(panel, "CAULDRON");

            GameObject flashObject = CreateStretchedChild(panel, "FeedbackFlash");
            Image flashImage = flashObject.AddComponent<Image>();
            flashImage.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            flashImage.raycastTarget = false;

            TMP_Text flashLabel = CreateLabel(flashObject.GetComponent<RectTransform>(), "FeedbackText",
                "", 22f, TextAlignmentOptions.Center, Color.white);
            StretchToParent(flashLabel.rectTransform);

            _feedbackVisualizer.FeedbackPanel = flashImage;
            _feedbackVisualizer.FeedbackLabel = flashLabel;
        }


        // Action method: turns the three word panels into scrollable grids of clickable containers.
        private void BuildWordPanels()
        {
            BuildSharedWordPanel();

            // The two old stands keep only a title. Pictures for the agent go in here later.
            BuildEmptyStandPanel("PotionStandAiTools", "TOOL SHELF");
            BuildEmptyStandPanel("PotionStandAiIngredients", "INGREDIENT SHELF");
        }


        // Action method: turns WordSelectionContainer into one shared word list with three page buttons.
        private void BuildSharedWordPanel()
        {
            RectTransform panel = FindPlacementPanel("WordSelectionContainer");

            if (panel == null)
            {
                return;
            }

            AddPanelTitle(panel, "WORDS   (1 actions   2 tools   3 ingredients)");

            ScrollRect wordScroll = CreateScrollView(panel, "WordListScroll", out RectTransform contentRect);

            // The list fills the panel except for the strip on the right that holds the page buttons.
            RectTransform scrollRect = wordScroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(0.86f, 0.89f);
            scrollRect.offsetMin = new Vector2(4f, 4f);
            scrollRect.offsetMax = new Vector2(-4f, 0f);

            GridLayoutGroup contentGrid = contentRect.gameObject.AddComponent<GridLayoutGroup>();
            contentGrid.cellSize = new Vector2(110f, 32f);
            contentGrid.spacing = new Vector2(4f, 4f);
            contentGrid.padding = new RectOffset(6, 6, 6, 6);
            contentGrid.constraint = GridLayoutGroup.Constraint.Flexible;

            ContentSizeFitter contentFitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // The three numbered buttons sit in a line down the right hand edge of the panel.
            Button actionsButton = CreatePageButton(panel, "PageOneButton", "1", 0.60f, 0.87f);
            Button toolsButton = CreatePageButton(panel, "PageTwoButton", "2", 0.31f, 0.58f);
            Button ingredientsButton = CreatePageButton(panel, "PageThreeButton", "3", 0.02f, 0.29f);

            _wordSelectionManager.WordListParent = contentRect;
            _wordSelectionManager.ActionsPageButton = actionsButton;
            _wordSelectionManager.ToolsPageButton = toolsButton;
            _wordSelectionManager.IngredientsPageButton = ingredientsButton;

            AddClickListener(actionsButton, _wordSelectionManager.ShowActionWords);
            AddClickListener(toolsButton, _wordSelectionManager.ShowToolWords);
            AddClickListener(ingredientsButton, _wordSelectionManager.ShowIngredientWords);
        }


        // Action method: builds one of the numbered buttons on the right edge of the word panel.
        private Button CreatePageButton(RectTransform panel, string buttonName, string numberText,
                                        float bottomShare, float topShare)
        {
            Button pageButton = CreateButton(panel, buttonName, numberText, new Color(0.55f, 0.55f, 0.6f));
            SetAnchors(pageButton.GetComponent<RectTransform>(), 0.88f, bottomShare, 0.98f, topShare);

            return pageButton;
        }


        // Action method: leaves an old stand panel empty, ready for the pictures that go in later.
        private void BuildEmptyStandPanel(string panelName, string titleText)
        {
            RectTransform panel = FindPlacementPanel(panelName);

            if (panel == null)
            {
                return;
            }

            AddPanelTitle(panel, titleText);
        }


        // Action method: turns WordContainerOutput into the sentence panel with the Send and Clear buttons.
        private void BuildOutputPanel()
        {
            RectTransform panel = FindPlacementPanel("WordContainerOutput");

            if (panel == null)
            {
                return;
            }

            AddPanelTitle(panel, "WHAT YOU ARE TELLING THE AGENT");

            // The chosen words sit in a grid that fills the upper part of the panel.
            GameObject gridObject = CreateStretchedChild(panel, "ChosenWords");
            RectTransform gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0f, 0.35f);
            gridRect.anchorMax = new Vector2(1f, 0.82f);
            gridRect.offsetMin = new Vector2(6f, 0f);
            gridRect.offsetMax = new Vector2(-6f, 0f);

            GridLayoutGroup chosenGrid = gridObject.AddComponent<GridLayoutGroup>();
            chosenGrid.cellSize = new Vector2(105f, 28f);
            chosenGrid.spacing = new Vector2(4f, 4f);
            chosenGrid.padding = new RectOffset(4, 4, 4, 4);

            // The full sentence is written out underneath in plain text.
            TMP_Text sentenceLabel = CreateLabel(panel, "SentenceText", "(no words chosen)", 16f,
                TextAlignmentOptions.Left, Color.black);
            RectTransform sentenceRect = sentenceLabel.rectTransform;
            sentenceRect.anchorMin = new Vector2(0f, 0.16f);
            sentenceRect.anchorMax = new Vector2(1f, 0.34f);
            sentenceRect.offsetMin = new Vector2(10f, 0f);
            sentenceRect.offsetMax = new Vector2(-10f, 0f);

            Button sendButton = CreateButton(panel, "SendButton", "SEND TO AGENT", new Color(0.3f, 0.6f, 0.9f));
            RectTransform sendRect = sendButton.GetComponent<RectTransform>();
            sendRect.anchorMin = new Vector2(0.04f, 0.03f);
            sendRect.anchorMax = new Vector2(0.5f, 0.15f);
            sendRect.offsetMin = Vector2.zero;
            sendRect.offsetMax = Vector2.zero;

            Button clearButton = CreateButton(panel, "ClearButton", "CLEAR", new Color(0.75f, 0.5f, 0.3f));
            RectTransform clearRect = clearButton.GetComponent<RectTransform>();
            clearRect.anchorMin = new Vector2(0.54f, 0.03f);
            clearRect.anchorMax = new Vector2(0.8f, 0.15f);
            clearRect.offsetMin = Vector2.zero;
            clearRect.offsetMax = Vector2.zero;

            Button menuButton = CreateButton(panel, "MenuButton", "MENU", new Color(0.45f, 0.45f, 0.5f));
            RectTransform menuRect = menuButton.GetComponent<RectTransform>();
            menuRect.anchorMin = new Vector2(0.82f, 0.03f);
            menuRect.anchorMax = new Vector2(0.96f, 0.15f);
            menuRect.offsetMin = Vector2.zero;
            menuRect.offsetMax = Vector2.zero;

            _wordSelectionManager.OutputWordsParent = gridRect;
            _wordSelectionManager.OutputSentenceLabel = sentenceLabel;

            // The three buttons are wired to the methods they trigger.
            AddClickListener(sendButton, _aiAgent.ExecutePlayerSentence);
            AddClickListener(clearButton, _wordSelectionManager.ClearSentence);
            AddClickListener(menuButton, _gameSequenceManager.ToggleMenuPanel);
        }


        // Action method: turns AiAgent into the panel where the agent says what it is doing.
        private void BuildAgentPanel()
        {
            RectTransform panel = FindPlacementPanel("AiAgent");

            if (panel == null)
            {
                return;
            }

            AddPanelTitle(panel, "THE AGENT");

            _aiAgent = panel.gameObject.GetComponent<AiAgentBehaviour>();
            if (_aiAgent == null)
            {
                _aiAgent = panel.gameObject.AddComponent<AiAgentBehaviour>();
            }

            _llmCommandAdvisor = panel.gameObject.GetComponent<LlmCommandAdvisor>();
            if (_llmCommandAdvisor == null)
            {
                _llmCommandAdvisor = panel.gameObject.AddComponent<LlmCommandAdvisor>();
            }

            TMP_Text statusLabel = CreateLabel(panel, "AgentStatus", "Waiting for instructions", 16f,
                TextAlignmentOptions.Left, new Color(0.2f, 0.2f, 0.2f));
            RectTransform statusRect = statusLabel.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0.78f);
            statusRect.anchorMax = new Vector2(1f, 0.9f);
            statusRect.offsetMin = new Vector2(10f, 0f);
            statusRect.offsetMax = new Vector2(-10f, 0f);

            // A small panel that hovers above the agent panel and shows one short step message at a
            // time (e.g. "I got it!" or "I'm lost... so I will ..."). It overlaps the top edge of the
            // agent panel and renders on top of it.
            GameObject hoverObject = new GameObject(BuiltObjectPrefix + "StepMessagePanel", typeof(RectTransform));
            hoverObject.transform.SetParent(panel, false);

            RectTransform hoverRect = hoverObject.GetComponent<RectTransform>();
            hoverRect.anchorMin = new Vector2(0f, 0.9f);
            hoverRect.anchorMax = new Vector2(1f, 1.18f);
            hoverRect.offsetMin = new Vector2(6f, 0f);
            hoverRect.offsetMax = new Vector2(-6f, 0f);
            hoverRect.SetAsLastSibling();

            Image hoverBackground = hoverObject.AddComponent<Image>();
            hoverBackground.color = new Color(0.15f, 0.15f, 0.18f, 0.92f);

            TMP_Text stepMessageLabel = CreateLabel(hoverRect, "StepMessageText", "", 15f,
                TextAlignmentOptions.Center, Color.white);

            TMP_Text thinkingLabel = CreateLabel(panel, "AgentThinking", "", 13f,
                TextAlignmentOptions.Left, new Color(0.35f, 0.35f, 0.6f));
            RectTransform thinkingRect = thinkingLabel.rectTransform;
            thinkingRect.anchorMin = new Vector2(0f, 0.0f);
            thinkingRect.anchorMax = new Vector2(1f, 0.09f);
            thinkingRect.offsetMin = new Vector2(10f, 0f);
            thinkingRect.offsetMax = new Vector2(-10f, 0f);

            _aiAgent.AgentStatusLabel = statusLabel;
            _aiAgent.StepMessageLabel = stepMessageLabel;
            _llmCommandAdvisor.ThinkingLabel = thinkingLabel;
        }


        // Action method: turns Menu into the leaderboard chart panel.
        private void BuildMenuPanel()
        {
            RectTransform panel = FindPlacementPanel("Menu");

            if (panel == null)
            {
                return;
            }

            _menuPanel = panel.gameObject;

            AddPanelTitle(panel, "LEADERBOARD");

            GameObject darkObject = CreateStretchedChild(panel, "MenuBackground");
            Image darkImage = darkObject.AddComponent<Image>();
            darkImage.color = new Color(0.1f, 0.1f, 0.14f, 0.97f);
            darkObject.transform.SetAsFirstSibling();

            CreateScrollView(panel, "LeaderboardScroll", out RectTransform rowsContent);

            VerticalLayoutGroup rowsLayout = rowsContent.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.childForceExpandHeight = false;
            rowsLayout.childControlHeight = true;
            rowsLayout.childControlWidth = true;
            rowsLayout.spacing = 2f;
            rowsLayout.padding = new RectOffset(6, 6, 6, 6);

            ContentSizeFitter rowsFitter = rowsContent.gameObject.AddComponent<ContentSizeFitter>();
            rowsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Button closeButton = CreateButton(panel, "CloseMenuButton", "CLOSE", new Color(0.5f, 0.3f, 0.3f));
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.6f, 0.01f);
            closeRect.anchorMax = new Vector2(0.98f, 0.1f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;

            Button refreshButton = CreateButton(panel, "RefreshMenuButton", "REFRESH", new Color(0.3f, 0.5f, 0.4f));
            RectTransform refreshRect = refreshButton.GetComponent<RectTransform>();
            refreshRect.anchorMin = new Vector2(0.02f, 0.01f);
            refreshRect.anchorMax = new Vector2(0.4f, 0.1f);
            refreshRect.offsetMin = Vector2.zero;
            refreshRect.offsetMax = Vector2.zero;

            _leaderboardUiManager.RowsParent = rowsContent;

            AddClickListener(closeButton, _gameSequenceManager.ToggleMenuPanel);
            AddClickListener(refreshButton, _leaderboardManager.DownloadLeaderboard);

            // The leaderboard starts closed so it does not cover the kitchen.
            panel.gameObject.SetActive(false);
        }


        // Action method: builds the full screen panel where the player names what came out of the cauldron.
        private void BuildNamingPanel()
        {
            _namingPanel = CreateFullScreenPanel("NamingPanel", new Color(0.08f, 0.09f, 0.13f, 0.96f));
            RectTransform panelRect = _namingPanel.GetComponent<RectTransform>();

            TMP_Text titleLabel = CreateLabel(panelRect, "NamingTitle", "NAME WHAT YOU MADE", 42f,
                TextAlignmentOptions.Center, Color.white);
            SetAnchors(titleLabel.rectTransform, 0.15f, 0.82f, 0.85f, 0.92f);

            TMP_Text outputLabel = CreateLabel(panelRect, "CauldronOutputText", "", 20f,
                TextAlignmentOptions.Top, new Color(0.85f, 0.85f, 0.9f));
            SetAnchors(outputLabel.rectTransform, 0.2f, 0.42f, 0.8f, 0.8f);

            TMP_InputField nameInput = CreateInputField(panelRect, "PotionNameInput", "Call it something...");
            SetAnchors(nameInput.GetComponent<RectTransform>(), 0.28f, 0.28f, 0.72f, 0.36f);

            Button confirmButton = CreateButton(panelRect, "ConfirmNameButton", "BOTTLE IT", new Color(0.3f, 0.65f, 0.4f));
            SetAnchors(confirmButton.GetComponent<RectTransform>(), 0.38f, 0.15f, 0.62f, 0.24f);

            _gameSequenceManager.CauldronOutputLabel = outputLabel;
            _gameSequenceManager.PotionNameInput = nameInput;

            AddClickListener(confirmButton, _gameSequenceManager.ConfirmPotionName);

            _namingPanel.SetActive(false);
        }


        // Action method: builds the full screen win or loss panel with the player name box.
        private void BuildResultPanel()
        {
            _resultPanel = CreateFullScreenPanel("ResultPanel", new Color(0.06f, 0.07f, 0.1f, 0.96f));
            RectTransform panelRect = _resultPanel.GetComponent<RectTransform>();

            TMP_Text titleLabel = CreateLabel(panelRect, "ResultTitle", "", 44f, TextAlignmentOptions.Center, Color.white);
            SetAnchors(titleLabel.rectTransform, 0.1f, 0.8f, 0.9f, 0.92f);

            TMP_Text detailLabel = CreateLabel(panelRect, "ResultDetail", "", 22f,
                TextAlignmentOptions.Top, new Color(0.85f, 0.85f, 0.9f));
            SetAnchors(detailLabel.rectTransform, 0.2f, 0.44f, 0.8f, 0.78f);

            TMP_Text verdictLabel = CreateLabel(panelRect, "ResultVerdict", "", 26f,
                TextAlignmentOptions.Center, Color.white);
            verdictLabel.fontStyle = FontStyles.Bold;
            SetAnchors(verdictLabel.rectTransform, 0.1f, 0.36f, 0.9f, 0.43f);

            TMP_Text namePrompt = CreateLabel(panelRect, "PlayerNamePrompt", "Your name for the leaderboard:", 18f,
                TextAlignmentOptions.Center, new Color(0.7f, 0.7f, 0.75f));
            SetAnchors(namePrompt.rectTransform, 0.28f, 0.30f, 0.72f, 0.35f);

            TMP_InputField playerInput = CreateInputField(panelRect, "PlayerNameInput", "Your name");
            SetAnchors(playerInput.GetComponent<RectTransform>(), 0.32f, 0.21f, 0.68f, 0.29f);

            Button submitButton = CreateButton(panelRect, "SubmitScoreButton", "SUBMIT SCORE", new Color(0.3f, 0.55f, 0.85f));
            SetAnchors(submitButton.GetComponent<RectTransform>(), 0.22f, 0.11f, 0.46f, 0.19f);

            Button restartButton = CreateButton(panelRect, "RestartButton", "BREW AGAIN", new Color(0.3f, 0.65f, 0.4f));
            SetAnchors(restartButton.GetComponent<RectTransform>(), 0.54f, 0.11f, 0.78f, 0.19f);

            Button openMenuButton = CreateButton(panelRect, "ResultMenuButton", "LEADERBOARD", new Color(0.45f, 0.45f, 0.5f));
            SetAnchors(openMenuButton.GetComponent<RectTransform>(), 0.38f, 0.01f, 0.62f, 0.09f);

            _gameSequenceManager.ResultTitleLabel = titleLabel;
            _gameSequenceManager.ResultDetailLabel = detailLabel;
            _gameSequenceManager.ResultVerdictLabel = verdictLabel;
            _gameSequenceManager.PlayerNameInput = playerInput;

            AddClickListener(submitButton, _gameSequenceManager.SubmitScoreToLeaderboard);
            AddClickListener(restartButton, _gameSequenceManager.RestartRound);
            AddClickListener(openMenuButton, _gameSequenceManager.ToggleMenuPanel);

            _resultPanel.SetActive(false);
        }


        // Action method: creates the LLM object and the agent component that talks to it.
        private void BuildLanguageModelObjects()
        {
            GameObject modelObject = new GameObject(BuiltObjectPrefix + "LLM");
            modelObject.AddComponent<LLM>();

            if (_aiAgent == null)
            {
                return;
            }

            LLMAgent modelAgent = _aiAgent.gameObject.GetComponent<LLMAgent>();
            if (modelAgent == null)
            {
                modelAgent = _aiAgent.gameObject.AddComponent<LLMAgent>();
            }

            if (_llmCommandAdvisor != null)
            {
                _llmCommandAdvisor.MyAgent = modelAgent;
            }
        }


        ///// Custom Methods - Linking /////

        // Action method: links every manager to the panels and events it needs.
        private void LinkEverythingTogether()
        {
            _gameSequenceManager.NamingPanel = _namingPanel;
            _gameSequenceManager.ResultPanel = _resultPanel;
            _gameSequenceManager.MenuPanel = _menuPanel;

            // The round is scored the moment the agent stops working.
            if (_aiAgent != null)
            {
                // A component added from code can still have an empty event, so it is made here if needed.
                if (_aiAgent.OnSequenceFinished == null)
                {
                    _aiAgent.OnSequenceFinished = new UnityEvent();
                }

                UnityEventTools.AddPersistentListener(_aiAgent.OnSequenceFinished, _gameSequenceManager.EvaluatePotion);
            }

            EditorUtility.SetDirty(_gameSystemsObject);

            if (_aiAgent != null)
            {
                EditorUtility.SetDirty(_aiAgent);
            }
        }


        ///// Custom Methods - Small UI Builders /////

        // Action method: writes a small title across the top of one of the placement panels.
        private TMP_Text AddPanelTitle(RectTransform panel, string titleText)
        {
            TMP_Text titleLabel = CreateLabel(panel, "PanelTitle", titleText, 15f,
                TextAlignmentOptions.Left, new Color(0.15f, 0.15f, 0.2f));

            RectTransform titleRect = titleLabel.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.9f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(8f, 0f);
            titleRect.offsetMax = new Vector2(-8f, 0f);

            titleLabel.fontStyle = FontStyles.Bold;

            return titleLabel;
        }


        // Action method: builds a scroll view that fills a panel and gives back its content area.
        private ScrollRect CreateScrollView(RectTransform parent, string scrollName, out RectTransform contentRect)
        {
            GameObject scrollObject = CreateStretchedChild(parent, scrollName);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 0.89f);
            scrollRect.offsetMin = new Vector2(4f, 4f);
            scrollRect.offsetMax = new Vector2(-4f, 0f);

            ScrollRect scrollView = scrollObject.AddComponent<ScrollRect>();
            scrollView.horizontal = false;
            scrollView.vertical = true;
            scrollView.movementType = ScrollRect.MovementType.Clamped;
            scrollView.scrollSensitivity = 25f;

            // The viewport is the window the content slides behind.
            GameObject viewportObject = CreateStretchedChild(scrollRect, "Viewport");
            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
            viewportObject.AddComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportObject.transform, false);

            contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            scrollView.viewport = viewportObject.GetComponent<RectTransform>();
            scrollView.content = contentRect;

            return scrollView;
        }


        // Action method: builds a plain TextMeshPro label as a child of something.
        private TMP_Text CreateLabel(RectTransform parent, string labelName, string labelText, float fontSize,
                                     TextAlignmentOptions alignment, Color textColour)
        {
            GameObject labelObject = new GameObject(BuiltObjectPrefix + labelName, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            StretchToParent(labelRect);

            TextMeshProUGUI newLabel = labelObject.AddComponent<TextMeshProUGUI>();
            newLabel.text = labelText;
            newLabel.fontSize = fontSize;
            newLabel.color = textColour;
            newLabel.alignment = alignment;
            newLabel.raycastTarget = false;

            return newLabel;
        }


        // Action method: builds a clickable button with a label on it.
        private Button CreateButton(RectTransform parent, string buttonName, string buttonText, Color buttonColour)
        {
            GameObject buttonObject = new GameObject(BuiltObjectPrefix + buttonName, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);

            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = buttonColour;

            Button newButton = buttonObject.AddComponent<Button>();
            newButton.targetGraphic = buttonImage;

            TMP_Text buttonLabel = CreateLabel(buttonObject.GetComponent<RectTransform>(), buttonName + "Label",
                buttonText, 17f, TextAlignmentOptions.Center, Color.white);
            StretchToParent(buttonLabel.rectTransform);

            return newButton;
        }


        // Action method: builds a text box the player can type into.
        private TMP_InputField CreateInputField(RectTransform parent, string fieldName, string placeholderText)
        {
            GameObject fieldObject = new GameObject(BuiltObjectPrefix + fieldName, typeof(RectTransform));
            fieldObject.transform.SetParent(parent, false);

            Image fieldImage = fieldObject.AddComponent<Image>();
            fieldImage.color = new Color(0.95f, 0.95f, 0.95f, 1f);

            TMP_InputField newField = fieldObject.AddComponent<TMP_InputField>();

            GameObject textAreaObject = CreateStretchedChild(fieldObject.GetComponent<RectTransform>(), "TextArea");
            RectTransform textAreaRect = textAreaObject.GetComponent<RectTransform>();
            textAreaRect.offsetMin = new Vector2(12f, 6f);
            textAreaRect.offsetMax = new Vector2(-12f, -6f);
            textAreaObject.AddComponent<RectMask2D>();

            TMP_Text placeholderLabel = CreateLabel(textAreaRect, fieldName + "Placeholder", placeholderText, 20f,
                TextAlignmentOptions.Left, new Color(0.5f, 0.5f, 0.5f));
            placeholderLabel.fontStyle = FontStyles.Italic;

            TMP_Text typedLabel = CreateLabel(textAreaRect, fieldName + "Text", "", 20f,
                TextAlignmentOptions.Left, Color.black);

            newField.textViewport = textAreaRect;
            newField.textComponent = typedLabel;
            newField.placeholder = placeholderLabel;
            newField.targetGraphic = fieldImage;
            newField.text = "";

            return newField;
        }


        // Action method: builds a panel that covers the whole canvas, used for the naming and result screens.
        private GameObject CreateFullScreenPanel(string panelName, Color backgroundColour)
        {
            GameObject panelObject = new GameObject(BuiltObjectPrefix + panelName, typeof(RectTransform));
            panelObject.transform.SetParent(_placementCanvas.transform, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            StretchToParent(panelRect);

            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.color = backgroundColour;

            return panelObject;
        }


        // Action method: builds an empty child that fills its parent.
        private GameObject CreateStretchedChild(RectTransform parent, string childName)
        {
            GameObject childObject = new GameObject(BuiltObjectPrefix + childName, typeof(RectTransform));
            childObject.transform.SetParent(parent, false);

            StretchToParent(childObject.GetComponent<RectTransform>());

            return childObject;
        }


        // Action method: makes a rect transform fill whatever it is parented to.
        private void StretchToParent(RectTransform targetRect)
        {
            targetRect.anchorMin = Vector2.zero;
            targetRect.anchorMax = Vector2.one;
            targetRect.offsetMin = Vector2.zero;
            targetRect.offsetMax = Vector2.zero;
        }


        // Action method: places a rect transform using shares of its parent, from bottom left to top right.
        private void SetAnchors(RectTransform targetRect, float minX, float minY, float maxX, float maxY)
        {
            targetRect.anchorMin = new Vector2(minX, minY);
            targetRect.anchorMax = new Vector2(maxX, maxY);
            targetRect.offsetMin = Vector2.zero;
            targetRect.offsetMax = Vector2.zero;
        }


        // Action method: wires a button to a method so the link survives after the editor closes.
        private void AddClickListener(Button targetButton, UnityAction actionToCall)
        {
            if (targetButton == null || actionToCall == null)
            {
                return;
            }

            UnityEventTools.AddPersistentListener(targetButton.onClick, actionToCall);
        }
    }
}


// Implementation steps:
// 1. Open the scene that holds UI-PlacementObj.
// 2. Open NTGD124 -> Build Potion Game Scene from the top menu.
// 3. Press Build Scene Now, then save the scene.
// 4. Select the Built_LLM object and download a small model from its Inspector before pressing Play.
