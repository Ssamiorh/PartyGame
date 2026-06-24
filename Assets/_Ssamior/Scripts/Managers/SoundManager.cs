using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;
using System.Collections;

namespace Utils
{
    [RequireComponent(typeof(AudioListener))]
    public class SoundManager : Singleton<SoundManager>
	{
		public enum GroupType
		{
			Music = 0,
			SFX = 1,
			UI = 2,

            Master = 100,
		}

        public AudioListener GlobalListener { get; private set; }
		//public bool toogleMusic, toggleEffects;

		[SerializeField] private AudioMixerGroup globalGroup;

        readonly List<Sound> sounds = new();
		//Split sounds in categories for clarity
		[Header("GameObjects")]
		[SerializeField] private AudioSource musicSource1;
		[SerializeField] private AudioSource musicSource2;

		[Header("Unity Sounds")]
        [SerializeField] List<Sound> musics;
        [SerializeField] List<Sound> nappes;
        [SerializeField] List<Sound> surfaceSounds;
        [SerializeField] List<Sound> underwaterSounds;
        [SerializeField] List<Sound> UISounds;

		//List of all sounds
        Dictionary<string, Sound> soundsIndex = new();

		private AudioSource activeMusicSource;
		private AudioSource swapMusicSource;
        private Coroutine swapCoroutine;


        private Sound ongoingMusic;
		private Tween fadeOutTween;
		private Tween fadeInTween;
		private bool isFirst = true;
        private readonly Dictionary<string, int> currentMusicSamples = new();

        public override void Awake()
		{
			base.Awake();

			//Ordering sounds
			sounds.Clear();
			sounds.AddRange(musics);
			sounds.AddRange(nappes);
			sounds.AddRange(surfaceSounds);
			sounds.AddRange(underwaterSounds);
			sounds.AddRange(UISounds);

            // Indexation de tous les sons qui ont un nom
            soundsIndex = sounds.Where(s => !string.IsNullOrEmpty(s.name)).ToDictionary(s => s.name, s => s);
			GlobalListener = GetComponent<AudioListener>();

			activeMusicSource = musicSource1;
            activeMusicSource.volume = 0f;
            activeMusicSource.pitch = 1;

            swapMusicSource = musicSource2;
            swapMusicSource.volume = 0f;
            swapMusicSource.pitch = 1;

            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigChanged;
        }

        private void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigChanged;
        }

        /// <summary>
        /// Set volumes according to saved data
        /// </summary>
        public void UpdateVolumes()
        {
            ChangeGroupVolume(GroupType.Master, PlayerPrefs.GetFloat(PreferencesManager.masterVolume_PlayerPrefKey, 0.8f));
            ChangeGroupVolume(GroupType.Music, PlayerPrefs.GetFloat(PreferencesManager.musicVolume_PlayerPrefKey, 0.5f));
            ChangeGroupVolume(GroupType.SFX, PlayerPrefs.GetFloat(PreferencesManager.sfxVolume_PlayerPrefKey, 0.8f));
            ChangeGroupVolume(GroupType.UI, PlayerPrefs.GetFloat(PreferencesManager.uiVolume_PlayerPrefKey, 0.5f));
        }

        private void OnAudioConfigChanged(bool deviceWasChanged)
        {
            //Restart music that was paused by audio config changing
            if (activeMusicSource != null && !activeMusicSource.isPlaying)
            {
                activeMusicSource.Play();
            }

            UpdateVolumes();
        }

        #region Public Methods

        public void Play(string name, float transitionLength = 3f, bool replay = true, float pitch = 1f)
		{
			if (string.IsNullOrEmpty(name)) throw new ArgumentNullException();
			try
			{
				Sound s = soundsIndex[name];
				Init(s);
				//Music
				if (s.isMusic)
				{
					Debug.Log($"Playing music {s.name} / {s.clip.name} / {transitionLength}s / replay:{replay}");
					if(s.clip == null)
					{
						StopCurrentMusic();
						return;
					}

					if (isFirst)
					{
                        ongoingMusic = s;
                        activeMusicSource.clip = s.clip;
                        activeMusicSource.loop = s.loop;
                        activeMusicSource.volume = 1f;

                        activeMusicSource.outputAudioMixerGroup = s.mixerGroup;
                        activeMusicSource.Play();
                    }
					else
					{
                        if (s == ongoingMusic)
						{
                            fadeInTween = activeMusicSource.DOFade(1f, transitionLength).SetUpdate(true);
							return;
						}
						//New music, which should replace the current playing music
						else
						{
                            if (swapCoroutine != null)
                            {
                                StopCoroutine(swapCoroutine);
                                (swapMusicSource, activeMusicSource) = (activeMusicSource, swapMusicSource);
                            }
                                
                            swapCoroutine = StartCoroutine(SmoothMusicSwap(s, transitionLength, replay));
						}
					}
					isFirst = false;
				}

                else
				{
					s.source.volume = 1f;
                    s.source.pitch = pitch;
                    s.source.time = 0;

                    //Same sound clip played multiple times can overlap
                    //But be careful to keep the tails of repeating sounds short to avoid clips getting culled (32 max by default)
                    s.source.PlayOneShot(s.clip);
				}
			}
			catch (Exception e)
			{
				Debug.LogError($"Impossible de lire le son {name}\n{e}");
			}
		}

