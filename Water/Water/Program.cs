using System;

namespace Water
{
#if WINDOWS || XBOX
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (var game = new Water())
            {
                game.Run();
            }
        }
    }
#endif
}

