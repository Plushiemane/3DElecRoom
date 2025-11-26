using UnityEngine;

public class TrackChanger : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip[] tracks;
    private int index = 0;

    public void NextTrack()
    {
        if (tracks.Length == 0) return;
        index = (index + 1) % tracks.Length;
        audioSource.clip = tracks[index];
        audioSource.Play();
        Debug.Log($"Switched to: {audioSource.clip.name}");
    }
}