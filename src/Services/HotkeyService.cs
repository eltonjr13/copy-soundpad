using System;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace SoundDeck.Services
{
    public static class HotkeyService
    {
        // Importações da API Win32
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Constantes dos Modificadores Win32
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        /// <summary>
        /// Registra um atalho de teclado global.
        /// </summary>
        public static bool Register(IntPtr hWnd, int id, string hotkeyText)
        {
            if (string.IsNullOrEmpty(hotkeyText)) return false;

            try
            {
                // Remove qualquer registro prévio com o mesmo ID
                Unregister(hWnd, id);

                uint modifiers;
                uint vk;
                if (ParseHotkey(hotkeyText, out modifiers, out vk))
                {
                    // Inclui MOD_NOREPEAT para evitar disparos contínuos ao segurar a tecla
                    bool success = RegisterHotKey(hWnd, id, modifiers | MOD_NOREPEAT, vk);
                    if (!success)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("Falha ao registrar hotkey '{0}' com ID {1}. CodErro Win32: {2}", hotkeyText, id, Marshal.GetLastWin32Error()));
                    }
                    return success;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Erro no registro de hotkey: {0}", ex.Message));
            }
            return false;
        }

        /// <summary>
        /// Cancela o registro de um atalho global.
        /// </summary>
        public static bool Unregister(IntPtr hWnd, int id)
        {
            try
            {
                return UnregisterHotKey(hWnd, id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Erro ao desregistrar hotkey ID {0}: {1}", id, ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Converte uma string no formato "Ctrl+Shift+A" para modificadores e virtual key.
        /// </summary>
        public static bool ParseHotkey(string hotkeyText, out uint modifiers, out uint vk)
        {
            modifiers = 0;
            vk = 0;

            if (string.IsNullOrEmpty(hotkeyText)) return false;

            string[] parts = hotkeyText.Split('+');
            string keyName = string.Empty;

            foreach (var part in parts)
            {
                string p = part.Trim().ToLower();
                if (p == "ctrl" || p == "control")
                {
                    modifiers |= MOD_CONTROL;
                }
                else if (p == "alt")
                {
                    modifiers |= MOD_ALT;
                }
                else if (p == "shift")
                {
                    modifiers |= MOD_SHIFT;
                }
                else if (p == "win" || p == "windows")
                {
                    modifiers |= MOD_WIN;
                }
                else
                {
                    keyName = part.Trim();
                }
            }

            if (string.IsNullOrEmpty(keyName)) return false;

            // Converter a string da tecla em enum Key do WPF
            Key wpfKey;
            if (Enum.TryParse(keyName, true, out wpfKey))
            {
                // Converte Key do WPF para o Virtual Key do Win32
                vk = (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
                return vk != 0;
            }

            // Mapeamentos manuais para caracteres especiais comuns se necessário
            if (keyName.Length == 1)
            {
                char c = char.ToUpper(keyName[0]);
                if (c >= 'A' && c <= 'Z')
                {
                    vk = (uint)c;
                    return true;
                }
                if (c >= '0' && c <= '9')
                {
                    vk = (uint)c;
                    return true;
                }
            }

            return false;
        }
    }
}
