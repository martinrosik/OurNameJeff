using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ResortEmpire.Tests
{
    public class GameSceneTests
    {
        private const string GameSceneName = "Game";

        [UnityTest]
        public IEnumerator GameSceneLoadsWithCameraAndEventSystem()
        {
            Assert.IsTrue(Application.CanStreamedLevelBeLoaded(GameSceneName),
                $"Scene '{GameSceneName}' is not in the Build Profile scene list.");

            yield return SceneManager.LoadSceneAsync(GameSceneName, LoadSceneMode.Single);

            Assert.IsNotNull(Camera.main, "Game scene has no camera tagged MainCamera.");
            Assert.IsNotNull(Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>(),
                "Game scene has no EventSystem, so UI will not receive input.");
        }
    }
}
