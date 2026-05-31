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

        private class PlayingSound
        {
            public WaveOutEvent WaveOut { get; set; }
            public AudioFileReader Reader { get; set; }
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
            if (string.IsNullOrEmpty(name) || name == "Padrão")
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
                    int deviceIndex = GetDeviceIndex(_selectedDeviceName);
                    
                    var waveOut = new WaveOutEvent
                    {
                        DeviceNumber = deviceIndex
                    };

                    var reader = new AudioFileReader(sound.FilePath)
                    {
                        Volume = sound.Volume * masterVolume
                    };

                    waveOut.Init(reader);

                    string soundId = sound.Id;
                    waveOut.PlaybackStopped += (s, e) =>
                    {
                        lock (_lock)
                        {
                            PlayingSound active;
                            if (_activeSounds.TryGetValue(soundId, out active))
                            {
                                active.Reader.Dispose();
                                active.WaveOut.Dispose();
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
                        WaveOut = waveOut,
                        Reader = reader
                    };

                    _activeSounds[sound.Id] = playingState;

                    waveOut.Play();
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
                    if (active.WaveOut.PlaybackState == PlaybackState.Playing)
                    {
                        active.WaveOut.Pause();
                    }
                    else if (active.WaveOut.PlaybackState == PlaybackState.Paused)
                    {
                        active.WaveOut.Play();
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
                    active.WaveOut.Stop();
                }
            }
        }

        // Para todos os áudios ativos
        public static void StopAll()
        {
            lock (_lock)
            {
                // Fazemos uma cópia das chaves para evitar modificação durante iteração
                var keys = new List<string>(_activeSounds.Keys);
                foreach (var key in keys)
                {
                    PlayingSound active;
                    if (_activeSounds.TryGetValue(key, out active))
                    {
                        active.WaveOut.Stop();
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
                    active.Reader.Volume = individualVolume * masterVolume;
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
                    return active.WaveOut.PlaybackState == PlaybackState.Playing;
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
