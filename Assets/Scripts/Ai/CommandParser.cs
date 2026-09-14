// Turns the player's list of one-word blocks into an ordered list of steps and marks what is missing.

using System.Collections.Generic;
using UnityEngine;


namespace NTGD124
{
    public class CommandParser : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)][SerializeField] private string _componentDescription =
            "Cuts the clunky word sentence into steps, one per verb. Every non verb word (an ingredient, " +
            "a tool, an amount) attaches to whichever verb sits closest to it, whether that word came " +
            "before or after the verb, so a batch like 'apple / watermelon' stays together and a tool " +
            "or action placed on either side of an ingredient still binds to it correctly. Steps that " +
            "are still missing a target, a tool or an amount after that are marked ambiguous, and that " +
            "is where the agent has to guess.";


        ///// Public Variables /////

        [Header("Singleton Access")]
        public static CommandParser Singleton;

        // "do nothing" is deliberately left out of all three lists below. It is the one verb that
        // never counts as missing anything, so an ingredient paired with it always resolves as a
        // clear step and goes into the cauldron completely raw (see IngredientProcessor.IsTriadEligible).

        [Header("Verbs That Need An Amount")]
        public List<string> VerbsNeedingAmount = new List<string> { "pour", "wait", "cook", "heat", "stir", "mix", "blend", "cut" };

        [Header("Verbs That Need A Target Ingredient")]
        public List<string> VerbsNeedingIngredient = new List<string> { "put", "pour", "cut", "peel", "blend", "pull", "mix" };

        [Header("Verbs That Need A Tool")]
        public List<string> VerbsNeedingTool = new List<string> { "cut", "peel", "blend", "stir", "mix" };


        ///// Unity Methods /////

        private void Awake()
        {
            Singleton = this;
        }


        ///// Custom Methods - Trigger Methods /////

        // Called by AiAgentBehaviour when the player submits a sentence.
        public List<CommandStep> ParseSentence(List<string> chosenWords)
        {
            List<CommandStep> parsedSteps = BuildStepsFromWords(chosenWords);
            MarkAmbiguousSteps(parsedSteps);

            Debug.Log("[CommandParser] Sentence split into " + parsedSteps.Count + " step(s).");

            return parsedSteps;
        }


        ///// Custom Methods - Action Methods /////

        // Action method: groups the words into steps by nearest verb, not just left to right.
        // A word attaches to whichever verb sits closest to it, whether that verb came before or
        // after it. This is what lets "apple / watermelon / cut / knife" and "knife / cut / apple /
        // watermelon" both become one single "cut the apple, watermelon with the knife" step, and
        // what lets an ingredient batch like "apple / watermelon" stay together and attach as a
        // whole to whichever tool or action sits next to it.
        private List<CommandStep> BuildStepsFromWords(List<string> chosenWords)
        {
            List<WordDefinition> resolvedWords = new List<WordDefinition>();

            for (int i = 0; i < chosenWords.Count; i++)
            {
                WordDefinition wordDefinition = WordDatabase.Singleton.GetWordDefinition(chosenWords[i]);

                // A word the registry does not know is simply skipped, the agent cannot read it.
                if (wordDefinition == null)
                {
                    Debug.LogWarning("[CommandParser] Unknown word ignored: " + chosenWords[i]);
                    continue;
                }

                resolvedWords.Add(wordDefinition);
            }

            List<int> verbIndices = new List<int>();

            for (int i = 0; i < resolvedWords.Count; i++)
            {
                if (resolvedWords[i].Category == WordCategory.Verb)
                {
                    verbIndices.Add(i);
                }
            }

            // No verb anywhere in the sentence: everything the player picked becomes one unclear step.
            if (verbIndices.Count == 0)
            {
                CommandStep onlyStep = new CommandStep();
                int noVerbPendingNumber = -1;

                for (int i = 0; i < resolvedWords.Count; i++)
                {
                    AttachWordToStep(resolvedWords[i], onlyStep, ref noVerbPendingNumber);
                }

                if (noVerbPendingNumber >= 0 && !onlyStep.HasAmount())
                {
                    onlyStep.Amount = noVerbPendingNumber;
                }

                List<CommandStep> noVerbStepList = new List<CommandStep>();
                noVerbStepList.Add(onlyStep);
                return noVerbStepList;
            }

            // One step per verb found, in the order the verbs were picked.
            List<CommandStep> parsedSteps = new List<CommandStep>();

            for (int i = 0; i < verbIndices.Count; i++)
            {
                CommandStep newStep = new CommandStep();
                newStep.Verb = resolvedWords[verbIndices[i]].WordText;
                parsedSteps.Add(newStep);
            }

            // Every non verb word is sorted into the group of its nearest verb, keeping each
            // group's words in the order the player originally picked them.
            List<List<WordDefinition>> wordGroups = new List<List<WordDefinition>>();

            for (int i = 0; i < verbIndices.Count; i++)
            {
                wordGroups.Add(new List<WordDefinition>());
            }

            for (int i = 0; i < resolvedWords.Count; i++)
            {
                if (resolvedWords[i].Category == WordCategory.Verb)
                {
                    continue;
                }

                int nearestVerbGroup = FindNearestVerbGroup(i, verbIndices);
                wordGroups[nearestVerbGroup].Add(resolvedWords[i]);
            }

            for (int i = 0; i < parsedSteps.Count; i++)
            {
                int pendingNumber = -1;

                for (int w = 0; w < wordGroups[i].Count; w++)
                {
                    AttachWordToStep(wordGroups[i][w], parsedSteps[i], ref pendingNumber);
                }

                if (pendingNumber >= 0 && !parsedSteps[i].HasAmount())
                {
                    parsedSteps[i].Amount = pendingNumber;
                }
            }

            return parsedSteps;
        }


        // Action method: finds which verb (by group index) sits closest to the word at wordIndex.
        // A tie (the word sits exactly between two verbs) goes to the earlier verb, so it attaches
        // backwards to whichever action was already being talked about.
        private int FindNearestVerbGroup(int wordIndex, List<int> verbIndices)
        {
            int closestGroup = 0;
            int closestDistance = int.MaxValue;

            for (int i = 0; i < verbIndices.Count; i++)
            {
                int distance = Mathf.Abs(wordIndex - verbIndices[i]);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestGroup = i;
                }
            }

            return closestGroup;
        }


        // Action method: writes one non-verb word into the step that is currently being built.
        private void AttachWordToStep(WordDefinition wordDefinition, CommandStep currentStep, ref int pendingNumber)
        {
            switch (wordDefinition.Category)
            {
                case WordCategory.Ingredient:
                    // Each word can only be clicked once, so one action carries every ingredient
                    // the player names after it, for example "put / salt / herbs / liver".
                    if (!currentStep.Ingredients.Contains(wordDefinition.WordText))
                    {
                        currentStep.Ingredients.Add(wordDefinition.WordText);
                    }
                    break;

                case WordCategory.Tool:
                    if (string.IsNullOrEmpty(currentStep.Tool))
                    {
                        currentStep.Tool = wordDefinition.WordText;
                    }
                    break;

                case WordCategory.Quality:
                    if (string.IsNullOrEmpty(currentStep.Quality))
                    {
                        currentStep.Quality = wordDefinition.WordText;
                    }
                    break;

                case WordCategory.Switch:
                    currentStep.SwitchState = wordDefinition.WordText;
                    break;

                case WordCategory.Number:
                    int parsedNumber = 0;

                    if (int.TryParse(wordDefinition.WordText, out parsedNumber))
                    {
                        if (currentStep.HasAmount())
                        {
                            pendingNumber = parsedNumber;
                        }
                        else
                        {
                            currentStep.Amount = parsedNumber;
                        }
                    }
                    break;

                case WordCategory.Unit:
                    if (string.IsNullOrEmpty(currentStep.Unit))
                    {
                        currentStep.Unit = wordDefinition.WordText;
                    }

                    // A unit gives a waiting number its meaning.
                    if (pendingNumber >= 0 && !currentStep.HasAmount())
                    {
                        currentStep.Amount = pendingNumber;
                        pendingNumber = -1;
                    }
                    break;
            }
        }


        // Action method: checks every step for missing information and writes down why it is unclear.
        private void MarkAmbiguousSteps(List<CommandStep> parsedSteps)
        {
            for (int i = 0; i < parsedSteps.Count; i++)
            {
                CommandStep checkedStep = parsedSteps[i];
                checkedStep.IsAmbiguous = false;
                checkedStep.AmbiguityReason = "";

                // No verb at all means the agent has no idea what to do with these words.
                if (string.IsNullOrEmpty(checkedStep.Verb))
                {
                    SetAmbiguous(checkedStep, "no action word was given");
                    continue;
                }

                string lowerVerb = checkedStep.Verb.ToLower();

                if (VerbsNeedingIngredient.Contains(lowerVerb) && !checkedStep.HasIngredient())
                {
                    SetAmbiguous(checkedStep, "no ingredient was named");
                }

                if (VerbsNeedingTool.Contains(lowerVerb) && string.IsNullOrEmpty(checkedStep.Tool))
                {
                    SetAmbiguous(checkedStep, "no tool was named");
                }

                if (VerbsNeedingAmount.Contains(lowerVerb) && !checkedStep.HasAmount())
                {
                    SetAmbiguous(checkedStep, "no amount was given");
                }

                // A number with no unit is just as unclear as no number at all.
                if (checkedStep.HasAmount() && string.IsNullOrEmpty(checkedStep.Unit))
                {
                    SetAmbiguous(checkedStep, "the number " + checkedStep.Amount + " has no unit");
                }
            }
        }


        // Action method: marks a step unclear and adds the reason to any reason already written.
        private void SetAmbiguous(CommandStep targetStep, string reasonText)
        {
            targetStep.IsAmbiguous = true;

            if (string.IsNullOrEmpty(targetStep.AmbiguityReason))
            {
                targetStep.AmbiguityReason = reasonText;
            }
            else
            {
                targetStep.AmbiguityReason += ", " + reasonText;
            }
        }
    }
}


// Implementation steps:
// 1. Place this component on the GameSystems object next to WordDatabase.
// 2. The three verb lists decide what counts as missing information, edit them in the Inspector.
// 3. AiAgentBehaviour calls ParseSentence() and then walks the returned list.
