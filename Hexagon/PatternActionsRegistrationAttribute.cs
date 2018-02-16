using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon
{
    [System.AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class PatternActionsRegistrationAttribute : Attribute
    {
        public PatternActionsRegistrationAttribute()
        {
        }
    }
}
