using System;
using System.IO;
using Newtonsoft.Json;
using SoundDeck.Models;

namespace SoundDeck.Services
{
    public static class SettingsService
    {
        private static readonly string ConfigFileName = "sounddeck_config.json";
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

        public static SoundDeckConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var config = JsonConvert.DeserializeObject<SoundDeckConfig>(json);
                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                // Em um app real, poderíamos logar o erro. Retornamos padrão se falhar.
                System.Diagnostics.Debug.WriteLine("Erro ao carregar configurações: " + ex.Message);
            }

            // Se falhar ou não existir, cria uma configuração padrão
            var defaultConfig = new SoundDeckConfig();
            SaveConfig(defaultConfig);
            return defaultConfig;
        }

        public static void SaveConfig(SoundDeckConfig config)
        {
            try
            {
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Erro ao salvar configurações: " + ex.Message);
            }
        }
    }
}
