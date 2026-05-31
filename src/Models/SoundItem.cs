using System;

namespace SoundDeck.Models
{
    public class SoundItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string FilePath { get; set; }
        public string DurationText { get; set; }
        public string CategoryName { get; set; }
        public float Volume { get; set; }
        public string Hotkey { get; set; }
        public bool IsFavorite { get; set; }
        public int HotkeyId { get; set; } // Identificador numérico para o RegisterHotKey

        public SoundItem()
        {
            Id = Guid.NewGuid().ToString();
            Volume = 1.0f;
            Hotkey = string.Empty;
        }
    }
}
