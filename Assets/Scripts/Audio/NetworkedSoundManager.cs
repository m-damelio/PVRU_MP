using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Collections;

public class NetworkedSoundManager : NetworkBehaviour
{
    [System.Serializable]
    public enum SoundCategory
    {
        UI,           // Menu sounds, button clicks
        Environment,  // Doors, alarms, ambient
        Effects,      // Gameplay effects, interactions
        Music         // Background music
    }

    [System.Serializable]
    public enum SoundPriority
    {
        Low = 0,      // Background sounds, can be interrupted
        Medium = 1,   // Normal gameplay sounds
        High = 2,     // Important feedback sounds
        Critical = 3  // Alarms, warnings - never interrupted
    }

    [System.Serializable]
    public class SoundClipData
    {
        public string soundName;
        public AudioClip clip;
        public SoundCategory category;
        public SoundPriority priority;
        [Range(0f, 1f)] public float defaultVolume = 1f;
        [Range(0.1f, 3f)] public float defaultPitch = 1f;
        public bool is3D = true;
        public float maxDistance = 50f;
        public float minDistance = 1f;
    }

    [Header("Sound Database")]
    [SerializeField] private List<SoundClipData> soundDatabase = new List<SoundClipData>();

    [Header("Audio Source Pool")]
    [SerializeField] private int audioSourcePoolSize = 10;
    [SerializeField] private GameObject audioSourcePrefab;

    [Header("Category Volume Controls")]
    [Range(0f, 1f)] public float uiVolume = 1f;
    [Range(0f, 1f)] public float environmentVolume = 1f;
    [Range(0f, 1f)] public float effectsVolume = 1f;
    [Range(0f, 1f)] public float voiceVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float masterVolume = 1f;

    [Header("VR Audio Settings")]
    [SerializeField] private bool enableSpatialAudio = true;
    [SerializeField] private AudioReverbZone reverbZone;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // Internal variables
    private Dictionary<string, SoundClipData> soundLookup = new Dictionary<string, SoundClipData>();
    private Queue<AudioSource> audioSourcePool = new Queue<AudioSource>();
    private List<AudioSource> activeAudioSources = new List<AudioSource>();
    private Dictionary<SoundCategory, float> categoryVolumes = new Dictionary<SoundCategory, float>();
    private Dictionary<AudioSource, float> sceneAudioSources = new Dictionary<AudioSource, float>();
    public static NetworkedSoundManager Instance { get; private set; }

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSoundManager();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        SetupAudioSourcePool();
        LoadSoundDatabase();
        UpdateCategoryVolumes();

