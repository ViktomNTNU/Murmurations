using System.ComponentModel;
using Description = ModSettings.DescriptionAttribute;

namespace Murmurations
{
    internal enum MurmurationBehaviorProfile
    {
        Dynamic,
        Compact,
        Spread,
        Fluid,
        Aggressive,
        Chaotic
    }

    internal enum CrowAnimationStyle
    {
        NoAnimations,
        Static,
        Directional
    }

    internal class MurmurationsModSettings : JsonModSettings
    {
        [Section("General")]

        [Name("Enable murmurations")]
        [Description("Enables distant visual crow murmurations.")]
        public bool EnableMurmurations = true;

        [Name("Auto spawn")]
        [Description("Allows murmurations to appear naturally while the player is outside.")]
        public bool AutoSpawn = false;

        [Name("Show Spawn settings")]
        [Description("Show natural spawn tuning settings.")]
        public bool ShowSpawnSettings = false;

        [Name("Maximum active murmurations")]
        [Description("Maximum number of murmurations that can exist at the same time. Default: 1.")]
        [Slider(1, 5, 5, NumberFormat = "{0:0}")]
        public int MaxActiveMurmurations = 1;

        [Name("Spawn chance")]
        [Description("Chance per natural spawn roll to spawn a murmuration while conditions are valid. Default: 5%.")]
        [Slider(0f, 100f, 1001, NumberFormat = "{0:0.0}%")]
        public float SpawnChancePerRoll = 5f;

        [Name("Minimum spawn roll delay")]
        [Description("Minimum in-game hours between natural spawn rolls. Default: 1 hour.")]
        [Slider(0.1f, 72f, 720, NumberFormat = "{0:0.##}h")]
        public float MinSpawnRollDelayHours = 1f;

        [Name("Maximum spawn roll delay")]
        [Description("Maximum in-game hours between natural spawn rolls. Default: 2 hours.")]
        [Slider(0.1f, 168f, 1680, NumberFormat = "{0:0.##}h")]
        public float MaxSpawnRollDelayHours = 2f;

        [Name("Respawn cooldown")]
        [Description("In-game hours after a successful spawn before another natural spawn can happen. Default: 24 hours.")]
        [Slider(0f, 720f, 721, NumberFormat = "{0:0.##}h")]
        public float RespawnCooldownHours = 24f;

        [Name("Minimum duration")]
        [Description("Minimum in-game hours before a murmuration naturally fades out. Default: 1.5 hours.")]
        [Slider(0.1f, 24f, 240, NumberFormat = "{0:0.##}h")]
        public float MinDurationHours = 1.5f;

        [Name("Maximum duration")]
        [Description("Maximum in-game hours before a murmuration naturally fades out. Default: 3 hours.")]
        [Slider(0.1f, 48f, 480, NumberFormat = "{0:0.##}h")]
        public float MaxDurationHours = 3f;

        [Name("Minimum spawn distance")]
        [Description("Minimum horizontal distance from the player where the murmuration can spawn. Default: 140m.")]
        [Slider(40f, 800f, 761, NumberFormat = "{0:0}m")]
        public float MinSpawnDistance = 140f;

        [Name("Maximum spawn distance")]
        [Description("Maximum horizontal distance from the player where the murmuration can spawn. Default: 240m.")]
        [Slider(40f, 1200f, 1161, NumberFormat = "{0:0}m")]
        public float MaxSpawnDistance = 240f;

        [Name("Minimum height above player")]
        [Description("Minimum height above the player where the murmuration can spawn. Default: 80m.")]
        [Slider(20f, 250f, 231, NumberFormat = "{0:0}m")]
        public float MinSpawnHeightAbovePlayer = 80f;

        [Name("Maximum height above player")]
        [Description("Maximum height above the player where the murmuration can spawn. Default: 350m.")]
        [Slider(20f, 350f, 331, NumberFormat = "{0:0}m")]
        public float MaxSpawnHeightAbovePlayer = 350f;

        [Name("Minimum height above terrain")]
        [Description("Minimum height above terrain for the murmuration volume. Default: 80m.")]
        [Slider(20f, 250f, 231, NumberFormat = "{0:0}m")]
        public float MinHeightAboveTerrain = 80f;

        [Name("Spawn validation attempts")]
        [Description("How many positions are tested before giving up on a spawn. Default: 12.")]
        [Slider(1, 64, 64, NumberFormat = "{0:0}")]
        public int SpawnValidationAttempts = 12;

