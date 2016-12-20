//********************************************************************************************
//Author: Sergey Stoyan, CliverSoft.com
//        http://cliversoft.com
//        stoyan@cliversoft.com
//        sergey.stoyan@gmail.com
//        27 February 2007
//Copyright: (C) 2007, Sergey Stoyan
//********************************************************************************************

using System;
using System.Linq;
using System.Net;
using System.Text;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using System.Web;
using System.Data;
using System.Web.Script.Serialization;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Net.Mail;
using Cliver;
using System.Configuration;
using System.Windows.Forms;
//using MySql.Data.MySqlClient;
using Cliver.Bot;
using Cliver.BotGui;
using Microsoft.Win32;
using System.Reflection;

namespace Cliver.BotCustomization
{
    public class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                Cliver.Config.Initialize(new string[] { "Engine", "Input", "Output", "Web", /*"Browser", "Spider",*/ "Proxy", "Log" });
                //Bot.Settings.Input.File = Regex.Replace(Bot.Settings.Input.File, @"[^\.]*$", "txt");
                //Bot.Settings.Input.FileFormat = FileFormatEnum.TSV;

                //Cliver.Bot.Program.Run();//It is the entry when the app runs as a console app.
                Cliver.BotGui.Program.Run();//It is the entry when the app uses the default GUI.
            }
            catch (Exception e)
            {
                LogMessage.Error(e);
            }
        }
    }

    public class CustomConfigForm : ConfigForm
    {
        override public List<string> GetConfigControlSections()
        {
            return new List<string> { "Engine", "Input", "Output", "Web", /*"Browser", "Spider",*/ "Proxy", "Log" };
        }
    }

    public class AboutFormForm : AboutForm
    {
        override public string GetAbout()
        {
            return @"CRAWLER
Compiled: " + Cliver.Bot.Program.GetCustomizationCompiledTime().ToString() + @"
Developed by: www.cliversoft.com";
        }
    }

    public class CustomSession : Session
    {
        public CustomSession()
        {
            //InternetDateTime.CHECK_TEST_PERIOD_VALIDITY(2016, 10, 7);

            FileWriter.This.WriteHeader(
               "Name",
               "City",
               "ZipCode",
               "State",
               "Phone",
               "Email",
               "Site",
               "Url"
            );
        }

        override public void __FillStartInputItemQueue(InputItemQueue start_input_item_queue, Type start_input_item_type)
        {
            Log.Main.Write("Filling queue of " + start_input_item_queue.Name + " with input file.");

            if (!File.Exists(Bot.Settings.Input.File))
                throw (new Exception("Input file " + Bot.Settings.Input.File + " does not exist."));

            if (Path.GetExtension(Bot.Settings.Input.File).StartsWith(".xls", StringComparison.InvariantCultureIgnoreCase))
                throw new Exception("Reading excel is not supported");

            FileReader fr = new FileReader(Bot.Settings.Input.File, Bot.Settings.Input.FileFormat);
            string input_locations = Cliver.Log.AppDir + "\\" + PathRoutines.GetFileNameFromPath("input_locations.txt");
            if (!File.Exists(input_locations))
                throw (new Exception("Input file " + input_locations + " does not exist."));
            FileReader fr2 = new FileReader(input_locations, FileFormatEnum.TSV);
            for (FileReader.Row r = fr.ReadLine(); r != null; r = fr.ReadLine())
                for (FileReader.Row r2 = fr2.ReadLine(); r2 != null; r2 = fr2.ReadLine())
                    start_input_item_queue.Add(new CustomBotCycle.SearchItem(r["Keyword"], r2["City"] + ", " + r2["State"]));

            if (start_input_item_queue.CountOfNew < 1)
                LogMessage.Error("Input queue is empty so nothing is to do. Check your input data.");
        }

        override public void __Closing()
        {
        }

        public class CustomBotCycle : BotCycle
        {
            public CustomBotCycle()
            {
                //IR = new IeRoutine(((IeRoutineBotThreadControl)BotThreadControl.GetInstanceForThisThread()).Browser);
                //IR.UseCache = false;
                HR = new Cliver.BotWeb.HttpRoutine();
            }

            //IeRoutine IR;

            readonly Cliver.BotWeb.HttpRoutine HR;

            public class SearchItem : InputItem
            {
                readonly public string Keyword;
                readonly public string Location;

                public SearchItem(string keyword, string location)
                {
                    Keyword = keyword;
                    Location = location;
                }

                override public void __Processor(BotCycle bc)
                {
                    CustomBotCycle cbc = (CustomBotCycle)bc;
                    string url = "http://www.yellowpages.com/search?search_terms=" + Keyword + "&geo_location_terms=" + Location;
                    cbc.search_processor(url);
                }
            }

            void search_processor(string url)
            {
                if (!HR.GetPage(url))
                    throw new ProcessorException(ProcessorExceptionType.RESTORE_AS_NEW, "Could not get: " + url);

                DataSifter.Capture c = yp.Parse(HR.HtmlResult);

                string npu = c.ValueOf("NextPageUrl");
                if (npu != null)
                    Add(new SearchNextPageItem(Cliver.BotWeb.Spider.GetAbsoluteUrl(npu, url)));

                foreach (string u in Cliver.BotWeb.Spider.GetAbsoluteUrls(c.ValuesOf("Company/Url"), url, HR.HtmlResult))
                    Add(new CompanyItem(u));
            }
            static DataSifter.Parser yp = new DataSifter.Parser("yp.fltr");

            public class SearchNextPageItem : InputItem
            {
                readonly public string Url;

                public SearchNextPageItem(string url)
                {
                    Url = url;
                }

                override public void __Processor(BotCycle bc)
                {
                    CustomBotCycle cbc = (CustomBotCycle)bc;
                    cbc.search_processor(Url);
                }
            }

            public class CompanyItem : InputItem
            {
                readonly public string Url;
                public SearchItem Search { get { return (SearchItem)__ParentItem; } }

                public CompanyItem(string url)
                {
                    Url = url;
                }

                override public void __Processor(BotCycle bc)
                {
                    CustomBotCycle cbc = (CustomBotCycle)bc;

                    if (!cbc.HR.GetPage(Url))
                        throw new ProcessorException(ProcessorExceptionType.RESTORE_AS_NEW, "Could not get: " + Url);

                    DataSifter.Capture c = yp2.Parse(cbc.HR.HtmlResult);

                    FileWriter.This.PrepareAndWriteHtmlLineWithHeader(
                        "Name", c.ValueOf("Name"),
                        "City", c.ValueOf("City"),
                        "ZipCode", c.ValueOf("ZipCode"),
                        "State", c.ValueOf("State"),
                        "Phone", c.ValueOf("Phone"),
                        "Email", c.ValueOf("Email"),
                        "Site", c.ValueOf("Site"),
                        "Url", Url
                        );
                }
                static DataSifter.Parser yp2 = new DataSifter.Parser("yp2.fltr");
            }
        }
    }
}