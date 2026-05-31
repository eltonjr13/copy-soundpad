using System;

namespace SoundDeck
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                App app = new App();
                app.Run();
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText("crash_log.txt", ex.ToString());
            }
        }
    }
}
