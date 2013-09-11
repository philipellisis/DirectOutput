﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DirectOutput;
using DirectOutput.GlobalConfiguration;
using DirectOutput_Test.Properties;
using System.Configuration;
using System.IO;
using DirectOutput.LedControl;
using DirectOutput.Table;
using DirectOutput.Cab;
using DirectOutput.Cab.Out.DMX;
using DirectOutput.Cab.Toys.Layer;
using DirectOutput.FX.RGBAFX;
using System.Xml.Serialization;
using DirectOutput.FX.TimmedFX;
using System.Reflection;
using DirectOutput.FX;


namespace DirectOutput_Test
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }







        private void Form1_Load(object sender, EventArgs e)
        {





        }

        private void button1_Click(object sender, EventArgs e)
        {

            AnalogLayerDictionary D = new AnalogLayerDictionary();


            Random Rnd = new Random();



            for (int i = 0; i <5; i++)
            {
                D.SetLayer(i, 0, 0);
            }
            DateTime Start = DateTime.Now;
            for (int t = 0; t < 2000000; t++)
            {
                int L = Rnd.Next(0, 4);
                D.SetLayer(L, 255, 255);
                D.SetLayer(L, 0, 0);
                D.SetLayer(L, 255, 255);
                D.SetLayer(L, 0, 0);
                D.SetLayer(L, 255, 255);
                D.SetLayer(L, 0, 0);
                D.SetLayer(L, 255, 255);
                D.SetLayer(L, 0, 0);
                D.SetLayer(L, 255, 255);
                D.SetLayer(L, 0, 0);
            }

            DateTime End = DateTime.Now;

            TimeSpan Duration = (End - Start);

            Console.WriteLine("Duration:  {0}", Duration);

            Console.WriteLine("SetLayer Calls per second:  {0}", 20000000 / Duration.TotalSeconds);


             Start = DateTime.Now;
            for (int t = 0; t < 2000000; t++)
            {
                int V = D.GetResultingValue();
                V = D.GetResultingValue();
                V = D.GetResultingValue();
                V = D.GetResultingValue();
                V = D.GetResultingValue();

                V = D.GetResultingValue();
                V = D.GetResultingValue();
                V = D.GetResultingValue();
                V = D.GetResultingValue();
                V = D.GetResultingValue();
            }

             End = DateTime.Now;

             Duration = (End - Start);

            Console.WriteLine("Duration:  {0}", Duration);

            Console.WriteLine("GetResultingValue Calls per second:  {0}", 20000000 / Duration.TotalSeconds);





        }


        public RetriggerBehaviourEnum RB { get; set; }

        private void button2_Click(object sender, EventArgs e)
        {

            List<int> L = new List<int>();

            Type LT = L.GetType();
            


            DurationEffect E = new DurationEffect();
           
            
            E.Name = "ABC";

     

            PropertyInfo EN = RB.GetType().GetProperty("RetriggerBehaviour");


            string Xml = "";
            using (MemoryStream ms = new MemoryStream())
            {
                
                new XmlSerializer(E.GetType()).Serialize(ms, E);
                ms.Position = 0;
                using (StreamReader sr = new StreamReader(ms, Encoding.Default))
                {
                    Xml = sr.ReadToEnd();
                    sr.Dispose();
                }
            }

            Console.WriteLine( Xml);


        }
    }
}
