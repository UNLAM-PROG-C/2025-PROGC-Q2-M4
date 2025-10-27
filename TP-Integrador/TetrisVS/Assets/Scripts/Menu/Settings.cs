using UnityEngine;
using UnityEngine.Audio;
public class Settings : MonoBehaviour
{
    public static float DifficultyLevel { get; private set; }
    public static float SoundVolume { get; private set; }
    public static float MusicVolume { get; private set; }
    public AudioMixer MixerMusicEffects;
    public AudioMixer MixerSoundEffects;

    public void SetSoundVolume(float volume)
    {
        SoundVolume = volume;
        MixerSoundEffects.SetFloat("SoundEffectsVolume", volume);
        #if UNITY_EDITOR
            Debug.Log("Sound volume set to: " + SoundVolume);
        #endif
    }

    public void SetMusicVolume(float volume)
    {
        MixerMusicEffects.SetFloat("MusicEffectsVolume", volume);
        MusicVolume = volume;
        #if UNITY_EDITOR
            Debug.Log("Music volume set to: " + MusicVolume);
        #endif
    }

    public void SetDifficultyLevel(int level)
    {
        DifficultyLevel = level+1;
        #if UNITY_EDITOR
                    Debug.Log("Difficulty level set to: " + DifficultyLevel);
        #endif
    }

    void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }

}
