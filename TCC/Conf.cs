using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCC;
internal class Conf {
    public long QuantidadeBuscado { get; set; }
    public long Erros { get; set; }
    public long Certos { get; set; }
    public long TotalDados { get; set; }
    public DateTime IniTime { get; set; }
    public DateTime EndTime { get; set; }
}
