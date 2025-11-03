using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class StartScreen : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeInDuration = 1.5f;
    [SerializeField] private float holdDuration = 1.5f;
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private GameObject mainMenuUI;

    private static bool hasPlayed = false;

    private void Start()
    {
        if (hasPlayed)
        {
            gameObject.SetActive(false);
            return;
        }

        hasPlayed = true;
        StartCoroutine(PlayStartupSequence());
    }

    private IEnumerator PlayStartupSequence()
    {
        fadeCanvasGroup.alpha = 0f;

        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(0, 1, t / fadeInDuration);
            yield return null;
        }

        yield return new WaitForSeconds(holdDuration);

        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(1, 0, t / fadeOutDuration);
            yield return null;
        }
        mainMenuUI.SetActive(true);
        gameObject.SetActive(false);
    }
}
