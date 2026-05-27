using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRChopping
{
    public class TreeFallSceneTransition : MonoBehaviour
    {
        [SerializeField] private string sceneToLoad = "AfterTreeFall";
        [SerializeField] private float delayAfterFallSeconds = 8f;
        [SerializeField] private LoadSceneMode loadMode = LoadSceneMode.Single;

        private bool _scheduled;

        public void ScheduleTransitionAfterFall()
        {
            if (_scheduled)
                return;

            _scheduled = true;
            StartCoroutine(LoadSceneAfterDelay());
        }

        private IEnumerator LoadSceneAfterDelay()
        {
            yield return new WaitForSeconds(delayAfterFallSeconds);

            if (string.IsNullOrWhiteSpace(sceneToLoad))
            {
                Debug.LogWarning($"{nameof(TreeFallSceneTransition)}: scene name is empty.", this);
                yield break;
            }

            SceneManager.LoadScene(sceneToLoad, loadMode);
        }
    }
}
