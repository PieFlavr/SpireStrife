using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] private AudioSource musicSource;

    [Header("Music Tracks")]
    public List<AudioClip> soundtracks;

    private int currentTrackIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (soundtracks.Count > 0)
            PlayTrack(0);
    }

    public void PlayTrack(int index)
    {
        if (index < 0 || index >= soundtracks.Count)
        {
            Debug.LogWarning("Invalid soundtrack index!");
            return;
        }

        if (currentTrackIndex == index)
            return;

        currentTrackIndex = index;
        musicSource.clip = soundtracks[index];
        musicSource.Play();
    }

    public void StopMusic()
    {
        musicSource.Stop();
    }

    public void NextTrack()
    {
        if (soundtracks.Count == 0) return;
        int nextIndex = (currentTrackIndex + 1) % soundtracks.Count;
        PlayTrack(nextIndex);
    }
    public void FadeToTrack(int newIndex, float fadeDuration = 1f)
    {
        StartCoroutine(FadeMusic(newIndex, fadeDuration));
    }

    private IEnumerator FadeMusic(int newIndex, float fadeDuration)
    {
        if (musicSource.isPlaying)
        {
            float startVol = musicSource.volume;
            for (float t = 0; t < fadeDuration; t += Time.deltaTime)
            {
                musicSource.volume = Mathf.Lerp(startVol, 0, t / fadeDuration);
                yield return null;
            }
        }

        PlayTrack(newIndex);

        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            musicSource.volume = Mathf.Lerp(0, 1, t / fadeDuration);
            yield return null;
        }
    }
}
