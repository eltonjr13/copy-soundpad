using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using SoundDeck.Models;

namespace SoundDeck.Services
{
    public static class AudioPlayerService
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, PlayingSound> _activeSounds = new Dictionary<string, PlayingSound>();
        private static float _masterVolume = 0.8f;
        private static string _selectedDeviceName = "Padrão";
        private static string _selectedMonitorName = "Nenhum";

        private class PlayingSound
        {
            public WaveOutEvent WaveOutPrimary { get; set; }
            public AudioFileReader ReaderPrimary { get; set; }
            public WaveOutEvent WaveOutMonitor { get; set; }
            public AudioFileReader ReaderMonitor { get; set; }
        }

        public static float MasterVolume
        {
            get { return _masterVolume; }
            set
            {
                lock (_lock)
                {
                    _masterVolume = value;
                }
            }
        }

        public static string SelectedDeviceName
        {
            get { return _selectedDeviceName; }
            set
            {
                lock (_lock)
                {
                    _selectedDeviceName = value;
                }
            }
        }

        public static string SelectedMonitorName
        {
            get { return _selectedMonitorName; }
            set
            {
                lock (_lock)
                {
                    _selectedMonitorName = value;
                }
            }
        }

        // Obtém a lista de nomes dos dispositivos de saída disponíveis
        public static List<string> GetOutputDevices()
        {
            var devices = new List<string> { "Padrão" };
            try
            {
                int deviceCount = WaveOut.DeviceCount;
                for (int i = 0; i < deviceCount; i++)
                {
                    var caps = WaveOut.GetCapabilities(i);
                    devices.Add(caps.ProductName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Erro ao listar dispositivos: " + ex.Message);
            }
            return devices;
        }

        // Obtém o índice do dispositivo pelo nome
        private static int GetDeviceIndex(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "Padrão" || name == "Nenhum")
            {
                return -1; // Dispositivo padrão do Windows
            }

            try
            {
                int deviceCount = WaveOut.DeviceCount;
                for (int i = 0; i < deviceCount; i++)
                {
                    if (WaveOut.GetCapabilities(i).ProductName == name)
                    {
                        return i;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Erro ao buscar índice do dispositivo: " + ex.Message);
            }

            return -1;
        }

        // Toca um áudio
        public static void Play(SoundItem sound, float masterVolume, Action onPlaybackFinished = null)
        {
            if (sound == null || string.IsNullOrEmpty(sound.FilePath) || !File.Exists(sound.FilePath))
            {
                throw new FileNotFoundException("Arquivo de som não encontrado.", sound != null ? sound.FilePath : "");
            }

            Stop(sound.Id); // Para o som se ele já estiver tocando

            lock (_lock)
            {
                try
                {
                    // 1. Configurar Dispositivo de Transmissão Primário
                    int primaryIndex = GetDeviceIndex(_selectedDeviceName);
                    var waveOutPrimary = new WaveOutEvent
                    {
                        DeviceNumber = primaryIndex
                    };

                    var readerPrimary = new AudioFileReader(sound.FilePath)
                    {
                        Volume = sound.Volume * masterVolume
                    };

                    waveOutPrimary.Init(readerPrimary);

                    // 2. Configurar Dispositivo de Monitoramento Secundário (se houver)
                    WaveOutEvent waveOutMonitor = null;
                    AudioFileReader readerMonitor = null;

                    if (!string.IsNullOrEmpty(_selectedMonitorName) && !_selectedMonitorName.Equals("Nenhum", StringComparison.OrdinalIgnoreCase))
                    {
                        int monitorIndex = GetDeviceIndex(_selectedMonitorName);
                        // Apenas cria se o dispositivo for diferente do primário
                        if (monitorIndex != primaryIndex)
                        {
                            waveOutMonitor = new WaveOutEvent
                            {
                                DeviceNumber = monitorIndex
                            };

                            readerMonitor = new AudioFileReader(sound.FilePath)
                            {
                                Volume = sound.Volume * masterVolume
                            };

                            waveOutMonitor.Init(readerMonitor);
                        }
                    }

                    string soundId = sound.Id;
                    waveOutPrimary.PlaybackStopped += (s, e) =>
                    {
                        lock (_lock)
                        {
                            PlayingSound active;
                            if (_activeSounds.TryGetValue(soundId, out active))
                            {
                                if (active.ReaderPrimary != null) active.ReaderPrimary.Dispose();
                                if (active.WaveOutPrimary != null) active.WaveOutPrimary.Dispose();
                                if (active.ReaderMonitor != null) active.ReaderMonitor.Dispose();
                                if (active.WaveOutMonitor != null) active.WaveOutMonitor.Dispose();
                                _activeSounds.Remove(soundId);
                            }
                        }
                        if (onPlaybackFinished != null)
                        {
                            onPlaybackFinished.Invoke();
                        }
                    };

                    var playingState = new PlayingSound
                    {
                        WaveOutPrimary = waveOutPrimary,
                        ReaderPrimary = readerPrimary,
                        WaveOutMonitor = waveOutMonitor,
                        ReaderMonitor = readerMonitor
                    };

                    _activeSounds[sound.Id] = playingState;

                    waveOutPrimary.Play();
                    if (waveOutMonitor != null)
                    {
                        waveOutMonitor.Play();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Erro ao reproduzir áudio: " + ex.Message);
                    throw;
                }
            }
        }

        // Pausa um áudio
        public static void Pause(string soundId)
        {
            lock (_lock)
            {
                PlayingSound active;
                if (_activeSounds.TryGetValue(soundId, out active))
                {
                    if (active.WaveOutPrimary.PlaybackState == PlaybackState.Playing)
                    {
                        active.WaveOutPrimary.Pause();
                        if (active.WaveOutMonitor != null)
                        {
                            active.WaveOutMonitor.Pause();
                        }
                    }
                    else if (active.WaveOutPrimary.PlaybackState == PlaybackState.Paused)
                    {
                        active.WaveOutPrimary.Play();
                        if (active.WaveOutMonitor != null)
                        {
                            active.WaveOutMonitor.Play();
                        }
                    }
                }
            }
        }

        // Para um áudio
        public static void Stop(string soundId)
        {
            lock (_lock)
            {
                PlayingSound active;
                if (_activeSounds.TryGetValue(soundId, out active))
                {
                    active.WaveOutPrimary.Stop();
                    if (active.WaveOutMonitor != null)
                    {
                        active.WaveOutMonitor.Stop();
                    }
                }
            }
        }

        // Para todos os áudios ativos
        public static void StopAll()
        {
            lock (_lock)
            {
                var keys = new List<string>(_activeSounds.Keys);
                foreach (var key in keys)
                {
                    PlayingSound active;
                    if (_activeSounds.TryGetValue(key, out active))
                    {
                        active.WaveOutPrimary.Stop();
                        if (active.WaveOutMonitor != null)
                        {
                            active.WaveOutMonitor.Stop();
                        }
                    }
                }
            }
        }

        // Atualiza dinamicamente o volume de um som em reprodução
        public static void UpdateActiveVolume(string soundId, float individualVolume, float masterVolume)
        {
            lock (_lock)
            {
                PlayingSound active;
                if (_activeSounds.TryGetValue(soundId, out active))
                {
                    active.ReaderPrimary.Volume = individualVolume * masterVolume;
                    if (active.ReaderMonitor != null)
                    {
                        active.ReaderMonitor.Volume = individualVolume * masterVolume;
                    }
                }
            }
        }

        // Verifica se o som está tocando
        public static bool IsPlaying(string soundId)
        {
            lock (_lock)
            {
                PlayingSound active;
                if (_activeSounds.TryGetValue(soundId, out active))
                {
                    return active.WaveOutPrimary.PlaybackState == PlaybackState.Playing;
                }
                return false;
            }
        }

        // Tenta obter a duração de um arquivo de áudio de forma segura
        public static string GetDurationText(string filePath)
        {
            try
            {
                using (var reader = new AudioFileReader(filePath))
                {
                    TimeSpan duration = reader.TotalTime;
                    return string.Format("{0:D2}:{1:D2}", (int)duration.TotalMinutes, duration.Seconds);
                }
            }
            catch
            {
                return "00:00";
            }
        }
    }
}
