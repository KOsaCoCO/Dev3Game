// Runs the whole round: hands out an order, scores what the cauldron produced, and lets the player name it.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;


namespace NTGD124
{
    // The stages one round moves through, in order.
    public enum GameStage
    {
        Preparing,   // the player is choosing words
        Executing,   // the agent is carrying the sentence out
        Brewing,     // the agent is done and the cauldron is brewing, waiting for player to finish
        Naming,      // the cauldron has produced something and the player names it
        Finished     // the round is over and the result screen is up
    }


    public class GameSequenceManager : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)][SerializeField] private string _componentDescription =
            "The round manager. It asks RecipeGenerator for an order, waits for the agent to finish, then " +
            "moves through a Brewing stage (waiting on the Finish Brewing button) before scoring the potion " +
            "on two things: how close each ingredient's delivered state is to the state the order asked " +
            "for, and whether the cauldron was kept at the right temperature. Ingredient scoring is never " +
            "all or nothing, a near miss on tool or time still earns real partial credit. Matching the " +
            "stated time is a bonus, and every ingredient that was not asked for gives the potion " +
            "sideeffects and costs a little score.";


        ///// Public Variables /////

        [Header("Singleton Access")]
        public static GameSequenceManager Singleton;

        [Header("Scoring Rules")]
        [Range(0f, 1f)] public float WinMatchThreshold = 0.6f;
        [Range(0f, 1f)] public float WinQualityThreshold = 0.5f;

        [Header("How The Match Score Is Split")]
        // The rest of the score comes from getting the cauldron temperature right.
        [Range(0f, 1f)] public float IngredientShareOfScore = 0.6f;
        [Range(0f, 0.5f)] public float BonusForCorrectTime = 0.1f;
        [Range(0f, 0.5f)] public float PenaltyPerExtraIngredient = 0.05f;

        // The leaderboard's own column headers just say "WINS" and "LOSSES", so these default to
        // plain counts (1 per round) to match what the chart actually promises. Raise them only if
        // you want a weighted score instead, and relabel the columns in LeaderboardUIManager to
        // match (e.g. "WIN POINTS" / "LOSS POINTS"), otherwise the numbers will look wrong again.
        [Header("Leaderboard Points")]
        public int PointsPerWin = 1;
        public int PointsPerLoss = 1;

        [Header("Result Messages")]
        public string SideEffectMessage = "The potion has gotten some sideeffects ...";
        public string WreckedMessage = "Your potion has wrecked the person!";
        public string GenerousMessage = "The potion actually gave them more than they asked for!";

        [Header("Panels")]
        public GameObject BrewingPanel;
        public GameObject NamingPanel;
        public GameObject ResultPanel;
        public GameObject MenuPanel;

        [Header("Naming Panel UI")]
        public TMP_Text CauldronOutputLabel;
        public TMP_InputField PotionNameInput;

        [Header("Result Panel UI")]
        public TMP_Text ResultTitleLabel;
        public TMP_Text ResultDetailLabel;
        public TMP_Text ResultVerdictLabel;
        public TMP_InputField PlayerNameInput;

        [Header("Shelf Of Everything Brewed This Session")]
        public List<BrewedPotion> BrewedPotions = new List<BrewedPotion>();

        [Header("Events")]
        public UnityEvent OnRoundStarted = new UnityEvent();
        public UnityEvent OnBrewingStarted = new UnityEvent();
        public UnityEvent OnPotionReadyToName = new UnityEvent();
        public UnityEvent OnRoundWon = new UnityEvent();
        public UnityEvent OnRoundLost = new UnityEvent();


        ///// Private Variables /////

        // The stage the round is in right now.
        private GameStage _currentStage = GameStage.Preparing;

        // The potion the player is naming at this moment.
        private BrewedPotion _potionBeingNamed;

        // Scores worked out when the agent finished, kept until the player has named the potion.
        private float _lastMatchScore = 0f;
        private float _lastQualityScore = 0f;
        private bool _lastRoundWasWon = false;
        private int _lastExtraIngredientCount = 0;


        ///// Unity Methods /////

        private void Awake()
        {
            Singleton = this;
        }


        private void Start()
        {
            StartNewRound();
        }


        ///// Custom Methods - Trigger Methods /////

        // Called on Start, and by the Brew Again button on the result screen.
        public void StartNewRound()
        {
            _currentStage = GameStage.Preparing;

            ShowPanel(BrewingPanel, false);
            ShowPanel(NamingPanel, false);
            ShowPanel(ResultPanel, false);

            if (WordSelectionManager.Singleton != null)
            {
                WordSelectionManager.Singleton.ClearSentence();
                WordSelectionManager.Singleton.UnlockAllIngredientButtons();
            }

            if (FeedbackVisualizer.Singleton != null)
            {
                FeedbackVisualizer.Singleton.ResetToResting();
            }

            if (RecipeGenerator.Singleton != null)
            {
                RecipeGenerator.Singleton.GenerateNewRecipe();

                // Update word selection with this round's recipe
                if (WordSelectionManager.Singleton != null)
                {
                    PotionRecipe currentRecipe = RecipeGenerator.Singleton.CurrentRecipe;
                    WordSelectionManager.Singleton.SetRequiredIngredients(GetRequiredIngredientNames(currentRecipe));
                    WordSelectionManager.Singleton.RotateActionsAndTools();
                }
            }

            if (AiAgentBehaviour.Singleton != null)
            {
                // A fresh round starts a fresh reasoning log and a fresh potion; whatever the last
                // round logged, accepted or added is gone for good once brewing starts again. This
                // is the ONLY place the potion is reset, it must survive every instruction submitted
                // within a single round.
                AiAgentBehaviour.Singleton.ClearReasoningLog();
                AiAgentBehaviour.Singleton.ClearAcceptedItems();
                AiAgentBehaviour.Singleton.ResetPotionState();
                AiAgentBehaviour.Singleton.ShowReasoningPanel();
            }

            Debug.Log("[GameSequence] ---------- NEW ROUND ----------");
            OnRoundStarted.Invoke();
        }


        // Hooked to AiAgentBehaviour.OnSequenceFinished, shows the brewing panel and waits for player
        // button. Renamed from EvaluatePotion to better reflect its behavior. Scoring is deliberately
        // NOT done here, an instruction can finish long before the player is done accepting items, so
        // the real scoring waits for FinishBrewing, once the player says they are actually done.
        public void EvaluatePotion()
        {
            _currentStage = GameStage.Brewing;

            ShowPanel(BrewingPanel, true);
            OnBrewingStarted.Invoke();
        }


        // Input-based trigger: wired to the Finish Brewing button on the brewing panel. This is where
        // the round is actually scored, using whatever the player has accepted with AcceptItemState
        // up to this exact moment.
        public void FinishBrewing()
        {
            PotionState finishedPotion = AiAgentBehaviour.Singleton.CurrentPotion;
            PotionRecipe orderedRecipe = RecipeGenerator.Singleton.CurrentRecipe;

            _lastExtraIngredientCount = finishedPotion.CountExtraIngredients(GetRequiredIngredientNames(orderedRecipe));
            _lastMatchScore = CalculateMatchScore(finishedPotion, orderedRecipe);
            _lastQualityScore = finishedPotion.GetAverageQuality();
            _lastRoundWasWon = _lastMatchScore >= WinMatchThreshold && _lastQualityScore >= WinQualityThreshold;

            Debug.Log("[GameSequence] Brewing finished by the player. Match with the order: " +
                      Mathf.RoundToInt(_lastMatchScore * 100f) + "% | " +
                      "Quality of the work: " + Mathf.RoundToInt(_lastQualityScore * 100f) + "% | " +
                      "Extra ingredients: " + _lastExtraIngredientCount + " | " +
                      "Result: " + (_lastRoundWasWon ? "WIN" : "LOSS"));

            ShowPanel(BrewingPanel, false);

            if (AiAgentBehaviour.Singleton != null)
            {
                // The reasoning log stops being shown, but it is not cleared yet, it stays around
                // until a new round of brewing actually starts.
                AiAgentBehaviour.Singleton.HideReasoningPanel();
            }

            OpenNamingPanel(finishedPotion, orderedRecipe);
        }


        // Input-based trigger: wired to the Bottle It button on the naming panel.
        public void ConfirmPotionName()
        {
            if (_potionBeingNamed == null)
            {
                return;
            }

            string typedName = PotionNameInput != null ? PotionNameInput.text.Trim() : "";

            // Something with no name still goes on the shelf, it just gets called what it is.
            if (string.IsNullOrEmpty(typedName))
            {
                typedName = "Unnamed Sludge";
            }

            _potionBeingNamed.PlayerGivenName = typedName;
            BrewedPotions.Add(_potionBeingNamed);

            Debug.Log("[GameSequence] The player named the cauldron output: " +
                      _potionBeingNamed.GetLabelText().Replace("\n", " | "));

            ShowPanel(NamingPanel, false);
            OpenResultPanel();
        }


        // Input-based trigger: wired to the Submit Score button on the result panel.
        public void SubmitScoreToLeaderboard()
        {
            string playerName = PlayerNameInput != null ? PlayerNameInput.text.Trim() : "";

            if (string.IsNullOrEmpty(playerName))
            {
                playerName = "Anonymous";
            }

            int winPoints = _lastRoundWasWon ? PointsPerWin : 0;
            int lossPoints = _lastRoundWasWon ? 0 : PointsPerLoss;

            if (LeaderboardManager.Singleton != null)
            {
                LeaderboardManager.Singleton.PostScoreOnline(playerName, winPoints, lossPoints);
            }

            Debug.Log("[GameSequence] Sent to the leaderboard -> " + playerName +
                      " win points: " + winPoints + " loss points: " + lossPoints);
        }


        // Input-based trigger: wired to the Brew Again button so the player can start another order.
        public void RestartRound()
        {
            StartNewRound();
        }


        // Input-based trigger: wired to a Reload button, for when the whole scene should start over.
        public void ReloadWholeScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }


        // Input-based trigger: wired to the Menu button so the leaderboard can be opened.
        public void ToggleMenuPanel()
        {
            if (MenuPanel == null)
            {
                return;
            }

            bool shouldOpen = !MenuPanel.activeSelf;
            MenuPanel.SetActive(shouldOpen);

            if (shouldOpen)
            {
                // Moving the panel to the end of the list draws it on top of the result screen.
                MenuPanel.transform.SetAsLastSibling();

                // Opening the menu is a good moment to fetch fresh leaderboard rows.
                if (LeaderboardManager.Singleton != null)
                {
                    LeaderboardManager.Singleton.DownloadLeaderboard();
                }
            }
        }


        ///// Custom Methods - Action Methods /////

        // Action method: works out how much of the order the agent actually delivered, from 0 to 1.
        private float CalculateMatchScore(PotionState finishedPotion, PotionRecipe orderedRecipe)
        {
            // Part one: how close every asked for ingredient's state ended up to what was ordered.
            // This is never all or nothing, an ingredient that went in but was processed the wrong
            // way, or for the wrong length of time, still earns partial credit.
            float ingredientScore = 0f;

            if (orderedRecipe.RequiredIngredients.Count > 0)
            {
                float totalCredit = 0f;

                for (int i = 0; i < orderedRecipe.RequiredIngredients.Count; i++)
                {
                    totalCredit += ScoreOneRequiredIngredient(orderedRecipe.RequiredIngredients[i], finishedPotion);
                }

                ingredientScore = totalCredit / orderedRecipe.RequiredIngredients.Count;
            }

            // Part two: whether the cauldron ran hot or cold as asked.
            float temperatureScore = finishedPotion.MatchesTemperature(orderedRecipe.RequiredTemperature) ? 1f : 0f;

            float totalScore = (ingredientScore * IngredientShareOfScore) +
                               (temperatureScore * (1f - IngredientShareOfScore));

            // Holding it for exactly the stated time is extra credit, not a requirement.
            if (finishedPotion.MatchesMinutes(orderedRecipe.RequiredMinutes))
            {
                totalScore += BonusForCorrectTime;
            }

            // Every ingredient nobody asked for drags the potion down a little.
            totalScore -= _lastExtraIngredientCount * PenaltyPerExtraIngredient;

            return Mathf.Clamp01(totalScore);
        }


        // Action method: how close the cauldron's actual outcome for one ingredient is to what the
        // order asked for. Never a flat yes or no, a near miss still earns a real chunk of credit,
        // since a broken-instruction agent is never going to get it perfectly right every time.
        //
        // Raw requirements are checked straight off what actually went in, since there was never a
        // processing decision to confirm. Processed requirements only count what the player actually
        // accepted with the AcceptItemState button, an ingredient the agent processed but the player
        // never accepted is scored exactly like one that went in raw.
        private float ScoreOneRequiredIngredient(RequiredIngredientState requiredState, PotionState finishedPotion)
        {
            if (!finishedPotion.AddedIngredients.Contains(requiredState.IngredientName))
            {
                return 0f;
            }

            if (requiredState.WantsRaw())
            {
                bool wasProcessedAnyway = finishedPotion.FindProcessedResult(requiredState.IngredientName) != null;

                // Wanted it untouched: full credit if it really stayed raw, still decent credit if
                // it got processed anyway, the right ingredient did make it into the cauldron.
                return wasProcessedAnyway ? 0.7f : 1f;
            }

            ProcessedIngredientResult acceptedResult = AiAgentBehaviour.Singleton != null
                ? AiAgentBehaviour.Singleton.FindAcceptedResult(requiredState.IngredientName)
                : null;

            if (acceptedResult == null)
            {
                // Wanted it processed a certain way, but nothing was ever confirmed for it.
                return 0.2f;
            }

            if (acceptedResult.UsedArchetype == requiredState.RequiredArchetype &&
                acceptedResult.UsedTimeBracket == requiredState.RequiredTimeBracket)
            {
                return 1f;
            }

            if (acceptedResult.UsedArchetype == requiredState.RequiredArchetype)
            {
                // The right kind of processing, just held for the wrong length of time.
                return 0.65f;
            }

            // A different kind of processing altogether, still a real attempt at the ingredient.
            return 0.35f;
        }


        // Action method: pulls the plain ingredient names back out of the recipe, for anything that
        // only needs to know what to look for on the ingredient stand, not what state it must end in.
        private List<string> GetRequiredIngredientNames(PotionRecipe recipe)
        {
            List<string> ingredientNames = new List<string>();

            for (int i = 0; i < recipe.RequiredIngredients.Count; i++)
            {
                ingredientNames.Add(recipe.RequiredIngredients[i].IngredientName);
            }

            return ingredientNames;
        }


        // Action method: shows what came out of the cauldron and invites the player to name it.
        private void OpenNamingPanel(PotionState finishedPotion, PotionRecipe orderedRecipe)
        {
            _potionBeingNamed = new BrewedPotion();
            _potionBeingNamed.OrderedHeadline = orderedRecipe.GetHeadlineText();
            _potionBeingNamed.ContainedIngredients = new List<string>(finishedPotion.AddedIngredients);
            _potionBeingNamed.UsedTools = new List<string>(finishedPotion.UsedTools);
            _potionBeingNamed.MatchScore = _lastMatchScore;
            _potionBeingNamed.QualityScore = _lastQualityScore;
            _potionBeingNamed.WasAccepted = _lastRoundWasWon;
            _potionBeingNamed.HasSideEffects = _lastExtraIngredientCount > 0;

            if (CauldronOutputLabel != null)
            {
                string outputText = "Something came out of the cauldron.\n\n" + finishedPotion.GetSummaryText();

                string processedResultsText = finishedPotion.GetProcessedResultsText();

                if (!string.IsNullOrEmpty(processedResultsText))
                {
                    outputText += "\n\nWhat went in became:\n" + processedResultsText;
                }

                if (_lastExtraIngredientCount > 0)
                {
                    outputText += "\n\n" + SideEffectMessage;
                }

                outputText += "\n\nWhatever this is, it is yours. What do you call it?";

                CauldronOutputLabel.text = outputText;
            }

            if (PotionNameInput != null)
            {
                PotionNameInput.text = "";
                PotionNameInput.Select();
                PotionNameInput.ActivateInputField();
            }

            ShowPanel(NamingPanel, true);
            OnPotionReadyToName.Invoke();
        }


        // Action method: shows the win or loss screen once the potion has a name.
        private void OpenResultPanel()
        {
            _currentStage = GameStage.Finished;

            string outcomeTitle = _lastRoundWasWon ? "THE ORDER WAS ACCEPTED" : "THE ORDER WAS REJECTED";

            if (ResultTitleLabel != null)
            {
                ResultTitleLabel.text = outcomeTitle;
                ResultTitleLabel.color = _lastRoundWasWon ? new Color(0.25f, 0.75f, 0.3f) : new Color(0.8f, 0.25f, 0.25f);
            }

            if (ResultDetailLabel != null)
            {
                ResultDetailLabel.text = BuildResultDetailText();
            }

            ShowVerdictLine();

            if (FeedbackVisualizer.Singleton != null)
            {
                if (_lastRoundWasWon)
                {
                    FeedbackVisualizer.Singleton.ShowWinFeedback(outcomeTitle);
                }
                else
                {
                    FeedbackVisualizer.Singleton.ShowLossFeedback(outcomeTitle);
                }
            }

            ShowPanel(ResultPanel, true);

            if (_lastRoundWasWon)
            {
                OnRoundWon.Invoke();
            }
            else
            {
                OnRoundLost.Invoke();
            }
        }


        // Action method: writes the block of text that explains how the round went.
        private string BuildResultDetailText()
        {
            PotionRecipe orderedRecipe = RecipeGenerator.Singleton.CurrentRecipe;
            PotionState finishedPotion = AiAgentBehaviour.Singleton.CurrentPotion;

            string detailText = orderedRecipe.GetHeadlineText() + "\n";
            detailText += "You handed them: \"" + _potionBeingNamed.PlayerGivenName + "\"\n\n";

            detailText += "Ingredients:\n";

            for (int i = 0; i < orderedRecipe.RequiredIngredients.Count; i++)
            {
                RequiredIngredientState requiredState = orderedRecipe.RequiredIngredients[i];
                float closeness = ScoreOneRequiredIngredient(requiredState, finishedPotion);

                detailText += "- wanted " + requiredState.RequiredStateName + ": " +
                               Mathf.RoundToInt(closeness * 100f) + "% match\n";
            }

            string askedTemperature = orderedRecipe.RequiredTemperature;
            string gotTemperature = string.IsNullOrEmpty(finishedPotion.Temperature) ? "neither" : finishedPotion.Temperature;
            detailText += "Cauldron: asked for " + askedTemperature + ", got " + gotTemperature + "\n";

            string askedTime = orderedRecipe.RequiredMinutes + " minutes";
            string gotTime = finishedPotion.MinutesHeld >= 0 ? finishedPotion.MinutesHeld + " minutes" : "no time given";
            detailText += "Time: asked for " + askedTime + ", got " + gotTime + "\n";

            // The sideeffect line only appears when the player overloaded the cauldron.
            if (_lastExtraIngredientCount > 0)
            {
                detailText += "\n" + SideEffectMessage + "\n";
            }

            detailText += "\nMatch with the order: " + Mathf.RoundToInt(_lastMatchScore * 100f) + "%\n";
            detailText += "Quality of the work: " + Mathf.RoundToInt(_lastQualityScore * 100f) + "%\n";
            detailText += "Steps the agent had to guess: " + finishedPotion.GuessedStepCount;

            return detailText;
        }


        // Action method: writes the final line that says what the potion did to the person.
        private void ShowVerdictLine()
        {
            if (ResultVerdictLabel == null)
            {
                return;
            }

            // The verdict follows how well the agent carried out the steps, which is decided
            // by the 50/50 and 0 to 100 rolls on everything the player left unclear.
            bool potionWasMostlyPositive = _lastQualityScore >= 0.5f;

            if (potionWasMostlyPositive)
            {
                ResultVerdictLabel.text = GenerousMessage;
                ResultVerdictLabel.color = new Color(0.35f, 0.8f, 0.45f);
            }
            else
            {
                ResultVerdictLabel.text = WreckedMessage;
                ResultVerdictLabel.color = new Color(0.9f, 0.35f, 0.35f);
            }

            Debug.Log("[GameSequence] Verdict: " + ResultVerdictLabel.text);
        }


        // Action method: turns a panel on or off without crashing when it was never linked.
        private void ShowPanel(GameObject panelObject, bool shouldShow)
        {
            if (panelObject != null)
            {
                panelObject.SetActive(shouldShow);
            }
        }


        // Action method: tells other scripts which stage the round is in.
        public GameStage GetCurrentStage()
        {
            return _currentStage;
        }
    }
}


// Implementation steps:
// 1. Place this component on the GameSystems object.
// 2. Link the four panels (BrewingPanel, NamingPanel, ResultPanel, MenuPanel) and their texts and
//    input fields in the Inspector.
// 3. Hook AiAgentBehaviour.OnSequenceFinished to EvaluatePotion().
// 4. Hook the brewing panel's Finish Brewing button to FinishBrewing().
// 5. Hook the naming panel's Bottle It button to ConfirmPotionName().
// 6. Hook the result panel's Submit Score button to SubmitScoreToLeaderboard(), and Brew Again to RestartRound().
// 7. Lower WinMatchThreshold in the Inspector to make the customer easier to please.
