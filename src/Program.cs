using System;

namespace SoundDeck
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            App app = new App();
            app.Run();
        }
    }
}
