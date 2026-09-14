// Asks the local language model what an unclear instruction most likely meant, as a multiple choice question.

using System.Collections;
using System.Collections.Generic;
using LLMUnity;
using TMPro;
using UnityEngine;


namespace NTGD124
{
    public class LlmCommandAdvisor : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)][SerializeField] private string _componentDescription =
            "The language model half of the agent. When a step is unclear this component asks the model to " +
            "pick the most likely meaning from a short list of choices. The model can only answer with one " +
            "of those choices because the answer is locked by a grammar. If no model is linked the game " +
            "still runs and simply falls back to the random guess.";


        ///// Public Variables /////

        [Header("Singleton Access")]
        public static LlmCommandAdvisor Singleton;

        [Header("Language Model")]
        // LLMAgent is the current component name in LLM for Unity. An LLMCharacter also fits here,
        // because LLMCharacter is just an older name for the same component.
        public LLMAgent MyAgent;
        public bool UseLanguageModel = true;

        [Header("UI Reference (optional)")]
        public TMP_Text ThinkingLabel;

        [Header("Timing")]
        public float MaximumWaitSeconds = 15f;

        [Header("Testing")]
        public bool LogEveryAnswer = true;


        ///// Private Variables /////

        // The answer the model gave for the question that is running right now.
        private string _lastAnswer = "";

        // True while a question is still being answered.
        private bool _isWaitingForAnswer = false;

        // Set once the model has let us down, so the game never waits on it twice.
        private bool _modelHasGivenUp = false;


        ///// Unity Methods /////

        private void Awake()
        {
            Singleton = this;
        }


        ///// Custom Methods - Reading /////

        // Action method: tells other scripts whether a usable model is actually ready to answer.
        public bool IsModelAvailable()
        {
            if (!UseLanguageModel || MyAgent == null)
            {
                return false;
            }

            // Once the model has failed to answer we stop asking, so the game never stalls again.
            if (_modelHasGivenUp)
            {
                return false;
            }

            // A remote server answers over the network and needs no local model file.
            if (MyAgent.remote)
            {
                return true;
            }

            LLM localModel = MyAgent.llm;

            if (localModel == null || localModel.failed)
            {
                return false;
            }

            // A model that has not started cannot answer, so the agent guesses on its own instead
            // of standing still and waiting for a reply that will never arrive.
            if (!localModel.started)
            {
                return false;
            }

            // An empty model name means no model has been downloaded in the LLM component yet.
            return !string.IsNullOrEmpty(localModel.model);
        }


        // Action method: returns the answer the model gave for the last question.
        public string GetLastAnswer()
        {
            return _lastAnswer;
        }


        ///// Coroutines /////

        // Asks the model one multiple choice question and waits until it answers or the wait runs out.
        public IEnumerator AskMultipleChoice(string questionText, List<string> choiceTexts)
        {
            _lastAnswer = "";

            if (!IsModelAvailable() || choiceTexts.Count == 0)
            {
                yield break;
            }

            ShowThinkingLabel("The agent is thinking about: " + questionText);

            // Locking the vocabulary means the model can only reply with one of the choices.
            MyAgent.SetGrammar(BuildMultipleChoiceGrammar(choiceTexts));

            string promptText = BuildPrompt(questionText, choiceTexts);

            _isWaitingForAnswer = true;
            // addToHistory is false so every question is asked on its own, with no memory of the last one.
            _ = MyAgent.Chat(promptText, ReceivePartialAnswer, ReceiveCompletedAnswer, false);

            float waitedSeconds = 0f;

            while (_isWaitingForAnswer && waitedSeconds < MaximumWaitSeconds)
            {
                waitedSeconds += Time.deltaTime;
                yield return null;
            }

            // If the model never answered in time the agent is left with no advice at all.
            if (_isWaitingForAnswer)
            {
                _isWaitingForAnswer = false;
                _lastAnswer = "";
                _modelHasGivenUp = true;

                Debug.LogWarning("[LlmCommandAdvisor] The model did not answer within " + MaximumWaitSeconds +
                                 " seconds. The agent will guess on its own from now on. Press Play again " +
                                 "once a model has finished loading.");
            }

            ShowThinkingLabel("");
        }


        ///// Custom Methods - Action Methods /////

        // Action method: called by the model again and again while it writes its answer.
        private void ReceivePartialAnswer(string partialAnswer)
        {
            _lastAnswer = partialAnswer;
        }


        // Action method: called by the model once the whole answer is finished.
        private void ReceiveCompletedAnswer()
        {
            _isWaitingForAnswer = false;

            if (LogEveryAnswer)
            {
                Debug.Log("[LlmCommandAdvisor] The model answered: " + _lastAnswer);
            }
        }


        // Action method: writes the multiple choice question the model has to answer.
        private string BuildPrompt(string questionText, List<string> choiceTexts)
        {
            string promptText = "You are a clumsy potion assistant reading a broken instruction made of loose words.\n";
            promptText += "Read the whole situation carefully and work out which choice actually belongs with the " +
                          "ingredient, tool and action the cook already named, before picking one.\n\n";
            promptText += "Situation: " + questionText + "\n\n";
            promptText += "Choices:\n";

            for (int i = 0; i < choiceTexts.Count; i++)
            {
                promptText += "- " + choiceTexts[i] + "\n";
            }

            promptText += "\nAnswer with exactly one of the choices above and nothing else.";

            return promptText;
        }


        // Action method: builds the grammar that forces the model to answer with one of the choices.
        private string BuildMultipleChoiceGrammar(List<string> choiceTexts)
        {
            return "root ::= (\"" + string.Join("\" | \"", choiceTexts) + "\")";
        }


        // Action method: shows or hides the small line of text that says the agent is thinking.
        private void ShowThinkingLabel(string message)
        {
            if (ThinkingLabel != null)
            {
                ThinkingLabel.text = message;
            }
        }
    }
}


// Implementation steps:
// 1. Add an "LLM" GameObject to the scene, put the LLM component on it and download a small model
//    from its Inspector (Model Manager -> Download model). A 0.5B to 1B model is enough here.
// 2. Add an LLMAgent component to the AiAgent object and link its LLM field to that LLM object.
// 3. Link that LLMAgent into MyAgent on this component.
// 4. Untick UseLanguageModel to test the game without loading a model at all.
// 5. With no model downloaded the component simply reports that no model is available, and the
//    agent falls back to its own random guessing, so the game is always playable.
