// Fills in whatever a processing step is still missing (the action, the tool, or the time) and then
// works out what each ingredient in that step actually turns into.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace NTGD124
{
    public class IngredientProcessor : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)][SerializeField] private string _componentDescription =
            "Runs whenever a step has an ingredient and a verb that needs an amount (pour, wait, cook, heat, " +
            "stir, mix, blend, cut), including when the verb itself was never said. It also runs for an idle " +
            "tool or action used with no ingredient at all, as long as the verb genuinely needs a target, in " +
            "which case the agent picks the ingredient entirely on its own. Whatever of the ingredient, the " +
            "action, the tool and the time is still missing gets filled in, the language model is asked first " +
            "when one is available, and the time is always a plain weighted guess that favours short waits. " +
            "Once everything is known, every ingredient in the step is looked up in IngredientOutcomeTable to " +
            "see what it became.";


        ///// Public Variables /////

        [Header("Singleton Access")]
        public static IngredientProcessor Singleton;

        [Header("Time Guessing (weighted so short waits come up more often)")]
        public List<int> CommonMinuteOptions = new List<int> { 1, 2, 3, 4, 5, 10 };
        public List<int> RareMinuteOptions = new List<int> { 30, 60 };
        [Range(0, 100)] public int CommonMinuteChancePercent = 85;

        [Header("What Counts As Over-Processed")]
        public int NormalTimeCutoffMinutes = 10;

        [Header("Language Model Use")]
        public bool AskModelForMissingVerbOrTool = true;

        [Header("Result Of The Last Step Processed (read only)")]
        public List<ProcessedIngredientResult> LastProcessedResults = new List<ProcessedIngredientResult>();

        // True when the step just processed never named an ingredient at all, so the agent chose one
        // entirely on its own. The player never touched that ingredient, so AiAgentBehaviour force
        // accepts it instead of waiting on the AcceptItemState button.
        public bool LastIngredientWasAgentChosen = false;


        ///// Private Variables /////

        // Which archetype a verb naturally belongs to, used to keep a guessed tool and a guessed
        // verb pointing at the same kind of processing instead of picking two unrelated ones.
        private static readonly Dictionary<string, ToolArchetype> _verbArchetypes = new Dictionary<string, ToolArchetype>
        {
            { "cut", ToolArchetype.Cutting },
            { "stir", ToolArchetype.Stirring },
            { "mix", ToolArchetype.Stirring },
            { "blend", ToolArchetype.Blending },
        };

        // Holds whatever the model answered for the choice question that is running right now.
        private string _pendingChoiceAnswer = "";


        ///// Unity Methods /////

        private void Awake()
        {
            Singleton = this;
        }


        ///// Custom Methods - Reading /////

        // Action method: true when this step should go through action/tool/time reasoning instead
        // of the plain single-gap guess. Covers a step that already has an ingredient and either no
        // verb yet or a verb that needs a duration, and also an "idle" tool or action used on its
        // own with no ingredient at all, as long as there is an actual reason to believe an
        // ingredient belongs there (the verb itself needs a target, or a tool was given with no
        // verb yet). A bare verb like "wait" or "stir" with nothing else never invents an ingredient.
        public bool IsTriadEligible(CommandStep step)
        {
            if (step == null)
            {
                return false;
            }

            bool verbIsEmpty = string.IsNullOrEmpty(step.Verb);
            bool verbNeedsAmount = !verbIsEmpty && CommandParser.Singleton.VerbsNeedingAmount.Contains(step.Verb.ToLower());

            if (step.HasIngredient())
            {
                return verbIsEmpty || verbNeedsAmount;
            }

            if (verbIsEmpty)
            {
                // Nothing named at all yet, only worth entering when a tool at least hints that
                // something is meant to be processed.
                return !string.IsNullOrEmpty(step.Tool);
            }

            bool verbNeedsIngredient = CommandParser.Singleton.VerbsNeedingIngredient.Contains(step.Verb.ToLower());

            return verbNeedsIngredient && verbNeedsAmount;
        }


        ///// Coroutines /////

        // Fills in whatever of verb, tool and time this step is missing, then works out what each
        // ingredient becomes. Read the outcome back through LastProcessedResults once this finishes.
        public IEnumerator ResolveAndProcessStep(CommandStep step)
        {
            LastProcessedResults = new List<ProcessedIngredientResult>();
            LastIngredientWasAgentChosen = false;

            if (!step.HasIngredient())
            {
                yield return StartCoroutine(FillMissingIngredient(step));
                LastIngredientWasAgentChosen = true;
            }

            if (string.IsNullOrEmpty(step.Verb))
            {
                yield return StartCoroutine(FillMissingVerb(step));
            }

            bool verbNeedsTool = !string.IsNullOrEmpty(step.Verb) &&
                                  CommandParser.Singleton.VerbsNeedingTool.Contains(step.Verb.ToLower());

            if (verbNeedsTool && string.IsNullOrEmpty(step.Tool))
            {
                yield return StartCoroutine(FillMissingTool(step));
            }

            if (!step.HasAmount() || string.IsNullOrEmpty(step.Unit))
            {
                FillMissingTime(step);
            }

            BuildProcessedResults(step);
        }


        // Coroutine: picks an ingredient for a step that never named one at all, preferring whatever
        // the current order still needs, so the agent's own pick is at least useful instead of random.
        private IEnumerator FillMissingIngredient(CommandStep step)
        {
            List<string> candidates = GetIngredientCandidates();

            string questionText = "The cook did something" +
                (string.IsNullOrEmpty(step.Verb) ? "" : " (" + step.Verb + ")") +
                (string.IsNullOrEmpty(step.Tool) ? "" : " with the " + step.Tool) +
                " but never named an ingredient. Which ingredient does the order still need most?";

            yield return StartCoroutine(AskModelToChoose(questionText, candidates));

            string chosenIngredient = PickAnswerOrRandom(candidates);

            if (!string.IsNullOrEmpty(chosenIngredient))
            {
                step.Ingredients.Add(chosenIngredient);
            }
        }


        // Coroutine: picks a verb for a step that never got one, preferring the model's answer when
        // a tool was already named and a model is available, otherwise a random pick from whatever
        // verbs make sense for that tool.
        private IEnumerator FillMissingVerb(CommandStep step)
        {
            List<string> candidates = new List<string>(CommandParser.Singleton.VerbsNeedingAmount);

            if (!string.IsNullOrEmpty(step.Tool))
            {
                List<string> toolMatched = FilterVerbsByArchetype(candidates, IngredientOutcomeTable.GetArchetypeForTool(step.Tool));

                if (toolMatched.Count > 0)
                {
                    candidates = toolMatched;
                }
            }

            string questionText = "The cook picked the " + step.GetIngredientsText() +
                (string.IsNullOrEmpty(step.Tool) ? "" : " and the " + step.Tool) +
                " but never said what to do with them. What action fits best?";

            yield return StartCoroutine(AskModelToChoose(questionText, candidates));

            step.Verb = PickAnswerOrRandom(candidates);
        }


        // Coroutine: picks a tool for a step that needs one but never got one, preferring the
        // model's answer when available, otherwise a random pick from tools matching the verb.
        private IEnumerator FillMissingTool(CommandStep step)
        {
            List<string> candidates = GetToolCandidates();

            if (!string.IsNullOrEmpty(step.Verb) && _verbArchetypes.ContainsKey(step.Verb.ToLower()))
            {
                List<string> archetypeMatched = FilterToolsByArchetype(candidates, _verbArchetypes[step.Verb.ToLower()]);

                if (archetypeMatched.Count > 0)
                {
                    candidates = archetypeMatched;
                }
            }

            string questionText = "The cook wants to " + step.Verb + " the " + step.GetIngredientsText() +
                                  " but never said with what. What tool fits best?";

            yield return StartCoroutine(AskModelToChoose(questionText, candidates));

            step.Tool = PickAnswerOrRandom(candidates);
        }


        // Coroutine: asks the model to pick from a short list, when a model is actually available.
        // Leaves _pendingChoiceAnswer empty when it cannot or does not answer.
        private IEnumerator AskModelToChoose(string questionText, List<string> choiceTexts)
        {
            _pendingChoiceAnswer = "";

            if (!AskModelForMissingVerbOrTool || choiceTexts.Count == 0 ||
                LlmCommandAdvisor.Singleton == null || !LlmCommandAdvisor.Singleton.IsModelAvailable())
            {
                yield break;
            }

            yield return StartCoroutine(LlmCommandAdvisor.Singleton.AskMultipleChoice(questionText, choiceTexts));

            _pendingChoiceAnswer = LlmCommandAdvisor.Singleton.GetLastAnswer();
        }


        ///// Custom Methods - Action Methods /////

        // Action method: the time slot is never asked to the model, it is always a plain weighted
        // guess so short waits come up far more often than the long ones.
        private void FillMissingTime(CommandStep step)
        {
            // A number the player already picked (say, 60) is kept exactly as it was. Only a truly
            // missing amount gets a fresh weighted guess, otherwise a player who picked a big number
            // but forgot to also click a unit word would have their choice thrown away and re-rolled,
            // which made the long, over-processed results far harder to reach on purpose than they
            // should be.
            if (!step.HasAmount())
            {
                bool pickCommon = Random.Range(0, 100) < CommonMinuteChancePercent;
                List<int> pool = pickCommon ? CommonMinuteOptions : RareMinuteOptions;

                if (pool == null || pool.Count == 0)
                {
                    pool = CommonMinuteOptions.Count > 0 ? CommonMinuteOptions : new List<int> { 5 };
                }

                step.Amount = pool[Random.Range(0, pool.Count)];
            }

            if (string.IsNullOrEmpty(step.Unit))
            {
                step.Unit = "minute";
            }
        }


        // Action method: works out what every ingredient in the now fully resolved step became.
        private void BuildProcessedResults(CommandStep step)
        {
            ToolArchetype archetype = IngredientOutcomeTable.GetArchetypeForTool(step.Tool);
            int totalMinutes = step.HasAmount() ? ConvertToMinutes(step.Amount, step.Unit) : 0;
            TimeBracket bracket = totalMinutes <= NormalTimeCutoffMinutes ? TimeBracket.Normal : TimeBracket.Over;

            for (int i = 0; i < step.Ingredients.Count; i++)
            {
                string ingredientName = step.Ingredients[i];

                ProcessedIngredientResult result = new ProcessedIngredientResult();
                result.IngredientName = ingredientName;
                result.UsedArchetype = archetype;
                result.UsedTimeBracket = bracket;
                result.ResultStateName = IngredientOutcomeTable.GetResultText(ingredientName, archetype, bracket);
                result.ToolWordUsed = step.Tool;
                result.VerbWordUsed = step.Verb;
                result.MinutesUsed = totalMinutes;
                result.WasAgentChosenIngredient = LastIngredientWasAgentChosen;

                LastProcessedResults.Add(result);
            }
        }


        // Action method: turns an amount and a unit into a plain minute count.
        private int ConvertToMinutes(int amount, string unit)
        {
            if (!string.IsNullOrEmpty(unit) && unit.ToLower() == "hour")
            {
                return amount * 60;
            }

            return amount;
        }


        // Action method: uses the model's answer when it gave one of the actual choices, otherwise
        // falls back to a plain random pick so the step always ends up resolved either way.
        private string PickAnswerOrRandom(List<string> candidates)
        {
            if (candidates.Count == 0)
            {
                return "";
            }

            if (!string.IsNullOrEmpty(_pendingChoiceAnswer) && candidates.Contains(_pendingChoiceAnswer.Trim()))
            {
                return _pendingChoiceAnswer.Trim();
            }

            return candidates[Random.Range(0, candidates.Count)];
        }


        // Action method: whatever the current order still asks for, falling back to every ingredient
        // in the database when there is no order to read from.
        private List<string> GetIngredientCandidates()
        {
            List<string> names = new List<string>();

            if (RecipeGenerator.Singleton != null && RecipeGenerator.Singleton.CurrentRecipe != null &&
                RecipeGenerator.Singleton.CurrentRecipe.RequiredIngredients.Count > 0)
            {
                List<RequiredIngredientState> requiredStates = RecipeGenerator.Singleton.CurrentRecipe.RequiredIngredients;

                for (int i = 0; i < requiredStates.Count; i++)
                {
                    names.Add(requiredStates[i].IngredientName);
                }

                return names;
            }

            if (WordDatabase.Singleton != null)
            {
                for (int i = 0; i < WordDatabase.Singleton.IngredientWords.Count; i++)
                {
                    names.Add(WordDatabase.Singleton.IngredientWords[i].WordText);
                }
            }

            return names;
        }


        // Action method: every tool on the stand except the cauldron itself, which is the vessel,
        // not something you process an ingredient with.
        private List<string> GetToolCandidates()
        {
            List<string> toolTexts = new List<string>();

            if (WordDatabase.Singleton == null)
            {
                return toolTexts;
            }

            for (int i = 0; i < WordDatabase.Singleton.ToolWords.Count; i++)
            {
                string toolText = WordDatabase.Singleton.ToolWords[i].WordText;

                if (toolText.ToLower() != "cauldron")
                {
                    toolTexts.Add(toolText);
                }
            }

            return toolTexts;
        }


        // Action method: narrows a verb list down to only the verbs that match one archetype.
        private List<string> FilterVerbsByArchetype(List<string> verbCandidates, ToolArchetype archetype)
        {
            List<string> matched = new List<string>();

            if (archetype == ToolArchetype.None)
            {
                return matched;
            }

            for (int i = 0; i < verbCandidates.Count; i++)
            {
                string lowerVerb = verbCandidates[i].ToLower();

                if (_verbArchetypes.ContainsKey(lowerVerb) && _verbArchetypes[lowerVerb] == archetype)
                {
                    matched.Add(verbCandidates[i]);
                }
            }

            return matched;
        }


        // Action method: narrows a tool list down to only the tools that match one archetype.
        private List<string> FilterToolsByArchetype(List<string> toolCandidates, ToolArchetype archetype)
        {
            List<string> matched = new List<string>();

            for (int i = 0; i < toolCandidates.Count; i++)
            {
                if (IngredientOutcomeTable.GetArchetypeForTool(toolCandidates[i]) == archetype)
                {
                    matched.Add(toolCandidates[i]);
                }
            }

            return matched;
        }
    }
}


// Implementation steps:
// 1. Place this component on the GameSystems object next to CommandParser and UncertaintyRandomizer.
// 2. AiAgentBehaviour checks IsTriadEligible on every step and, when true, runs ResolveAndProcessStep
//    instead of the older single-gap guess.
// 3. CommonMinuteChancePercent controls how often the short options (1 to 10 minutes) get picked
//    over the long ones (30, 60), lower it to make the agent stew things for longer more often.
// 4. NormalTimeCutoffMinutes decides where a "normal" result turns into an "over-processed" one.
