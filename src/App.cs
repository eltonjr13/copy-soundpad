using System;
using System.Windows;

namespace SoundDeck
{
    public class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Criar e exibir a janela principal
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
