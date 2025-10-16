using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GoalTrigger : MonoBehaviour
{
    private bool restarting = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (restarting) return;

        BallController ball = other.GetComponentInParent<BallController>();
        if (ball == null) return;

        Debug.Log("⚽ GOL!");
        var goalUI = FindFirstObjectByType<GoalUIManager>();
        if (goalUI != null)
            goalUI.ShowGoal();

        Destroy(ball.gameObject);
        StartCoroutine(RestartSceneAfterDelay(0.5f));
    }

    private IEnumerator RestartSceneAfterDelay(float delay)
    {
        restarting = true;
        yield return new WaitForSeconds(delay);

        var activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }
}