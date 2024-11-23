using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCC;
internal class Resposta {
    public List<Ob> results { get; set; }
    public Info pageInfo { get; set; }
}

internal class Ob {
    public string goId { get; set; }
    public string goAspect { get; set; }
    public string goEvidence { get; set; }
    public string evidenceCode { get; set; }
    public string reference { get; set; }
    public string goName { get; set; }
    public string geneProductId { get; set; }
}

internal class Info {
    public int resultsPerPage { get; set; }
    public int current { get; set; }
    public int total { get; set; }
}
