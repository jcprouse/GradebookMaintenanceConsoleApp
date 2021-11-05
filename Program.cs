using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using GradebookMaintenance.Actions;
using IdentityModel.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GradebookMaintenance
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.SetWindowSize(UI.Width, UI.Height);
            Console.Clear();
            MainMenu();
        }

        private static void MainMenu()
        {
            while (true)
            {
                UpdateTitle("MAIN MENU");

                UI.WriteIndentedLine("1) Select School");

                var disabledOptions = new int[0];
                if (Datastore.selectedSchool == null)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    disabledOptions = new int[] { 2, 3, 4, 5 };
                }
                const int TotalOptions = 6;

                UI.WriteIndentedLine("2) Reevaluate all Gradebook calculations");
                UI.WriteIndentedLine("3) Reevaluate selected Gradebook calculations");
                UI.WriteIndentedLine("4) Mass creation of Gradebooks");
                UI.WriteIndentedLine("5) Add mid semester historic column for semester 2");

                Console.ForegroundColor = ConsoleColor.Gray;
                UI.WriteIndentedLine("6) Exit");
                
                var userChoice = UI.AskForInput(TotalOptions, disabledOptions);

                if (userChoice == 1)
                {
                    SetSchool();
                }
                else if (userChoice == 2)
                {
                    ReloadAll();
                }
                else if (userChoice == 3)
                {
                    ReloadSelected();
                }
                else if (userChoice == 4)
                {
                    MenuCreateGradebooks();
                }
                else if (userChoice == 5)
                {
                    MenuAddMidSemesterAverage();
                }
                else if (userChoice == 6)
                {
                    break;
                }
            }

        }

        public static void SetSchool()
        {
            UpdateTitle("SELECT SCHOOL");
            var i = 1;
            foreach (var school in SchoolData.Schools)
            {
                UI.WriteIndentedLine($"{i++}) {school.name}");
            }

            var userChoice = UI.AskForInput(SchoolData.Schools.Count);

            Datastore.selectedSchool = SchoolData.Schools[userChoice - 1];
        }
        
        private static void UpdateTitle(string title)
        {
            UI.UpdateTitle(title);
        }

        private static void MenuCreateGradebooks()
        {
            UpdateTitle("MASS CREATION OF GRADEBOOKS");

            UI.WriteIndentedLine($"This will create new gradebooks for {Datastore.selectedSchool.name}.");
            UI.WriteIndentedLine("");
            UI.WriteIndentedLine($"Please provide a csv containing a list of gradebook names. A gradebook will be created for each name.");
            UI.WriteIndentedLine(@"Example: C:\Data\file.csv");

            Datastore.filename = UI.AskForInput("Enter filename or leave blank to go back");
            if (Datastore.filename == string.Empty)
            {
                return;
            }

            UI.ClearFormDisplay();

            UI.WriteIndentedLine($"This will create new gradebooks for {Datastore.selectedSchool.name}.");
            UI.WriteIndentedLine("");
            UI.WriteIndentedLine($"Gradebook names will be read from : {Datastore.filename}");
            
            RunJob(AddGradebooks.Run);
        }

        private static void MenuAddMidSemesterAverage()
        {
            UpdateTitle("ADD MID SEMESTER AVERAGE FOR SEMESTER 2");
            UI.WriteIndentedLine($"This will add a mid semester average in semester 2 for {Datastore.selectedSchool.name}.");
            RunJob(AddMidSemesterAverage);
        }

        private static void ReloadAll()
        {
            UpdateTitle("REEVALUATE ALL GRADEBOOKS");
            UI.WriteIndentedLine($"This will reevaluate all gradebooks for {Datastore.selectedSchool.name}.");
            Datastore.filename = null;
            RunJob(ReloadAllGradebooks);
        }
        
        private static void ReloadSelected()
        {
            UpdateTitle("REEVALUATE SELECTED GRADEBOOKS");

            UI.WriteIndentedLine($"This will reevaluate selected gradebooks for {Datastore.selectedSchool.name}.");
            UI.WriteIndentedLine("");
            UI.WriteIndentedLine($"Please provide a csv containing a list of gradebook Ids.");
            UI.WriteIndentedLine(@"Example: C:\Data\file.csv");

            Datastore.filename = UI.AskForInput("Enter filename or leave blank to go back");
            if (Datastore.filename == string.Empty)
            {
                return;
            }

            UI.ClearFormDisplay();

            UI.WriteIndentedLine($"This will reevaluate selected gradebooks for {Datastore.selectedSchool.name}.");
            UI.WriteIndentedLine("");
            UI.WriteIndentedLine($"Gradebook Ids will be read from : {Datastore.filename}");
            
            RunJob(ReloadAllGradebooks);
        }
        
        private static void RunJob(Func<Task> job)
        {
            if (UI.AskForConfirmation())
            {
                UI.ClearInput();
                var stopwatch = new Stopwatch();
                Common.apiClientCounter = 0;
                try
                {
                    stopwatch.Start();
                    UI.ClearFormDisplay();
                    job().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to complete: {ex.Message}");
                }
                finally
                {
                    UI.WriteIndentedLine(
                        $"{Common.apiClientCounter} requests made to the REST API in {stopwatch.ElapsedMilliseconds / 1000} seconds.");
                    stopwatch.Stop();
                    Datastore.filename = "";
                    UI.AskForAnyKey();
                }
            }
        }
        
        private static async Task ReloadAllGradebooks()
        {
            var accessToken = Common.Authenticate().GetAwaiter().GetResult();
            
            using (var apiClient = new HttpClient())
            {
                apiClient.BaseAddress = new Uri($"{Datastore.selectedSchool.domain.TrimEnd('/')}/api");
                apiClient.DefaultRequestHeaders.Clear();

                UI.WriteIndentedLine("Bearer token set for all API requests.");

                apiClient.SetBearerToken(accessToken);

                Common.SetRequestHeaders(apiClient, "application/hal+json");
                var apiPath = $"{Datastore.selectedSchool.domain.TrimEnd('/')}{SchoolData.Endpoint}all";

                UI.WriteIndented("Retrieving all gradebooks:");
                UI.StatusInProgress();

                var response = await Common.InternalPostAsync(apiClient, apiPath);
                if (!response.IsSuccessStatusCode)
                {
                    UI.StatusError();
                    UI.WriteIndented($"[{response.StatusCode}] Error retrieving the gradebooks [{response.ReasonPhrase}].");
                }
                UI.StatusOk();

                var markbookIds = new List<string>();
                UI.WriteIndented("Extracting gradebook Ids:");
                UI.StatusInProgress();
                if (Datastore.filename == null)
                {
                    var stringContent = await response.Content.ReadAsStringAsync();
                    var jsonContent = (JObject)JsonConvert.DeserializeObject(stringContent);
                    var markbooks = (JArray)jsonContent["markbooks"];
                    markbookIds = markbooks.Select(x => x["id"].ToString()).ToList();
                    UI.StatusOk();
                }
                else
                {
                    try
                    {
                        using (var reader = new StreamReader(Datastore.filename))
                        {
                            while (!reader.EndOfStream)
                            {
                                var line = reader.ReadLine();
                                markbookIds = line.Split(',').ToList();
                            }
                        }
                        UI.StatusOk();
                    }
                    catch (FileNotFoundException e)
                    {
                        UI.StatusError();
                        UI.WriteIndentedLine("CSV file could not be found");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        return;
                    }
                }

                UI.WriteIndentedLine($"There are {markbookIds.Count} gradebooks to update.");
                Console.WriteLine("");
                var markbookCounter = 0;
                var failingMarkbooks = new List<JToken>();

                foreach (var markbookId in markbookIds)
                {
                    markbookCounter++;
                    apiPath =
                        $"{Datastore.selectedSchool.domain.TrimEnd('/')}{SchoolData.Endpoint}{markbookId}";

                    var singleMarkbookResponse = await Common.InternalGetAsync(apiClient, apiPath);

                    if (!singleMarkbookResponse.IsSuccessStatusCode)
                    {
                        failingMarkbooks.Add(markbookId);
                    }

                    Common.drawTextProgressBar(markbookCounter, markbookIds.Count, failingMarkbooks.Count);
                }
                Console.WriteLine("");
                Console.WriteLine("");

                if (failingMarkbooks.Count > 0)
                {
                    var successfulCount = markbookIds.Count - failingMarkbooks.Count;
                    if (successfulCount > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        UI.WriteIndentedLine($"{successfulCount} gradebooks processed.");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        UI.WriteIndentedLine($"All gradebooks failed.");
                    }
                    Console.WriteLine("");
                    Console.ForegroundColor = ConsoleColor.Red;
                    UI.WriteIndentedLine("Failing gradebook IDs:");
                    foreach (var failure in failingMarkbooks)
                    {
                        UI.WriteIndentedLine(failure.ToString());
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    UI.WriteIndentedLine("All gradebooks processed.");
                }
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }
        
        private static async Task AddMidSemesterAverage()
        {
            var semesterId = 2;
            var accessToken = Common.Authenticate().GetAwaiter().GetResult();
            
            using (var apiClient = new HttpClient())
            {
                apiClient.BaseAddress = new Uri($"{Datastore.selectedSchool.domain}/api");
                apiClient.DefaultRequestHeaders.Clear();

                UI.WriteIndentedLine("Bearer token set for all API requests.");

                apiClient.SetBearerToken(accessToken);

                Common.SetRequestHeaders(apiClient, "application/hal+json");
                var apiPath = $"{Datastore.selectedSchool.domain.TrimEnd('/')}{SchoolData.Endpoint}all";

                UI.WriteIndented("Retrieving all gradebooks:");
                UI.StatusInProgress();



                var response = await Common.InternalPostAsync(apiClient, apiPath);
                if (!response.IsSuccessStatusCode)
                {
                    UI.StatusError();
                    UI.WriteIndented($"[{response.StatusCode}] Error retrieving the gradebooks [{response.ReasonPhrase}].");
                }
                UI.StatusOk();

                var markbookIds = new List<string>();
                UI.WriteIndented("Extracting gradebook Ids:");
                UI.StatusInProgress();

                try
                {
                    using (var reader = new StreamReader(@"FAKE.csv"))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            markbookIds = line.Split(',').ToList();
                        }
                    }
                    UI.StatusOk();
                }
                catch (FileNotFoundException e)
                {
                    UI.StatusError();
                    UI.WriteIndentedLine("CSV file could not be found");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    return;
                }

                UI.WriteIndentedLine($"There are {markbookIds.Count} gradebooks to update.");
                Console.WriteLine("");
                
                var markbookCounter = 0;
                var failingMarkbooks = new List<JToken>();

                Console.WriteLine("Starting individual gradebook processing...");

                foreach (var markbookId in markbookIds)
                {
                    Common.drawTextProgressBar(markbookCounter, markbookIds.Count, failingMarkbooks.Count);

                    markbookCounter++;
                    apiPath =
                        $"{Datastore.selectedSchool.domain.TrimEnd('/')}{SchoolData.Endpoint}{markbookId}/structure";

                    var structureResponse = await Common.InternalGetAsync(apiClient, apiPath);

                    if (!structureResponse.IsSuccessStatusCode)
                    {
                        failingMarkbooks.Add(markbookId);
                        continue;
                    }

                    var structureContent = await structureResponse.Content.ReadAsStringAsync();
                    var jsonStructureContent = (JObject)JsonConvert.DeserializeObject(structureContent);
                    var columns = (JArray)jsonStructureContent["items"];

                    var semester = columns.FirstOrDefault(x => x["name"].ToString() == $"HS Semester {semesterId} [SM{semesterId}]");
                    if (semester == null || semester["items"] == null)
                    {
                        failingMarkbooks.Add(markbookId);
                        continue;
                    }
                    var semesterCourse = semester["items"].FirstOrDefault(x => x["name"].ToString() == $"HS Semester Course {semesterId}");
                    if (semesterCourse == null || semesterCourse["items"] == null)
                    {
                        failingMarkbooks.Add(markbookId);
                        continue;
                    }
                    var semesterAverage = semesterCourse["items"].FirstOrDefault(x => x["name"].ToString() == "Semester Average");
                    if (semesterAverage == null || semesterAverage["id"] == null)
                    {
                        failingMarkbooks.Add(markbookId);
                        continue;
                    }
                    var semesterAverageId = semesterAverage["id"].ToString();
                    apiPath =
                        $"{Datastore.selectedSchool.domain.TrimEnd('/')}{SchoolData.Endpoint}{markbookId}/structure/historicaspects";
                    var request =
                        @"{
                        'targetHistoricAspectId': " + semesterAverageId + @",
                        'name': 'Mid Semester Average',
                        'shortName': 'MID SEM AVG',
                        'date': '2021-04-07T18:00:00Z'
                    }";

                    var historicColumnResponse = await Common.InternalPostAsync(apiClient, apiPath, request);
                    if (!historicColumnResponse.IsSuccessStatusCode)
                    {
                        failingMarkbooks.Add(markbookId);
                        continue;
                    }
                    var historicColumnId = historicColumnResponse.Content.ReadAsStringAsync().Result;

                    var changedItem = new JObject();
                    changedItem["type"] = "Column";
                    changedItem["newParentId"] = semesterCourse["id"];

                    var reorder = new JObject();
                    reorder["changedItem"] = changedItem;
                    reorder["markbookHierarchyItems"] = GetItems(columns, semesterAverageId, historicColumnId);

                    apiPath =
                        $"{Datastore.selectedSchool.domain.TrimEnd('/')}{SchoolData.Endpoint}{markbookId}/structure/columns/order";

                    var reorderResponse = await Common.InternalPutAsync(apiClient, apiPath, reorder.ToString());
                    if (!reorderResponse.IsSuccessStatusCode)
                    {
                        failingMarkbooks.Add(markbookId);
                        Console.WriteLine($"Failed to reorder {markbookId}. Total failures: {failingMarkbooks.Count}");
                        continue;
                    }

                }
                Console.WriteLine("");
                Console.WriteLine("");

                if (failingMarkbooks.Count > 0)
                {
                    var successfulCount = markbookIds.Count - failingMarkbooks.Count;
                    if (successfulCount > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        UI.WriteIndentedLine($"{successfulCount} gradebooks processed.");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        UI.WriteIndentedLine($"All gradebooks failed.");
                    }
                    Console.WriteLine("");
                    Console.ForegroundColor = ConsoleColor.Red;
                    UI.WriteIndentedLine("Failing gradebook IDs:");
                    foreach (var failure in failingMarkbooks)
                    {
                        UI.WriteIndentedLine(failure.ToString());
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    UI.WriteIndentedLine("All gradebooks processed.");
                }
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        private static JArray GetItems(JArray items, string targetId, string newId)
        {
            var response = new JArray();
            foreach (var item in items)
            {
                if (item["id"].ToString() == newId)
                {
                    continue;
                }

                var obj = new JObject();
                obj["id"] = item["id"];
                if (item["items"] != null)
                {
                    obj["items"] = GetItems((JArray)item["items"], targetId, newId);
                }
                response.Add(obj);

                if (item["id"].ToString() == targetId)
                {
                    obj = new JObject();
                    obj["id"] = int.Parse(newId);
                    response.Add(obj);
                }
            }
            return response;
        }
        public class ExceptionResponse
        {
            public string Message { get; set; }
            public string ExceptionMessage { get; set; }
            public string ExceptionType { get; set; }
            public string StackTrace { get; set; }
            public Program.ExceptionResponse InnerException { get; set; }
        }
    }
}
