using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using IdentityModel.Client;

namespace GradebookMaintenance
{
    internal static class Common
    {
        public static int apiClientCounter;

        public static async Task<string> Authenticate()
        {
            TokenResponse tokenResponse;

            using (var httpClient = new HttpClient())
            {
                UI.WriteIndented("Retrieving the discovery document:");
                UI.StatusInProgress();

                var discoveryDocumentResponse =
                    await httpClient.GetDiscoveryDocumentAsync(Datastore.selectedSchool.authority);
                if (discoveryDocumentResponse.IsError)
                {
                    UI.StatusError();
                    UI.WriteIndented(
                        $"[{discoveryDocumentResponse.HttpStatusCode}] Error retrieving the discovery document [{discoveryDocumentResponse.Error}].");
                }

                UI.StatusOk();

                var authTokenUrl = $"{Datastore.selectedSchool.domain.TrimEnd('/')}/{SchoolData.TokenPath.TrimStart('/')}";
                var apiClientCredentials = new ClientCredentialsTokenRequest();
                apiClientCredentials.Address = authTokenUrl;
                apiClientCredentials.ClientId = SchoolData.RestApiClientId;
                apiClientCredentials.ClientSecret = SchoolData.RestApiClientSecret;
                apiClientCredentials.Scope = SchoolData.RestApiScope;

                UI.WriteIndented($"Authenticating {SchoolData.RestApiClientId}:");
                UI.StatusInProgress();

                tokenResponse = await httpClient.RequestClientCredentialsTokenAsync(apiClientCredentials);
                if (tokenResponse.IsError)
                {
                    UI.StatusError();
                    UI.WriteIndented($"[{tokenResponse.HttpStatusCode}] Error authenticating [{tokenResponse.Error}].");
                }

                UI.StatusOk();
            }

            UI.WriteIndentedLine("Access Token: " + tokenResponse.AccessToken);
            return tokenResponse.AccessToken;
        }

        public static void SetRequestHeaders(HttpClient apiClient, string accept)
        {
            apiClient.DefaultRequestHeaders.Accept.Clear();
            apiClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        }

        public static Task<HttpResponseMessage> InternalGetAsync(HttpClient apiClient, string apiPath)
        {
            apiClientCounter++;
            return apiClient.GetAsync(apiPath);
        }

        public static Task<HttpResponseMessage> InternalPostAsync(HttpClient apiClient, string apiPath,
            string content = "{}")
        {
            apiClientCounter++;
            return apiClient.PostAsync(apiPath, new StringContent(content, Encoding.UTF8, "application/json"));
        }

        public static Task<HttpResponseMessage> InternalPutAsync(HttpClient apiClient, string apiPath,
            string content = "{}")
        {
            apiClientCounter++;
            return apiClient.PutAsync(apiPath, new StringContent(content, Encoding.UTF8, "application/json"));
        }

        public static void drawTextProgressBar(int progress, int total, int failingTotal)
        {
            //draw empty progress bar
            Console.CursorLeft = 3;
            Console.Write("["); //start
            Console.CursorLeft = 54;
            Console.Write("]"); //end
            Console.CursorLeft = 4;
            var onechunk = 50.0f / total;

            //draw filled part
            var position = 4;
            for (var i = 0; i < onechunk * progress; i++)
            {
                Console.BackgroundColor = ConsoleColor.Green;
                Console.CursorLeft = position++;
                Console.Write(" ");
            }

            //draw unfilled part
            for (var i = position; i <= 53; i++)
            {
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.CursorLeft = position++;
                Console.Write(" ");
            }

            //draw totals
            Console.CursorLeft = 58;
            Console.BackgroundColor = ConsoleColor.Black;
            var percentage = Math.Round(progress / (double)total * 100, 2);

            Console.Write(progress + " of " + total + " [" + percentage +
                          "%]   "); //blanks at the end remove any excess
            if (failingTotal > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"{failingTotal} failure(s)        ");
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }
    }
}