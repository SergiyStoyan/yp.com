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
    class Program
    {
        [STAThread]
        static void Main()
        {
            //Cliver.CrawlerHost.Linker.ResolveAssembly();
            main();
        }

        static void main()
        {
            //Cliver.Bot.Program.Run();//It is the entry when the app runs as a console app.
            Cliver.BotGui.Program.Run();//It is the entry when the app uses the default GUI.
        }
    }

    public class CustomBotGui : Cliver.BotGui.BotGui
    {
        override public string[] GetConfigControlNames()
        {
            return new string[] { "General", "Input", "Output", "Web", /*"Browser", "Spider",*/ "Proxy", "Log" };
        }

        override public Cliver.BaseForm GetToolsForm()
        {
            return null;
        }

        //override public Type GetBotThreadControlType()
        //{
        //    return typeof(IeRoutineBotThreadControl);
        //    //return typeof(WebRoutineBotThreadControl);
        //}
    }

    public class CustomBot : Cliver.Bot.Bot
    {
        new static public string GetAbout()
        {
            return @"WEB CRAWLER
Created: " + Cliver.Bot.Program.GetCustomizationCompiledTime().ToString() + @"
Developed by: www.cliversoft.com";
        }

        new static public void SessionCreating()
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

        new static public void SessionClosing()
        {
        }

        override public void CycleBeginning()
        {
            //IR = new IeRoutine(((IeRoutineBotThreadControl)BotThreadControl.GetInstanceForThisThread()).Browser);
            //IR.UseCache = false;
            HR = new HttpRoutine();
        }

        //IeRoutine IR;

        HttpRoutine HR;

        public class SearchItem : InputItem
        {
            readonly public string Keyword;
            readonly public string Location;

            override public void PROCESSOR(BotCycle bc)
            {
                CustomBot cb = (CustomBot)bc.Bot;
                string url = "http://www.yellowpages.com/search?search_terms=" + Keyword + "&geo_location_terms=" + Location;
                cb.search_processor(url);
            }
        }

        void search_processor(string url)
        {
            if (!HR.GetPage(url))
                throw new ProcessorException(ProcessorExceptionType.RESTORE_AS_NEW, "Could not get: " + url);

            DataSifter.Capture c = yp.Parse(HR.HtmlResult);

            string npu = c.ValueOf("NextPageUrl"); 
            if (npu != null)
                BotCycle.Add(new SearchNextPageItem(Spider.GetAbsoluteUrl(npu, url)));

            foreach (string u in Spider.GetAbsoluteUrls(c.ValuesOf("Company/Url"), url, HR.HtmlResult))
                BotCycle.Add(new CompanyItem(u));
        }
        static DataSifter.Parser yp = new DataSifter.Parser("yp.fltr");

        public class SearchNextPageItem : InputItem
        {
            readonly public string Url;

            public SearchNextPageItem(string url)
            {
                Url = url;
            }

            override public void PROCESSOR(BotCycle bc)
            {
                CustomBot cb = (CustomBot)bc.Bot;
                cb.search_processor(Url);
            }
        }

        public class CompanyItem : InputItem
        {
            readonly public string Url;

            public CompanyItem(string url)
            {
                Url = url;
            }

            override public void PROCESSOR(BotCycle bc)
            {
                CustomBot cb = (CustomBot)bc.Bot;

                if (!cb.HR.GetPage(Url))
                    throw new ProcessorException(ProcessorExceptionType.RESTORE_AS_NEW, "Could not get: " + Url);

                DataSifter.Capture c = yp2.Parse(cb.HR.HtmlResult);

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