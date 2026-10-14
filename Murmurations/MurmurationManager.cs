namespace Murmurations
{
    internal static class MurmurationManager
    {
        private const float SpawnRaycastHeight = 500f;
        private const float SpawnRaycastDistance = 1200f;
        private const float ObstacleProbeRadius = 30f;
        private static readonly List<MurmurationController> s_Controllers = new();
        private static float s_SpawnRollTimerHours;
        private static float s_RespawnCooldownHours;
        private static float s_LastHoursPlayed = -1f;

        public static void Update()
        {
            if (Settings.options == null || !Settings.options.EnableMurmurations)
            {
                Despawn();
                ResetGameHourTracking();
                return;
            }

            if (GameManager.m_IsPaused)
            {
                PauseSounds();
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float elapsedHours = GetElapsedGameHours();

            UpdateControllers(elapsedHours);

            if (s_RespawnCooldownHours > 0f) s_RespawnCooldownHours -= elapsedHours;

            if (!Settings.options.AutoSpawn) return;
            if (s_Controllers.Count >= GetMaxActiveMurmurations()) return;
            if (s_RespawnCooldownHours > 0f) return;
            if (!CanSpawn(out bool isAuroraMurmuration, out float timeMultiplier)) return;

            if (s_SpawnRollTimerHours <= 0f) ResetSpawnRollTimer();
            s_SpawnRollTimerHours -= elapsedHours;
            if (s_SpawnRollTimerHours > 0f) return;

            ResetSpawnRollTimer();

            float chance = Mathf.Clamp(Settings.options.SpawnChancePerRoll * Mathf.Max(0f, timeMultiplier), 0f, 100f);
            if (UnityEngine.Random.value > chance / 100f) return;

            if (TrySpawnNearPlayer("ambient roll", isAuroraMurmuration))
            {
                s_RespawnCooldownHours = Mathf.Max(0f, Settings.options.RespawnCooldownHours);
            }
        }

        public static void ForceSpawn(string source)
        {
            if (Settings.options == null || !Settings.options.EnableMurmurations) return;

            bool isAuroraMurmuration = IsAuroraActive() && Settings.options.AllowAuroraMurmurations;
            TrySpawnNearPlayer(source, isAuroraMurmuration, true);
        }

        public static void Despawn()
        {
            for (int i = 0; i < s_Controllers.Count; i++)
            {
                s_Controllers[i]?.Destroy();
            }

            s_Controllers.Clear();
        }

        private static void PauseSounds()
        {
            for (int i = 0; i < s_Controllers.Count; i++)
            {
                s_Controllers[i]?.PauseSound();
            }
        }

        public static void RefreshSettings()
        {
            ResetSpawnRollTimer();

            for (int i = 0; i < s_Controllers.Count; i++)
            {
                s_Controllers[i]?.RefreshSettings();
            }
        }

        public static void LogStatus()
        {
            if (Settings.options == null)
            {
                Core.Log("Murmuration status: settings not loaded.", false);
                return;
            }

            WeatherStage weatherStage = GetCurrentWeatherStage();
            float hour = GetCurrentHour();
            bool canSpawn = CanSpawn(out bool aurora, out float timeMultiplier);
            Core.Log($"Murmuration status | Active:{s_Controllers.Count}/{GetMaxActiveMurmurations()} | Enabled:{Settings.options.EnableMurmurations} | AutoSpawn:{Settings.options.AutoSpawn} | CanSpawn:{canSpawn} | Aurora:{aurora} | Weather:{weatherStage} | Hour:{hour:0.0} | TimeWeight:{timeMultiplier:0.00} | NextRoll:{s_SpawnRollTimerHours:0.##}h | Cooldown:{s_RespawnCooldownHours:0.##}h", false);
        }

        private static void UpdateControllers(float elapsedHours)
        {
            bool globalConditionsValid = AreRuntimeConditionsStillValid(out bool auroraActive);
            int maxActiveMurmurations = GetMaxActiveMurmurations();

            for (int i = s_Controllers.Count - 1; i >= 0; i--)
            {
                MurmurationController controller = s_Controllers[i];
                if (controller == null)
                {
                    s_Controllers.RemoveAt(i);
                    continue;
                }

                bool useAuroraVisuals = globalConditionsValid && auroraActive && Settings.options.AllowAuroraMurmurations;
                controller.SetAuroraVisuals(useAuroraVisuals);

                if (i >= maxActiveMurmurations)
                {
                    controller.BeginFadeOut(Settings.options.InvalidWeatherFadeOutHours, "active limit reduced");
                }
                else if (!globalConditionsValid)
                {
                    controller.BeginFadeOut(Settings.options.InvalidWeatherFadeOutHours, "invalid runtime conditions");
                }

                controller.Update(elapsedHours);

                if (!controller.IsDestroyed) continue;

                controller.Destroy();
                s_Controllers.RemoveAt(i);
            }
        }

        private static bool TrySpawnNearPlayer(string source, bool isAuroraMurmuration, bool ignoreActiveLimit = false)
        {
            if (Settings.options == null || !Settings.options.EnableMurmurations) return false;
            if (!ignoreActiveLimit && s_Controllers.Count >= GetMaxActiveMurmurations()) return false;

            if (!CanSpawn(out bool canBeAurora, out _)) return false;
            if (isAuroraMurmuration && !canBeAurora) isAuroraMurmuration = false;

            Transform playerTransform = GameManager.GetPlayerTransform();
            if (playerTransform == null) return false;

            int attempts = Mathf.Max(1, Settings.options.SpawnValidationAttempts);
            for (int i = 0; i < attempts; i++)
            {
                if (!TryFindSpawnPosition(playerTransform, out Vector3 spawnCenter)) continue;

                MurmurationController controller = new();
                float durationHours = UnityEngine.Random.Range(
                    Mathf.Min(Settings.options.MinDurationHours, Settings.options.MaxDurationHours),
                    Mathf.Max(Settings.options.MinDurationHours, Settings.options.MaxDurationHours));

                controller.Initialize(spawnCenter, durationHours, isAuroraMurmuration, source);
                s_Controllers.Add(controller);

                Core.Log($"Spawned distant crow murmuration #{s_Controllers.Count} | Boids:{Settings.options.BoidCount} | Aurora:{isAuroraMurmuration} | Duration:{durationHours:0.##}h | Source:{source}", false);
                return true;
            }

            Core.Log($"Murmuration spawn failed after {attempts} attempts | Source:{source}");
            return false;
        }

        private static bool TryFindSpawnPosition(Transform playerTransform, out Vector3 spawnCenter)
        {
            spawnCenter = Vector3.zero;

            float minDistance = Mathf.Min(Settings.options.MinSpawnDistance, Settings.options.MaxSpawnDistance);
            float maxDistance = Mathf.Max(Settings.options.MinSpawnDistance, Settings.options.MaxSpawnDistance);
            float minHeight = Mathf.Min(Settings.options.MinSpawnHeightAbovePlayer, Settings.options.MaxSpawnHeightAbovePlayer);
            float maxHeight = Mathf.Max(Settings.options.MinSpawnHeightAbovePlayer, Settings.options.MaxSpawnHeightAbovePlayer);

            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle;
            if (randomCircle.sqrMagnitude < 0.001f) randomCircle = Vector2.up;
            randomCircle.Normalize();

            Vector3 horizontalDirection = new(randomCircle.x, 0f, randomCircle.y);
            Vector3 candidate = playerTransform.position
                + horizontalDirection * UnityEngine.Random.Range(minDistance, maxDistance)
                + Vector3.up * UnityEngine.Random.Range(minHeight, maxHeight);

            float groundY = GetGroundY(candidate, out bool foundGround);
            if (foundGround)
            {
                float minTerrainY = groundY + Mathf.Max(20f, Settings.options.MinHeightAboveTerrain);
                if (candidate.y < minTerrainY) candidate.y = minTerrainY;
            }

            if (candidate.y < playerTransform.position.y + Mathf.Max(20f, Settings.options.MinSpawnHeightAbovePlayer)) return false;
            if (foundGround && candidate.y < groundY + Mathf.Max(20f, Settings.options.MinHeightAboveTerrain)) return false;
            if (IsTooCloseToObstacle(candidate)) return false;
            if (IsViewObstructed(playerTransform.position + Vector3.up * 1.65f, candidate)) return false;

            spawnCenter = candidate;
            return true;
        }

        private static bool CanSpawn(out bool isAuroraMurmuration, out float timeMultiplier)
        {
            isAuroraMurmuration = false;
            timeMultiplier = 0f;

            if (GameManager.GetPlayerManagerComponent() == null) return false;
            if (GameManager.GetPlayerTransform() == null) return false;

            Weather weather = GameManager.GetWeatherComponent();
            if (weather == null) return false;
            if (weather.IsIndoorEnvironment()) return false;
            if (!IsAllowedWeatherStage(GetCurrentWeatherStage())) return false;

            bool auroraActive = IsAuroraActive();
            bool dayAllowed = IsTimeAllowed(auroraActive, out timeMultiplier);
            if (!dayAllowed) return false;

            isAuroraMurmuration = auroraActive && Settings.options.AllowAuroraMurmurations;
            return true;
        }

        private static bool AreRuntimeConditionsStillValid(out bool auroraActive)
        {
            auroraActive = IsAuroraActive();

            if (Settings.options == null || !Settings.options.EnableMurmurations) return false;

            Weather weather = GameManager.GetWeatherComponent();
            if (weather == null) return false;
            if (weather.IsIndoorEnvironment()) return false;
            if (!IsAllowedWeatherStage(GetCurrentWeatherStage())) return false;
            if (!IsTimeAllowed(auroraActive, out _)) return false;

            return true;
        }

        private static bool IsTimeAllowed(bool auroraActive, out float multiplier)
        {
            multiplier = 1f;

            float hour = GetCurrentHour();
            bool isDay = hour >= 6f && hour < 20f;

            if (!isDay)
            {
                if (auroraActive && Settings.options.AllowAuroraMurmurations)
                {
                    multiplier = Mathf.Max(0f, Settings.options.NightChanceMultiplier);
                    return multiplier > 0f;
                }

                if (Settings.options.DayOnly) return false;

                multiplier = Mathf.Max(0f, Settings.options.NightChanceMultiplier);
                return multiplier > 0f;
            }

            if (IsHourInsideRange(hour, Settings.options.MorningPeakStartHour, Settings.options.MorningPeakEndHour) ||
                IsHourInsideRange(hour, Settings.options.EveningPeakStartHour, Settings.options.EveningPeakEndHour))
            {
                multiplier = Mathf.Max(0f, Settings.options.PeakChanceMultiplier);
            }

            return multiplier > 0f;
        }

        private static bool IsHourInsideRange(float hour, float start, float end)
        {
            start = Mathf.Repeat(start, 24f);
            end = Mathf.Repeat(end, 24f);
            hour = Mathf.Repeat(hour, 24f);

            if (Mathf.Approximately(start, end)) return false;
            if (start < end) return hour >= start && hour < end;

            return hour >= start || hour < end;
        }

        private static bool IsAllowedWeatherStage(WeatherStage weatherStage)
        {
            return weatherStage switch
            {
                WeatherStage.Clear => Settings.options.AllowWeatherClear,
                WeatherStage.ClearAurora => Settings.options.AllowWeatherClear,
                WeatherStage.PartlyCloudy => Settings.options.AllowWeatherPartlyCloudy,
                WeatherStage.LightFog => Settings.options.AllowWeatherLightFog,
                WeatherStage.LightSnow => Settings.options.AllowWeatherLightSnow,
                _ => false
            };
        }

        private static WeatherStage GetCurrentWeatherStage()
        {
            Weather weather = GameManager.GetWeatherComponent();
            return weather != null ? weather.GetWeatherStage() : WeatherStage.Undefined;
        }

        private static float GetCurrentHour()
        {
            TimeOfDay timeOfDay = GameManager.GetTimeOfDayComponent();
            return timeOfDay != null ? timeOfDay.GetHour() : 12f;
        }

        private static bool IsAuroraActive()
        {
            AuroraManager auroraManager = GameManager.GetAuroraManager();
            if (auroraManager != null && auroraManager.AuroraIsActive()) return true;

            return GetCurrentWeatherStage() == WeatherStage.ClearAurora;
        }

        private static void ResetSpawnRollTimer()
        {
            if (Settings.options == null)
            {
                s_SpawnRollTimerHours = 1f;
                return;
            }

            float minDelay = Mathf.Min(Settings.options.MinSpawnRollDelayHours, Settings.options.MaxSpawnRollDelayHours);
            float maxDelay = Mathf.Max(Settings.options.MinSpawnRollDelayHours, Settings.options.MaxSpawnRollDelayHours);
            s_SpawnRollTimerHours = UnityEngine.Random.Range(Mathf.Max(0.01f, minDelay), Mathf.Max(0.01f, maxDelay));
        }

        private static float GetElapsedGameHours()
        {
            TimeOfDay timeOfDay = GameManager.GetTimeOfDayComponent();
            if (timeOfDay == null)
            {
                ResetGameHourTracking();
                return 0f;
            }

            float currentHoursPlayed = timeOfDay.GetHoursPlayedNotPaused();
            if (s_LastHoursPlayed < 0f)
            {
                s_LastHoursPlayed = currentHoursPlayed;
                return 0f;
            }

            float elapsedHours = Mathf.Max(0f, currentHoursPlayed - s_LastHoursPlayed);
            s_LastHoursPlayed = currentHoursPlayed;
            return elapsedHours;
        }

        private static void ResetGameHourTracking()
        {
            s_LastHoursPlayed = -1f;
        }

        private static int GetMaxActiveMurmurations()
        {
            return Settings.options != null ? Mathf.Clamp(Settings.options.MaxActiveMurmurations, 1, 5) : 1;
        }

        private static float GetGroundY(Vector3 position, out bool foundGround)
        {
            Vector3 rayOrigin = position + Vector3.up * SpawnRaycastHeight;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, SpawnRaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                foundGround = true;
                return hit.point.y;
            }

            foundGround = false;
            return position.y - Settings.options.MinHeightAboveTerrain;
        }

        private static bool IsTooCloseToObstacle(Vector3 center)
        {
            float radius = Mathf.Max(10f, ObstacleProbeRadius);
            return Physics.CheckSphere(center, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private static bool IsViewObstructed(Vector3 viewerPosition, Vector3 center)
        {
            Vector3 direction = center - viewerPosition;
            float distance = direction.magnitude;
            if (distance <= 0.1f) return false;

            return Physics.Raycast(viewerPosition, direction / distance, distance * 0.85f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
    }

    internal enum MurmurationLifecycleState
    {
        Forming,
        Active,
        FadingOut,
        Destroyed
    }

    internal sealed class MurmurationController
    {
        private const float MaxForce = 18f;
        private const float AnchorGroundCheckIntervalSeconds = 1f;
        private const float PlayerAvoidDistance = 80f;
        private const float ObstacleCheckIntervalSeconds = 0.25f;
        private const float ObstacleLookAheadSeconds = 2.5f;
        private const float ObstacleAvoidanceForceMultiplier = 2.5f;
        private const float InitialSpawnHalfSize = 42f;
        private const int DefaultMaxNeighborChecksPerBoid = 24;
        private const int BirdTextureSize = 64;
        private const string BirdTextureResourceName = "Murmurations.Icons.Bird_Icon_1.png";
        private const string BirdTextureFrame1ResourceName = "Murmurations.Icons.Bird_Icon_1.png";
        private const string BirdTextureFrame2ResourceName = "Murmurations.Icons.Bird_Icon_2.png";
        private const string BirdTextureFrame3ResourceName = "Murmurations.Icons.Bird_Icon_3.png";
        private const float MinBirdScale = 1.4f;
        private const float MaxBirdScale = 2.4f;
        private const float WingFlutterStrength = 0.12f;
        private const float WingFlutterSpeed = 10.5f;
        private const float BirdAnimationFps = 8f;
        private const float DefaultFollowerTrailLength = 80f;
        private const int LeaderTrailHistorySize = 512;
        private const float LeaderTrailRecordMinDistance = 0.25f;
        private const float BehaviorRegimeMinSeconds = 8f;
        private const float BehaviorRegimeMaxSeconds = 18f;
        private const float BehaviorRegimeBlendSpeed = 0.32f;

        private static readonly string[] s_AnimatedBirdTextureResourceNames =
        {
            BirdTextureFrame1ResourceName,
            BirdTextureFrame2ResourceName,
            BirdTextureFrame3ResourceName
        };

        private static Mesh s_QuadMesh;
        private static Texture2D s_BirdTexture;
        private static Texture2D[] s_AnimatedBirdTextures;

        private readonly List<BoidData> m_Boids = new();
        private readonly Dictionary<int, List<int>> m_SpatialGrid = new(256);
        private Vector3 m_AnchorStart;
        private Vector3 m_Anchor;
        private float m_Elapsed;
        private float m_LifecycleElapsedHours;
        private float m_DurationHours;
        private float m_FadeOutElapsedHours;
        private float m_FadeOutDurationHours;
        private GameObject m_Root;
        private Material[] m_Materials;
        private readonly MurmurationSound m_Sound = new();
        private int m_TargetBoidCount;
        private int m_TargetSimulatedBoidCount;
        private int m_LastAppliedBoidCount = -1;
        private int m_LastAppliedSimulatedBoidCount = -1;
        private float m_LastAppliedFollowerTrailLength = -1f;
        private bool m_LastAppliedAnimatedCrows;
        private float m_SimulationTimer;
        private float m_GroundCheckTimerSeconds;
        private Camera m_CachedViewerCamera;
        private float m_ViewerCameraRefreshTimer;
        private bool m_LoggedNoViewerCamera;
        private bool m_IsAuroraMurmuration;
        private MurmurationLifecycleState m_State;
        private Vector3 m_CurrentManeuverDirection = Vector3.forward;
        private Vector3 m_TargetManeuverDirection = Vector3.forward;
        private float m_BehaviorTimerSeconds;
        private float m_CurrentAlignmentWeight = 1f;
        private float m_TargetAlignmentWeight = 1f;
        private float m_CurrentCohesionWeight = 1f;
        private float m_TargetCohesionWeight = 1f;
        private float m_CurrentSeparationWeight = 1f;
        private float m_TargetSeparationWeight = 1f;
        private float m_CurrentAnchorWeight = 1f;
        private float m_TargetAnchorWeight = 1f;
        private float m_CurrentNoiseWeight = 1f;
        private float m_TargetNoiseWeight = 1f;
        private float m_CurrentManeuverWeight = 1f;
        private float m_TargetManeuverWeight = 1f;
        private float m_CurrentExpansionBias;
        private float m_TargetExpansionBias;
        private MurmurationBehaviorProfile m_LastAppliedBehaviorProfile = (MurmurationBehaviorProfile)(-1);

        public bool IsDestroyed => m_State == MurmurationLifecycleState.Destroyed;

        public void Initialize(Vector3 center, float durationHours, bool isAuroraMurmuration, string source)
        {
            m_Root = new GameObject("Murmurations_Controller");
            m_AnchorStart = center;
            m_Anchor = center;
            m_DurationHours = Mathf.Max(0.01f, durationHours);
            m_IsAuroraMurmuration = isAuroraMurmuration;
            m_State = MurmurationLifecycleState.Forming;
            m_FadeOutDurationHours = Mathf.Max(0.01f, Settings.options.FadeOutDurationHours);

            RefreshSettings();
            PickNewBehaviorRegime(true);
            UpdateVisualAlpha();

            Core.Log($"Initialized murmuration | Visible:{m_TargetBoidCount} | Simulated:{m_TargetSimulatedBoidCount} | Aurora:{m_IsAuroraMurmuration} | Duration:{m_DurationHours:0.##}h | Source:{source}");
        }

        public void RefreshSettings()
        {
            ApplyRuntimeSettings(true);
        }

        private void ApplyRuntimeSettings(bool force = false)
        {
            if (Settings.options == null) return;

            int newTargetBoidCount = Mathf.Clamp(Settings.options.BoidCount, 1, 10000);
            int newTargetSimulatedBoidCount = Mathf.Clamp(Settings.options.SimulatedBoidCount, 1, newTargetBoidCount);
            float newFollowerTrailLength = Mathf.Clamp(Settings.options.FollowerTrailLength, 5f, 400f);
            bool newAnimatedCrows = Settings.options.AnimatedCrows;
            MurmurationBehaviorProfile newBehaviorProfile = Settings.options.BehaviorProfile;

            bool boidCountChanged = force || newTargetBoidCount != m_LastAppliedBoidCount;
            bool simulatedCountChanged = force || newTargetSimulatedBoidCount != m_LastAppliedSimulatedBoidCount;
            bool followerTrailLengthChanged = force || Mathf.Abs(newFollowerTrailLength - m_LastAppliedFollowerTrailLength) > 0.01f;
            bool visualMaterialChanged = force || m_Materials == null || newAnimatedCrows != m_LastAppliedAnimatedCrows || !AreMaterialsAlive();
            bool behaviorProfileChanged = newBehaviorProfile != m_LastAppliedBehaviorProfile;

            m_TargetBoidCount = newTargetBoidCount;
            m_TargetSimulatedBoidCount = newTargetSimulatedBoidCount;
            m_FadeOutDurationHours = Mathf.Max(0.01f, Settings.options.FadeOutDurationHours);

            float minDurationHours = Mathf.Max(0.01f, Mathf.Min(Settings.options.MinDurationHours, Settings.options.MaxDurationHours));
            float maxDurationHours = Mathf.Max(minDurationHours, Mathf.Max(Settings.options.MinDurationHours, Settings.options.MaxDurationHours));
            m_DurationHours = Mathf.Clamp(m_DurationHours, minDurationHours, maxDurationHours);

            if (visualMaterialChanged) RefreshVisualMaterials();

            while (m_Boids.Count > m_TargetBoidCount)
            {
                RemoveLastBoid();
            }

            if (m_State == MurmurationLifecycleState.Active)
            {
                while (m_Boids.Count < m_TargetBoidCount)
                {
                    AddBoid();
                }
            }

            if (boidCountChanged || simulatedCountChanged || followerTrailLengthChanged || force)
            {
                RefreshBoidRoles();
            }

            m_LastAppliedBoidCount = m_TargetBoidCount;
            m_LastAppliedSimulatedBoidCount = m_TargetSimulatedBoidCount;
            m_LastAppliedFollowerTrailLength = newFollowerTrailLength;
            m_LastAppliedAnimatedCrows = newAnimatedCrows;
            m_LastAppliedBehaviorProfile = newBehaviorProfile;

            if (behaviorProfileChanged) PickNewBehaviorRegime(false);

            UpdateVisualAlpha();
        }

        private void RefreshVisualMaterials()
        {
            DestroyMaterials();

            m_Materials = CreateMaterials();

            for (int i = 0; i < m_Boids.Count; i++)
            {
                BoidData boid = m_Boids[i];
                if (boid.Renderer != null) boid.Renderer.sharedMaterial = GetMaterialForFrame(0);

                boid.FrameIndex = 0;
                m_Boids[i] = boid;
            }
        }

        // Scene loads can unload the materials or textures the mod created, which leaves the crows drawn as black quads.
        private bool AreMaterialsAlive()
        {
            if (m_Materials == null) return false;

            for (int i = 0; i < m_Materials.Length; i++)
            {
                if (m_Materials[i] == null || m_Materials[i].mainTexture == null) return false;
            }

            return true;
        }

        private Material GetMaterialForFrame(int frameIndex)
        {
            if (m_Materials == null || m_Materials.Length == 0) return null;

            int index = Mathf.Abs(frameIndex) % m_Materials.Length;
            return m_Materials[index];
        }

        private bool IsUsingAnimatedCrowFrames()
        {
            return Settings.options != null && Settings.options.AnimatedCrows && m_Materials != null && m_Materials.Length > 1;
        }

        public void BeginFadeOut(float fadeOutHours, string reason)
        {
            if (m_State == MurmurationLifecycleState.FadingOut || m_State == MurmurationLifecycleState.Destroyed) return;

            m_State = MurmurationLifecycleState.FadingOut;
            m_FadeOutElapsedHours = 0f;
            m_FadeOutDurationHours = Mathf.Max(0.01f, fadeOutHours);

            Core.Log($"Murmuration fading out | Reason:{reason} | Duration:{m_FadeOutDurationHours:0.##}h");
        }

        public void SetAuroraVisuals(bool isAurora)
        {
            if (m_IsAuroraMurmuration == isAurora) return;

            m_IsAuroraMurmuration = isAurora;
            UpdateVisualAlpha();
        }

        public void Update(float elapsedHours)
        {
            if (GameManager.m_IsPaused) return;
            if (m_State == MurmurationLifecycleState.Destroyed) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            m_Elapsed += dt;
            m_LifecycleElapsedHours += Mathf.Max(0f, elapsedHours);
            m_SimulationTimer += dt;

            ApplyRuntimeSettings();
            UpdateLifecycle(elapsedHours);
            UpdateSound();

            if (Settings.options.SimulationFPSLimit <= 0f || Settings.options.MaxSpeed <= 0f)
            {
                UpdateVisualAlpha();
                UpdateBoidVisualsOnly();
                return;
            }

            float simulationIntervalSeconds = GetSimulationIntervalSeconds();
            if (m_SimulationTimer < simulationIntervalSeconds)
            {
                UpdateVisualAlpha();
                return;
            }

            float simulationDt = Mathf.Min(m_SimulationTimer, simulationIntervalSeconds * 2f);
            m_SimulationTimer = 0f;

            UpdateBehaviorRegime(simulationDt);
            UpdateAnchor(simulationDt);
            UpdateBoids(simulationDt);
            UpdateVisualAlpha();
        }

        private void UpdateBehaviorRegime(float dt)
        {
            if (m_TargetManeuverDirection.sqrMagnitude <= 0.001f) PickNewBehaviorRegime(true);

            m_BehaviorTimerSeconds -= dt;
            if (m_BehaviorTimerSeconds <= 0f) PickNewBehaviorRegime(false);

            float t = Mathf.Clamp01(dt * BehaviorRegimeBlendSpeed);
            m_CurrentManeuverDirection = Vector3.Slerp(m_CurrentManeuverDirection, m_TargetManeuverDirection, t);
            if (m_CurrentManeuverDirection.sqrMagnitude <= 0.001f) m_CurrentManeuverDirection = Vector3.forward;
            m_CurrentManeuverDirection.Normalize();

            m_CurrentAlignmentWeight = Mathf.Lerp(m_CurrentAlignmentWeight, m_TargetAlignmentWeight, t);
            m_CurrentCohesionWeight = Mathf.Lerp(m_CurrentCohesionWeight, m_TargetCohesionWeight, t);
            m_CurrentSeparationWeight = Mathf.Lerp(m_CurrentSeparationWeight, m_TargetSeparationWeight, t);
            m_CurrentAnchorWeight = Mathf.Lerp(m_CurrentAnchorWeight, m_TargetAnchorWeight, t);
            m_CurrentNoiseWeight = Mathf.Lerp(m_CurrentNoiseWeight, m_TargetNoiseWeight, t);
            m_CurrentManeuverWeight = Mathf.Lerp(m_CurrentManeuverWeight, m_TargetManeuverWeight, t);
            m_CurrentExpansionBias = Mathf.Lerp(m_CurrentExpansionBias, m_TargetExpansionBias, t);
        }

        private void PickNewBehaviorRegime(bool instant)
        {
            Vector3 direction = UnityEngine.Random.onUnitSphere;
            direction.y *= 0.25f;
            if (direction.sqrMagnitude <= 0.001f) direction = Vector3.forward;
            direction.Normalize();
            m_TargetManeuverDirection = direction;
            m_BehaviorTimerSeconds = UnityEngine.Random.Range(BehaviorRegimeMinSeconds, BehaviorRegimeMaxSeconds);

            int mode = GetBehaviorRegimeMode();
            if (mode == 0)
            {
                m_TargetAlignmentWeight = 0.55f;
                m_TargetCohesionWeight = 1.3f;
                m_TargetSeparationWeight = 0.85f;
                m_TargetAnchorWeight = 0.85f;
                m_TargetNoiseWeight = 0.9f;
                m_TargetManeuverWeight = 0.75f;
                m_TargetExpansionBias = -0.45f;
            }
            else if (mode == 1)
            {
                m_TargetAlignmentWeight = 0.45f;
                m_TargetCohesionWeight = 0.65f;
                m_TargetSeparationWeight = 1.35f;
                m_TargetAnchorWeight = 0.65f;
                m_TargetNoiseWeight = 1.35f;
                m_TargetManeuverWeight = 1.15f;
                m_TargetExpansionBias = 0.8f;
            }
            else if (mode == 2)
            {
                m_TargetAlignmentWeight = 0.85f;
                m_TargetCohesionWeight = 0.85f;
                m_TargetSeparationWeight = 0.95f;
                m_TargetAnchorWeight = 0.45f;
                m_TargetNoiseWeight = 0.85f;
                m_TargetManeuverWeight = 1.35f;
                m_TargetExpansionBias = 0.1f;
            }
            else if (mode == 3)
            {
                m_TargetAlignmentWeight = 0.7f;
                m_TargetCohesionWeight = 0.95f;
                m_TargetSeparationWeight = 1f;
                m_TargetAnchorWeight = 0.6f;
                m_TargetNoiseWeight = 0.95f;
                m_TargetManeuverWeight = 1.65f;
                m_TargetExpansionBias = 0f;
            }
            else
            {
                m_TargetAlignmentWeight = 0.35f;
                m_TargetCohesionWeight = 0.8f;
                m_TargetSeparationWeight = 1.45f;
                m_TargetAnchorWeight = 0.75f;
                m_TargetNoiseWeight = 1.55f;
                m_TargetManeuverWeight = 1.25f;
                m_TargetExpansionBias = 0.45f;
            }

            if (!instant) return;

            m_CurrentManeuverDirection = m_TargetManeuverDirection;
            m_CurrentAlignmentWeight = m_TargetAlignmentWeight;
            m_CurrentCohesionWeight = m_TargetCohesionWeight;
            m_CurrentSeparationWeight = m_TargetSeparationWeight;
            m_CurrentAnchorWeight = m_TargetAnchorWeight;
            m_CurrentNoiseWeight = m_TargetNoiseWeight;
            m_CurrentManeuverWeight = m_TargetManeuverWeight;
            m_CurrentExpansionBias = m_TargetExpansionBias;
        }

        private static int GetBehaviorRegimeMode()
        {
            if (Settings.options == null || Settings.options.BehaviorProfile == MurmurationBehaviorProfile.Dynamic)
            {
                return UnityEngine.Random.Range(0, 5);
            }

            return Settings.options.BehaviorProfile switch
            {
                MurmurationBehaviorProfile.Compact => 0,
                MurmurationBehaviorProfile.Spread => 1,
                MurmurationBehaviorProfile.Fluid => 2,
                MurmurationBehaviorProfile.Aggressive => 3,
                MurmurationBehaviorProfile.Chaotic => 4,
                _ => UnityEngine.Random.Range(0, 5)
            };
        }

        private void UpdateLifecycle(float elapsedHours)
        {
            if (m_State == MurmurationLifecycleState.Forming)
            {
                AddBoidsForCurrentFormationTime();

                if (m_Boids.Count >= m_TargetBoidCount && m_LifecycleElapsedHours >= Mathf.Max(0f, Settings.options.FadeInDurationHours))
                {
                    m_State = MurmurationLifecycleState.Active;
                }
            }
            else if (m_State == MurmurationLifecycleState.Active)
            {
                if (m_LifecycleElapsedHours >= m_DurationHours)
                {
                    BeginFadeOut(Settings.options.FadeOutDurationHours, "duration expired");
                }
            }
            else if (m_State == MurmurationLifecycleState.FadingOut)
            {
                m_FadeOutElapsedHours += Mathf.Max(0f, elapsedHours);
                RemoveBoidsForCurrentFadeTime();

                if (m_FadeOutElapsedHours >= m_FadeOutDurationHours)
                {
                    m_State = MurmurationLifecycleState.Destroyed;
                }
            }
        }

        private void AddBoidsForCurrentFormationTime()
        {
            int wantedCount = GetWantedBoidCountForFormation();
            while (m_Boids.Count < wantedCount)
            {
                AddBoid();
            }
        }

        private int GetWantedBoidCountForFormation()
        {
            float spawnInDuration = Mathf.Max(0f, Settings.options.SpawnInDurationHours);
            if (spawnInDuration <= 0.01f) return m_TargetBoidCount;

            float progress = Mathf.Clamp01(m_LifecycleElapsedHours / spawnInDuration);
            return Mathf.Clamp(Mathf.CeilToInt(m_TargetBoidCount * progress), 1, m_TargetBoidCount);
        }

        private void RemoveBoidsForCurrentFadeTime()
        {
            if (m_Boids.Count <= 0) return;

            float progress = Mathf.Clamp01(m_FadeOutElapsedHours / m_FadeOutDurationHours);
            int wantedCount = Mathf.Clamp(Mathf.CeilToInt(m_TargetBoidCount * (1f - progress)), 0, m_TargetBoidCount);

            while (m_Boids.Count > wantedCount)
            {
                RemoveLastBoid();
            }
        }

        public void PauseSound()
        {
            m_Sound.Pause();
        }

        private void UpdateSound()
        {
            int simulatedCount = GetActiveSimulatedBoidCount();
            Vector3 center = GetLeaderCenter(simulatedCount);
            m_Sound.Update(m_Root != null ? m_Root.transform : null, center, GetLeaderRadius(simulatedCount, center), GetLifecycleFade());
        }

        // Typical distance of the leaders from the flock center.
        private float GetLeaderRadius(int simulatedCount, Vector3 center)
        {
            int count = Mathf.Min(simulatedCount, m_Boids.Count);
            if (count <= 0) return 0f;

            float sqrDistanceSum = 0f;
            for (int i = 0; i < count; i++) sqrDistanceSum += (m_Boids[i].Position - center).sqrMagnitude;

            return Mathf.Sqrt(sqrDistanceSum / count);
        }

        private float GetCurrentVisualAlpha()
        {
            return GetLifecycleFade() * Mathf.Clamp01(Settings.options.BaseAlpha);
        }

        // 0 to 1 while the murmuration fades in and out.
        private float GetLifecycleFade()
        {
            float fadeInDuration = Mathf.Max(0.01f, Settings.options.FadeInDurationHours);
            float fadeIn = Mathf.Clamp01(m_LifecycleElapsedHours / fadeInDuration);

            float fadeOut = 1f;
            if (m_State == MurmurationLifecycleState.FadingOut)
            {
                fadeOut = 1f - Mathf.Clamp01(m_FadeOutElapsedHours / m_FadeOutDurationHours);
            }
            else if (m_State == MurmurationLifecycleState.Destroyed)
            {
                fadeOut = 0f;
            }

            return Mathf.Clamp01(fadeIn * fadeOut);
        }

        private void UpdateVisualAlpha()
        {
            if (m_Materials == null) return;

            float alpha = GetCurrentVisualAlpha();
            Color targetColor = GetCrowColor(alpha);

            for (int i = 0; i < m_Materials.Length; i++)
            {
                if (m_Materials[i] == null) continue;

                m_Materials[i].color = targetColor;
            }
        }

        private Color GetCrowColor(float alpha)
        {
            Color normal = new(0.03f, 0.03f, 0.03f, alpha);
            if (!m_IsAuroraMurmuration) return normal;

            float tint = Mathf.Clamp01(Settings.options.AuroraGreenTintStrength);
            Color aurora = new(0.08f, 0.42f, 0.22f, alpha);
            return Color.Lerp(normal, aurora, tint);
        }

        private static float GetSimulationIntervalSeconds()
        {
            float simulationFps = Settings.options != null ? Settings.options.SimulationFPSLimit : 25f;
            if (simulationFps <= 0f) return float.PositiveInfinity;

            return 1f / Mathf.Clamp(simulationFps, 1f, 50f);
        }

        private static int GetMaxNeighborChecksPerBoid()
        {
            return Settings.options != null ? Mathf.Clamp(Settings.options.MaxNeighborChecks, 8, 64) : DefaultMaxNeighborChecksPerBoid;
        }

        private static float GetBirdSizeMultiplier()
        {
            return Settings.options != null ? Mathf.Clamp(Settings.options.BirdSize, 0.1f, 2f) : 1f;
        }

        private static int GetTransformUpdateStride()
        {
            return Settings.options != null ? Mathf.Clamp(Settings.options.TransformUpdateStride, 1, 4) : 1;
        }

        private void AddBoid()
        {
            int index = m_Boids.Count;
            bool isSimulated = index < m_TargetSimulatedBoidCount;

            Vector3 randomOffset = new(
                UnityEngine.Random.Range(-InitialSpawnHalfSize, InitialSpawnHalfSize),
                UnityEngine.Random.Range(-InitialSpawnHalfSize, InitialSpawnHalfSize),
                UnityEngine.Random.Range(-InitialSpawnHalfSize, InitialSpawnHalfSize));

            Vector3 position = m_Anchor + randomOffset;
            float maxSpeed = Mathf.Max(0f, Settings.options.MaxSpeed);
            Vector3 velocity = maxSpeed <= 0f ? Vector3.zero : UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(6f, Mathf.Max(6.1f, maxSpeed));
            velocity.y *= 0.35f;

            GameObject boidObject = new(isSimulated ? "Murmurations_Crow_Boid_Simulated" : "Murmurations_Crow_Boid_Follower");
            boidObject.transform.SetParent(m_Root.transform, true);
            boidObject.transform.position = position;
            float scale = UnityEngine.Random.Range(MinBirdScale, MaxBirdScale);
            boidObject.transform.localScale = Vector3.one * scale * GetBirdSizeMultiplier();

            MeshFilter meshFilter = boidObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = GetSharedQuadMesh();

            MeshRenderer meshRenderer = boidObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetMaterialForFrame(0);
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.allowOcclusionWhenDynamic = false;

            BoidData boid = new()
            {
                Transform = boidObject.transform,
                Renderer = meshRenderer,
                Position = position,
                Velocity = velocity,
                Phase = UnityEngine.Random.Range(0f, 1000f),
                Scale = scale,
                FrameIndex = 0,
                IsSimulated = isSimulated,
                LeaderIndex = 0,
                LeaderOffset = Vector3.zero,
                TrailPositions = isSimulated ? CreateInitialTrail(position) : null,
                TrailHead = 0,
                TrailCount = isSimulated ? 1 : 0,
                LastTrailPosition = position,
                AvoidanceCheckTimer = UnityEngine.Random.Range(0f, ObstacleCheckIntervalSeconds)
            };

            if (!isSimulated) AssignFollowerLeader(ref boid, index);

            m_Boids.Add(boid);
        }

        private void RefreshBoidRoles()
        {
            if (m_Boids.Count <= 0) return;

            int simulatedCount = GetActiveSimulatedBoidCount();

            for (int i = 0; i < m_Boids.Count; i++)
            {
                BoidData boid = m_Boids[i];
                bool shouldBeSimulated = i < simulatedCount;

                boid.IsSimulated = shouldBeSimulated;
                if (boid.Transform != null)
                {
                    boid.Transform.gameObject.name = shouldBeSimulated ? "Murmurations_Crow_Boid_Simulated" : "Murmurations_Crow_Boid_Follower";
                }

                if (shouldBeSimulated)
                {
                    boid.LeaderIndex = i;
                    boid.LeaderOffset = Vector3.zero;
                    EnsureLeaderTrailInitialized(ref boid);
                }
                else
                {
                    boid.TrailPositions = null;
                    boid.TrailHead = 0;
                    boid.TrailCount = 0;
                    boid.LastTrailPosition = boid.Position;
                    AssignFollowerLeader(ref boid, i);
                }

                m_Boids[i] = boid;
            }
        }

        private int GetActiveSimulatedBoidCount()
        {
            return Mathf.Clamp(m_TargetSimulatedBoidCount, 1, Mathf.Max(1, m_Boids.Count));
        }

        private void AssignFollowerLeader(ref BoidData boid, int boidIndex)
        {
            int simulatedCount = Mathf.Max(1, GetActiveSimulatedBoidCount());
            boid.IsSimulated = false;

            int followerOrdinal = boidIndex >= simulatedCount ? boidIndex - simulatedCount : Mathf.Abs(boidIndex);
            boid.LeaderIndex = followerOrdinal % simulatedCount;

            int visibleFollowerCount = Mathf.Max(1, m_TargetBoidCount - simulatedCount);
            int followersPerLeader = Mathf.Max(1, Mathf.CeilToInt(visibleFollowerCount / Mathf.Max(1f, simulatedCount)));
            int slotForLeader = followerOrdinal / simulatedCount;

            float slotT = followersPerLeader <= 1 ? 0.5f : slotForLeader / Mathf.Max(1f, followersPerLeader - 1f);
            float randomT = Hash01(followerOrdinal * 47.47f + boid.Phase * 0.083f);
            float distanceT = Mathf.Clamp01(slotT * 0.7f + randomT * 0.3f);

            float trailLength = Settings.options != null ? Mathf.Clamp(Settings.options.FollowerTrailLength, 5f, 400f) : DefaultFollowerTrailLength;
            float trailingDistance = Mathf.Lerp(3f, trailLength, Mathf.Pow(distanceT, 0.72f));

            boid.LeaderOffset = new Vector3(0f, 0f, -trailingDistance);

            if (boidIndex >= simulatedCount && boid.LeaderIndex >= 0 && boid.LeaderIndex < m_Boids.Count)
            {
                BoidData leader = m_Boids[boid.LeaderIndex];
                boid.Position = GetFollowerTargetPosition(boid, leader);
                if (boid.Transform != null) boid.Transform.position = boid.Position;
            }
        }

        private static float Hash01(float value)
        {
            float raw = Mathf.Sin(value * 12.9898f) * 43758.5453f;
            return raw - Mathf.Floor(raw);
        }

        private static Vector3[] CreateInitialTrail(Vector3 position)
        {
            Vector3[] trail = new Vector3[LeaderTrailHistorySize];
            trail[0] = position;
            return trail;
        }

        private static void EnsureLeaderTrailInitialized(ref BoidData boid)
        {
            if (boid.TrailPositions == null || boid.TrailPositions.Length != LeaderTrailHistorySize)
            {
                boid.TrailPositions = CreateInitialTrail(boid.Position);
                boid.TrailHead = 0;
                boid.TrailCount = 1;
                boid.LastTrailPosition = boid.Position;
                return;
            }

            if (boid.TrailCount <= 0)
            {
                boid.TrailHead = 0;
                boid.TrailPositions[0] = boid.Position;
                boid.TrailCount = 1;
                boid.LastTrailPosition = boid.Position;
            }
        }

        private static void RecordLeaderTrailPoint(ref BoidData boid)
        {
            EnsureLeaderTrailInitialized(ref boid);

            if ((boid.Position - boid.LastTrailPosition).sqrMagnitude < LeaderTrailRecordMinDistance * LeaderTrailRecordMinDistance) return;

            boid.TrailHead = (boid.TrailHead + 1) % LeaderTrailHistorySize;
            boid.TrailPositions[boid.TrailHead] = boid.Position;
            boid.TrailCount = Mathf.Min(boid.TrailCount + 1, LeaderTrailHistorySize);
            boid.LastTrailPosition = boid.Position;
        }

        private static Vector3 SampleLeaderTrailPosition(BoidData leader, float distanceBehind)
        {
            if (leader.TrailPositions == null || leader.TrailCount <= 0) return leader.Position;
            if (distanceBehind <= 0f) return leader.Position;

            Vector3 previous = leader.Position;
            float accumulatedDistance = 0f;

            for (int i = 0; i < leader.TrailCount; i++)
            {
                int index = leader.TrailHead - i;
                if (index < 0) index += LeaderTrailHistorySize;

                Vector3 current = leader.TrailPositions[index];
                float segmentDistance = Vector3.Distance(previous, current);

                if (segmentDistance > 0.001f)
                {
                    float nextAccumulatedDistance = accumulatedDistance + segmentDistance;
                    if (nextAccumulatedDistance >= distanceBehind)
                    {
                        float segmentT = (distanceBehind - accumulatedDistance) / segmentDistance;
                        return Vector3.Lerp(previous, current, Mathf.Clamp01(segmentT));
                    }

                    accumulatedDistance = nextAccumulatedDistance;
                }

                previous = current;
            }

            return previous;
        }

        private void RemoveLastBoid()
        {
            int index = m_Boids.Count - 1;
            BoidData boid = m_Boids[index];
            if (boid.Transform != null) UnityEngine.Object.Destroy(boid.Transform.gameObject);

            m_Boids.RemoveAt(index);
        }

        private void UpdateAnchor(float dt)
        {
            m_Anchor = m_AnchorStart;

            m_GroundCheckTimerSeconds -= dt;
            if (m_GroundCheckTimerSeconds > 0f) return;

            m_GroundCheckTimerSeconds = AnchorGroundCheckIntervalSeconds;
            KeepAnchorAboveTerrain();
        }

        private void KeepAnchorAboveTerrain()
        {
            Vector3 rayOrigin = m_Anchor + Vector3.up * 500f;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;

            float minY = hit.point.y + Mathf.Max(20f, Settings.options.MinHeightAboveTerrain);
            if (m_Anchor.y >= minY) return;

            float delta = minY - m_Anchor.y;
            m_Anchor.y = minY;
            m_AnchorStart.y += delta;
        }

        private Vector3 GetLeaderCenter(int simulatedCount)
        {
            int count = Mathf.Min(simulatedCount, m_Boids.Count);
            if (count <= 0) return m_Anchor;

            Vector3 center = Vector3.zero;
            for (int i = 0; i < count; i++) center += m_Boids[i].Position;

            return center / count;
        }

        private void UpdateBoids(float dt)
        {
            if (m_Boids.Count <= 0) return;

            Transform playerTransform = GameManager.GetPlayerTransform();
            Vector3 playerPosition = playerTransform != null ? playerTransform.position : Vector3.zero;
            Camera viewerCamera = GetViewerCamera(playerPosition);
            Vector3 viewerPosition = viewerCamera != null ? viewerCamera.transform.position : playerPosition + Vector3.up * 1.65f;
            float neighborRadius = Mathf.Max(0.1f, Settings.options.NeighborRadius);
            float separationRadius = Mathf.Max(0.1f, Settings.options.SeparationRadius);
            float neighborRadiusSqr = neighborRadius * neighborRadius;
            float separationRadiusSqr = separationRadius * separationRadius;
            float cellSize = neighborRadius;
            float maxSpeed = Mathf.Max(0f, Settings.options.MaxSpeed);

            if (maxSpeed <= 0f)
            {
                UpdateBoidVisualsOnly(viewerCamera, viewerPosition);
                return;
            }

            RebuildSpatialGrid(cellSize);

            int simulatedCount = GetActiveSimulatedBoidCount();
            Vector3 leaderCenter = GetLeaderCenter(simulatedCount);
            int maxNeighborChecks = GetMaxNeighborChecksPerBoid();
            int transformUpdateStride = GetTransformUpdateStride();
            int transformPhase = Mathf.FloorToInt(m_Elapsed * 60f);

            for (int i = 0; i < m_Boids.Count; i++)
            {
                BoidData boid = m_Boids[i];

                if (i < simulatedCount && boid.IsSimulated)
                {
                    UpdateSimulatedBoid(ref boid, i, dt, playerPosition, leaderCenter, neighborRadiusSqr, separationRadiusSqr, cellSize, maxNeighborChecks, maxSpeed);
                }
                else
                {
                    UpdateFollowerBoid(ref boid, dt, simulatedCount);
                }

                if (boid.Transform != null && (transformUpdateStride <= 1 || ((i + transformPhase) % transformUpdateStride) == 0))
                {
                    UpdateBillboardTransform(ref boid, viewerCamera, viewerPosition);
                }

                m_Boids[i] = boid;
            }
        }

        private void UpdateBoidVisualsOnly()
        {
            Transform playerTransform = GameManager.GetPlayerTransform();
            Vector3 playerPosition = playerTransform != null ? playerTransform.position : Vector3.zero;
            Camera viewerCamera = GetViewerCamera(playerPosition);
            Vector3 viewerPosition = viewerCamera != null ? viewerCamera.transform.position : playerPosition + Vector3.up * 1.65f;

            UpdateBoidVisualsOnly(viewerCamera, viewerPosition);
        }

        private void UpdateBoidVisualsOnly(Camera viewerCamera, Vector3 viewerPosition)
        {
            int transformUpdateStride = GetTransformUpdateStride();
            int transformPhase = Mathf.FloorToInt(m_Elapsed * 60f);

            for (int i = 0; i < m_Boids.Count; i++)
            {
                BoidData boid = m_Boids[i];
                if (boid.Transform != null && (transformUpdateStride <= 1 || ((i + transformPhase) % transformUpdateStride) == 0))
                {
                    UpdateBillboardTransform(ref boid, viewerCamera, viewerPosition);
                    m_Boids[i] = boid;
                }
            }
        }

        private void UpdateSimulatedBoid(ref BoidData boid, int boidIndex, float dt, Vector3 playerPosition, Vector3 leaderCenter, float neighborRadiusSqr, float separationRadiusSqr, float cellSize, int maxNeighborChecks, float maxSpeed)
        {
            Vector3 separation = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            Vector3 cohesion = Vector3.zero;
            int neighborCount = 0;
            int separationCount = 0;
            int checks = 0;

            int cellX = Mathf.FloorToInt(boid.Position.x / cellSize);
            int cellY = Mathf.FloorToInt(boid.Position.y / cellSize);
            int cellZ = Mathf.FloorToInt(boid.Position.z / cellSize);

            for (int x = cellX - 1; x <= cellX + 1 && checks < maxNeighborChecks; x++)
            {
                for (int y = cellY - 1; y <= cellY + 1 && checks < maxNeighborChecks; y++)
                {
                    for (int z = cellZ - 1; z <= cellZ + 1 && checks < maxNeighborChecks; z++)
                    {
                        if (!m_SpatialGrid.TryGetValue(GetCellKey(x, y, z), out List<int> indices)) continue;

                        for (int index = 0; index < indices.Count && checks < maxNeighborChecks; index++)
                        {
                            int j = indices[index];
                            if (boidIndex == j) continue;

                            checks++;
                            BoidData other = m_Boids[j];
                            Vector3 offset = other.Position - boid.Position;
                            float sqrDistance = offset.sqrMagnitude;
                            if (sqrDistance > neighborRadiusSqr) continue;

                            alignment += other.Velocity;
                            cohesion += other.Position;
                            neighborCount++;

                            if (sqrDistance < separationRadiusSqr && sqrDistance > 0.001f)
                            {
                                separation -= offset / Mathf.Max(0.25f, sqrDistance);
                                separationCount++;
                            }
                        }
                    }
                }
            }

            Vector3 acceleration = Vector3.zero;

            if (neighborCount > 0)
            {
                acceleration += SteerToward(alignment / neighborCount, boid.Velocity) * 0.95f * m_CurrentAlignmentWeight;
                acceleration += SteerToward((cohesion / neighborCount) - boid.Position, boid.Velocity) * 0.35f * m_CurrentCohesionWeight;
            }

            if (separationCount > 0)
            {
                acceleration += SteerToward(separation / separationCount, boid.Velocity) * 1.15f * m_CurrentSeparationWeight;
            }

            acceleration += SteerToward(m_Anchor - boid.Position, boid.Velocity) * 0.12f * m_CurrentAnchorWeight;
            acceleration += GetSphereContainmentForce(boid.Position, boid.Velocity) * Mathf.Clamp(Settings.options.ConfinementSphereStrength, 0f, 3f);
            acceleration += GetNoiseForce(boid.Phase) * 0.45f * m_CurrentNoiseWeight;
            acceleration += GetManeuverForce(boid, boidIndex, leaderCenter) * Mathf.Clamp(Settings.options.ManeuverVariationStrength, 0f, 3f) * m_CurrentManeuverWeight;
            acceleration += GetPlayerAvoidance(boid.Position, playerPosition) * 1.4f;
            if (m_State == MurmurationLifecycleState.FadingOut) acceleration += SteerToward(boid.Position - m_Anchor + Vector3.up * 0.25f, boid.Velocity) * 0.35f;
            acceleration = Vector3.ClampMagnitude(acceleration, MaxForce);

            // Avoiding terrain and objects overrides the flocking forces, and can turn harder so crows don't swing wide into a hill.
            UpdateObstacleAvoidance(ref boid, dt);
            float avoidanceStrength = Mathf.Clamp01(boid.Avoidance.magnitude);
            if (avoidanceStrength > 0.001f)
            {
                Vector3 avoidanceVelocity = boid.Avoidance.normalized * maxSpeed;
                Vector3 avoidanceForce = Vector3.ClampMagnitude(avoidanceVelocity - boid.Velocity, MaxForce * ObstacleAvoidanceForceMultiplier);
                acceleration = Vector3.Lerp(acceleration, avoidanceForce, avoidanceStrength);
            }

            boid.Velocity = Vector3.ClampMagnitude(boid.Velocity + acceleration * dt, maxSpeed);
            boid.Position += boid.Velocity * dt;
            RecordLeaderTrailPoint(ref boid);
        }

        private void UpdateFollowerBoid(ref BoidData boid, float dt, int simulatedCount)
        {
            if (simulatedCount <= 0) return;

            if (boid.LeaderIndex < 0 || boid.LeaderIndex >= simulatedCount)
            {
                AssignFollowerLeader(ref boid, -1);
            }

            BoidData leader = m_Boids[boid.LeaderIndex];
            Vector3 targetPosition = GetFollowerTargetPosition(boid, leader);

            boid.Position = targetPosition;
            boid.Velocity = leader.Velocity;
        }

        private Vector3 GetFollowerTargetPosition(BoidData follower, BoidData leader)
        {
            float distanceBehind = Mathf.Abs(follower.LeaderOffset.z);
            return SampleLeaderTrailPosition(leader, distanceBehind);
        }

        private Camera GetViewerCamera(Vector3 playerPosition)
        {
            if (IsValidViewerCamera(m_CachedViewerCamera)) return m_CachedViewerCamera;

            m_ViewerCameraRefreshTimer -= Time.deltaTime;
            if (m_ViewerCameraRefreshTimer > 0f) return null;

            m_ViewerCameraRefreshTimer = 0.5f;

            Camera mainCamera = Camera.main;
            if (IsValidViewerCamera(mainCamera))
            {
                m_CachedViewerCamera = mainCamera;
                m_LoggedNoViewerCamera = false;
                return m_CachedViewerCamera;
            }

            Camera[] cameras = Camera.allCameras;
            Camera bestCamera = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (!IsValidViewerCamera(camera)) continue;

                float distanceSqr = (camera.transform.position - playerPosition).sqrMagnitude;
                float score = camera.depth - distanceSqr * 0.001f;

                string cameraName = camera.name;
                if (!string.IsNullOrEmpty(cameraName))
                {
                    if (cameraName.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0) score += 100f;
                    if (cameraName.IndexOf("Game", StringComparison.OrdinalIgnoreCase) >= 0) score += 50f;
                    if (cameraName.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0) score += 50f;
                }

                if (score <= bestScore) continue;

                bestScore = score;
                bestCamera = camera;
            }

            m_CachedViewerCamera = bestCamera;

            if (m_CachedViewerCamera != null)
            {
                m_LoggedNoViewerCamera = false;
                return m_CachedViewerCamera;
            }

            if (!m_LoggedNoViewerCamera)
            {
                Core.Warn("No active viewer camera found. Using player-position billboard fallback.");
                m_LoggedNoViewerCamera = true;
            }

            return null;
        }

        private static bool IsValidViewerCamera(Camera camera)
        {
            if (camera == null) return false;
            if (!camera.enabled) return false;
            if (!camera.gameObject.activeInHierarchy) return false;
            if (camera.orthographic) return false;
            if (camera.pixelWidth < 64 || camera.pixelHeight < 64) return false;

            return true;
        }

        private void UpdateBillboardTransform(ref BoidData boid, Camera viewerCamera, Vector3 viewerPosition)
        {
            boid.Transform.position = boid.Position;
            bool directional = Settings.options != null && Settings.options.DirectionalCrows;

            if (viewerCamera != null)
            {
                Transform cameraTransform = viewerCamera.transform;
                boid.Transform.rotation = directional
                    ? GetDirectionalBillboardRotation(boid, cameraTransform.forward, cameraTransform.up)
                    : cameraTransform.rotation;
            }
            else
            {
                Vector3 fromViewer = boid.Position - viewerPosition;
                if (fromViewer.sqrMagnitude > 0.001f)
                {
                    boid.Transform.rotation = directional
                        ? GetDirectionalBillboardRotation(boid, fromViewer.normalized, Vector3.up)
                        : Quaternion.LookRotation(fromViewer.normalized, Vector3.up);
                }
            }

            if (IsUsingAnimatedCrowFrames() && boid.Renderer != null)
            {
                int frameIndex = Mathf.FloorToInt((Time.time * BirdAnimationFps) + boid.Phase) % m_Materials.Length;
                if (frameIndex < 0) frameIndex = 0;

                if (frameIndex != boid.FrameIndex)
                {
                    boid.Renderer.sharedMaterial = GetMaterialForFrame(frameIndex);
                    boid.FrameIndex = frameIndex;
                }
            }

            float flutter = 1f + Mathf.Sin(Time.time * WingFlutterSpeed + boid.Phase) * WingFlutterStrength;
            float scale = boid.Scale * GetBirdSizeMultiplier();
            boid.Transform.localScale = new Vector3(scale * flutter, scale, 1f);
        }

        private void RebuildSpatialGrid(float cellSize)
        {
            m_SpatialGrid.Clear();

            int simulatedCount = GetActiveSimulatedBoidCount();

            for (int i = 0; i < simulatedCount; i++)
            {
                Vector3 position = m_Boids[i].Position;
                int cellX = Mathf.FloorToInt(position.x / cellSize);
                int cellY = Mathf.FloorToInt(position.y / cellSize);
                int cellZ = Mathf.FloorToInt(position.z / cellSize);
                int key = GetCellKey(cellX, cellY, cellZ);

                if (!m_SpatialGrid.TryGetValue(key, out List<int> indices))
                {
                    indices = new List<int>(16);
                    m_SpatialGrid[key] = indices;
                }

                indices.Add(i);
            }
        }

        private static int GetCellKey(int x, int y, int z)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + z;
                return hash;
            }
        }

        // Followers trace their leader's path, so only simulated leaders need to check for obstacles.
        // The result is kept between checks so a crow keeps turning until the next check.
        private static void UpdateObstacleAvoidance(ref BoidData boid, float dt)
        {
            if (Settings.options == null || !Settings.options.AvoidObstacles)
            {
                boid.Avoidance = Vector3.zero;
                return;
            }

            boid.AvoidanceCheckTimer -= dt;
            if (boid.AvoidanceCheckTimer > 0f) return;

            boid.AvoidanceCheckTimer = ObstacleCheckIntervalSeconds;

            float clearance = Mathf.Clamp(Settings.options.ObstacleClearance, 5f, 100f);
            Vector3 avoidance = Vector3.zero;

            // Starting the ray above the crow also catches crows that have already dipped slightly below the surface.
            if (Physics.Raycast(boid.Position + Vector3.up * clearance, Vector3.down, out RaycastHit groundHit, clearance * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                float heightAboveGround = boid.Position.y - groundHit.point.y;
                float urgency = 1f - Mathf.Clamp01(heightAboveGround / clearance);
                avoidance += (groundHit.normal + Vector3.up).normalized * urgency;
            }

            float speed = boid.Velocity.magnitude;
            if (speed > 0.1f)
            {
                Vector3 direction = boid.Velocity / speed;
                float lookAhead = Mathf.Max(clearance * 2f, speed * ObstacleLookAheadSeconds);
                float probeRadius = clearance * 0.25f;

                if (Physics.SphereCast(boid.Position, probeRadius, direction, out RaycastHit hit, lookAhead, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    float urgency = 1f - Mathf.Clamp01((hit.distance - clearance) / Mathf.Max(0.01f, lookAhead - clearance));

                    // Slide along the surface and away from it, preferring to fly over obstacles rather than around them.
                    Vector3 slide = Vector3.ProjectOnPlane(direction, hit.normal);
                    Vector3 escape = hit.normal + Vector3.up * 0.5f;
                    if (slide.sqrMagnitude > 0.01f) escape += slide.normalized;

                    avoidance += escape.normalized * urgency;
                }
            }

            boid.Avoidance = Vector3.ClampMagnitude(avoidance, 1f);
        }

        private static Vector3 SteerToward(Vector3 desiredDirection, Vector3 currentVelocity)
        {
            if (desiredDirection.sqrMagnitude < 0.001f) return Vector3.zero;

            float maxSpeed = Settings.options != null ? Mathf.Max(0f, Settings.options.MaxSpeed) : 0f;
            if (maxSpeed <= 0f) return Vector3.zero;

            Vector3 desiredVelocity = desiredDirection.normalized * maxSpeed;
            return Vector3.ClampMagnitude(desiredVelocity - currentVelocity, MaxForce);
        }

        private Vector3 GetSphereContainmentForce(Vector3 boidPosition, Vector3 currentVelocity)
        {
            float radius = Settings.options != null ? Mathf.Clamp(Settings.options.ConfinementSphereRadius, 30f, 300f) : 95f;
            Vector3 fromCenter = boidPosition - m_Anchor;
            float distance = fromCenter.magnitude;
            if (distance <= radius || distance <= 0.001f) return Vector3.zero;

            Vector3 inward = -fromCenter / distance;
            float softMargin = Mathf.Max(12f, radius * 0.45f);
            float edgeT = Mathf.Clamp01((distance - radius) / softMargin);
            edgeT = Mathf.SmoothStep(0f, 1f, edgeT);

            Vector3 desired = inward;

            if (currentVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 outward = fromCenter / distance;
                float outwardSpeed = Vector3.Dot(currentVelocity, outward);

                if (outwardSpeed > 0f)
                {
                    Vector3 tangent = currentVelocity - outward * outwardSpeed;
                    if (tangent.sqrMagnitude > 0.01f)
                    {
                        tangent.Normalize();
                        desired = Vector3.Lerp(inward, tangent - outward * 0.35f, Mathf.Lerp(0.35f, 0.7f, edgeT)).normalized;
                    }
                }
            }

            return SteerToward(desired, currentVelocity) * Mathf.Lerp(0.35f, 1f, edgeT);
        }

        private Vector3 GetManeuverForce(BoidData boid, int boidIndex, Vector3 leaderCenter)
        {
            if (m_CurrentManeuverDirection.sqrMagnitude <= 0.001f) return Vector3.zero;

            float waveStrength = Settings.options != null ? Mathf.Clamp(Settings.options.ManeuverWaveStrength, 0f, 3f) : 0.75f;
            Vector3 direction = m_CurrentManeuverDirection;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            if (side.sqrMagnitude <= 0.001f) side = Vector3.right;
            side.Normalize();

            Vector3 radial = boid.Position - leaderCenter;
            if (radial.sqrMagnitude > 0.001f) radial.Normalize();
            else radial = Vector3.zero;

            float wave = Mathf.Sin(m_Elapsed * 0.73f + boidIndex * 0.37f + boid.Phase * 0.011f);
            float verticalWave = Mathf.Sin(m_Elapsed * 0.41f + boidIndex * 0.19f + boid.Phase * 0.017f);
            float slowPulse = Mathf.Sin(m_Elapsed * 0.19f + boid.Phase * 0.007f);

            Vector3 desired = direction * (0.9f + slowPulse * 0.2f);
            desired += side * wave * waveStrength * 0.55f;
            desired += Vector3.up * verticalWave * waveStrength * 0.22f;
            desired += radial * m_CurrentExpansionBias * (0.45f + Mathf.Abs(wave) * 0.35f);

            return SteerToward(desired, boid.Velocity);
        }

        private Vector3 GetNoiseForce(float phase)
        {
            float seed = phase * 0.0137f;
            float t = m_Elapsed * 0.17f;
            float x = Mathf.PerlinNoise(seed + 11.31f, t + 3.17f) * 2f - 1f;
            float y = (Mathf.PerlinNoise(seed + 23.73f, t * 0.83f + 19.91f) * 2f - 1f) * 0.28f;
            float z = Mathf.PerlinNoise(seed + 41.19f, t + 37.43f) * 2f - 1f;
            return new Vector3(x, y, z);
        }

        // The bird icons are drawn head up, so the quad's up axis is turned towards the flight direction as seen from the viewer.
        private static Quaternion GetDirectionalBillboardRotation(BoidData boid, Vector3 viewForward, Vector3 fallbackUp)
        {
            Vector3 screenDirection = Vector3.ProjectOnPlane(boid.Velocity, viewForward);

            // Flying almost straight towards or away from the viewer leaves no clear direction, so keep the current heading.
            if (screenDirection.sqrMagnitude <= boid.Velocity.sqrMagnitude * 0.01f || screenDirection.sqrMagnitude < 0.0001f)
            {
                screenDirection = Vector3.ProjectOnPlane(boid.Transform.up, viewForward);
                if (screenDirection.sqrMagnitude < 0.0001f) screenDirection = fallbackUp;
            }

            return Quaternion.LookRotation(viewForward, screenDirection);
        }

        private static Vector3 GetPlayerAvoidance(Vector3 boidPosition, Vector3 playerPosition)
        {
            Vector3 away = boidPosition - playerPosition;
            float distance = away.magnitude;
            if (distance >= PlayerAvoidDistance || distance <= 0.01f) return Vector3.zero;

            float strength = 1f - Mathf.Clamp01(distance / PlayerAvoidDistance);
            return away.normalized * strength * MaxForce;
        }

        private static Mesh GetSharedQuadMesh()
        {
            if (s_QuadMesh != null) return s_QuadMesh;

            GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            MeshFilter meshFilter = tempQuad.GetComponent<MeshFilter>();
            s_QuadMesh = meshFilter != null ? meshFilter.sharedMesh : null;

            Collider collider = tempQuad.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            UnityEngine.Object.Destroy(tempQuad);
            return s_QuadMesh;
        }

        private static Material[] CreateMaterials()
        {
            Texture2D[] textures = GetBirdTextures();
            if (textures == null || textures.Length == 0) return Array.Empty<Material>();

            Material[] materials = new Material[textures.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = CreateMaterial(textures[i]);
            }

            return materials;
        }

        private static Material CreateMaterial(Texture2D texture)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Unlit/Texture");

            Material material = new(shader);
            material.mainTexture = texture;
            material.color = new Color(0.03f, 0.03f, 0.03f, 0.92f);
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            return material;
        }

        private static Texture2D[] GetBirdTextures()
        {
            if (Settings.options != null && Settings.options.AnimatedCrows)
            {
                Texture2D[] animatedTextures = GetAnimatedBirdTextures();
                if (animatedTextures != null && animatedTextures.Length == s_AnimatedBirdTextureResourceNames.Length) return animatedTextures;

                Core.Warn("Animated crows are enabled, but one or more animated bird icon frames are missing.");
                return Array.Empty<Texture2D>();
            }

            return new[] { GetBirdTexture() };
        }

        private static Texture2D GetBirdTexture()
        {
            if (s_BirdTexture != null) return s_BirdTexture;

            s_BirdTexture = LoadEmbeddedTexture(BirdTextureResourceName, true);
            if (s_BirdTexture != null) return s_BirdTexture;

            s_BirdTexture = CreateFallbackBirdTexture();
            return s_BirdTexture;
        }

        private static Texture2D[] GetAnimatedBirdTextures()
        {
            s_AnimatedBirdTextures ??= new Texture2D[s_AnimatedBirdTextureResourceNames.Length];

            // Unity's == treats textures destroyed by a scene load as null, so those frames get reloaded.
            for (int i = 0; i < s_AnimatedBirdTextures.Length; i++)
            {
                if (s_AnimatedBirdTextures[i] != null) continue;

                s_AnimatedBirdTextures[i] = LoadEmbeddedTexture(s_AnimatedBirdTextureResourceNames[i], false);
                if (s_AnimatedBirdTextures[i] == null) return null;
            }

            return s_AnimatedBirdTextures;
        }

        private static Texture2D LoadEmbeddedTexture(string resourceName, bool logMissing)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using System.IO.Stream stream = GetEmbeddedResourceStream(assembly, resourceName);
            if (stream == null)
            {
                if (logMissing) Core.Warn($"Embedded bird texture not found: {resourceName}. Using procedural fallback.");
                return null;
            }

            byte[] bytes;
            using (System.IO.MemoryStream memoryStream = new())
            {
                stream.CopyTo(memoryStream);
                bytes = memoryStream.ToArray();
            }

            Texture2D texture = new(2, 2, TextureFormat.ARGB32, false);
            bool loaded = ImageConversion.LoadImage(texture, bytes, false);
            if (!loaded)
            {
                UnityEngine.Object.Destroy(texture);
                Core.Warn($"Failed to load embedded bird texture: {resourceName}.");
                return null;
            }

            texture.name = resourceName;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        private static System.IO.Stream GetEmbeddedResourceStream(Assembly assembly, string resourceName)
        {
            System.IO.Stream stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null) return stream;

            string suffix = resourceName;
            int iconsIndex = suffix.IndexOf(".Icons.", StringComparison.Ordinal);
            if (iconsIndex >= 0) suffix = suffix.Substring(iconsIndex);

            string[] resourceNames = assembly.GetManifestResourceNames();

            for (int i = 0; i < resourceNames.Length; i++)
            {
                if (!resourceNames[i].EndsWith(suffix, StringComparison.Ordinal)) continue;

                return assembly.GetManifestResourceStream(resourceNames[i]);
            }

            return null;
        }

        private static Texture2D CreateFallbackBirdTexture()
        {
            Texture2D texture = new(BirdTextureSize, BirdTextureSize, TextureFormat.ARGB32, false);
            Color32 clear = new(255, 255, 255, 0);
            Color32 fill = new(255, 255, 255, 255);
            Color32[] pixels = new Color32[BirdTextureSize * BirdTextureSize];

            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            for (int y = 0; y < BirdTextureSize; y++)
            {
                for (int x = 0; x < BirdTextureSize; x++)
                {
                    float nx = (x + 0.5f) / BirdTextureSize * 2f - 1f;
                    float ny = (y + 0.5f) / BirdTextureSize * 2f - 1f;
                    if (IsBirdSilhouettePixel(nx, ny)) pixels[y * BirdTextureSize + x] = fill;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.name = "Murmurations_Bird_Icon_Fallback";
            return texture;
        }

        private static bool IsBirdSilhouettePixel(float x, float y)
        {
            float bodyX = x * 2.4f;
            float bodyY = (y + 0.02f) * 4.2f;
            bool body = bodyX * bodyX + bodyY * bodyY <= 0.18f;

            float headX = (x - 0.16f) * 9f;
            float headY = (y + 0.04f) * 9f;
            bool head = headX * headX + headY * headY <= 0.08f;

            bool leftWing = DistanceToSegment(new Vector2(x, y), new Vector2(-0.08f, 0.06f), new Vector2(-0.92f, 0.28f)) <= 0.09f
                || DistanceToSegment(new Vector2(x, y), new Vector2(-0.26f, 0.02f), new Vector2(-0.78f, -0.04f)) <= 0.08f;

            bool rightWing = DistanceToSegment(new Vector2(x, y), new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.28f)) <= 0.09f
                || DistanceToSegment(new Vector2(x, y), new Vector2(0.26f, 0.02f), new Vector2(0.78f, -0.04f)) <= 0.08f;

            bool tail = DistanceToSegment(new Vector2(x, y), new Vector2(-0.04f, -0.06f), new Vector2(-0.18f, -0.42f)) <= 0.045f
                || DistanceToSegment(new Vector2(x, y), new Vector2(0.04f, -0.06f), new Vector2(0.18f, -0.42f)) <= 0.045f;

            return body || head || leftWing || rightWing || tail;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float denominator = Vector2.Dot(ab, ab);
            if (denominator <= 0.0001f) return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / denominator);
            Vector2 closest = a + ab * t;
            return Vector2.Distance(point, closest);
        }

        private void DestroyMaterials()
        {
            if (m_Materials == null) return;

            for (int i = 0; i < m_Materials.Length; i++)
            {
                if (m_Materials[i] != null) UnityEngine.Object.Destroy(m_Materials[i]);
            }

            m_Materials = null;
        }

        public void Destroy()
        {
            for (int i = 0; i < m_Boids.Count; i++)
            {
                if (m_Boids[i].Transform != null) UnityEngine.Object.Destroy(m_Boids[i].Transform.gameObject);
            }

            m_Boids.Clear();
            m_SpatialGrid.Clear();

            DestroyMaterials();
            m_Sound.Destroy();

            if (m_Root != null)
            {
                UnityEngine.Object.Destroy(m_Root);
                m_Root = null;
            }
        }
    }

    internal struct BoidData
    {
        public Transform Transform;
        public MeshRenderer Renderer;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Phase;
        public float Scale;
        public int FrameIndex;
        public bool IsSimulated;
        public int LeaderIndex;
        public Vector3 LeaderOffset;
        public Vector3[] TrailPositions;
        public int TrailHead;
        public int TrailCount;
        public Vector3 LastTrailPosition;
        public Vector3 Avoidance;
        public float AvoidanceCheckTimer;
    }
}