        [Name("Show Spawn Conditions")]
        [Description("Show time and weather conditions used by natural spawns.")]
        public bool ShowSpawnConditions = false;

        [Name("Day only")]
        [Description("Only normal murmurations can spawn during the day. Aurora murmurations can still happen at night if enabled.")]
        public bool DayOnly = true;

        [Name("Allow aurora murmurations")]
        [Description("Allows aurora murmurations to spawn during an active aurora, including at night.")]
        public bool AllowAuroraMurmurations = true;

        [Name("Morning peak start")]
        [Description("Start hour for the morning spawn chance multiplier. Default: 6.")]
        [Slider(0f, 24f, 97, NumberFormat = "{0:0.0}h")]
        public float MorningPeakStartHour = 6f;

        [Name("Morning peak end")]
        [Description("End hour for the morning spawn chance multiplier. Default: 9.")]
        [Slider(0f, 24f, 97, NumberFormat = "{0:0.0}h")]
        public float MorningPeakEndHour = 9f;

        [Name("Evening peak start")]
        [Description("Start hour for the evening spawn chance multiplier. Default: 15.")]
        [Slider(0f, 24f, 97, NumberFormat = "{0:0.0}h")]
        public float EveningPeakStartHour = 15f;

        [Name("Evening peak end")]
        [Description("End hour for the evening spawn chance multiplier. Default: 19.")]
        [Slider(0f, 24f, 97, NumberFormat = "{0:0.0}h")]
        public float EveningPeakEndHour = 19f;

        [Name("Peak chance multiplier")]
        [Description("Spawn chance multiplier during morning and evening peaks. Default: 2.5x.")]
        [Slider(0f, 10f, 101, NumberFormat = "{0:0.0}x")]
        public float PeakChanceMultiplier = 2.5f;

        [Name("Night chance multiplier")]
        [Description("Spawn chance multiplier at night when night spawns are allowed. Default: 0.25x.")]
        [Slider(0f, 5f, 101, NumberFormat = "{0:0.00}x")]
        public float NightChanceMultiplier = 0.25f;

        [Name("Allow clear")]
        [Description("Allow murmurations during Clear and ClearAurora weather.")]
        public bool AllowWeatherClear = true;

        [Name("Allow partly cloudy")]
        [Description("Allow murmurations during PartlyCloudy weather.")]
        public bool AllowWeatherPartlyCloudy = true;

        [Name("Allow light fog")]
        [Description("Allow murmurations during LightFog weather.")]
        public bool AllowWeatherLightFog = true;

        [Name("Allow light snow")]
        [Description("Allow murmurations during LightSnow weather.")]
        public bool AllowWeatherLightSnow = true;

        [Name("Invalid weather fade out")]
        [Description("In-game hours used to fade out when the weather becomes invalid. Default: 0.05h.")]
        [Slider(0.01f, 3f, 300, NumberFormat = "{0:0.##}h")]
        public float InvalidWeatherFadeOutHours = 0.05f;

        [Name("Visible bird count")]
        [Description("Number of rendered birds per murmuration. Higher values look denser but cost more rendering cost. Default: 300.")]
        [Slider(20, 10000, 9981, NumberFormat = "{0:0}")]
        public int BoidCount = 300;

        [Name("Simulated bird count")]
        [Description("Number of birds using the full boid simulation. Extra visible birds follow simulated birds visually. Default: 100.")]
        [Slider(1, 500, 500, NumberFormat = "{0:0}")]
        public int SimulatedBoidCount = 100;

        [Name("Follower trail length")]
        [Description("Maximum distance behind each simulated leader used by follower birds. Default: 80m.")]
        [Slider(5f, 400f, 396, NumberFormat = "{0:0}m")]
        public float FollowerTrailLength = 80f;

        [Name("Max speed")]
        [Description("Maximum movement speed for simulated boids. 0 freezes movement. Default: 40.")]
        [Slider(0f, 50f, 501, NumberFormat = "{0:0.0}")]
        public float MaxSpeed = 40f;

        [Name("Confinement sphere radius")]
        [Description("Radius of the soft spherical volume keeping the simulated leaders near the murmuration spawn area. Default: 95m.")]
        [Slider(30f, 300f, 271, NumberFormat = "{0:0}m")]
        public float ConfinementSphereRadius = 95f;

        [Name("Confinement sphere strength")]
        [Description("Strength of the soft spherical confinement. 0 disables it. Default: 0.65.")]
        [Slider(0f, 3f, 301, NumberFormat = "{0:0.00}")]
        public float ConfinementSphereStrength = 0.65f;

