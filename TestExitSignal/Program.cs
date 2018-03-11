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
            Console.WriteLine("Dispose !!");
        }
    }
    class Program
    {
        private static readonly ManualResetEvent _quitEvent = new ManualResetEvent(false);

        static void Exit(object sender, EventArgs agrs)
        {
        }
        static void Main(string[] args)
        {
            using (var dispo = new Dispo())
            {
                Hexagon.WinExitSignal exitSignal = new Hexagon.WinExitSignal(dispo);
                Console.CancelKeyPress += (sender, e) =>
                {
                    _quitEvent.Set();
                    e.Cancel = true;
                };
                Console.WriteLine("Press CTRL-C...");
                _quitEvent.WaitOne();
            }
        }
    }
}
