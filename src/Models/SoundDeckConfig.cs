using System;
using System.Collections.Generic;

namespace SoundDeck.Models
{
    public class SoundDeckConfig
    {
        public List<SoundItem> Sounds { get; set; }
        public List<Category> Categories { get; set; }
        public float MasterVolume { get; set; }
        public string SelectedDevice { get; set; }
        public string SelectedMonitor { get; set; }
        public string SelectedMic { get; set; }
        public bool MinimizeToTray { get; set; }

        public SoundDeckConfig()
        {
            Sounds = new List<SoundItem>();
            Categories = new List<Category>();
            MasterVolume = 0.8f;
            SelectedDevice = "Padrão";
            SelectedMonitor = "Nenhum";
            SelectedMic = "Nenhum";
            MinimizeToTray = true;

            // Categorias padrão sugeridas pelo usuário
            Categories.Add(new Category("Todos", "📁"));
            Categories.Add(new Category("Favoritos", "⭐"));
            Categories.Add(new Category("Memes", "😂"));
            Categories.Add(new Category("Efeitos", "⚡"));
            Categories.Add(new Category("Músicas", "🎵"));
            Categories.Add(new Category("Vozes", "🎙️"));
        }
    }
}
