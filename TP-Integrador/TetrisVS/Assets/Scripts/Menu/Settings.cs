using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
public class Settings : MonoBehaviour
{
    public AudioMixer MixerMusicEffects;
    public AudioMixer MixerSoundEffects;
    public Dropdown DifficultyDropDown;
    public Slider musicSlider;
    public Slider soundSlider;

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

    void Awake()
    {
        int savedDifficultyLevel = PlayerPrefs.GetInt("DifficultyLevel", 2) - 1;
        DifficultyDropDown.value = savedDifficultyLevel;
        float savedMusicVolume = PlayerPrefs.GetFloat("MusicEffectsVolume", 0f);
        musicSlider.value = savedMusicVolume;
        float savedSoundVolume = PlayerPrefs.GetFloat("SoundVolume", 0f);
        soundSlider.value = savedSoundVolume;

    }

}
