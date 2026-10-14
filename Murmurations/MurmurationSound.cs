using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Murmurations
{
    // Plays the sound of one murmuration from the birds nearest to the listener, fading with distance.
    internal sealed class MurmurationSound
    {
        // Unity's own distance attenuation is switched off by keeping the listener inside minDistance; the volume is set by hand instead.
        private const float NoUnityRolloffDistance = 100000f;
        private const float SoundSpread = 90f;

        // Set when the sound throws, so a sound problem silences murmurations instead of freezing them.
        private static bool s_DisabledByError;

        private GameObject? m_Emitter;
        private AudioSource? m_Source;
        private bool m_IsPaused;

        public void Update(Transform? parent, Vector3 flockCenter, float flockRadius, float fade)
        {
            if (s_DisabledByError) return;

            try
            {
                UpdateSound(parent, flockCenter, flockRadius, fade);
            }
            catch (Exception exception)
            {
                s_DisabledByError = true;
                Core.Warn($"Murmuration sound failed and is switched off until the game restarts: {exception}", false);
                Destroy();
            }
        }

        private void UpdateSound(Transform? parent, Vector3 flockCenter, float flockRadius, float fade)
        {
            if (!IsEnabled() || parent == null)
            {
                Stop();
                return;
            }

            AudioClip? clip = MurmurationSoundClip.Get();
            if (clip == null) return;

            AudioSource source = m_Source != null ? m_Source : CreateSource(parent);

            if (source.clip != clip)
            {
                source.clip = clip;
                source.time = UnityEngine.Random.Range(0f, clip.length);
                source.Play();
            }

            if (m_IsPaused)
            {
                source.UnPause();
                m_IsPaused = false;
            }

            if (!AudioManagerBridge.TryGetListenerPosition(out Vector3 listenerPosition))
            {
                source.volume = 0f;
                return;
            }

            // The sound comes from the edge of the flock nearest to the listener, or from the listener when it is inside the flock.
            Vector3 toListener = listenerPosition - flockCenter;
            float distanceToCenter = toListener.magnitude;
            float edgeDistance = Mathf.Min(distanceToCenter, Mathf.Max(0f, flockRadius));
            source.transform.position = distanceToCenter > 0.01f ? flockCenter + toListener / distanceToCenter * edgeDistance : listenerPosition;

            float distanceToBirds = distanceToCenter - edgeDistance;
            source.volume = GetDistanceVolume(distanceToBirds) * Mathf.Clamp01(fade) * AudioManagerBridge.GetAmbienceVolume();
        }

        public void Pause()
        {
            if (m_Source == null || m_IsPaused) return;

            m_Source.Pause();
            m_IsPaused = true;
        }

        public void Destroy()
        {
            if (m_Emitter != null) UnityEngine.Object.Destroy(m_Emitter);

            m_Emitter = null;
            m_Source = null;
            m_IsPaused = false;
        }

        private void Stop()
        {
            if (m_Source != null && m_Source.isPlaying) m_Source.Stop();
            if (m_Source != null) m_Source.clip = null;

            m_IsPaused = false;
        }

        private AudioSource CreateSource(Transform parent)
        {
            Destroy();

            m_Emitter = new GameObject("Murmurations_Sound");
            m_Emitter.transform.SetParent(parent, false);

            AudioSource source = m_Emitter.AddComponent<AudioSource>();
            m_Source = source;
            source.playOnAwake = false;
            source.loop = true;
            source.volume = 0f;
            source.spatialBlend = 1f;
            source.spread = SoundSpread;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = NoUnityRolloffDistance;
            source.maxDistance = NoUnityRolloffDistance + 1f;
            return source;
        }

        private static bool IsEnabled()
        {
            return Settings.options != null && Settings.options.FlockSound && Settings.options.SoundVolume > 0f && AudioManagerBridge.IsAvailable;
        }

        // Full volume next to the birds, fading out to silence at the sound range.
        private static float GetDistanceVolume(float distance)
        {
            float range = Mathf.Clamp(Settings.options.SoundRange, 100f, 1000f);
            float remaining = 1f - Mathf.Clamp01(distance / range);
            return Mathf.Clamp01(Settings.options.SoundVolume) * remaining * remaining;
        }
    }

    // Loads the sound file and keeps the clip for all murmurations.
    // The Long Dark is missing the parts of Unity that decode compressed audio files, so the sound is a plain PCM WAV:
    // it is read on a background thread and copied into the clip a few seconds of audio per frame to avoid a hitch.
    internal static class MurmurationSoundClip
    {
        internal const string SoundFolderName = "Murmurations";
        internal const string SoundFileName = "Sound_of_massed_starlings.wav";
        private const int UploadSecondsPerFrame = 4;

        private static AudioClip? s_Clip;
        private static Task<WavData>? s_ReadTask;
        private static WavData? s_Data;
        private static AudioClip? s_UploadingClip;
        private static int s_UploadedSamples;
        private static bool s_LoadFailed;

        // Returns null while the clip is loading or when it could not be loaded.
        public static AudioClip? Get()
        {
            // Unity's == treats a clip destroyed by a scene load as null, so it gets reloaded.
            if (s_Clip != null) return s_Clip;
            if (s_LoadFailed) return null;

            if (s_Data == null)
            {
                ReadFile();
                return null;
            }

            UploadNextChunk(s_Data);
            return s_Clip;
        }

        private static void ReadFile()
        {
            if (s_ReadTask == null)
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Mods", SoundFolderName, SoundFileName));
                if (!File.Exists(path))
                {
                    Fail($"Murmuration sound not found at {path}.");
                    return;
                }

                s_ReadTask = Task.Run(() => WavData.Read(path));
                return;
            }

            if (!s_ReadTask.IsCompleted) return;

            if (s_ReadTask.IsFaulted) Fail($"Failed to read the murmuration sound: {s_ReadTask.Exception?.GetBaseException().Message}");
            else s_Data = s_ReadTask.Result;

            s_ReadTask = null;
        }

        private static void UploadNextChunk(WavData data)
        {
            if (s_UploadingClip == null)
            {
                s_UploadingClip = AudioClip.Create(SoundFileName, data.FrameCount, data.Channels, data.SampleRate, false);
                s_UploadedSamples = 0;
            }

            // SetData counts its offset in frames (one sample per channel), but takes interleaved samples.
            int chunkLength = Math.Min(data.SampleRate * data.Channels * UploadSecondsPerFrame, data.Samples.Length - s_UploadedSamples);
            float[] chunk = new float[chunkLength];
            Array.Copy(data.Samples, s_UploadedSamples, chunk, 0, chunkLength);
            s_UploadingClip.SetData(new Il2CppStructArray<float>(chunk), s_UploadedSamples / data.Channels);
            s_UploadedSamples += chunkLength;

            if (s_UploadedSamples < data.Samples.Length) return;

            s_Clip = s_UploadingClip;
            s_UploadingClip = null;
            s_Data = null;
        }

        private static void Fail(string message)
        {
            s_LoadFailed = true;
            s_ReadTask = null;
            s_Data = null;
            Core.Warn(message + " Murmurations will be silent.", false);
        }
    }

    // A 16-bit PCM WAV file, as interleaved samples between -1 and 1.
    internal sealed class WavData
    {
        public float[] Samples { get; private init; } = Array.Empty<float>();
        public int Channels { get; private init; }
        public int SampleRate { get; private init; }
        public int FrameCount => Samples.Length / Channels;

        public static WavData Read(string path)
        {
            using BinaryReader reader = new(File.OpenRead(path));
            if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("Not a WAV file.");
            reader.ReadInt32();
            if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("Not a WAV file.");

            int channels = 0, sampleRate = 0, bitsPerSample = 0;

            while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
            {
                string chunkId = new(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();
                long chunkEnd = reader.BaseStream.Position + chunkSize + (chunkSize & 1);

                if (chunkId == "fmt ")
                {
                    short format = reader.ReadInt16();
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt16();
                    bitsPerSample = reader.ReadInt16();
                    if (format != 1 || bitsPerSample != 16 || channels < 1) throw new InvalidDataException("Only 16-bit PCM WAV files are supported.");
                }
                else if (chunkId == "data")
                {
                    if (channels == 0) throw new InvalidDataException("WAV data comes before its format.");

                    byte[] bytes = reader.ReadBytes(chunkSize);
                    float[] samples = new float[bytes.Length / 2 / channels * channels];
                    for (int i = 0; i < samples.Length; i++) samples[i] = BitConverter.ToInt16(bytes, i * 2) / 32768f;

                    return new WavData { Samples = samples, Channels = channels, SampleRate = sampleRate };
                }

                reader.BaseStream.Position = chunkEnd;
            }

            throw new InvalidDataException("WAV file has no audio data.");
        }
    }

    // Unity audio in The Long Dark is only heard through the AudioListener that the AudioManager mod adds,
    // and AudioManager follows the game's volume sliders. AudioManager is optional, so AudioMgr types are only
    // used inside the methods below, which are only called when IsAvailable is true.
    internal static class AudioManagerBridge
    {
        private static bool? s_IsAvailable;
        private static bool s_LoggedMissing;

        public static bool IsAvailable
        {
            get
            {
                s_IsAvailable ??= AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "AudioManager");
                if (s_IsAvailable == false) LogMissingOnce();
                return s_IsAvailable.Value;
            }
        }

        public static bool TryGetListenerPosition(out Vector3 position)
        {
            AudioListener? listener = AudioMgr.AudioMaster.audioListener;
            if (listener == null)
            {
                position = Vector3.zero;
                return false;
            }

            position = listener.transform.position;
            return true;
        }

        public static float GetAmbienceVolume()
        {
            return Mathf.Clamp01(AudioMgr.VolumeMaster.GetVolume(AudioMgr.AudioMaster.SourceType.Ambience));
        }

        private static void LogMissingOnce()
        {
            if (s_LoggedMissing) return;

            s_LoggedMissing = true;
            Core.Warn("The AudioManager mod is not installed, so murmurations will be silent.", false);
        }
    }
}