        public void StopSound(string name, float transitionLength = 0.1f)
		{
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException();
            try
            {
                Sound s = soundsIndex[name];
                if (s.isMusic)
                {
					return;
                }
                if (s.source != null)
				{
                    s.source.DOFade(0f, transitionLength).SetUpdate(true).OnComplete(() => s.source.Stop());
				}

            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("Impossible de stopper le son '{0}'.\n{1}", name, e);
            }
        }

        public void StopAllSounds()
        {
            Debug.Log("StopAllSounds");
            //When you pause the AudioListener, it stops processing all sounds, including those played via PlayOneShot()
            AudioListener.pause = true;
            AudioListener.pause = false;
        }

        private IEnumerator SmoothMusicSwap(Sound newMusic, float transitionLength, bool replay)
		{
			if (ongoingMusic.clip == newMusic.clip)
				yield break;

            Debug.Log($"Called music swap : old = {ongoingMusic.clip.name} / new = {newMusic.clip.name} / source:{swapMusicSource}");
            fadeOutTween?.Kill(true);
            fadeInTween?.Kill(true);

            //Save the sample for the music playing before
            SaveMusicSamples();

            //Fade out the old music
            AudioSource currActiveSource = activeMusicSource;
            fadeOutTween = currActiveSource.DOFade(0f, transitionLength).SetUpdate(true).OnComplete(() => {
                currActiveSource.Pause();
                fadeOutTween = null;
            });

            //Set new music
            ongoingMusic = newMusic;
            swapMusicSource.clip = newMusic.clip;
            swapMusicSource.loop = newMusic.loop;
            swapMusicSource.volume = 0f;
            swapMusicSource.outputAudioMixerGroup = newMusic.mixerGroup;

            //Reset to the beginning of the audio clip if needed
            if (replay)
            {
                swapMusicSource.timeSamples = 0;
            }
            else
            {
                if(currentMusicSamples.TryGetValue(newMusic.clip.name, out int savedSamples))
                {
                    swapMusicSource.timeSamples = savedSamples;
                }
            }

            //Fade in new music after a delay
            yield return new WaitForSecondsRealtime(transitionLength / 2f);
            swapMusicSource.Play();

            fadeInTween = swapMusicSource.DOFade(1f, transitionLength + 0.05f).SetUpdate(true).OnComplete(() =>
			{
                fadeInTween = null;
            });
            //Swap
            (swapMusicSource, activeMusicSource) = (activeMusicSource, swapMusicSource);
            swapCoroutine = null;
        }

        public void StopCurrentMusic()
		{
            fadeOutTween?.Kill(true);
            fadeInTween?.Kill(true);

			if (activeMusicSource != null && activeMusicSource.isPlaying)
			{
                SaveMusicSamples();
                fadeOutTween = activeMusicSource.DOFade(0f, 2f).SetUpdate(true).OnComplete(() => { 
                    activeMusicSource.Pause();
                });
            }
        }
        
        /// <summary>
        /// Save or update the samples for current music, in order to know where to replay this music later
        /// </summary>
        private void SaveMusicSamples()
        {
            currentMusicSamples[activeMusicSource.clip.name] = activeMusicSource.timeSamples;
        }


        public bool CheckIfAlreadyPlaying(string name)
		{
			return (ongoingMusic.name == name);
		}

        public void ChangeGroupVolume(GroupType type, float vol)
		{
			string parameterName = "";
			switch (type)
			{
                case GroupType.Master:
                    parameterName = PreferencesManager.masterVolume_PlayerPrefKey;
                    break;
                case GroupType.Music:
					parameterName = PreferencesManager.musicVolume_PlayerPrefKey;
                    break;
				case GroupType.SFX:
					parameterName = PreferencesManager.sfxVolume_PlayerPrefKey;
                    break;
				case GroupType.UI:
                    parameterName = PreferencesManager.uiVolume_PlayerPrefKey;
                    break;
            }

			if(!string.IsNullOrEmpty(parameterName))
			{
                if (vol == 0f)
                    vol = 0.0001f;

                globalGroup.audioMixer.SetFloat(parameterName, Mathf.Log10(vol) * 20);
            }
			else
			{
				Debug.LogWarning($"Trying to change volume for a non-configured volume GroupType : {type}");
			}
        }

        private void Init(Sound s)
        {
            if (s == null)
                throw new ArgumentNullException();
            if (string.IsNullOrEmpty(s.name))
                throw new ArgumentException();
            if (s.isMusic || s.source != null)
                return;       // Son deja initialise

            s.source = gameObject.AddComponent<AudioSource>();
            s.source.clip = s.clip;
            s.source.loop = s.loop;
            s.source.volume = 0;
            s.source.Pause();
            s.source.outputAudioMixerGroup = s.mixerGroup;
        }
        public void PlayRandomSoundFromList(List<string> sounds)
        {
            if (sounds != null && sounds.Any())
            {
                int random = UnityEngine.Random.Range(0, sounds.Count());
                Play(sounds[random]);
            }
        }
        #endregion
    }
    
    [System.Serializable]
	public class Sound
	{
		public string name;
		public AudioClip clip;
		public bool isMusic;
		public bool loop;
		public AudioMixerGroup mixerGroup;

		[System.NonSerialized] public AudioSource source;
	}
}