﻿using System;
using System.IO;
using System.Reflection;
using DirectOutput.Cab;
using DirectOutput.FX;
using DirectOutput.GlobalConfig;
using DirectOutput.LedControl;
using DirectOutput.Scripting;

namespace DirectOutput
{
    /// <summary>
    /// Pinball is the main object of the DirectOutput framework.<br/>
    /// It holds all objects required to process Pinmame data, trigger the necessary effects and update toys and output controllers.
    /// </summary>
    public class Pinball
    {
        private DirectOutput.PinmameHandling.PinmameInputManager PinmameInputManager = new PinmameHandling.PinmameInputManager();
        #region Properties

        private ScriptList _Scripts = new ScriptList();
        /// <summary>
        /// Gets the list of loaded scripts.
        /// </summary>
        /// <value>
        /// The list of loaded scripts.
        /// </value>
        public ScriptList Scripts
        {
            get { return _Scripts; }
            private set { _Scripts = value; }
        }

        private CombinedEffectList _Effects = new CombinedEffectList();

        public CombinedEffectList Effects
        {
            get { return _Effects; }
            set { _Effects = value; }
        }
        


        private Table.Table _Table = new Table.Table();

        /// <summary>
        /// Gets the table object for the Pinball object.
        /// </summary>
        /// <value>
        /// The table object for the Pinball object.
        /// </value>
        public Table.Table Table
        {
            get { return _Table; }
            private set { _Table = value; }
        }
        private Cabinet _Cabinet = new Cabinet();

        /// <summary>
        /// Gets the Cabinet object for the Pinball object.
        /// </summary>
        /// <value>
        /// The cabinet object for the Pinball object.
        /// </value>
        public Cabinet Cabinet
        {
            get { return _Cabinet; }
            private set { _Cabinet = value; }
        }



        private UpdateTimer _UpdateTimer = new UpdateTimer();

        /// <summary>
        /// Gets the UpdateTimer for the Pinball object.
        /// </summary>
        /// <value>
        /// The UpdateTimer for the pinball object.
        /// </value>
        public UpdateTimer UpdateTimer
        {
            get { return _UpdateTimer; }
            private set { _UpdateTimer = value; }
        }




        #endregion

