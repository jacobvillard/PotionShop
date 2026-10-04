using UnityEngine;

public class MusicScript : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] musicClips;
    
    private int lastSoundIndex = -1;
    
    private bool hasFocus = true;

    private void OnApplicationFocus(bool focused)
    {
        hasFocus = focused;
    }
    
    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.loop = false;
        
        
    
        
    }

    // Update is called once per frame
    private  void Update()
    {
        if (audioSource == null)
            return;

        if (SettingsManager.Current != null)
            audioSource.volume = SettingsManager.Current.musicVolume;

        if(hasFocus && !audioSource.isPlaying)
        {
            PlayRandomMusic();
        }
    }
    
    private void PlayRandomMusic()
    {
        if (musicClips.Length == 0)
            return;

        int randomIndex;
        do
        {
            randomIndex = Random.Range(0, musicClips.Length);
        } while (randomIndex == lastSoundIndex && musicClips.Length > 1);

        lastSoundIndex = randomIndex;
        audioSource.clip = musicClips[randomIndex];
        audioSource.Play();
    }
}
