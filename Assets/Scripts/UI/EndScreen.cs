using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndScreen : MonoBehaviour
{
    [Header("UI References")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI titleText;
    // public TextMeshPro subText;

    [Header("Timing")]
    public float fadeDuration = 1f;
    public float displayDuration = 3f;

    private Coroutine currentRoutine;

    void Awake()
    {
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Subscribes to game events when the component is enabled.
    /// Ensures EndScreen responds to match end notifications.
    /// </summary>
    private void OnEnable()
    {
        GameEvents.OnMatchEnded += HandleMatchEnd;
    }

    /// <summary>
    /// Unsubscribes from game events when the component is disabled.
    /// Prevents memory leaks and duplicate event subscriptions.
    /// </summary>
    private void OnDisable()
    {
        GameEvents.OnMatchEnded -= HandleMatchEnd;
    }

    /// <summary>
    /// Handles match end notifications from the GameEvents system.
    /// Displays appropriate victory, defeat, or draw screen based on the result.
    /// </summary>
    /// <param name="result">The final match outcome from ScoreMgr</param>
    /// <remarks>
    /// Called automatically when GameEvents.OnMatchEnded is fired.
    /// This method is the event handler that responds to match completion.
    /// </remarks>
    private void HandleMatchEnd(ScoreMgr.GameResult result)
    {
        switch (result)
        {
            case ScoreMgr.GameResult.PlayerWin:
                ShowVictory();
                break;
            case ScoreMgr.GameResult.AiWin:
                ShowDefeat();
                break;
            case ScoreMgr.GameResult.Draw:
                ShowScreen("DRAW", Color.yellow);
                break;
        }
    }

    public void ShowVictory()
    {
        ShowScreen("VICTORY", Color.green);
    }

    public void ShowDefeat()
    {
        ShowScreen("DEFEAT", Color.red);
    }

    public void ShowScreen(string message, Color color)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        gameObject.SetActive(true);
        titleText.text = message;
        titleText.color = color;
        currentRoutine = StartCoroutine(DisplayRoutine());
    }

    IEnumerator DisplayRoutine()
    {
        yield return Fade(0, 1); // fade in
        yield return new WaitForSecondsRealtime(displayDuration);
        yield return Fade(1, 0); // fade out
        gameObject.SetActive(false);
    }

    IEnumerator Fade(float start, float end)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, end, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = end;
    }
}
