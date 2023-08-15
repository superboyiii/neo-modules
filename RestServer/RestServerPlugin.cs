using Microsoft.AspNetCore.Hosting;
using Neo;
using Neo.Plugins;

namespace RestServer
{
    public class RestServerPlugin : Plugin
    {
        #region Globals

        private NeoSystem _neosystem;
        private Settings _settings;

        #endregion

        public override string Name => "RestServer";
        public override string Description => "Enables REST Web Services for the node";

        #region Overrides

        protected override void Configure()
        {
            _settings = new Settings(GetConfiguration());
        }

        public override void Dispose()
        {

        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (_settings.Network != system.Settings.Network) return;
            var rest_ws = new WebHostBuilder()
                .UseKestrel()
                .UseUrls($"http://{_settings.BindAddress}:{_settings.Port}");
        }

        #endregion
    }
}
