using Gintarine_Vaistine_Test.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Gintarine_Vaistine_Test.Services;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net;
using Newtonsoft.Json.Linq;

namespace Gintarine_Vaistine_Test.Controllers
{
    public class HomeController : Controller
    {
        //TODO: DO NOT FORGET TO UPDATE THE SQL CONECTION IN TO GLOBAL VARS!
        private readonly string connectionString = Gintarine_Vaistine_Test.Properties.Resources.ConnectionString;

        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        List<DataDAO> jsonData = new List<DataDAO>();

        public IActionResult Index()//Insert data
        {
            if (jsonData.Count >0)
            {
                jsonData.Clear();
            }
            jsonData = ReadJson("klientai.json");
            foreach (var entry in jsonData)
            {
                if (DoesRecordExists(entry.Name))
                {
                    InsertData(entry.Name, entry.Address, entry.PostCode, "UPDATE");
                }
                else
                {
                    InsertData(entry.Name, entry.Address, entry.PostCode, "INSERT");
                }
            }
            ViewBag.ReadFromJson = "Data From Json Writed to DataBase ....!";

            return View();
        }

        public IActionResult Privacy() //Update post Codes
        {
            if (jsonData.Count > 0)
            {
                jsonData.Clear();
            }
            jsonData = ReadJson("klientai.json");
            PrepUrl();
            ViewBag.GetPostCodes = "Updated PostCodes ...!";
            return View();
        }

        public IActionResult ShowData()
        {
            GetData();
            return View(DataFromBase);
        }

        private void PrepUrl()
        {
            foreach (var item in jsonData)
            {
                Uri testUri = new Uri(string.Format("{0}?city={1}&address={2}&key={3}", Properties.Resources.PostitLink, SplitAddress(item.Address, "city"), SplitAddress(item.Address), Properties.Resources.PostitKey));
                InsertData(item.Name, item.Address, CallApi(testUri), "UPDATE");
            }
           
        }

        private string SplitAddress(string value, string selector = "address")
        {
            if (!String.IsNullOrWhiteSpace(value))
            {
                switch (selector)
                {
                    case "address":
                        int locationOfChar = value.IndexOf(",", StringComparison.Ordinal);
                        if (locationOfChar > 0)
                        {
                            return value.Substring(0, locationOfChar);
                        }
                        break;
                    case "city":
                        int countOfEvilChars = value.Split(',').Length - 1;
                        if (countOfEvilChars == 2)
                        {
                            return EvilCase(value, ',', ',') ;
                        }
                        string city = value.Substring(value.LastIndexOf(",") + 2);
                        return city;
                        break;
                    default:
                        break;
                }
            }
            
            return String.Empty;
        }
        
        //lazy way to solve the edge case of having 2 ',' chars in the Address string i am 100% sure ther are better ways of doing this
        private string EvilCase(string input, char charFrom, char charTo)
        {
            int posFrom = input.IndexOf(charFrom);
            if (posFrom != -1) //if found char
            {
                int posTo = input.IndexOf(charTo, posFrom + 1);
                if (posTo != -1) //if found char
                {
                    return input.Substring(posFrom + 1, posTo - posFrom - 1);
                }
            }

            return string.Empty;
        }

        public string CallApi(Uri url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                var test = reader.ReadToEnd();
                JObject joResponse = JObject.Parse(test);
                JArray array = (JArray)joResponse["data"];

                return array[0]["post_code"].ToString();
            }
        }

        //InsertData to sql database
        //PS. is this stupid? yes does it work? 50% of the time evry time <3
        public void InsertData(string name, string address, string postCode, string selector)
        {
            string sqlStatementInsert = "INSERT INTO klientai (Name, Address, PostCode) VALUES (" + "'" + name +
                "'" + ", " + "'" + address + "'" + ", " + "'" + postCode + "'" + ")";
            
            string sqlStatementUpdate = "UPDATE klientai SET Name = " + "'" + name + "'" +
                ", " + "Address = " + "'" + address + "'" + ", " + "PostCode = " + "'" + postCode + "'" + "WHERE Name = " + "'" + name + "'";
            
            
            SqlConnection sqlConnection = new SqlConnection(connectionString);
            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            switch (selector)
            {
                case "INSERT":
                    cmd.CommandText = sqlStatementInsert;
                    break;
                case "UPDATE":
                    cmd.CommandText = sqlStatementUpdate;
                    break;
                default:
                    break;
            }
            cmd.Connection = sqlConnection;
            sqlConnection.Open();
            cmd.ExecuteNonQuery();
            sqlConnection.Close();
        }

        //Read from JsonFile
        public List<DataDAO> ReadJson(string filelocation)
        {
            using (StreamReader r = new StreamReader(filelocation))
            {
                string jsonString = r.ReadToEnd();
                List<DataDAO> items = JsonConvert.DeserializeObject<List<DataDAO>>(jsonString);
                return items;
            }
        }

        public List<ShowDataModel> DataFromBase = new List<ShowDataModel>();

        private void GetData()
        {
            SqlCommand cmd = new SqlCommand();
            SqlDataReader dr;
            SqlConnection con = new SqlConnection();

            con.ConnectionString = connectionString;

            if (DataFromBase.Count > 0)
            {
                DataFromBase.Clear();
            }

            try
            {
                con.Open();
                cmd.Connection = con;
                cmd.CommandText = "SELECT TOP (1000) [Name], [Address], [PostCode] FROM[GintarineVaistineTest].[dbo].[klientai]";
                dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    DataFromBase.Add(new ShowDataModel()
                    {
                        Name = dr["Name"].ToString(),
                        Address = dr["Address"].ToString(),
                        PostCode = dr["PostCode"].ToString()
                    });
                }
                con.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        //check if a record exists in database
        public bool DoesRecordExists(string name)
        {
            bool DoesItExists = false;

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();
                string sqlStatement = "SELECT 1 FROM klientai WHERE Name = " + "'" + name + "'" + ";";

                using (SqlCommand command = new SqlCommand(sqlStatement, con))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var test = reader.GetValue(0);
                            if (test.Equals(1))
                            {
                                DoesItExists = true;
                            }
                        }
                    }
                }

            }

            return DoesItExists;
        }



        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
