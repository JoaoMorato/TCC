using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;
using TCC;

internal class Program
{
    private static Semaphore SemSave = new Semaphore(1, 1);
    private static Semaphore SemaphoreConsole = new Semaphore(1, 1);
    private static bool shutdown = false;
    private static bool isRunning = false;
    private static int posiY = 0;
    private static DateTime dateIni;
    private static long lidas = 0;
    private static long total = 0;

    private static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Esperado argumentos.");
            return;
        }
        Console.Clear();
        Thread th;
        dateIni = DateTime.Now;
        switch (args[0].ToLower())
        {
            case "web":
                th = new Thread(new ThreadStart(() => SearchWeb(args[1..])));
                th.Start();
                break;
            case "find":
                th = new Thread(new ThreadStart(() => FindInFile(args[1..].ToList())));
                th.Start();
                break;
            default:
                Console.WriteLine("Paramentro não compreendido.");
                return;
        }
        th.IsBackground = true;
        string txt = "";
        if (th.ThreadState != System.Threading.ThreadState.Running)
            Thread.Sleep(2000);
        while (isRunning)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey();
                if ((int)key.KeyChar < 32)
                {
                    if (key.Key == ConsoleKey.Backspace && txt.Length > 0)
                    {
                        txt = txt[..^1] + " ";
                        WriteConsole(txt, null, false);
                        txt = txt[..^1];
                    }
                    if (key.Key == ConsoleKey.Enter && txt.Length > 0)
                    {
                        RunCommand(txt);
                        txt = "";
                    }
                }
                else
                {
                    if (key.Key != ConsoleKey.Escape && key.Key != ConsoleKey.Delete)
                        txt += key.KeyChar;
                }
            }
            else
            {
                if (txt.Length > 0)
                    WriteConsole(txt, null, false);
                Thread.Sleep(200);
            }
        }
        Thread.Sleep(5000);
    }

    private static void RunCommand(string txt)
    {
        var lista = txt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        WriteConsole(txt);
        switch (lista[0].ToLower())
        {
            case "shutdown":
                if (lista.Length < 2)
                {
                    WriteConsole("Falta de argumento. [enable | disable]");
                    break;
                }
                switch (lista[1].ToLower())
                {
                    case "enable":
                        shutdown = true;
                        WriteConsole("shutdown habilitado");
                        break;
                    case "disable":
                        shutdown = false;
                        WriteConsole("shutdown desabilitado");
                        break;
                    default:
                        WriteConsole("Comando não compreendido");
                        break;
                }
                break;
            case "time":
                var tempo = DateTime.Now - dateIni;
                WriteConsole($"{tempo.Hours}:{tempo.Minutes,02}:{tempo.Seconds,02}, tempo de execução");
                break;
            case "searched":
                WriteConsole($"Processado: {lidas}/{total}");
                break;
        }
    }

    private static void WriteConsole(string txt, ConsoleColor? cor = null, bool confirmed = true)
    {
        if (posiY > 1000)
        {
            posiY = 0;
            Console.Clear();
        }
        SemaphoreConsole.WaitOne();
        Console.SetCursorPosition(0, posiY);
        Console.WriteLine(new string(' ', Console.BufferWidth));
        Console.SetCursorPosition(0, posiY);
        if (cor != null)
            Console.ForegroundColor = (ConsoleColor)cor;
        if (confirmed)
        {
            var textos = txt.Split('\n');
            foreach (var t in textos)
            {
                int quant = t.Length / (Console.BufferWidth + 1);
                posiY += 1 + quant;
                Console.WriteLine(t);
            }
        }
        else
            Console.Write(txt);
        Console.ResetColor();
        SemaphoreConsole.Release();
    }

    private static void SearchWeb(string[] args)
    {
        isRunning = true;
        try
        {
            string fileRead = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, args[0]);
            string fileOut = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saida");
            string fileConf = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

            int quant = 25;
            string type = "";
            if (args.Length >= 2)
            {
                if (args[1].All(e => char.IsDigit(e)))
                    quant = int.Parse(args[1]);
                if(args.Length >= 3)
                    type = args[2];
            }

            ParallelOptions parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = quant };

            List<Fasta> retorno = new List<Fasta>();

            Semaphore semaphore = new Semaphore(1, 1);

            var linhas = File.ReadAllLines(fileRead);
            var ids = new List<string>();
            int count = 0;
            string line = "";
            foreach (string l in linhas)
            {
                if (count >= 10)
                {
                    if (line.Length > 0)
                        line = line[..^1];
                    ids.Add(line);
                    count = 0;
                    line = "";
                }
                line += l + ",";
                count++;
            }
            long certo = 0;
            long erro = 0;
            long totalDados = 0;
            total = linhas.Count();

            Parallel.ForEach(ids, parallelOptions, (id) =>
            {
                var split = id.Split(',');
                lidas += split.Length;
                int page = 1;

                List<Ob> dados = new List<Ob>();

                //HttpWebRequest web = (HttpWebRequest)WebRequest.Create();
                while (true)
                {
                    try
                    {

                        HttpClient client = new HttpClient
                        {
                            BaseAddress = new Uri($"https://www.ebi.ac.uk/QuickGO/services/annotation/search?page={page}&geneProductId={id}&includeFields=goName&limit=50")
                        };

                        var resp = client.Send(new HttpRequestMessage
                        {
                            Method = HttpMethod.Get,
                        });

                        StreamReader sr = new StreamReader(resp.Content.ReadAsStream());
                        string line = sr.ReadToEnd();
                        sr.Close();

                        Resposta re = JsonConvert.DeserializeObject<Resposta>(line);

                        if ((re?.results.Count <= 0 && page == 1) || re == null)
                        {
                            WriteConsole("Nada encontrado: " + id, ConsoleColor.Red);
                            erro += id.Split(',').Length;
                            return;
                        }

                        foreach (var r in re.results)
                        {
                            if (string.IsNullOrEmpty(r.goId) || !string.IsNullOrEmpty(r.goName))
                                continue;

                            client = new HttpClient
                            {
                                BaseAddress = new Uri($"https://www.ebi.ac.uk/QuickGO/services/ontology/go/search?query={r.goId}&limit=1&page=1")
                            };

                            resp = client.Send(new HttpRequestMessage { Method = HttpMethod.Get });
                            if (!resp.IsSuccessStatusCode)
                                r.goName = "Nome não encontrado";
                            sr = new StreamReader(resp.Content.ReadAsStream());
                            line = sr.ReadToEnd();
                            sr.Close();

                            GoResponse? g = JsonConvert.DeserializeObject<GoResponse>(line);
                            if (g != null && g.results.Count > 0)
                                r.goName = g.results[0].name ?? "Nome não encontrado";
                        }

                        dados.AddRange(re.results);

                        if (re.pageInfo.total <= page)
                            break;

                        page++;
                    }
                    catch { }
                }

                semaphore.WaitOne();

                dados.GroupBy(e => e.geneProductId)
                    .ToList()
                    .ForEach(e =>
                        retorno.Add(new Fasta
                        {
                            Dados = [.. e],
                            FastaId = e.Key.Split(':')[1]
                        })
                    );

                totalDados += dados.Count;
                certo += dados.Count;
                erro += id.Split(',').Length - dados.Count;

                if (retorno.Count >= 5000)
                {
                    SavePartial(retorno, fileOut, type);
                    retorno.Clear();
                }
                semaphore.Release();
            });

            if (retorno.Count > 0)
                SavePartial(retorno, fileOut, type);

            WriteConsole("Finalizando...", ConsoleColor.Green);

            if (!File.Exists(fileConf))
                File.Create(fileConf).Close();

            Conf conf = new Conf
            {
                Certos = certo,
                Erros = erro,
                QuantidadeBuscado = lidas,
                TotalDados = totalDados,
                IniTime = dateIni,
                EndTime = DateTime.Now
            };

            StreamWriter sw = new StreamWriter(fileConf);
            sw.Write(JsonConvert.SerializeObject(conf));
            sw.Close();


            if (shutdown)
            {
                WriteConsole("Desligando computador...", ConsoleColor.Yellow);
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = "/C shutdown /s /f /t 60 /c \"Desligamento projeto TCC\"";
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "erro.txt"), ex.InnerException?.Message ?? ex.Message);
        }
        finally
        {
            isRunning = false;
        }
    }

    public static void SavePartial(List<Fasta> lista, string saida, string type)
    {
        SemSave.WaitOne();
        WriteConsole("Salvando dados recebidos...", ConsoleColor.Green);
    jmp:
        try
        {
            StreamWriter sw;
            if(type.ToLower() == "csv")
            {
                sw = new StreamWriter(File.Open(saida + ".csv", FileMode.Append));
                if (sw.BaseStream.Position == 0)
                {
                    sw.WriteLine("FastaId|goId|goName|evidenceCode|geneProductId|goAspect|reference|goEvidence");
                }
                foreach (Fasta f in lista)
                    foreach (Ob o in f.Dados)
                        sw.WriteLine($"{f.FastaId}|{o.goId}|{o.goName}|{o.evidenceCode}|{o.geneProductId}|{o.goAspect}|{o.reference}|{o.goEvidence}");
            }
            else
            {
                sw = new StreamWriter(File.Open(saida + ".json", FileMode.Append));
                sw.Write(JsonConvert.SerializeObject(lista));
            }
            sw.Close();
        }
        catch
        {
            goto jmp;
        }
        SemSave.Release();
        WriteConsole("Dados recebidos salvo com sucesso...", ConsoleColor.Green);
    }

    private static void FindInFile(List<string> args)
    {
        isRunning = true;
        try
        {
            string busca = "";
            foreach (string s in args)
                busca += $"{s}, ";
            busca = busca.Trim().Remove(busca.Length - 1);
            WriteConsole($"Começando busca por [{busca}]", ConsoleColor.Green);
            StreamReader reader = new StreamReader("saida.json");
            int chaves = 0;
            string ob = "";
            List<Fasta> fastas = new List<Fasta>();
            int objLidos = 0;

            while (!reader.EndOfStream)
            {
                char c = (char)reader.Read();
                if (c == '{')
                {
                    chaves++;
                    ob += c;
                    continue;
                }
                if (chaves == 0)
                    continue;
                ob += c;

                if (c == '}')
                {
                    if (--chaves == 0)
                    {
                        var obj = JsonConvert.DeserializeObject<Fasta>(ob);
                        if (args.Contains(obj.FastaId))
                        {
                            fastas.Add(obj);
                            args.Remove(obj.FastaId);
                        }
                        ob = "";
                        objLidos++;
                        if (objLidos % 10 == 0)
                            WriteConsole($"{objLidos} Objetos lidos. ({reader.BaseStream.Position}/{reader.BaseStream.Length} bytes)", ConsoleColor.Blue);

                        if (args.Count == 0)
                        {
                            WriteConsole("Todos os FastasId encontrados, encerrando busca...", ConsoleColor.Green);
                            break;
                        }
                    }
                }
            }
            reader.Close();

            foreach (Fasta fasta in fastas)
            {
                WriteConsole(fasta.FastaId, ConsoleColor.Yellow);
                foreach (var d in fasta.Dados)
                {
                    WriteConsole($"   {d.goId}", ConsoleColor.Yellow);
                }
                WriteConsole("\n");
            }
        }
        finally
        {
            isRunning = false;
        }
    }
}