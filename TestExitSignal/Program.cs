using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestExitSignal
{

    internal class Dispo : IDisposable
    {
        public void Dispose()
        {
            Console.WriteLine("Resource disposed");
        }
    }
    class Program
    {
        private static readonly ManualResetEvent _quitEvent = new ManualResetEvent(false);

        static void Exit(object sender, EventArgs agrs)
        {
            while (true)
            {
                Console.WriteLine("This window will be closed soon...");
                Thread.Sleep(1000);
            }
        }
        static void Main(string[] args)
        {
            using (var dispo = new Dispo())
            {
                Hexagon.WinExitSignal exitSignal = new Hexagon.WinExitSignal(false, dispo);
                exitSignal.Exit += Exit;
                Console.CancelKeyPress += (sender, e) =>
                {
                    _quitEvent.Set();
                    e.Cancel = true;
                };
                Console.WriteLine("Press CTRL-C...");
                _quitEvent.WaitOne();
            }
            Console.ReadKey(true);
        }
    }
}
