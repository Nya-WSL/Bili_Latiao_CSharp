using Bili_Latiao_CSharp.Models;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace Bili_Latiao_CSharp.Services
{
    public static class ConfigService
    {
        private static readonly string AppFolder = Directory.GetCurrentDirectory();

        private static readonly string config_file = Path.Combine(AppFolder, "config.json");
        private static AppConfig? _current_config;

        public static AppConfig Load()
        {
            AppConfig config;
            if (!File.Exists(config_file))
            {
                config = new AppConfig();
                Save(config);
            }
            else
            {
                try
                {
                    string json = File.ReadAllText(config_file);
                    config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                catch
                {
                    config = new AppConfig();
                }
            }

            _current_config = config;
            config.PropertyChanged += Config_Changed;
            return config;
        }

        private static void Config_Changed(object? sender, PropertyChangedEventArgs e)
        {
            if (_current_config != null) Save(_current_config);
        }

        public static void Save(AppConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(config_file, json);
            }
            catch (Exception e)
            {
                LogService.Error($"[Config] 保存配置失败：{e.Message}");
            }
        }
    }
}