        [Name("Avoid terrain and objects")]
        [Description("Crows look ahead and steer around terrain, trees and buildings instead of flying through them. Default: Enabled.")]
        public bool AvoidObstacles = true;

        [Name("Obstacle clearance")]
        [Description("How close crows can get to the ground and to objects before they steer away. Default: 25m.")]
        [Slider(5f, 100f, 96, NumberFormat = "{0:0}m")]
        public float ObstacleClearance = 25f;

        [Name("Maneuver variation strength")]
        [Description("Strength of slow behavior changes used to prevent stable repeated patterns. 0 disables it. Default: 0.9.")]
        [Slider(0f, 3f, 301, NumberFormat = "{0:0.00}")]
        public float ManeuverVariationStrength = 0.9f;

        [Name("Behavior profile")]
        [Description("Controls the overall flocking style. Dynamic cycles between all profiles automatically. Default: Dynamic.")]
        [Choice("Dynamic", "Compact", "Spread", "Fluid", "Aggressive", "Chaotic")]
        public MurmurationBehaviorProfile BehaviorProfile = MurmurationBehaviorProfile.Dynamic;

        [Name("Base alpha")]
        [Description("Maximum opacity for normal crow silhouettes. Default: 0.9.")]
        [Slider(0.05f, 1f, 96, NumberFormat = "{0:0.00}")]
        public float BaseAlpha = 0.9f;

        [Name("Bird size")]
        [Description("Size multiplier for the birds. 1.0 is about the size of the crows in the game, 0.2 is closer to real starlings. Default: 1.0.")]
        [Slider(0.1f, 2f, 39, NumberFormat = "{0:0.00}x")]
        public float BirdSize = 1f;

        [Name("Crow animation")]
        [Description("No animations uses a single bird icon. Static plays a three frames wing animation with every crow facing the same way. Directional plays the same animation and turns each crow towards the direction it is flying. Default: No animations.")]
        [Choice("No animations", "Static", "Directional")]
        public CrowAnimationStyle CrowAnimation = CrowAnimationStyle.NoAnimations;

        internal bool AnimatedCrows => CrowAnimation != CrowAnimationStyle.NoAnimations;
        internal bool DirectionalCrows => CrowAnimation == CrowAnimationStyle.Directional;

        [Section("Sound")]
        [Name("Flock sound")]
        [Description("Plays the sound of the flock from the birds nearest to you. It gets louder as the flock comes closer and fades out with distance. Requires the AudioManager mod. Default: Enabled.")]
        public bool FlockSound = true;

        [Name("Sound volume")]
        [Description("Volume of the flock sound when you are right next to the birds. Also follows the game's ambience volume. Default: 0.35.")]
        [Slider(0f, 1f, 101, NumberFormat = "{0:0.00}")]
        public float SoundVolume = 0.35f;

        [Name("Sound range")]
        [Description("Distance from the nearest birds at which the flock sound has faded to silence. Default: 300m.")]
        [Slider(100f, 1000f, 91, NumberFormat = "{0:0}m")]
        public float SoundRange = 300f;

        [Section("Advanced")]

        [Name("Fade in duration")]
        [Description("In-game hours used for the murmuration to fully appear. Default: 0.1h.")]
        [Slider(0f, 3f, 301, NumberFormat = "{0:0.##}h")]
        public float FadeInDurationHours = 0.1f;

        [Name("Fade out duration")]
        [Description("In-game hours used for the murmuration to disappear. Default: 0.1h.")]
        [Slider(0.01f, 3f, 300, NumberFormat = "{0:0.##}h")]
        public float FadeOutDurationHours = 0.1f;

        [Name("Spawn-in duration")]
        [Description("In-game hours used for all boids to be created after the murmuration starts. Default: 0.1h.")]
        [Slider(0f, 3f, 301, NumberFormat = "{0:0.##}h")]
        public float SpawnInDurationHours = 0.1f;

        [Name("Aurora green tint")]
        [Description("Strength of the subtle green aurora tint. Default: 0.35.")]
        [Slider(0f, 1f, 101, NumberFormat = "{0:0.00}")]
        public float AuroraGreenTintStrength = 0.35f;

        [Name("Neighbor radius")]
        [Description("Radius used by boids to align and gather with nearby boids. Default: 25m.")]
        [Slider(4f, 40f, 361, NumberFormat = "{0:0.0}m")]
        public float NeighborRadius = 25f;