        RegisterSceneAudioSources();
    }

    private void Update()
    {
        CleanupFinishedAudioSources();
    }

    #endregion

    #region Initialization

    private void InitializeSoundManager()
    {
        // Initialize category volumes dictionary
        categoryVolumes[SoundCategory.UI] = uiVolume;
        categoryVolumes[SoundCategory.Environment] = environmentVolume;
        categoryVolumes[SoundCategory.Effects] = effectsVolume;
        categoryVolumes[SoundCategory.Music] = musicVolume;

        Debug.Log("NetworkedSoundManager: Initialized successfully");
    }

    private void SetupAudioSourcePool()
    {
        // Create audio source pool
        for (int i = 0; i < audioSourcePoolSize; i++)
        {
            GameObject audioObj = new GameObject($"PooledAudioSource_{i}");
            audioObj.transform.SetParent(transform);

            AudioSource audioSource = audioObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = enableSpatialAudio ? 1f : 0f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;

            audioSourcePool.Enqueue(audioSource);
        }

        Debug.Log($"NetworkedSoundManager: Created audio source pool with {audioSourcePoolSize} sources");
    }

    private void LoadSoundDatabase()
    {
        soundLookup.Clear();

        foreach (var soundData in soundDatabase)
        {
            if (!string.IsNullOrEmpty(soundData.soundName) && soundData.clip != null)
            {
                soundLookup[soundData.soundName] = soundData;
            }
        }

        Debug.Log($"NetworkedSoundManager: Loaded {soundLookup.Count} sounds into database");
    }

    #endregion

    #region Public RPC Methods

    // Play a 2D sound (UI, non-positional)
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_PlaySound2D(string soundName, float volumeMultiplier = 1f, float pitchMultiplier = 1f)
    {
        PlaySoundInternal(soundName, Vector3.zero, false, volumeMultiplier, pitchMultiplier);

        if (debugMode)
            Debug.Log($"NetworkedSoundManager: Playing 2D sound '{soundName}'");
    }

    // Play a 3D positioned sound in the world
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_PlaySound3D(string soundName, Vector3 position, float volumeMultiplier = 1f, float pitchMultiplier = 1f)
    {
        PlaySoundInternal(soundName, position, true, volumeMultiplier, pitchMultiplier);

        if (debugMode)
            Debug.Log($"NetworkedSoundManager: Playing 3D sound '{soundName}' at {position}");
    }

    // Play sound attached to a specific player
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_PlaySoundAtPlayer(string soundName, PlayerRef targetPlayer, float volumeMultiplier = 1f)
    {
        // Find the target player's position
        var players = FindObjectsOfType<VRPlayer>();
        foreach (var player in players)
        {
            if (player.Object.InputAuthority == targetPlayer)
            {
                PlaySoundInternal(soundName, player.transform.position, true, volumeMultiplier, 1f);
                break;
            }
        }

        if (debugMode)
            Debug.Log($"NetworkedSoundManager: Playing sound '{soundName}' at player {targetPlayer}");
    }

    // Stop all sounds of a specific category
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_StopSoundsByCategory(SoundCategory category)
    {
        StopSoundsByCategoryInternal(category);

        if (debugMode)
            Debug.Log($"NetworkedSoundManager: Stopped all sounds in category '{category}'");
    }

    // Stop all currently playing sounds
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_StopAllSounds()
    {
        StopAllSoundsInternal();

        if (debugMode)
            Debug.Log("NetworkedSoundManager: Stopped all sounds");
    }

    // Update master volume for all clients
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateAllAudioSourceVolumes();

        UpdateSceneAudioSources();   

        if (debugMode)
            Debug.Log($"NetworkedSoundManager: Set master volume to {masterVolume}");
    }

    // Update category volume for all clients
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SetCategoryVolume(SoundCategory category, float volume)
    {
        volume = Mathf.Clamp01(volume);
        categoryVolumes[category] = volume;

        // Update the corresponding public field
        switch (category)
        {
            case SoundCategory.UI: uiVolume = volume; break;
            case SoundCategory.Environment: environmentVolume = volume; break;
            case SoundCategory.Effects: effectsVolume = volume; break;
            case SoundCategory.Music: musicVolume = volume; break;
        }

        UpdateAllAudioSourceVolumes();

        if (debugMode)
            Debug.Log($"NetworkedSoundManager: Set {category} volume to {volume}");
    }

    #endregion

    #region Public Convenience Methods

    // Play UI sound - always 2D
    public void PlayUISound(string soundName, float volume = 1f)
    {
        if (Object.HasInputAuthority)
        {
            RPC_PlaySound2D(soundName, volume);
        }
    }

    /// Play environment sound - 3D positioned
    public void PlayEnvironmentSound(string soundName, Vector3 position, float volume = 1f)
    {
        RPC_PlaySound3D(soundName, position, volume);
    }


    /// Play effect sound - 3D positioned
    public void PlayEffectSound(string soundName, Vector3 position, float volume = 1f)
    {
            RPC_PlaySound3D(soundName, position, volume);
    }

    #endregion

    #region Internal Sound Playback

    private void PlaySoundInternal(string soundName, Vector3 position, bool is3D, float volumeMultiplier, float pitchMultiplier)
    {
        if (!soundLookup.TryGetValue(soundName, out SoundClipData soundData))
        {
            Debug.LogWarning($"NetworkedSoundManager: Sound '{soundName}' not found in database!");
            return;
        }

        AudioSource audioSource = GetAvailableAudioSource();
        if (audioSource == null)
        {
            Debug.LogWarning("NetworkedSoundManager: No available audio sources in pool!");
            return;
        }

        // Configure audio source
        audioSource.clip = soundData.clip;
        audioSource.volume = CalculateFinalVolume(soundData, volumeMultiplier);
        audioSource.pitch = soundData.defaultPitch * pitchMultiplier;
        audioSource.priority = (int)soundData.priority;

        // Configure 3D settings
        if (is3D && soundData.is3D)
        {
            audioSource.transform.position = position;
            audioSource.spatialBlend = 1f; // Full 3D
            audioSource.minDistance = soundData.minDistance;
            audioSource.maxDistance = soundData.maxDistance;
        }
        else
        {
            audioSource.spatialBlend = 0f; // 2D
        }

        // Play the sound
        audioSource.Play();
        activeAudioSources.Add(audioSource);

        // Store sound data for volume updates
        audioSource.gameObject.name = $"Playing_{soundName}_{soundData.category}";

        if (debugMode)
        {
            DebugSoundPlayback(soundName, audioSource, soundData);
        }
    }

    private float CalculateFinalVolume(SoundClipData soundData, float volumeMultiplier)
    {
        float categoryVolume = categoryVolumes.ContainsKey(soundData.category) ? categoryVolumes[soundData.category] : 1f;
        return soundData.defaultVolume * volumeMultiplier * categoryVolume * masterVolume;
    }

    private AudioSource GetAvailableAudioSource()
    {
        if (audioSourcePool.Count > 0)
        {
            return audioSourcePool.Dequeue();
        }

        // If pool is empty, try to find a low-priority sound to interrupt
        AudioSource lowestPrioritySource = null;
        int lowestPriority = int.MaxValue;

        foreach (var source in activeAudioSources)
        {
            if (source.priority < lowestPriority)
            {
                lowestPriority = source.priority;
                lowestPrioritySource = source;
            }
        }

        if (lowestPrioritySource != null && lowestPriority < (int)SoundPriority.Critical)
        {
            lowestPrioritySource.Stop();
            activeAudioSources.Remove(lowestPrioritySource);
            return lowestPrioritySource;
        }

        return null;
    }

    #endregion

    #region Sound Control

    private void StopSoundsByCategoryInternal(SoundCategory category)
    {
        for (int i = activeAudioSources.Count - 1; i >= 0; i--)
        {
            var audioSource = activeAudioSources[i];
            if (audioSource.gameObject.name.Contains(category.ToString()))
            {
                audioSource.Stop();
                activeAudioSources.RemoveAt(i);
                audioSourcePool.Enqueue(audioSource);
            }
        }
    }

    private void StopAllSoundsInternal()
    {
        foreach (var audioSource in activeAudioSources)
        {
            audioSource.Stop();
            audioSourcePool.Enqueue(audioSource);
        }
        activeAudioSources.Clear();
    }

    private void CleanupFinishedAudioSources()
    {
        for (int i = activeAudioSources.Count - 1; i >= 0; i--)
        {
            var audioSource = activeAudioSources[i];
            if (!audioSource.isPlaying)
            {
                activeAudioSources.RemoveAt(i);
                audioSourcePool.Enqueue(audioSource);
                audioSource.gameObject.name = "PooledAudioSource";
            }
        }
    }

    #endregion

    #region Volume Management

    private void UpdateCategoryVolumes()
    {
        categoryVolumes[SoundCategory.UI] = uiVolume;
        categoryVolumes[SoundCategory.Environment] = environmentVolume;
        categoryVolumes[SoundCategory.Effects] = effectsVolume;
        categoryVolumes[SoundCategory.Music] = musicVolume;
    }

    private void UpdateAllAudioSourceVolumes()
    {
        foreach (var audioSource in activeAudioSources)
        {
            // Extract category from audio source name
            string[] nameParts = audioSource.gameObject.name.Split('_');
            if (nameParts.Length >= 3 && System.Enum.TryParse(nameParts[2], out SoundCategory category))
            {
                // Find original sound data to recalculate volume
                string soundName = nameParts[1];
                if (soundLookup.TryGetValue(soundName, out SoundClipData soundData))
                {
                    audioSource.volume = CalculateFinalVolume(soundData, 1f);
                }
            }
        }
    }

    #endregion

    private void RegisterSceneAudioSources()
    {
        sceneAudioSources.Clear();
        AudioSource[] allSources = FindObjectsOfType<AudioSource>(true); 

        foreach (var src in allSources)
        {
            if (!audioSourcePool.Contains(src) && !sceneAudioSources.ContainsKey(src))
            {
                sceneAudioSources.Add(src, src.volume);
            }
        }

        if (debugMode)
            Debug.Log($"NetworkedSoundManager: {sceneAudioSources.Count} static audio sources registered");
    }

    private void UpdateSceneAudioSources()
    {
        foreach (var kvp in sceneAudioSources)
        {
            if (kvp.Key != null) 
            {
                kvp.Key.volume = kvp.Value * masterVolume;
            }
        }
    }

    private void LateUpdate()
    {
        AudioSource[] allSources = FindObjectsOfType<AudioSource>(true);
        foreach (var src in allSources)
        {
            if (!sceneAudioSources.ContainsKey(src) && !audioSourcePool.Contains(src))
            {
                sceneAudioSources.Add(src, src.volume);
            }
        }
    }

    private void DebugSoundPlayback(string soundName, AudioSource audioSource, SoundClipData soundData)
    {
        Debug.Log($"[Sound Debug] Playing '{soundName}' " +
                  $"| Clip: {(soundData.clip != null ? soundData.clip.name : "MISSING")} " +
                  $"| Volume: {audioSource.volume:F2} " +
                  $"| Pitch: {audioSource.pitch:F2} " +
                  $"| Category: {soundData.category} " +
                  $"| Is3D: {soundData.is3D} " +
                  $"| Position: {audioSource.transform.position} " +
                  $"| Listener Found: {(FindObjectOfType<AudioListener>() != null ? "YES" : "NO")} " +
                  $"| Listener Pos: {(FindObjectOfType<AudioListener>() ? FindObjectOfType<AudioListener>().transform.position.ToString() : "N/A")}");
    }

}

