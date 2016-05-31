using NetTopologySuite.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTFSBuilder
{
    public static class NTSExtensions
    {
        public static bool TryGetValue(this IAttributesTable table, string name, out object value)
        {
            var names = table.GetNames();
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i] == name)
                {
                    value = table.GetValues()[i];
                    return true;
                }
            }
            value = null;
            return false;
        }
    }
}
