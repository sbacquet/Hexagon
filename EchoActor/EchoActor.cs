using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;

namespace EchoActor
{
    public class EchoActor : ReceiveActor
    {
        public EchoActor()
        {
            Receive<string>(message => { Console.WriteLine($@"=====> {message}"); return true; });
        }
    }
}
