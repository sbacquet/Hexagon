using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon.AkkaRest
{
    [System.AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class RestRequestConvertersRegistrationAttribute : Attribute
    {
    }
}
