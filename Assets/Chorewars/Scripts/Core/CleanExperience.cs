using UnityEngine.SceneManagement;

namespace Chorewars.Core
{
    public static class CleanExperience
    {
        public const string IntroScene = "Intro";
        public const string SetupScene = "DollhouseSetup";
        public const string IntroPath = "Assets/Chorewars/Scenes/Intro.unity";
        public const string SetupPath = "Assets/Chorewars/Scenes/DollhouseSetup.unity";

        public static bool IsActive => IsCleanScene(SceneManager.GetActiveScene().name);

        public static bool IsCleanScene(string sceneName)
            => sceneName == IntroScene || sceneName == SetupScene;
    }
}
