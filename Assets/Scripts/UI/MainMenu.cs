using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Buttons")]
    public Button playButton;
    public Button quitButton;
    public Button instructionsButton;
    public Button creditsButton;
    public Button backButtonInstructions;
    public Button backButtonCredits;

    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject instructionsPanel;
    public GameObject creditsPanel;

    [Header("Fade Settings")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.5f;

    [Header("Scene")]
    public string gameplaySceneName = "HexGridTest";

    private void Start()
    {
        playButton.onClick.AddListener(PlayGame);
        quitButton.onClick.AddListener(QuitGame);
        instructionsButton.onClick.AddListener(() => StartCoroutine(SwitchPanel(mainPanel, instructionsPanel)));
        creditsButton.onClick.AddListener(() => StartCoroutine(SwitchPanel(mainPanel, creditsPanel)));
        backButtonInstructions.onClick.AddListener(() => StartCoroutine(SwitchPanel(instructionsPanel, mainPanel)));
        backButtonCredits.onClick.AddListener(() => StartCoroutine(SwitchPanel(creditsPanel, mainPanel)));

        fadeCanvasGroup.alpha = 0f;
        StartCoroutine(FadeIn());
    }

    private void PlayGame()
    {
        StartCoroutine(LoadSceneWithFade());
    }

    private void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit game triggered.");
    }

    private IEnumerator SwitchPanel(GameObject from, GameObject to)
    {
        yield return FadeOut();
        from.SetActive(false);
        to.SetActive(true);
        yield return FadeIn();
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(0, 1, t / fadeDuration);
            yield return null;
        }
    }

    private IEnumerator FadeOut()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(1, 0, t / fadeDuration);
            yield return null;
        }
    }

    private IEnumerator LoadSceneWithFade()
    {
        yield return FadeOut();
        SceneManager.LoadScene(gameplaySceneName);
    }
}
