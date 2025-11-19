using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class Settings : MonoBehaviour
{
  // PlayerPrefs keys
  private const string KeyDifficultyLevel = "DifficultyLevel";
  private const string KeySoundVolume = "SoundVolume";
  private const string KeyMusicEffectsVolume = "MusicEffectsVolume";

  // Audio mixer parameter names
  private const string MixerParamSoundEffects = "SoundEffectsVolume";
  private const string MixerParamMusicEffects = "MusicEffectsVolume";

  // Defaults
  private const int DefaultDifficultyLevel = 2;
  private const float DefaultVolume = 0f;

  public AudioMixer MixerMusicEffects;
  public AudioMixer MixerSoundEffects;
  public Dropdown DifficultyDropDown;
  public Slider musicSlider;
  public Slider soundSlider;

  public void SetSoundVolume(float volume)
  {
    if (MixerSoundEffects != null)
    {
      MixerSoundEffects.SetFloat(MixerParamSoundEffects, volume);
    }
    PlayerPrefs.SetFloat(KeySoundVolume, volume);
#if UNITY_EDITOR
    Debug.Log("Sound volume set to: " + volume);
#endif
  }

  public void SetMusicVolume(float volume)
  {
    if (MixerMusicEffects != null)
    {
      MixerMusicEffects.SetFloat(MixerParamMusicEffects, volume);
    }
    PlayerPrefs.SetFloat(KeyMusicEffectsVolume, volume);
#if UNITY_EDITOR
    Debug.Log("Music volume set to: " + volume);
#endif
  }

  public void SetDifficultyLevel(int levelIndexFromDropdown)
  {
    int handledLevel = levelIndexFromDropdown + 1; // Store as 1-based
    PlayerPrefs.SetInt(KeyDifficultyLevel, handledLevel);
#if UNITY_EDITOR
    Debug.Log("Difficulty level set to: " + handledLevel);
#endif
  }

  private void Awake()
  {
    int savedDifficultyLevel = PlayerPrefs.GetInt(KeyDifficultyLevel, DefaultDifficultyLevel) - 1;
    if (DifficultyDropDown != null)
    {
      DifficultyDropDown.value = Mathf.Max(0, savedDifficultyLevel);
    }

    float savedMusicVolume = PlayerPrefs.GetFloat(KeyMusicEffectsVolume, DefaultVolume);
    if (musicSlider != null)
    {
      musicSlider.value = savedMusicVolume;
    }

    float savedSoundVolume = PlayerPrefs.GetFloat(KeySoundVolume, DefaultVolume);
    if (soundSlider != null)
    {
      soundSlider.value = savedSoundVolume;
    }
  }
}