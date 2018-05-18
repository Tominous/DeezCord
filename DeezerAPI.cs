using System;
using System.IO;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace DeezCord
{
    public static class DeezerAPI
    {
        private const string redirectUri = "http://127.0.0.1:5000/";
        private const string authorizationEndpoint = "https://connect.deezer.com/oauth/auth.php";
        private const string tokenRequestUri = "https://connect.deezer.com/oauth/access_token.php";     
        private const string lastTrackEndpoint = "https://api.deezer.com/user/me/history";
        private const string userEndpoint = "https://api.deezer.com/user/me";

        private static string token;   

        public static async Task Authenticate ()
        {
            HttpListener http = new HttpListener ();
            http.Prefixes.Add (redirectUri);
            http.Start ();

            string authorizationRequest = $"{authorizationEndpoint}?app_id={Program.Configuration ["Deezer:ClientId"]}&redirect_uri={Uri.EscapeDataString (redirectUri)}&perms=listening_history";

            Process.Start ("xdg-open", authorizationRequest);

            HttpListenerContext context = await http.GetContextAsync ();

            HttpListenerResponse response = context.Response;
            const string responseString = "<html><head><script type=\"text/javascript\">function closeMe() { window.close(); } setTimeout(closeMe, 1000)</script></head><body>Please return to the app.</body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes (responseString);
            response.ContentLength64 = buffer.Length;
            Stream responseOutput = response.OutputStream;
            await responseOutput.WriteAsync (buffer, 0, buffer.Length).ContinueWith ((tasl) =>
            {
                responseOutput.Close();
                http.Stop();
                Console.WriteLine ("HTTP server stopped.");
            });

            if (context.Request.QueryString.Get ("error") != null)
            {
                Console.WriteLine ($"OAuth authorization error: {context.Request.QueryString.Get ("error")}");
                return;
            }

            if (context.Request.QueryString.Get ("code") == null)
            {
                Console.WriteLine ($"Malformed authorization response. {context.Request.QueryString}");
                return;
            }

            string code = context.Request.QueryString.Get ("code");

            string tokenRequestBody = $"code={code}&app_id={Program.Configuration ["Deezer:ClientId"]}&secret={Program.Configuration ["Deezer:ClientSecret"]}&response_type=token";

            HttpWebRequest tokenRequest = (HttpWebRequest) WebRequest.Create (tokenRequestUri);
            tokenRequest.Method = "POST";
            tokenRequest.ContentType = "application/x-www-form-urlencoded";
            tokenRequest.Accept = "application/json";
            byte[] byteVersion = Encoding.ASCII.GetBytes (tokenRequestBody);
            tokenRequest.ContentLength = byteVersion.Length;
            Stream stream = tokenRequest.GetRequestStream ();
            await stream.WriteAsync (byteVersion, 0, byteVersion.Length);
            stream.Close();

            try
            {
                WebResponse tokenResponse = await tokenRequest.GetResponseAsync ();
                using (StreamReader reader = new StreamReader (tokenResponse.GetResponseStream () ?? throw new Exception ()))
                {
                    string responseText = await reader.ReadToEndAsync ();

                    token = responseText.Split('&')[0].Remove(0, 13);

                    System.Console.WriteLine(token);
                }
            }
            catch (WebException e)
            {
                if (e.Response is HttpWebResponse r)
                {
                    Console.WriteLine ($"HTTP: {response.StatusCode}");
                    using (StreamReader reader = new StreamReader (r.GetResponseStream () ?? throw new Exception ()))
                    {
                        string responseText = await reader.ReadToEndAsync ();
                    }
                }
            }
        }

        public static async Task<Track> LastTrack ()
        {
            string requestUri = $"{lastTrackEndpoint}?access_token={token}";

            HttpWebRequest userinfoRequest = (HttpWebRequest)WebRequest.Create(requestUri);
            userinfoRequest.Method = "GET";
            userinfoRequest.ContentType = "application/x-www-form-urlencoded";
            userinfoRequest.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

            WebResponse userinfoResponse = await userinfoRequest.GetResponseAsync();
            using (StreamReader userinfoResponseReader = new StreamReader(userinfoResponse.GetResponseStream() ?? throw new Exception ()))
            {
                string userinfoResponseText = await userinfoResponseReader.ReadToEndAsync();

                History tracks = JsonConvert.DeserializeObject<History> (userinfoResponseText);

                return tracks.Tracks [0];
            }
        }

        public static async Task<User> User ()
        {
            string requestUri = $"{userEndpoint}?access_token={token}";

            HttpWebRequest userinfoRequest = (HttpWebRequest)WebRequest.Create(requestUri);
            userinfoRequest.Method = "GET";
            userinfoRequest.ContentType = "application/x-www-form-urlencoded";
            userinfoRequest.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

            WebResponse userinfoResponse = await userinfoRequest.GetResponseAsync();
            using (StreamReader userinfoResponseReader = new StreamReader(userinfoResponse.GetResponseStream() ?? throw new Exception ()))
            {
                string userinfoResponseText = await userinfoResponseReader.ReadToEndAsync();

                User user = JsonConvert.DeserializeObject<User> (userinfoResponseText);

                return user;
            }
        }
    }
}