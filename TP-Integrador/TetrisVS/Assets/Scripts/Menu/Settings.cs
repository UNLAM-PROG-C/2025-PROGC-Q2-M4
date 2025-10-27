using UnityEngine;
using UnityEngine.Audio;
public class Settings : MonoBehaviour
{
    public AudioMixer MixerMusicEffects;
    public AudioMixer MixerSoundEffects;

    public void SetSoundVolume(float volume)
    {
        MixerSoundEffects.SetFloat("SoundEffectsVolume", volume);
        PlayerPrefs.SetFloat("SoundVolume", volume);
#if UNITY_EDITOR
            Debug.Log("Sound volume set to: " + volume);
#endif
    }

    public void SetMusicVolume(float volume)
    {
        MixerMusicEffects.SetFloat("MusicEffectsVolume", volume);
        PlayerPrefs.SetFloat("MusicEffectsVolume", volume);
#if UNITY_EDITOR
            Debug.Log("Music volume set to: " + volume);
#endif
    }

    public void SetDifficultyLevel(int level)
    {
        int HandledLevel = level + 1;
        PlayerPrefs.SetInt("DifficultyLevel", HandledLevel);
#if UNITY_EDITOR
                    Debug.Log("Difficulty level set to: " + HandledLevel);
#endif
    }

}
