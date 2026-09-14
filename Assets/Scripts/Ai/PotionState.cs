// Keeps a record of everything the agent actually did to the cauldron during one round.

using System.Collections.Generic;
using UnityEngine;


namespace NTGD124
{
    [System.Serializable]
    public class PotionState
    {
        ///// Public Variables /////

        [Header("What Really Ended Up In The Cauldron")]
        public List<string> AddedIngredients = new List<string>();
        public List<string> UsedTools = new List<string>();
        public List<string> PerformedVerbs = new List<string>();

        [Header("How The Cauldron Was Actually Kept")]
        // Stays empty when the agent never made the cauldron hot or cold at all.
        public string Temperature = "";
        public int MinutesHeld = -1;

        [Header("How Well Each Step Went (0 to 1)")]
        public List<float> StepQualities = new List<float>();

        [Header("How Many Steps The Agent Had To Guess")]
        public int GuessedStepCount = 0;

        [Header("What Each Processed Ingredient Actually Became")]
        public List<ProcessedIngredientResult> ProcessedResults = new List<ProcessedIngredientResult>();


        ///// Custom Methods - Recording /////

        // Action method: clears the record so a new round starts from an empty cauldron.
        public void ResetState()
        {
            AddedIngredients.Clear();
            UsedTools.Clear();
            PerformedVerbs.Clear();
            StepQualities.Clear();

            Temperature = "";
            MinutesHeld = -1;
            GuessedStepCount = 0;
            ProcessedResults.Clear();
        }


        // Action method: writes down what one ingredient became once its step was fully resolved.
        public void RecordProcessedResult(ProcessedIngredientResult result)
        {
            ProcessedResults.Add(result);
        }


        // Action method: finds the processed outcome for one ingredient, or null when it went in
        // without ever being processed (added by a plain verb like "put" or "pour").
        public ProcessedIngredientResult FindProcessedResult(string ingredientName)
        {
            for (int i = 0; i < ProcessedResults.Count; i++)
            {
                if (ProcessedResults[i].IngredientName == ingredientName)
                {
                    return ProcessedResults[i];
                }
            }

            return null;
        }


        // Action method: writes down one finished step and how well it went.
        public void RecordStep(CommandStep finishedStep, float stepQuality, bool wasGuessed)
        {
            // One step can carry several ingredients, so every one of them goes into the cauldron.
            for (int i = 0; i < finishedStep.Ingredients.Count; i++)
            {
                if (!AddedIngredients.Contains(finishedStep.Ingredients[i]))
                {
                    AddedIngredients.Add(finishedStep.Ingredients[i]);
                }
            }

            if (!string.IsNullOrEmpty(finishedStep.Tool) && !UsedTools.Contains(finishedStep.Tool))
            {
                UsedTools.Add(finishedStep.Tool);
            }

            if (!string.IsNullOrEmpty(finishedStep.Verb) && !PerformedVerbs.Contains(finishedStep.Verb))
            {
                PerformedVerbs.Add(finishedStep.Verb);
            }

            ReadTemperatureFromStep(finishedStep);
            ReadMinutesFromStep(finishedStep);

            StepQualities.Add(Mathf.Clamp01(stepQuality));

            if (wasGuessed)
            {
                GuessedStepCount++;
            }
        }


        // Action method: works out whether this step made the cauldron hot or cold.
        private void ReadTemperatureFromStep(CommandStep finishedStep)
        {
            string quality = finishedStep.Quality.ToLower();
            string verb = finishedStep.Verb.ToLower();

            // The newest temperature word wins, because it is the last thing the agent did.
            if (quality == "hot" || verb == "heat")
            {
                Temperature = "hot";
            }
            else if (quality == "cold" || quality == "cool")
            {
                Temperature = "cold";
            }
        }


        // Action method: works out how long the cauldron was held at that temperature.
        private void ReadMinutesFromStep(CommandStep finishedStep)
        {
            if (!finishedStep.HasAmount())
            {
                return;
            }

            string unit = finishedStep.Unit.ToLower();

            if (unit == "minute")
            {
                MinutesHeld = finishedStep.Amount;
            }
            else if (unit == "hour")
            {
                // An hour is stored as minutes so the order and the result can be compared directly.
                MinutesHeld = finishedStep.Amount * 60;
            }
        }


        ///// Custom Methods - Reading /////

        // Action method: returns how well the whole potion was made, from 0 to 1.
        public float GetAverageQuality()
        {
            if (StepQualities.Count == 0)
            {
                return 0f;
            }

            float total = 0f;

            for (int i = 0; i < StepQualities.Count; i++)
            {
                total += StepQualities[i];
            }

            return total / StepQualities.Count;
        }


        // Action method: counts the ingredients that went in but were never asked for.
        public int CountExtraIngredients(List<string> requiredIngredients)
        {
            int extraCount = 0;

            for (int i = 0; i < AddedIngredients.Count; i++)
            {
                if (!requiredIngredients.Contains(AddedIngredients[i]))
                {
                    extraCount++;
                }
            }

            return extraCount;
        }


        // Action method: tells whether the cauldron ended up at the temperature that was ordered.
        public bool MatchesTemperature(string requiredTemperature)
        {
            if (string.IsNullOrEmpty(Temperature))
            {
                return false;
            }

            return Temperature.ToLower() == requiredTemperature.ToLower();
        }


        // Action method: tells whether the cauldron was held for the time that was ordered.
        public bool MatchesMinutes(int requiredMinutes)
        {
            return MinutesHeld == requiredMinutes;
        }


        // Action method: writes the finished potion out as a readable summary.
        public string GetSummaryText()
        {
            string summaryText = "Ingredients used: " + JoinOrNone(AddedIngredients) + "\n";
            summaryText += "Tools used: " + JoinOrNone(UsedTools) + "\n";
            summaryText += "Actions done: " + JoinOrNone(PerformedVerbs) + "\n";
            summaryText += "Cauldron ended up: " + (string.IsNullOrEmpty(Temperature) ? "never heated or cooled" : Temperature) + "\n";
            summaryText += "Held for: " + (MinutesHeld >= 0 ? MinutesHeld + " minutes" : "no time was given") + "\n";
            summaryText += "Steps the agent had to guess: " + GuessedStepCount + "\n";
            summaryText += "Average quality: " + Mathf.RoundToInt(GetAverageQuality() * 100f) + "%";

            return summaryText;
        }


        // Action method: writes every "ingredient -> result" line, for the finished potion panel.
        // Returns an empty string when nothing was ever processed with a tool or time.
        public string GetProcessedResultsText()
        {
            if (ProcessedResults.Count == 0)
            {
                return "";
            }

            List<string> lines = new List<string>();

            for (int i = 0; i < ProcessedResults.Count; i++)
            {
                lines.Add(ProcessedResults[i].GetLogLine());
            }

            return string.Join("\n", lines);
        }


        // Action method: joins a list with commas, or says "none" when the list is empty.
        private string JoinOrNone(List<string> sourceList)
        {
            if (sourceList.Count == 0)
            {
                return "none";
            }

            return string.Join(", ", sourceList);
        }
    }
}


// Implementation steps:
// 1. This file only holds data, it is not attached to a GameObject.
// 2. AiAgentBehaviour owns one PotionState and fills it while it works.
// 3. GameSequenceManager reads it at the end of the round to decide win or loss.
// 4. Temperature and MinutesHeld are read straight out of the steps, so no extra wiring is needed.
