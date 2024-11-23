using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCC
{
    internal class GoResponse
    {
        public List<GoResult> results { get; set; } = new();

        public class GoResult
        {
            public string? name { get; set; }
        }
    }
}
