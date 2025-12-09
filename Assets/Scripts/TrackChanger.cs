using UnityEngine;

public class TrackChanger : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip[] tracks;

    private int lastIndex = -1;

    private void Start()
{
    NextTrack(); // picks a random track immediately
}

    public void NextTrack()
    {
        if (tracks.Length == 0) return;

        int newIndex;

        // Make sure we don't pick the same track twice
        do
        {
            newIndex = Random.Range(0, tracks.Length);
        } 
        while (newIndex == lastIndex && tracks.Length > 1);

        lastIndex = newIndex;

        audioSource.clip = tracks[newIndex];
        audioSource.Play();

        Debug.Log($"Random track: {audioSource.clip.name}");
    }
}
