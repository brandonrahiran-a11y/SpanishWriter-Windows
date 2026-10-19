using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// Serves only the add-in files over the local loopback interface.
public sealed class WpsAddinServer : IDisposable
{
    public const int Port = 17825;
    readonly TcpListener listener;
    readonly Thread thread;
    readonly string root;
    readonly Func<string, string> suggest;

    public WpsAddinServer(string baseDirectory, Func<string, string> suggestCallback)
    {
        root = Path.Combine(baseDirectory, "wps_addin");
        suggest = suggestCallback;
        listener = new TcpListener(IPAddress.Loopback, Port);
        listener.Start();
        thread = new Thread(Run) { IsBackground = true, Name = "SpanishWriter WPS add-in" };
        thread.Start();
    }

    void Run()
    {
        while (true)
        {
            TcpClient client;
            try { client = listener.AcceptTcpClient(); }
            catch (SocketException) { return; }
            catch (ObjectDisposedException) { return; }
            try { Serve(client); }
            catch (Exception ex) { Log("Error al servir complemento: " + ex.Message); }
            finally { client.Close(); }
        }
    }

    void Serve(TcpClient client)
    {
        client.ReceiveTimeout = 3000;
        client.SendTimeout = 3000;
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
        {
            string request = reader.ReadLine();
            if (request == null) return;
            string[] words = request.Split(' ');
            string target = words.Length >= 2 ? words[1] : "";
            string route = target.Split('?')[0];
            bool get = words.Length >= 1 && (words[0] == "GET" || words[0] == "HEAD");
            string type = "text/plain; charset=utf-8";
            byte[] body;
            if (!get) body = Encoding.UTF8.GetBytes("Method not allowed");
            else if (route == "/wps/" || route == "/wps/index.html")
            {
                type = "text/html; charset=utf-8";
                body = Encoding.UTF8.GetBytes("<!doctype html><html><head><meta charset=\"utf-8\"></head><body><script src=\"main.js\"></script></body></html>");
            }
            else if (route == "/wps/main.js")
            {
                type = "application/javascript; charset=utf-8";
                body = File.ReadAllBytes(Path.Combine(root, "main.js"));
            }
            else if (route == "/wps/ribbon.xml")
            {
                type = "application/xml; charset=utf-8";
                body = File.ReadAllBytes(Path.Combine(root, "ribbon.xml"));
            }
            else if (route == "/wps/suggest")
            {
                type = "application/json; charset=utf-8";
                string query = target.Contains("?") ? target.Substring(target.IndexOf('?') + 1) : "";
                string word = query.StartsWith("word=") ? Uri.UnescapeDataString(query.Substring(5)) : "";
                body = Encoding.UTF8.GetBytes(word.Length <= 100 && suggest != null ? suggest(word) : "{\"suggestions\":[]}");
            }
            else body = Encoding.UTF8.GetBytes("Not found");
            int status = !get ? 405 : route == "/wps/" || route == "/wps/index.html" || route == "/wps/main.js" || route == "/wps/ribbon.xml" || route == "/wps/suggest" ? 200 : 404;
            string header = "HTTP/1.1 " + status + (status == 200 ? " OK" : " Error") + "\r\n" +
                "Content-Type: " + type + "\r\nContent-Length: " + body.Length +
                "\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            if (words[0] != "HEAD") stream.Write(body, 0, body.Length);
            Log("WPS complemento: " + route + " -> " + status);
        }
    }

    static void Log(string message)
    {
        try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") + " " + message + Environment.NewLine); }
        catch { }
    }

    public void Dispose() { listener.Stop(); }
}
