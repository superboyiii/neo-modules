using Microsoft.Extensions.Configuration;

namespace RestServer
{
    internal class Settings
    {
        #region Settings

        public uint Network { get; init; }
        public string BindAddress { get; init; }
        public uint Port { get; init; }

        #endregion

        public static Settings Default { get; private set; }

        public Settings(IConfigurationSection section)
        {
            Network = section.GetValue("Network", 5195086u);
            BindAddress = section.GetValue("BindAddress", "127.0.0.1");
            Port = section.GetValue("Port", 10335u);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
