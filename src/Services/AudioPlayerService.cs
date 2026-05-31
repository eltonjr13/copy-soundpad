using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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
        private static string _selectedMicName = "Nenhum";

        // Dispositivo de Transmissão (Principal)
        private static WaveOutEvent _transmissionOut;
        private static MixingSampleProvider _transmissionMixer;
        private static int _transmissionDeviceIndex = -2;

        // Dispositivo de Monitoramento (Secundário)
        private static WaveOutEvent _monitorOut;
        private static MixingSampleProvider _monitorMixer;
        private static int _monitorDeviceIndex = -2;

        // Variáveis para o Passthrough do Microfone
        private static WaveInEvent _micIn;
        private static BufferedWaveProvider _micBuffer;
        private static ISampleProvider _micSampleProvider;

        private class PlayingSound
        {
            public AudioFileReader ReaderPrimary { get; set; }
            public ISampleProvider MixerInputPrimary { get; set; }
            public AudioFileReader ReaderMonitor { get; set; }
            public ISampleProvider MixerInputMonitor { get; set; }
        }

        private static void LogDebug(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio_log.txt");
                File.AppendAllText(logPath, string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}\r\n", DateTime.Now, message));
            }
            catch {}
        }

        private class CallbackSampleProvider : ISampleProvider
        {
            private readonly ISampleProvider _source;
            private readonly Action _onEnded;
            private bool _ended;

            public CallbackSampleProvider(ISampleProvider source, Action onEnded)
            {
                _source = source;
                _onEnded = onEnded;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                if (_ended)
                {
                    return 0;
                }

                int read = _source.Read(buffer, offset, count);
                if (read == 0)
                {
                    _ended = true;
                    if (_onEnded != null)
                    {
                        _onEnded();
                    }
                }
                return read;
            }

            public WaveFormat WaveFormat
            {
                get { return _source.WaveFormat; }
            }
        }

        public static float MasterVolume
        {
            get { return _masterVolume; }
            set
            {
                lock (_lock)
                {
                    _masterVolume = value;
                    LogDebug("Volume Master atualizado para: " + value);
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
                    LogDebug("Dispositivo de Transmissão selecionado: " + value);
                    EnsureTransmissionDevice();
                }
                UpdateMicPassthrough();
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
                    LogDebug("Dispositivo de Monitoramento selecionado: " + value);
                    EnsureMonitorDevice();
                }
            }
        }

        public static string SelectedMicName
        {
            get { return _selectedMicName; }
            set
            {
                lock (_lock)
                {
                    _selectedMicName = value;
                    LogDebug("Microfone selecionado: " + value);
                }
                UpdateMicPassthrough();
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
                LogDebug("Erro ao listar dispositivos de saída: " + ex.Message);
            }
            return devices;
        }

        // Obtém a lista de nomes dos microfones disponíveis
        public static List<string> GetInputDevices()
        {
            var mics = new List<string> { "Nenhum" };
            try
            {
                int count = WaveIn.DeviceCount;
                for (int i = 0; i < count; i++)
                {
                    var caps = WaveIn.GetCapabilities(i);
                    mics.Add(caps.ProductName);
                }
            }
            catch (Exception ex)
            {
                LogDebug("Erro ao listar microfones: " + ex.Message);
            }
            return mics;
        }

        // Obtém o índice do dispositivo de saída pelo nome
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
                LogDebug("Erro ao buscar índice do dispositivo '" + name + "': " + ex.Message);
            }

            return -1;
        }

        // Obtém o índice do microfone pelo nome
        private static int GetMicIndex(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "Nenhum")
            {
                return -1;
            }

            try
            {
                int count = WaveIn.DeviceCount;
                for (int i = 0; i < count; i++)
                {
                    if (WaveIn.GetCapabilities(i).ProductName == name)
                    {
                        return i;
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug("Erro ao buscar índice do microfone '" + name + "': " + ex.Message);
            }

            return -1;
        }

        private static void EnsureTransmissionDevice()
        {
            int outputIndex = GetDeviceIndex(_selectedDeviceName);
            LogDebug(string.Format("EnsureTransmissionDevice: Selecionado={0}, Index={1}", _selectedDeviceName, outputIndex));

            if (_transmissionMixer == null)
            {
                var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
                _transmissionMixer = new MixingSampleProvider(format);
                _transmissionMixer.ReadFully = true;
                LogDebug("Mixer de Transmissão criado.");
            }

            if (_transmissionOut != null && _transmissionDeviceIndex == outputIndex)
            {
                LogDebug("EnsureTransmissionDevice: Player já está inicializado no dispositivo correto.");
                return;
            }

            if (_transmissionOut != null)
            {
                try
                {
                    LogDebug("EnsureTransmissionDevice: Parando player anterior.");
                    _transmissionOut.Stop();
                }
                catch (Exception ex) { LogDebug("Erro ao parar _transmissionOut: " + ex.Message); }
                try { _transmissionOut.Dispose(); } catch {}
                _transmissionOut = null;
            }

            try
            {
                LogDebug(string.Format("EnsureTransmissionDevice: Inicializando WaveOutEvent no index {0}", outputIndex));
                _transmissionOut = new WaveOutEvent
                {
                    DeviceNumber = outputIndex,
                    DesiredLatency = 100
                };
                
                // Monitoramento de paradas inesperadas
                _transmissionOut.PlaybackStopped += (s, e) =>
                {
                    if (e.Exception != null)
                    {
                        LogDebug("CRÍTICO: _transmissionOut parou com erro: " + e.Exception.Message);
                    }
                    else
                    {
                        LogDebug("_transmissionOut parou normalmente.");
                    }
                };

                _transmissionOut.Init(_transmissionMixer);
                _transmissionOut.Play();
                _transmissionDeviceIndex = outputIndex;
                LogDebug("EnsureTransmissionDevice: Player iniciado com sucesso.");
            }
            catch (Exception ex)
            {
                LogDebug("CRÍTICO: Erro ao iniciar dispositivo de transmissão: " + ex.Message);
            }
        }

        private static void EnsureMonitorDevice()
        {
            int monitorIndex = GetDeviceIndex(_selectedMonitorName);
            LogDebug(string.Format("EnsureMonitorDevice: Selecionado={0}, Index={1}", _selectedMonitorName, monitorIndex));

            if (string.IsNullOrEmpty(_selectedMonitorName) || _selectedMonitorName.Equals("Nenhum", StringComparison.OrdinalIgnoreCase) || monitorIndex < -1)
            {
                if (_monitorOut != null)
                {
                    try
                    {
                        LogDebug("EnsureMonitorDevice: Desativando monitoramento.");
                        _monitorOut.Stop();
                    }
                    catch (Exception ex) { LogDebug("Erro ao parar _monitorOut: " + ex.Message); }
                    try { _monitorOut.Dispose(); } catch {}
                    _monitorOut = null;
                }
                _monitorDeviceIndex = -2;
                return;
            }

            if (_monitorMixer == null)
            {
                var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
                _monitorMixer = new MixingSampleProvider(format);
                _monitorMixer.ReadFully = true;
                LogDebug("Mixer de Monitoramento criado.");
            }

            if (_monitorOut != null && _monitorDeviceIndex == monitorIndex)
            {
                LogDebug("EnsureMonitorDevice: Player de monitoramento já inicializado no dispositivo correto.");
                return;
            }

            if (_monitorOut != null)
            {
                try
                {
                    LogDebug("EnsureMonitorDevice: Parando player de monitoramento anterior.");
                    _monitorOut.Stop();
                }
                catch (Exception ex) { LogDebug("Erro ao parar _monitorOut: " + ex.Message); }
                try { _monitorOut.Dispose(); } catch {}
                _monitorOut = null;
            }

            try
            {
                LogDebug(string.Format("EnsureMonitorDevice: Inicializando WaveOutEvent no index {0}", monitorIndex));
                _monitorOut = new WaveOutEvent
                {
                    DeviceNumber = monitorIndex,
                    DesiredLatency = 100
                };
                
                _monitorOut.PlaybackStopped += (s, e) =>
                {
                    if (e.Exception != null)
                    {
                        LogDebug("CRÍTICO: _monitorOut parou com erro: " + e.Exception.Message);
                    }
                };

                _monitorOut.Init(_monitorMixer);
                _monitorOut.Play();
                _monitorDeviceIndex = monitorIndex;
                LogDebug("EnsureMonitorDevice: Player de monitoramento iniciado com sucesso.");
            }
            catch (Exception ex)
            {
                LogDebug("CRÍTICO: Erro ao iniciar dispositivo de monitoramento: " + ex.Message);
            }
        }

        private static ISampleProvider PrepareSampleProvider(ISampleProvider source, WaveFormat targetFormat)
        {
            ISampleProvider current = source;

            if (current.WaveFormat.SampleRate != targetFormat.SampleRate)
            {
                LogDebug(string.Format("PrepareSampleProvider: Resampling de {0}Hz para {1}Hz", current.WaveFormat.SampleRate, targetFormat.SampleRate));
                current = new WdlResamplingSampleProvider(current, targetFormat.SampleRate);
            }

            if (current.WaveFormat.Channels != targetFormat.Channels)
            {
                LogDebug(string.Format("PrepareSampleProvider: Convertendo canais de {0} para {1}", current.WaveFormat.Channels, targetFormat.Channels));
                if (current.WaveFormat.Channels == 1 && targetFormat.Channels == 2)
                {
                    current = new MonoToStereoSampleProvider(current);
                }
            }

            return current;
        }

        // Atualiza o redirecionamento do microfone para o canal de transmissão
        public static void UpdateMicPassthrough()
        {
            lock (_lock)
            {
                LogDebug("UpdateMicPassthrough: Iniciando atualização.");
                StopMicPassthrough();

                if (string.IsNullOrEmpty(_selectedMicName) || _selectedMicName.Equals("Nenhum", StringComparison.OrdinalIgnoreCase))
                {
                    LogDebug("UpdateMicPassthrough: Nenhum microfone selecionado.");
                    return;
                }

                try
                {
                    int micIndex = GetMicIndex(_selectedMicName);
                    LogDebug(string.Format("UpdateMicPassthrough: MicName={0}, Index={1}", _selectedMicName, micIndex));
                    
                    if (micIndex >= 0)
                    {
                        EnsureTransmissionDevice();

                        _micIn = new WaveInEvent
                        {
                            DeviceNumber = micIndex,
                            BufferMilliseconds = 50, // Latência baixa e estável (50ms)
                            WaveFormat = new WaveFormat(44100, 16, 1) // Qualidade CD (44.1 kHz, 16-bit, Mono)
                        };

                        _micBuffer = new BufferedWaveProvider(_micIn.WaveFormat)
                        {
                            DiscardOnBufferOverflow = true
                        };

                        ISampleProvider floatSource = _micBuffer.ToSampleProvider();
                        ISampleProvider stereoSource = new MonoToStereoSampleProvider(floatSource);

                        _micSampleProvider = PrepareSampleProvider(stereoSource, _transmissionMixer.WaveFormat);

                        _micIn.DataAvailable += (sender, e) =>
                        {
                            var buffer = _micBuffer;
                            if (buffer != null)
                            {
                                buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                            }
                        };

                        LogDebug("UpdateMicPassthrough: Adicionando microfone ao mixer de transmissão.");
                        _transmissionMixer.AddMixerInput(_micSampleProvider);
                        
                        LogDebug("UpdateMicPassthrough: Iniciando gravação do microfone.");
                        _micIn.StartRecording();
                        LogDebug("UpdateMicPassthrough: Gravação iniciada.");
                    }
                    else
                    {
                        LogDebug("UpdateMicPassthrough: Index do microfone é menor que zero.");
                    }
                }
                catch (Exception ex)
                {
                    LogDebug("CRÍTICO: Erro ao iniciar passthrough de microfone: " + ex.Message);
                }
            }
        }

        // Para o passthrough de microfone
        public static void StopMicPassthrough()
        {
            lock (_lock)
            {
                try
                {
                    if (_micIn != null)
                    {
                        LogDebug("StopMicPassthrough: Parando e descartando WaveInEvent.");
                        _micIn.StopRecording();
                        _micIn.Dispose();
                        _micIn = null;
                    }

                    if (_micSampleProvider != null && _transmissionMixer != null)
                    {
                        LogDebug("StopMicPassthrough: Removendo microfone do mixer.");
                        _transmissionMixer.RemoveMixerInput(_micSampleProvider);
                        _micSampleProvider = null;
                    }

                    _micBuffer = null;
                    LogDebug("StopMicPassthrough: Parado com sucesso.");
                }
                catch (Exception ex)
                {
                    LogDebug("Erro ao parar passthrough de microfone: " + ex.Message);
                }
            }
        }

        // Toca um áudio
        public static void Play(SoundItem sound, float masterVolume, Action onPlaybackFinished = null)
        {
            if (sound == null || string.IsNullOrEmpty(sound.FilePath) || !File.Exists(sound.FilePath))
            {
                throw new FileNotFoundException("Arquivo de som não encontrado.", sound != null ? sound.FilePath : "");
            }

            LogDebug("Play: Solicitado som " + sound.Name + " (" + sound.FilePath + ")");
            Stop(sound.Id); // Para o som se ele já estiver tocando

            lock (_lock)
            {
                try
                {
                    EnsureTransmissionDevice();

                    var readerPrimary = new AudioFileReader(sound.FilePath)
                    {
                        Volume = sound.Volume * masterVolume
                    };

                    string soundId = sound.Id;

                    Action primaryFinished = new Action(() =>
                    {
                        lock (_lock)
                        {
                            LogDebug("Playback de som finalizado no canal Primário: " + soundId);
                            PlayingSound active;
                            if (_activeSounds.TryGetValue(soundId, out active))
                            {
                                if (active.ReaderPrimary != null)
                                {
                                    active.ReaderPrimary.Dispose();
                                    active.ReaderPrimary = null;
                                }
                                active.MixerInputPrimary = null;

                                if (active.ReaderMonitor == null)
                                {
                                    _activeSounds.Remove(soundId);
                                    LogDebug("Som removido completamente dos ativos (Primário encerrou por último/único).");
                                    if (onPlaybackFinished != null)
                                    {
                                        onPlaybackFinished();
                                    }
                                }
                            }
                        }
                    });

                    ISampleProvider primarySource = PrepareSampleProvider(readerPrimary, _transmissionMixer.WaveFormat);
                    var primaryInput = new CallbackSampleProvider(primarySource, primaryFinished);

                    AudioFileReader readerMonitor = null;
                    ISampleProvider monitorInput = null;

                    EnsureMonitorDevice();
                    int primaryDeviceIndex = GetDeviceIndex(_selectedDeviceName);
                    int monitorDeviceIndex = GetDeviceIndex(_selectedMonitorName);

                    if (_monitorOut != null && monitorDeviceIndex != primaryDeviceIndex)
                    {
                        LogDebug("Play: Configurando canal de Monitoramento para o som.");
                        readerMonitor = new AudioFileReader(sound.FilePath)
                        {
                            Volume = sound.Volume * masterVolume
                        };

                        Action monitorFinished = new Action(() =>
                        {
                            lock (_lock)
                            {
                                LogDebug("Playback de som finalizado no canal Monitor: " + soundId);
                                PlayingSound active;
                                if (_activeSounds.TryGetValue(soundId, out active))
                                {
                                    if (active.ReaderMonitor != null)
                                    {
                                        active.ReaderMonitor.Dispose();
                                        active.ReaderMonitor = null;
                                    }
                                    active.MixerInputMonitor = null;

                                    if (active.ReaderPrimary == null)
                                    {
                                        _activeSounds.Remove(soundId);
                                        LogDebug("Som removido completamente dos ativos (Monitor encerrou por último).");
                                        if (onPlaybackFinished != null)
                                        {
                                            onPlaybackFinished();
                                        }
                                    }
                                }
                            }
                        });

                        ISampleProvider monitorSource = PrepareSampleProvider(readerMonitor, _monitorMixer.WaveFormat);
                        monitorInput = new CallbackSampleProvider(monitorSource, monitorFinished);
                    }

                    var playingState = new PlayingSound
                    {
                        ReaderPrimary = readerPrimary,
                        MixerInputPrimary = primaryInput,
                        ReaderMonitor = readerMonitor,
                        MixerInputMonitor = monitorInput
                    };

                    _activeSounds[sound.Id] = playingState;

                    LogDebug("Play: Adicionando som ao mixer de transmissão.");
                    _transmissionMixer.AddMixerInput(primaryInput);
                    if (monitorInput != null && _monitorMixer != null)
                    {
                        LogDebug("Play: Adicionando som ao mixer de monitoramento.");
                        _monitorMixer.AddMixerInput(monitorInput);
                    }
                }
                catch (Exception ex)
                {
                    LogDebug("CRÍTICO: Erro ao reproduzir áudio: " + ex.Message);
                    throw;
                }
            }
        }

        // Pausa um áudio
        public static void Pause(string soundId)
        {
            // O Soundboard profissional usa apenas Play e Stop para reprodução instantânea
        }

        // Para um áudio
        public static void Stop(string soundId)
        {
            lock (_lock)
            {
                PlayingSound active;
                if (_activeSounds.TryGetValue(soundId, out active))
                {
                    LogDebug("Stop: Forçando parada do som: " + soundId);
                    if (active.MixerInputPrimary != null)
                    {
                        _transmissionMixer.RemoveMixerInput(active.MixerInputPrimary);
                    }
                    if (active.MixerInputMonitor != null && _monitorMixer != null)
                    {
                        _monitorMixer.RemoveMixerInput(active.MixerInputMonitor);
                    }

                    if (active.ReaderPrimary != null)
                    {
                        active.ReaderPrimary.Dispose();
                    }
                    if (active.ReaderMonitor != null)
                    {
                        active.ReaderMonitor.Dispose();
                    }

                    _activeSounds.Remove(soundId);
                }
            }
        }

        // Para todos os áudios ativos
        public static void StopAll()
        {
            lock (_lock)
            {
                LogDebug("StopAll: Parando todos os sons.");
                var keys = new List<string>(_activeSounds.Keys);
                foreach (var key in keys)
                {
                    Stop(key);
                }

                if (_transmissionOut != null)
                {
                    try { _transmissionOut.Stop(); } catch {}
                    try { _transmissionOut.Dispose(); } catch {}
                    _transmissionOut = null;
                }
                _transmissionDeviceIndex = -2;

                if (_monitorOut != null)
                {
                    try { _monitorOut.Stop(); } catch {}
                    try { _monitorOut.Dispose(); } catch {}
                    _monitorOut = null;
                }
                _monitorDeviceIndex = -2;
            }
        }

        // Para todos os áudios e também desliga o microfone (usado no encerramento)
        public static void Shutdown()
        {
            LogDebug("Shutdown: Solicitado desligamento total.");
            StopAll();
            StopMicPassthrough();
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
                return _activeSounds.ContainsKey(soundId);
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
