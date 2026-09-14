// Editor helper that registers the LLM for Unity package under its correct name.

using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;


namespace NTGD124
{
    public class LlmPackageFixer
    {
        ///// Private Variables /////

        // The name the package actually calls itself inside its own manifest.
        private const string CorrectPackageName = "ai.undream.llm";

        // The name that was requested by mistake, which Unity refuses to match.
        private const string WrongPackageName = "com.undreamai.llmunity";

        // Where the package is downloaded from.
        private const string PackageGitUrl = "https://github.com/undreamai/LLMUnity.git";

        // The request that is running right now, watched until it finishes.
        private static Request _runningRequest;


        ///// Custom Methods - Trigger Methods /////

        // Editor-based trigger: called from the NTGD124 menu.
        [MenuItem("NTGD124/Fix LLM Package Entry")]
        public static void FixPackageEntry()
        {
            Debug.Log("[LlmPackageFixer] Removing the wrongly named entry '" + WrongPackageName + "'...");

            _runningRequest = Client.Remove(WrongPackageName);
            EditorApplication.update += WaitForRemoveToFinish;
        }


        ///// Custom Methods - Action Methods /////

        // Action method: watched every editor frame until the remove request is done.
        private static void WaitForRemoveToFinish()
        {
            if (_runningRequest == null || !_runningRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= WaitForRemoveToFinish;

            // A failed remove usually just means the entry was already gone, which is fine.
            if (_runningRequest.Status == StatusCode.Failure)
            {
                Debug.Log("[LlmPackageFixer] Nothing to remove: " + _runningRequest.Error.message);
            }

            Debug.Log("[LlmPackageFixer] Adding the package again under its correct name...");

            _runningRequest = Client.Add(PackageGitUrl);
            EditorApplication.update += WaitForAddToFinish;
        }


        // Action method: watched every editor frame until the add request is done.
        private static void WaitForAddToFinish()
        {
            if (_runningRequest == null || !_runningRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= WaitForAddToFinish;

            if (_runningRequest.Status == StatusCode.Failure)
            {
                Debug.LogError("[LlmPackageFixer] Could not add the package: " + _runningRequest.Error.message);
            }
            else
            {
                Debug.Log("[LlmPackageFixer] The package is now listed as '" + CorrectPackageName + "'. " +
                          "The resolve error should not come back.");
            }

            _runningRequest = null;
        }
    }
}


// Implementation steps:
// 1. This is a one-off repair tool, run it from NTGD124 -> Fix LLM Package Entry.
// 2. It removes the wrongly spelled package entry and adds the package again from its git address.
// 3. Once the console says the package is listed correctly, this script can be deleted.
