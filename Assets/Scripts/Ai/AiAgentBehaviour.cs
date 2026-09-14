// The potion assistant. Walks the parsed instructions one step at a time and guesses whenever they are unclear.

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;


namespace NTGD124
{
    public class AiAgentBehaviour : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)][SerializeField] private string _componentDescription =
            "The agent that carries out the player's sentence. It walks the parsed steps in order, does " +
            "exactly what it was told when the step is clear, and guesses when it is not. Guesses ask the " +
            "language model for advice first, then go through the 50/50 and the 0 to 100 randomisers. " +
            "Every single step is written to the console so the sequencing can be checked while testing.";


        ///// Public Variables /////

        [Header("Singleton Access")]
        public static AiAgentBehaviour Singleton;

        [Header("UI References")]
        public TMP_Text AgentStatusLabel;
        public TMP_Text StepMessageLabel;

        [Header("Reasoning Log (shares the Menu panel's screen space, hidden once brewing finishes)")]
        public GameObject ReasoningPanelObject;
        public TMP_Text ReasoningLogLabel;
        public int MaxReasoningLogLines = 10;

        [Header("Accepted Items")]
        public TMP_Text TextAcceptedItems;

        [Header("Message Display")]
        public float MinTimeBetweenMessages = 1.0f;

        [Header("Timing")]
        public float SecondsBetweenSteps = 0.6f;

        [Header("Language Model Use")]
        public bool AskModelWhenUnclear = true;

        [Header("Events")]
        public UnityEvent OnSequenceStarted = new UnityEvent();
        public UnityEvent OnStepDoneWell = new UnityEvent();
        public UnityEvent OnStepDoneBadly = new UnityEvent();
        public UnityEvent OnSequenceFinished = new UnityEvent();

        [Header("Result Of The Last Run (read only)")]
        public PotionState CurrentPotion = new PotionState();


        ///// Private Variables /////

        // True while the agent is still working through a sentence.
        private bool _isWorking = false;

        // Every "ingredient -> result" line logged this round, shown in the reasoning panel.
        private List<string> _reasoningLogLines = new List<string>();

        // The most recently processed ingredient, ready to be accepted, or null once accepted
        // or before anything has been processed yet.
        private ProcessedIngredientResult _lastAcceptableResult = null;

        // Every processed result the player has confirmed this round. Only these count toward the
        // recipe's processed ingredient requirements once brewing is finished.
        private List<ProcessedIngredientResult> _acceptedItems = new List<ProcessedIngredientResult>();


        ///// Unity Methods /////

        private void Awake()
        {
            Singleton = this;

            // A backstop against a single long line wrapping past the panel's edge, on top of the
            // line cap in AppendReasoningLine which keeps the log short in the first place.
            if (ReasoningLogLabel != null)
            {
                ReasoningLogLabel.overflowMode = TextOverflowModes.Truncate;
            }
        }


        ///// Custom Methods - Trigger Methods /////

        // Input-based trigger: wired to the Submit button so the player can send the sentence.
        public void ExecutePlayerSentence()
        {
            // The agent refuses a second order while it is still busy with the first.
            if (_isWorking)
            {
                Debug.Log("[AiAgent] Still working on the previous instruction.");
                return;
            }

            if (WordSelectionManager.Singleton == null || WordSelectionManager.Singleton.GetSelectedWordCount() == 0)
            {
                if (StepMessageLabel != null)
                {
                    StepMessageLabel.text = "Tell me something first.";
                }
                return;
            }

            // Rotate the available actions and tools for the next instruction
            if (WordSelectionManager.Singleton != null)
            {
                WordSelectionManager.Singleton.RotateActionsAndTools();
            }

            List<string> chosenWords = WordSelectionManager.Singleton.GetSelectedWordTexts();
            StartCoroutine(RunInstructionSequence(chosenWords));
        }




        ///// Coroutines /////

        // Walks the whole sentence step by step, pausing between steps so the player can follow along.
        private IEnumerator RunInstructionSequence(List<string> chosenWords)
        {
            _isWorking = true;

            // CurrentPotion is NOT reset here. A round can span several submitted instructions
            // (cut the apple, then separately blend the carrot, and so on), and everything the
            // player has built up needs to survive from one instruction to the next within the
            // same round. It is only reset when a whole new round actually starts, in
            // GameSequenceManager.StartNewRound().
            OnSequenceStarted.Invoke();

            string sentenceText = string.Join(" / ", chosenWords);
            Debug.Log("[AiAgent] ===== NEW INSTRUCTION: " + sentenceText + " =====");

            List<CommandStep> parsedSteps = CommandParser.Singleton.ParseSentence(chosenWords);

            if (parsedSteps.Count == 0)
            {
                if (StepMessageLabel != null)
                {
                    StepMessageLabel.text = "I found nothing to do there.";
                }

                Debug.LogWarning("[AiAgent] The sentence produced no steps at all.");
            }

            for (int i = 0; i < parsedSteps.Count; i++)
            {
                yield return StartCoroutine(RunSingleStep(parsedSteps[i], i + 1, parsedSteps.Count));
                yield return new WaitForSeconds(SecondsBetweenSteps);
            }

            Debug.Log("[AiAgent] ===== FINISHED =====\n" + CurrentPotion.GetSummaryText());

            SetAgentStatus("Finished");
            _isWorking = false;
            OnSequenceFinished.Invoke();
        }


        // Carries out one single step, asking for advice and guessing when the step is unclear.
        private IEnumerator RunSingleStep(CommandStep currentStep, int stepNumber, int totalSteps)
        {
            string stepHeader = "Step " + stepNumber + "/" + totalSteps + ": ";
            SetAgentStatus("Working on step " + stepNumber + " of " + totalSteps);

            // "do nothing" paired with an ingredient is its own 50/50 gamble, not a triad step and
            // not a plain clear step, so it is handled before either of those.
            if (currentStep.HasIngredient() && !string.IsNullOrEmpty(currentStep.Verb) &&
                currentStep.Verb.ToLower() == "do nothing")
            {
                yield return StartCoroutine(RunDoNothingStep(currentStep, stepHeader));
                yield break;
            }

            bool isTriadStep = IngredientProcessor.Singleton != null && IngredientProcessor.Singleton.IsTriadEligible(currentStep);

            // A clear step is simply carried out exactly as the player asked. A triad step is still
            // "clear" here (nothing was ambiguous), but it still needs to be resolved so the ingredient
            // actually turns into something, so it runs through the processor even without a roll.
            if (!currentStep.IsAmbiguous)
            {
                Debug.Log("[AiAgent] " + stepHeader + "CLEAR -> " + currentStep.GetReadableText());
                yield return StartCoroutine(ShowStepMessageWithDelay("I got it!"));

                if (isTriadStep)
                {
                    yield return StartCoroutine(IngredientProcessor.Singleton.ResolveAndProcessStep(currentStep));
                    LogProcessedResults(IngredientProcessor.Singleton.LastProcessedResults, 100, "");
                }

                CurrentPotion.RecordStep(currentStep, 1f, false);
                UpdateCauldronTemperatureDisplay();
                OnStepDoneWell.Invoke();
                ShowStepFeedback(true, 100);
                yield break;
            }

            // A processing step (has an ingredient, and the verb needs a duration, or has no verb
            // at all yet) fills in whatever of the action, tool and time it is missing on its own.
            if (isTriadStep)
            {
                yield return StartCoroutine(RunTriadStep(currentStep, stepHeader));
                yield break;
            }

            // Any other unclear step goes to the language model for an opinion first.
            Debug.Log("[AiAgent] " + stepHeader + "UNCLEAR (" + currentStep.AmbiguityReason + ") -> " + currentStep.GetReadableText());
            yield return StartCoroutine(AskModelAboutStep(currentStep));

            // Then the two randomisers decide how the guess actually turns out.
            UncertaintyRoll roll = UncertaintyRandomizer.Singleton.RollForUnclearStep(currentStep);
            float stepQuality = UncertaintyRandomizer.Singleton.GetStepQualityFromRoll(roll);

            string guessSentence = BuildGuessSentence(currentStep, roll);
            yield return StartCoroutine(ShowStepMessageWithDelay(guessSentence));

            CurrentPotion.RecordStep(currentStep, stepQuality, true);
            UpdateCauldronTemperatureDisplay();

            if (roll.IsPositive)
            {
                OnStepDoneWell.Invoke();
            }
            else
            {
                OnStepDoneBadly.Invoke();
            }

            ShowStepFeedback(roll.IsPositive, roll.Magnitude);
        }


        // Fills in whatever a processing step is missing (action, tool, time), logs what each
        // ingredient became, then still rolls the usual uncertainty check for the step's quality.
        private IEnumerator RunTriadStep(CommandStep currentStep, string stepHeader)
        {
            Debug.Log("[AiAgent] " + stepHeader + "PROCESSING (" + currentStep.AmbiguityReason + ") -> " + currentStep.GetReadableText());

            yield return StartCoroutine(IngredientProcessor.Singleton.ResolveAndProcessStep(currentStep));

            // The roll happens before the log line is written, so the log can explain the roll,
            // not just the outcome text, which can look fine even when the roll went badly.
            UncertaintyRoll roll = UncertaintyRandomizer.Singleton.RollForUnclearStep(currentStep);
            float stepQuality = UncertaintyRandomizer.Singleton.GetStepQualityFromRoll(roll);

            LogProcessedResults(IngredientProcessor.Singleton.LastProcessedResults, roll.Magnitude, roll.IsPositive ? "well" : "badly");

            // An ingredient the player never named was entirely the agent's own call, so it counts
            // immediately, the player never gets a chance to accept or reject something they never chose.
            if (IngredientProcessor.Singleton.LastIngredientWasAgentChosen)
            {
                ForceAcceptResults(IngredientProcessor.Singleton.LastProcessedResults);
            }

            string messageText = roll.IsPositive ? "I got it!" : "I'm lost... so I will " + currentStep.Verb + " it.";
            yield return StartCoroutine(ShowStepMessageWithDelay(messageText));

            CurrentPotion.RecordStep(currentStep, stepQuality, true);
            UpdateCauldronTemperatureDisplay();

            if (roll.IsPositive)
            {
                OnStepDoneWell.Invoke();
            }
            else
            {
                OnStepDoneBadly.Invoke();
            }

            ShowStepFeedback(roll.IsPositive, roll.Magnitude);
        }


        // Handles "do nothing" paired with an ingredient. It is a straight 50/50 gamble: either the
        // ingredient goes into the cauldron exactly as it was, completely raw, and counts immediately
        // (the player never gets a say in a coin flip, so it force accepts just like an agent's own
        // pick would), or genuinely nothing happens to it at all. Either way it is logged the same
        // way any other processed ingredient is, so the player can see the coin flip actually landed.
        private IEnumerator RunDoNothingStep(CommandStep currentStep, string stepHeader)
        {
            Debug.Log("[AiAgent] " + stepHeader + "DO NOTHING -> " + currentStep.GetReadableText());

            bool leavesItRaw = Random.Range(0, 100) < 50;
            string temperatureNote = BuildTemperatureNote();
            List<ProcessedIngredientResult> results = new List<ProcessedIngredientResult>();

            for (int i = 0; i < currentStep.Ingredients.Count; i++)
            {
                ProcessedIngredientResult result = new ProcessedIngredientResult();
                result.IngredientName = currentStep.Ingredients[i];
                result.UsedArchetype = ToolArchetype.None;
                result.UsedTimeBracket = TimeBracket.Normal;
                result.VerbWordUsed = currentStep.Verb;
                result.QualityPercent = leavesItRaw ? 100 : 0;
                result.CauldronTemperatureNote = temperatureNote;
                // A raw pass-through has no result state of its own, it stays exactly what it was.
                result.ResultStateName = leavesItRaw ? result.IngredientName : "nothing happened";

                results.Add(result);
                AppendReasoningLine(result.GetLogLine());
            }

            string messageText = leavesItRaw ? "I did nothing, and that worked out." : "I did nothing, and nothing happened.";
            yield return StartCoroutine(ShowStepMessageWithDelay(messageText));

            if (leavesItRaw)
            {
                // Doing nothing worked, so the ingredient really did make it into the cauldron. The
                // player never made a call here either, a coin flip did, so it force accepts.
                CurrentPotion.RecordStep(currentStep, 1f, true);
                UpdateCauldronTemperatureDisplay();
                ForceAcceptResults(results);
                OnStepDoneWell.Invoke();
            }
            else
            {
                // Genuinely nothing happened, the ingredient never touches the cauldron at all.
                OnStepDoneBadly.Invoke();
            }

            ShowStepFeedback(leavesItRaw, leavesItRaw ? 100 : 0);
        }


        // Asks the language model to fill in the blank in an unclear step, when a model is available.
        private IEnumerator AskModelAboutStep(CommandStep unclearStep)
        {
            if (!AskModelWhenUnclear || LlmCommandAdvisor.Singleton == null || !LlmCommandAdvisor.Singleton.IsModelAvailable())
            {
                yield break;
            }

            List<string> choiceTexts = BuildChoicesForStep(unclearStep);

            if (choiceTexts.Count == 0)
            {
                yield break;
            }

            string questionText = BuildModelQuestionText(unclearStep);

            yield return StartCoroutine(LlmCommandAdvisor.Singleton.AskMultipleChoice(questionText, choiceTexts));

            string modelAnswer = LlmCommandAdvisor.Singleton.GetLastAnswer();
            ApplyModelAnswerToStep(unclearStep, modelAnswer);
        }


        ///// Custom Methods - Action Methods /////

        // Action method: lays out everything the step already knows so the model reasons about how
        // the ingredient, tool and action relate to each other, not just about the isolated gap.
        private string BuildModelQuestionText(CommandStep unclearStep)
        {
            string knownSoFar = string.IsNullOrEmpty(unclearStep.Verb) ? "do something" : unclearStep.Verb;

            if (unclearStep.HasIngredient())
            {
                knownSoFar += " the " + unclearStep.GetIngredientsText();
            }

            if (!string.IsNullOrEmpty(unclearStep.Tool))
            {
                knownSoFar += " using the " + unclearStep.Tool;
            }

            if (unclearStep.HasAmount())
            {
                knownSoFar += ", " + unclearStep.Amount + (string.IsNullOrEmpty(unclearStep.Unit) ? "" : " " + unclearStep.Unit);
            }

            return "The cook wants to " + knownSoFar + ", but " + unclearStep.AmbiguityReason + ". " +
                   "Think about how the ingredient, tool and action the cook already named would " +
                   "normally go together, then answer with the single choice that fits best.";
        }


        // Action method: works out what the model is allowed to answer for this particular gap.
        private List<string> BuildChoicesForStep(CommandStep unclearStep)
        {
            List<string> choiceTexts = new List<string>();

            if (WordDatabase.Singleton == null)
            {
                return choiceTexts;
            }

            // A missing ingredient is guessed from the ingredients on the stand.
            if (!unclearStep.HasIngredient() &&
                CommandParser.Singleton.VerbsNeedingIngredient.Contains(unclearStep.Verb.ToLower()))
            {
                for (int i = 0; i < WordDatabase.Singleton.IngredientWords.Count; i++)
                {
                    choiceTexts.Add(WordDatabase.Singleton.IngredientWords[i].WordText);
                }

                return choiceTexts;
            }

            // A missing tool is guessed from the tools on the stand.
            if (string.IsNullOrEmpty(unclearStep.Tool) &&
                CommandParser.Singleton.VerbsNeedingTool.Contains(unclearStep.Verb.ToLower()))
            {
                for (int i = 0; i < WordDatabase.Singleton.ToolWords.Count; i++)
                {
                    choiceTexts.Add(WordDatabase.Singleton.ToolWords[i].WordText);
                }

                return choiceTexts;
            }

            // A missing amount is guessed from the numbers the player could have said.
            if (!unclearStep.HasAmount())
            {
                choiceTexts = WordDatabase.Singleton.GetWordTextsOfCategory(WordCategory.Number);
                return choiceTexts;
            }

            // A missing unit is guessed from the units the player could have said.
            if (string.IsNullOrEmpty(unclearStep.Unit))
            {
                choiceTexts = WordDatabase.Singleton.GetWordTextsOfCategory(WordCategory.Unit);
                return choiceTexts;
            }

            return choiceTexts;
        }


        // Action method: writes the model's guess into the step so the agent acts on it.
        private void ApplyModelAnswerToStep(CommandStep unclearStep, string modelAnswer)
        {
            if (string.IsNullOrEmpty(modelAnswer))
            {
                return;
            }

            WordDefinition answerDefinition = WordDatabase.Singleton.GetWordDefinition(modelAnswer.Trim());

            if (answerDefinition == null)
            {
                return;
            }

            switch (answerDefinition.Category)
            {
                case WordCategory.Ingredient:
                    unclearStep.Ingredients.Add(answerDefinition.WordText);
                    break;

                case WordCategory.Tool:
                    unclearStep.Tool = answerDefinition.WordText;
                    break;

                case WordCategory.Unit:
                    unclearStep.Unit = answerDefinition.WordText;
                    break;

                case WordCategory.Number:
                    int parsedNumber = 0;

                    if (int.TryParse(answerDefinition.WordText, out parsedNumber))
                    {
                        unclearStep.Amount = parsedNumber;
                    }
                    break;
            }

            Debug.Log("[AiAgent] The model filled the gap with: " + answerDefinition.WordText);
        }


        // Action method: turns a roll into the sentence the agent says about its guess.
        // Returns either "I got it!" or "I'm lost... so I will do [action]"
        private string BuildGuessSentence(CommandStep unclearStep, UncertaintyRoll roll)
        {
            if (roll.IsPositive)
            {
                return "I got it!";
            }

            // Build description of what will happen
            string whatIllDo = "";

            if (unclearStep.AmbiguityReason.Contains("amount"))
            {
                if (roll.Magnitude >= 50)
                {
                    whatIllDo = "pour in way too much";
                }
                else
                {
                    whatIllDo = "add barely any";
                }
            }
            else if (unclearStep.AmbiguityReason.Contains("tool"))
            {
                whatIllDo = "grab a random tool";
            }
            else if (unclearStep.AmbiguityReason.Contains("ingredient"))
            {
                whatIllDo = "pick a random ingredient";
            }
            else
            {
                whatIllDo = "guess at what you meant";
            }

            return "I'm lost... so I will " + whatIllDo + ".";
        }


        // Action method: shows a step message with a minimum delay before continuing.
        private IEnumerator ShowStepMessageWithDelay(string messageText)
        {
            if (StepMessageLabel != null)
            {
                StepMessageLabel.text = messageText;
            }

            yield return new WaitForSeconds(MinTimeBetweenMessages);
        }


        // Action method: tells the feedback component about whatever the cauldron's temperature is
        // right now, so the text in the middle of the cauldron can turn red or blue to match.
        private void UpdateCauldronTemperatureDisplay()
        {
            if (FeedbackVisualizer.Singleton != null)
            {
                FeedbackVisualizer.Singleton.ShowCauldronTemperature(CurrentPotion.Temperature);
            }
        }


        // Action method: asks the feedback component to colour the panels for this step.
        private void ShowStepFeedback(bool wasPositive, int magnitude)
        {
            if (FeedbackVisualizer.Singleton != null)
            {
                FeedbackVisualizer.Singleton.ShowFeedback(wasPositive, magnitude);
            }
        }


        // Action method: makes the reasoning panel visible. Called at the start of every round.
        public void ShowReasoningPanel()
        {
            if (ReasoningPanelObject != null)
            {
                ReasoningPanelObject.SetActive(true);
            }
        }


        // Action method: hides the reasoning panel without touching its logged text. Called the
        // moment Finish Brewing is pressed, the log is kept around until the next round clears it.
        public void HideReasoningPanel()
        {
            if (ReasoningPanelObject != null)
            {
                ReasoningPanelObject.SetActive(false);
            }
        }


        // Action method: empties the reasoning log. Called only when a new round of brewing starts.
        public void ClearReasoningLog()
        {
            _reasoningLogLines.Clear();

            if (ReasoningLogLabel != null)
            {
                ReasoningLogLabel.text = "";
            }
        }


        // Action method: adds one "ingredient -> result" line to the reasoning panel. Once the log
        // is full the oldest line is dropped, so the newest line is always visible and nothing ever
        // spills past the panel's borders.
        private void AppendReasoningLine(string lineText)
        {
            _reasoningLogLines.Add(lineText);

            while (_reasoningLogLines.Count > MaxReasoningLogLines)
            {
                _reasoningLogLines.RemoveAt(0);
            }

            if (ReasoningLogLabel != null)
            {
                ReasoningLogLabel.text = string.Join("\n", _reasoningLogLines);
            }
        }


        // Action method: logs every ingredient this step processed, records it on the potion, and
        // remembers the very last one as the one AcceptItemState would confirm right now. The quality
        // percentage and word explain why the cauldron feedback might disagree with how good the
        // result text looks on its own, and the cauldron's current temperature is noted alongside it.
        private void LogProcessedResults(List<ProcessedIngredientResult> processedResults, int qualityPercent, string qualityWord)
        {
            string temperatureNote = BuildTemperatureNote();

            for (int i = 0; i < processedResults.Count; i++)
            {
                ProcessedIngredientResult result = processedResults[i];
                result.QualityPercent = qualityPercent;
                result.QualityWord = qualityWord;
                result.CauldronTemperatureNote = temperatureNote;

                AppendReasoningLine(result.GetLogLine());
                CurrentPotion.RecordProcessedResult(result);
                _lastAcceptableResult = result;
            }
        }


        // Action method: turns the cauldron's current temperature into the reasoning log's bracket
        // note ("heated" / "cooled"), or an empty string while the cauldron has never been touched.
        private string BuildTemperatureNote()
        {
            if (string.IsNullOrEmpty(CurrentPotion.Temperature))
            {
                return "";
            }

            return CurrentPotion.Temperature.ToLower() == "hot" ? "heated" : "cooled";
        }


        // Action method: accepts every result in a batch immediately, without waiting on the
        // AcceptItemState button, because the player never had any say in picking the ingredient.
        private void ForceAcceptResults(List<ProcessedIngredientResult> processedResults)
        {
            for (int i = 0; i < processedResults.Count; i++)
            {
                ProcessedIngredientResult result = processedResults[i];

                if (FindAcceptedResult(result.IngredientName) != null)
                {
                    continue;
                }

                _acceptedItems.Add(result);

                if (WordSelectionManager.Singleton != null)
                {
                    WordSelectionManager.Singleton.LockIngredientButton(result.IngredientName);
                }

                if (_lastAcceptableResult == result)
                {
                    _lastAcceptableResult = null;
                }
            }

            RefreshAcceptedItemsLabel();
        }


        // Input-based trigger: wired to the AcceptItemState button next to the reasoning log. Confirms
        // whatever ingredient was processed most recently, locks its raw ingredient button so it
        // cannot be picked again, and adds its result to the accepted items panel.
        public void AcceptItemState()
        {
            if (_lastAcceptableResult == null)
            {
                return;
            }

            _acceptedItems.Add(_lastAcceptableResult);
            RefreshAcceptedItemsLabel();

            if (WordSelectionManager.Singleton != null)
            {
                WordSelectionManager.Singleton.LockIngredientButton(_lastAcceptableResult.IngredientName);
            }

            _lastAcceptableResult = null;
        }


        // Action method: finds an accepted result for one ingredient, or null when it was never
        // accepted. GameSequenceManager reads this while scoring processed ingredient requirements.
        public ProcessedIngredientResult FindAcceptedResult(string ingredientName)
        {
            for (int i = 0; i < _acceptedItems.Count; i++)
            {
                if (_acceptedItems[i].IngredientName == ingredientName)
                {
                    return _acceptedItems[i];
                }
            }

            return null;
        }


        // Action method: rewrites the accepted items panel with every accepted result so far.
        private void RefreshAcceptedItemsLabel()
        {
            if (TextAcceptedItems == null)
            {
                return;
            }

            List<string> lines = new List<string>();

            for (int i = 0; i < _acceptedItems.Count; i++)
            {
                lines.Add(_acceptedItems[i].ResultStateName);
            }

            TextAcceptedItems.text = string.Join("\n", lines);
        }


        // Action method: clears every accepted item and the pending "last processed" pointer.
        // Called only when a new round of brewing starts.
        public void ClearAcceptedItems()
        {
            _acceptedItems.Clear();
            _lastAcceptableResult = null;

            if (TextAcceptedItems != null)
            {
                TextAcceptedItems.text = "";
            }
        }


        // Action method: writes the short status line at the top of the agent panel.
        private void SetAgentStatus(string statusText)
        {
            if (AgentStatusLabel != null)
            {
                AgentStatusLabel.text = statusText;
            }
        }


        // Action method: tells other scripts whether the agent is busy right now.
        public bool IsWorking()
        {
            return _isWorking;
        }


        // Action method: empties the potion record. Called only by GameSequenceManager at the very
        // start of a new round, never between instructions within the same round.
        public void ResetPotionState()
        {
            CurrentPotion.ResetState();
        }
    }
}


// Implementation steps:
// 1. Place this component on the AiAgent panel object in the scene.
// 2. Link AgentStatusLabel to the title TMP text (shows "Working on step X of Y" / "Finished").
// 3. Link StepMessageLabel to a TMP text on a hovering panel above the agent panel, this is the
//    only place the agent's per-step reasoning is shown ("I got it!" / "I'm lost... so I will ...").
// 4. Hook the Submit button's onClick to ExecutePlayerSentence().
// 5. Hook OnSequenceFinished to GameSequenceManager.EvaluatePotion() so brewing can start.
// 6. Adjust MinTimeBetweenMessages to control message display speed.
// 7. Link ReasoningLogLabel and TextAcceptedItems to TMP texts placed in the Menu panel's screen
//    space, and ReasoningPanelObject to whatever object should show and hide with it.
// 8. Add an AcceptItemState button next to the reasoning log, hook its onClick to AcceptItemState().
//    It confirms whatever ingredient was processed most recently.
// 9. MaxReasoningLogLines caps how many lines the reasoning log keeps, raise it if the panel is tall.