        [Name("Separation radius")]
        [Description("Radius used by simulated boids to avoid crowding each other. Default: 15m.")]
        [Slider(1f, 20f, 191, NumberFormat = "{0:0.0}m")]
        public float SeparationRadius = 15f;

        [Name("Maneuver wave strength")]
        [Description("Strength of local wave offsets applied to simulated leaders during behavior changes. 0 disables it. Default: 0.75.")]
        [Slider(0f, 3f, 301, NumberFormat = "{0:0.00}")]
        public float ManeuverWaveStrength = 0.75f;

        [Name("Simulation FPS limit")]
        [Description("How many times per second the boid simulation updates. 0 freezes movement. Default: 25.")]
        [Slider(0f, 50f, 51, NumberFormat = "{0:0} FPS")]
        public float SimulationFPSLimit = 25f;

        [Name("Max neighbor checks")]
        [Description("Maximum number of nearby boids checked by each boid per simulation step. Lower values improve performance, especially at high boid counts. Default: 24.")]
        [Slider(8, 64, 57, NumberFormat = "{0:0}")]
        public int MaxNeighborChecks = 24;

        [Name("Transform update stride")]
        [Description("Updates only one out of N boid transforms per simulation step. 1 is smoothest, higher values are faster. Default: 1.")]
        [Slider(1, 4, 4, NumberFormat = "{0:0}")]
        public int TransformUpdateStride = 1;

        [Name("ML Logging")]
        [Description("Add logs for debugging in the ML console.")]
        public bool IsLogging = false;

        protected override void OnChange(FieldInfo field, object oldValue, object newValue)
        {
            RefreshVisibility();
        }

        protected override void OnConfirm()
        {
            base.OnConfirm();
            RefreshVisibility();
            MurmurationManager.RefreshSettings();
        }

        internal void RefreshVisibility()
        {
            SetFieldVisible(nameof(MaxActiveMurmurations), ShowSpawnSettings);
            SetFieldVisible(nameof(SpawnChancePerRoll), ShowSpawnSettings);
            SetFieldVisible(nameof(MinSpawnRollDelayHours), ShowSpawnSettings);
            SetFieldVisible(nameof(MaxSpawnRollDelayHours), ShowSpawnSettings);
            SetFieldVisible(nameof(RespawnCooldownHours), ShowSpawnSettings);
            SetFieldVisible(nameof(MinDurationHours), ShowSpawnSettings);
            SetFieldVisible(nameof(MaxDurationHours), ShowSpawnSettings);
            SetFieldVisible(nameof(MinSpawnDistance), ShowSpawnSettings);
            SetFieldVisible(nameof(MaxSpawnDistance), ShowSpawnSettings);
            SetFieldVisible(nameof(MinSpawnHeightAbovePlayer), ShowSpawnSettings);
            SetFieldVisible(nameof(MaxSpawnHeightAbovePlayer), ShowSpawnSettings);
            SetFieldVisible(nameof(MinHeightAboveTerrain), ShowSpawnSettings);
            SetFieldVisible(nameof(SpawnValidationAttempts), ShowSpawnSettings);

            SetFieldVisible(nameof(DayOnly), ShowSpawnConditions);
            SetFieldVisible(nameof(AllowAuroraMurmurations), ShowSpawnConditions);
            SetFieldVisible(nameof(MorningPeakStartHour), ShowSpawnConditions);
            SetFieldVisible(nameof(MorningPeakEndHour), ShowSpawnConditions);
            SetFieldVisible(nameof(EveningPeakStartHour), ShowSpawnConditions);
            SetFieldVisible(nameof(EveningPeakEndHour), ShowSpawnConditions);
            SetFieldVisible(nameof(PeakChanceMultiplier), ShowSpawnConditions);
            SetFieldVisible(nameof(NightChanceMultiplier), ShowSpawnConditions);
            SetFieldVisible(nameof(AllowWeatherClear), ShowSpawnConditions);
            SetFieldVisible(nameof(AllowWeatherPartlyCloudy), ShowSpawnConditions);
            SetFieldVisible(nameof(AllowWeatherLightFog), ShowSpawnConditions);
            SetFieldVisible(nameof(AllowWeatherLightSnow), ShowSpawnConditions);
            SetFieldVisible(nameof(InvalidWeatherFadeOutHours), ShowSpawnConditions);
        }
    }

    internal static class Settings
    {
        public static MurmurationsModSettings options;

        public static void OnLoad()
        {
            options = new MurmurationsModSettings();
            options.RefreshVisibility();
            options.AddToModSettings("Murmurations");
        }
    }
}