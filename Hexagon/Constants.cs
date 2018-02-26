using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon
{
    public static class Constants
    {
        public const string NodeRoleName = "_node_";
        public static string GetProcessingUnitName(string nodeId, string processingUnitId)
            => $"{nodeId}_{processingUnitId}";
    }
}
