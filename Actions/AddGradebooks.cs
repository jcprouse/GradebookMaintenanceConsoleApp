using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using IdentityModel.Client;
using Newtonsoft.Json.Linq;

namespace GradebookMaintenance.Actions
{
    internal class AddGradebooks
    {
        public static async Task Run()
        {
            var accessToken = Common.Authenticate().GetAwaiter().GetResult();

            using (var apiClient = new HttpClient())
            {
                apiClient.BaseAddress = new Uri($"{Datastore.selectedSchool.domain.TrimEnd('/')}/api");
                apiClient.DefaultRequestHeaders.Clear();

                UI.WriteIndentedLine("Bearer token set for all API requests.");

                apiClient.SetBearerToken(accessToken);


                Common.SetRequestHeaders(apiClient, "application/hal+json");

                var markbookNames = new List<string>();
                UI.WriteIndented("Extracting gradebook names:");
                UI.StatusInProgress();

                try
                {
                    using (var reader = new StreamReader(Datastore.filename))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            markbookNames = line.Split(',').ToList();
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

                UI.WriteIndentedLine($"There are {markbookNames.Count} gradebooks to create.");
                Console.WriteLine("");

                var markbookCounter = 0;
                var failingMarkbooks = new List<JToken>();

                Console.WriteLine("Starting individual gradebook processing...");

                foreach (var markbookName in markbookNames)
                {
                    markbookCounter++;
                    var apiPath =
                        $"{Datastore.selectedSchool.domain}{SchoolData.Endpoint}details";

                    var request =
                        @"{
                            'characteristicColumns':[
                                {'name':'Gender','order':1},
                                {'name':'Name','order':2}
                            ],
                            'displayGradesetCountRow':'NotIncluded',
                            'displayMaximumRow':'NotIncluded',
                            'displayMeanRow':'NotIncluded',
                            'displayMedianRow':'NotIncluded',
                            'displayMinimumRow':'NotIncluded',
                            'displayStandardDeviationRow':'NotIncluded',
                            'name':'" + markbookName + @"',
                            'staffAllocations':{'type':'Staff'},
                            'staffAssignments':['" + SchoolData.UserId + @"'],
                            'tags':[]
                        }";

                    var historicColumnResponse = await Common.InternalPostAsync(apiClient, apiPath, request);
                    if (!historicColumnResponse.IsSuccessStatusCode) failingMarkbooks.Add(markbookName);
                    Common.drawTextProgressBar(markbookCounter, markbookNames.Count, failingMarkbooks.Count);
                }

                Console.WriteLine("");
                Console.WriteLine("");

                if (failingMarkbooks.Count > 0)
                {
                    var successfulCount = markbookNames.Count - failingMarkbooks.Count;
                    if (successfulCount > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        UI.WriteIndentedLine($"{successfulCount} gradebooks processed.");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        UI.WriteIndentedLine("All gradebooks failed.");
                    }

                    Console.WriteLine("");
                    Console.ForegroundColor = ConsoleColor.Red;
                    UI.WriteIndentedLine("Failing gradebook names:");
                    foreach (var failure in failingMarkbooks) UI.WriteIndentedLine(failure.ToString());
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
    }
}