        /// <summary>
        /// Initializes and configures the Pinball object
        /// </summary>
        /// <param name="TableFile">The table file.</param>
        /// <param name="RomName">Name of the rom.</param>
        public void Init(FileInfo TableFile, string RomName = "")
        {
            Log.Init(Path.Combine(TableFile.Directory.FullName,TableFile.GetNameWithoutExtension(),"_{0}.log".Build(DateTime.Now.ToString("yyyy.MM.dd hh:mm:ss"))));

            Log.Write("Loading Pinball parts");


            //Load the global config
            Config GlobalConfig = Config.GetGlobalConfigFromConfigXmlFile();
            if (GlobalConfig == null)
            {
                Log.Write("No GlobalConfig file loaded. Using new inanciated GlobalConfig object instead.");
                //set new global config object if it config could not be loaded from the file.
                GlobalConfig = new Config();
            }
            else
            {
                Log.Write("Global config loaded");
            }

            //Load global script files
            Log.Write("Loading script files");
            Scripts.LoadAndAddScripts(GlobalConfig.GetGlobalScriptFiles());

            //Load cabinet script files
            Scripts.LoadAndAddScripts(GlobalConfig.GetCabinetScriptFiles());

            //Load table script files
            Scripts.LoadAndAddScripts(GlobalConfig.GetTableScriptFiles(TableFile.FullName));

            Log.Write("Script files loaded");

            UpdateTimer.IntervalMs = GlobalConfig.UpdateTimerIntervall;

            Log.Write("Loading cabinet");
            //Load cabinet config
            Cabinet = null;
            FileInfo CCF = GlobalConfig.GetCabinetConfigFile();
            if (CCF != null)
            {
                Log.Write("Will load cabinet config file: {0}".Build(CCF.FullName));
                try
                {
                    Cabinet = Cabinet.GetCabinetFromConfigXmlFile(CCF);
                    Cabinet.CabinetConfigurationFilename = CCF.FullName;
                    Log.Write("Cabinet config loaded successfully from {0}".Build(CCF.FullName));
                }
                catch (Exception E)
                {
                    Log.Exception("A exception occured when load cabinet config file: {0}".Build(CCF.FullName), E);

                    
                }
            }
            if (Cabinet == null)
            {
                Log.Warning("No cabinet config file loaded. Will use AutoConfig.");
                //default to a new cabinet object if the config cant be loaded
                Cabinet = new Cabinet();
                Cabinet.AutoConfig();
            }
            Log.Write("Cabinet loaded");

            Log.Write("Loading table config");
            //Load table config
            Table = null;
            FileInfo TCF = GlobalConfig.GetTableConfigFile(TableFile.FullName);
            if (TCF != null)
            {
                Log.Write("Will load table config from {0}".Build(TCF.FullName));
                try
                {
                    Table = DirectOutput.Table.Table.GetTableFromConfigXmlFile(GlobalConfig.GetTableConfigFile(TableFile.FullName));
                    Table.TableConfigurationFilename = GlobalConfig.GetTableConfigFile(TableFile.FullName).FullName;
                    Log.Write("Table config loaded successfully from {0}".Build(TCF.FullName));
                }
                catch (Exception E)
                {
                    Log.Exception("A exception occured when loading table config: {0}".Build(TCF.FullName), E);
                }
            }
            else
            {
                Log.Warning("No table config file found. Will try to load config from LedControl file(s).");
            }
            if (Table == null)
            {
                if (!RomName.IsNullOrWhiteSpace())
                {
                    Log.Write("Will try to load table config from LedControl file(s) for RomName {0}".Build(RomName));
                    //Load ledcontrol
                    LedControlConfigList L = new LedControlConfigList();
                    if (GlobalConfig.LedControlIniFiles.Count > 0)
                    {
                        Log.Write("Will try to load table config from LedControl  file(s) specified in global config.");
                        L.LoadLedControlFiles(GlobalConfig.LedControlIniFiles, false);
                    }
                    else if (File.Exists(Path.Combine(TableFile.Directory.FullName, "ledcontrol.ini")))
                    {
                        Log.Write("Will try to load table config from LedControl.ini file in the table directory {0}".Build(TableFile.Directory.FullName));
                        L.LoadLedControlFile(Path.Combine(TableFile.Directory.FullName, "ledcontrol.ini"), 1, false);
                    }
                    else if (File.Exists(Path.Combine(Config.GlobalConfigDirectory.FullName, "ledcontrol.ini")))
                    {
                        Log.Write("Will try to load table config from LedControl.ini file in the global config directory {0}".Build(Config.GlobalConfigDirectory.FullName));
                        L.LoadLedControlFile(Path.Combine(Config.GlobalConfigDirectory.FullName, "ledcontrol.ini"), 1, false);
                    }
                    else if (File.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ledcontrol.ini")))
                    {
                        Log.Write("Will try to load table config from LedControl.ini file in the DirectOutput directory {0}".Build(Assembly.GetExecutingAssembly().Location));
                        L.LoadLedControlFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ledcontrol.ini"), 1, false);
                    }
                    if (!L.ContainsConfig(RomName))
                    {
                        Log.Warning("No config found in LedControl data for RomName {0}.".Build(RomName));
                    }
                        Table = L.GetTable(RomName, Cabinet);
                }
                else
                {
                    Table = new Table.Table();
                    Log.Warning("Cant load config from LedControl file(s) since no RomName was supplied. Will use empty table definition (will result in no action from DirectOutput).");
                }
                Table.TableName = Path.GetFileNameWithoutExtension(TableFile.FullName);
            }
            Table.TableFilename = TableFile.FullName;
            Table.RomName = RomName;

            Log.Write("Table config loaded");

            Effects = new CombinedEffectList(new EffectList[] { Table.Effects, Cabinet.Effects });

            Log.Write("Pinball parts loaded");

            Log.Write("Initializing framework");
            Cabinet.Init(this);
            Table.Init(this);
            UpdateTimer.Init();
            Table.TriggerStaticEffects();
            Cabinet.OutputControllers.Update();
            PinmameInputManager.Init();
            Log.Write("Framework initialized.");
            Log.Write("Have fun!");

        }


        /// <summary>
        /// Finishes the Pinball object.
        /// </summary>
        public void Finish()
        {
            Log.Write("Finishing framework");
            PinmameInputManager.Terminate();
            UpdateTimer.Finish();
            Table.Effects.Finish();
            Cabinet.Effects.Finish();
            Cabinet.Toys.Finish();
            Cabinet.OutputControllers.Finish();
            Log.Write("DirectOutput framework finished.");
            Log.Write("Bye and thanks for using!");
        }


        public void ReceivePinmameData(char TableElementTypeChar, int Number, int Value)
        {
            PinmameInputManager.EnqueuePinmameData(TableElementTypeChar, Number, Value);
        }


        #region Event handlers
        void UpdateTimer_AlarmsTriggered(object sender, EventArgs e)
        {
            //TODO: Implement update logic which takes both souces of update calls into account
            Cabinet.OutputControllers.Update();
        }


        private void PinmameInputManager_AllPinmameDataProcessed(object sender, EventArgs e)
        {
            if ((UpdateTimer.NextUpdate - DateTime.Now).TotalMilliseconds > 2)
            {
                Cabinet.OutputControllers.Update();
            }

        }

        private void PinmameInputManager_PinmameDataReceived(object sender, DirectOutput.PinmameHandling.PinmameInputManager.PinmameDataReceivedEventArgs e)
        {

            Table.UpdateTableElement(e.PinmameData);
        }
        
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the <see cref="Pinball"/> class.
        /// </summary>
        public Pinball()
        {

            PinmameInputManager.PinmameDataReceived += new EventHandler<DirectOutput.PinmameHandling.PinmameInputManager.PinmameDataReceivedEventArgs>(PinmameInputManager_PinmameDataReceived);
            PinmameInputManager.PinmameDataProcessed += new EventHandler<EventArgs>(PinmameInputManager_AllPinmameDataProcessed);

            UpdateTimer.AlarmsTriggered += new EventHandler<EventArgs>(UpdateTimer_AlarmsTriggered);

        }


        /// <summary>
        /// Initializes a new instance of the <see cref="Pinball"/> class and calls the Init method with the specified parameters.
        /// </summary>
        /// <param name="TableFile">The table file.</param>
        /// <param name="RomName">Name of the rom.</param>
        public Pinball(FileInfo TableFile, string RomName = "")
            : this()
        {
            Init(TableFile, RomName);
        } 
        #endregion


    }
}
