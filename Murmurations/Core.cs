[assembly: MelonInfo(typeof(Murmurations.Core), "Murmurations", "1.0.1", "EtherSystem", null)]
[assembly: MelonGame("Hinterland", "TheLongDark")]
[assembly: MelonOptionalDependencies("AudioManager")]

namespace Murmurations
{
    public class Core : MelonMod
    {
        public static Core Instance { get; private set; }

        public override void OnInitializeMelon()
        {
            Instance = this;
            Settings.OnLoad();
            RegisterConsoleCommands();

            Log("Initialized.", false);
        }

        public override void OnUpdate()
        {
            MurmurationManager.Update();
        }

        private static void RegisterConsoleCommands()
        {
            uConsole.RegisterCommand("murmuration", new Action(() => MurmurationManager.ForceSpawn("console command")));
            uConsole.RegisterCommand("murmuration_spawn", new Action(() => MurmurationManager.ForceSpawn("console command")));
            uConsole.RegisterCommand("murmuration_clear", new Action(MurmurationManager.Despawn));
            uConsole.RegisterCommand("murmuration_status", new Action(MurmurationManager.LogStatus));
        }

        internal static void Log(string message, bool onlyWhenDebugEnabled = true)
        {
            if (onlyWhenDebugEnabled && (Settings.options == null || !Settings.options.IsLogging)) return;

            Instance?.LoggerInstance.Msg(message);
        }

        internal static void Warn(string message, bool onlyWhenDebugEnabled = true)
        {
            if (onlyWhenDebugEnabled && (Settings.options == null || !Settings.options.IsLogging)) return;

            Instance?.LoggerInstance.Warning(message);
        }
    }
}