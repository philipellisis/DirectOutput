﻿using DirectOutput.Cab.Schedules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;


namespace DirectOutput.Cab.Out.LW
{
    /// <summary>
    /// The LedWiz is a easy to use outputcontroller with 32 outputs which all support 49 <a target="_blank" href="https://en.wikipedia.org/wiki/Pulse-width_modulation">pwm</a> levels with a PWM frequency of approx. 50hz. The LedWiz is able to drive leds and smaller loads directly, but will require some kind of booster for power hungery gadgets like big contactors or motors.
    ///
    /// \image html LedWizboard.jpg
    /// 
    /// The DirectOutput framework does fully support the LedWiz and can control up to 16 LedWiz units. 
    /// 
    /// The framework can automatically detect connected LedWiz units and configure them for use with the framework. 
    /// 
    /// The LedWiz is made by <a href="http://groovygamegear.com/">GroovyGameGear</a> and can by ordered directly on GroovyGamegears website, but also from some other vendors.
    /// 
    /// This unit was the first output controller which was widely used in the virtual pinball community and was the unit for which the legacy vbscript solution was developed. The DirectOutput framework replaces the vbscript solution, but can reuse the ini files which were used for the configuration of the tables. Please read \ref ledcontrolfiles for more information.
    /// 
    /// The current implementation of the LedWiz driver uses a separate thread for every ledwiz connected to the system to ensure max. performance.
    /// 
    /// \image html LedWizLogo.jpg
    /// 
    /// </summary>
    public class LedWiz : OutputControllerBase, IOutputController, IDisposable
    {
        #region Number


        private object NumberUpdateLocker = new object();
        private int _Number = -1;

        /// <summary>
        /// Gets or sets the number of the LedWiz.<br />
        /// The number of the LedWiz must be unique.<br />
        /// Setting changes the Name property, if it is blank or if the Name coresponds to LedWiz {Number}.
        /// </summary>
        /// <value>
        /// The unique number of the LedWiz (Range 1-16).
        /// </value>
        /// <exception cref="System.Exception">
        /// LedWiz Numbers must be between 1-16. The supplied number {0} is out of range.
        /// </exception>
        public int Number
        {
            get { return _Number; }
            set
            {
                if (!value.IsBetween(1, 16))
                {
                    throw new Exception("LedWiz Numbers must be between 1-16. The supplied number {0} is out of range.".Build(value));
                }
                lock (NumberUpdateLocker)
                {
                    if (_Number != value)
                    {

                        if (Name.IsNullOrWhiteSpace() || Name == "LedWiz {0:00}".Build(_Number))
                        {
                            Name = "LedWiz {0:00}".Build(value);
                        }

                        _Number = value;

                    }
                }
            }
        }

        #endregion


        #region MinCommandIntervalMs property of type int with events
        private int _MinCommandIntervalMs = 1;
        private bool MinCommandIntervalMsSet = false;

        /// <summary>
        /// Gets or sets the mininimal interval between command in miliseconds (Default: 1ms).
        /// Depending on the mainboard, usb hardware on the board, usb drivers and other factors the LedWiz does sometime tend to loose or misunderstand commands received if the are sent in to short intervals.
        /// The settings allows to increase the default minmal interval between commands from 1ms to a higher value. Higher values will make problems less likely, but decreases the number of possible updates of the ledwiz outputs in a given time frame.
        /// It is recommended to use the default interval of 1 ms and only to increase this interval if problems occur (Toys which are sometimes not reacting, random knocks of replay knocker or solenoids).
        /// </summary>
        /// <value>
        /// The mininimal interval between command in miliseconds (Default: 1ms).
        /// Depending on the mainboard, usb hardware on the board, usb drivers and other factors the LedWiz does sometime tend to loose or misunderstand commands received if the are sent in to short intervals.
        /// The settings allows to increase the default minmal interval between commands from 1ms to a higher value. Higher values will make problems less likely, but decreases the number of possible updates of the ledwiz outputs in a given time frame.
        /// It is recommended to use the default interval of 1 ms and only to increase this interval if problems occur (Toys which are sometimes not reacting, random knocks of replay knocker or solenoids).
        /// </value>
        public int MinCommandIntervalMs
        {
            get { return _MinCommandIntervalMs; }
            set
            {
                _MinCommandIntervalMs = value.Limit(0, 1000);
                MinCommandIntervalMsSet = true;
            }
        }

        #endregion


        #region IOutputcontroller implementation
        /// <summary>
        /// Updates all outputs of the LedWiz output controller.<br/>
        /// Signals the workerthread that all pending updates for the ledwiz should be sent to the physical LedWiz unit.
        /// </summary>
        public override void Update()
        {
            LedWizUnits[Number].TriggerLedWizUpdaterThread();
        }


        /// <summary>
        /// Initializes the LedWiz object.<br />
        /// This method does also start the workerthread which does the actual update work when Update() is called.<br />
        /// This method should only be called once. Subsequent calls have no effect.
        /// </summary>
        /// <param name="Cabinet">The Cabinet object which is using the LedWiz instance.</param>
        public override void Init(Cabinet Cabinet)
        {
            Log.Debug("Initializing LedWiz Nr. {0:00}".Build(Number));
            AddOutputs();
            if (!MinCommandIntervalMsSet && Cabinet.Owner.ConfigurationSettings.ContainsKey("LedWizDefaultMinCommandIntervalMs") && Cabinet.Owner.ConfigurationSettings["LedWizDefaultMinCommandIntervalMs"] is int)
            {
                MinCommandIntervalMs = (int)Cabinet.Owner.ConfigurationSettings["LedWizDefaultMinCommandIntervalMs"];
            }

            LedWizUnits[Number].Init(Cabinet, MinCommandIntervalMs);

            Log.Write("LedWiz Nr. {0:00} initialized and updater thread initialized.".Build(Number));

        }

        /// <summary>
        /// Finishes the LedWiz object.<br/>
        /// Finish does also terminate the workerthread for updates.
        /// </summary>
        public override void Finish()
        {
            Log.Debug("Finishing LedWiz Nr. {0:00}".Build(Number));
            LedWizUnits[Number].Finish();
            LedWizUnits[Number].ShutdownLighting();
            Log.Write("LedWiz Nr. {0:00} finished and updater thread stopped.".Build(Number));

        }
        #endregion



        #region Outputs



        /// <summary>
        /// Adds the outputs for a LedWiz.<br/>
        /// A LedWiz has 32 outputs numbered from 1 to 32. This method adds LedWizOutput objects for all outputs to the list.
        /// </summary>
        private void AddOutputs()
        {
            for (int i = 1; i <= 32; i++)
            {
                if (!Outputs.Any(x => ((LedWizOutput)x).LedWizOutputNumber == i))
                {
                    Outputs.Add(new LedWizOutput(i) { Name = "{0}.{1:00}".Build(Name, i) });
                }
            }
        }


        /// <summary>
        /// This method is called whenever the value of a output in the Outputs property changes its value.<br />
        /// Updates the internal arrays holding the states of the LedWiz outputs.
        /// </summary>
        /// <param name="Output">The output which has changed.</param>
        /// <exception cref="System.Exception">
        /// The OutputValueChanged event handler for LedWiz {0:00} has been called by a sender which is not a LedWizOutput.<br/>
        /// or<br/>
        /// LedWiz output numbers must be in the range of 1-32. The supplied output number {0} is out of range.
        /// </exception>
        protected override void OnOutputValueChanged(IOutput Output)
        {
            if (!(Output is LedWizOutput))
            {
                throw new Exception("The OutputValueChanged event handler for LedWiz {0:00} has been called by a sender which is not a LedWizOutput.".Build(Number));
            }
            LedWizOutput LWO = (LedWizOutput)Output;

            if (!LWO.LedWizOutputNumber.IsBetween(1, 32))
            {
                throw new Exception("LedWiz output numbers must be in the range of 1-32. The supplied output number {0} is out of range.".Build(LWO.LedWizOutputNumber));
            }

            LedWizUnit S = LedWizUnits[this.Number];
            //S.UpdateValue(LWO);

            //uses ledwizoutput instead of standard output, need to mirror ledwizoutputnumber
            //note, compensate for id [1-16] not being zero-based
            IOutput recalculatedOutput = ScheduledSettings.Instance.getnewrecalculatedOutput(Output, 1, Number-1);
            LWO.Value = recalculatedOutput.Value;

            S.UpdateValue(LWO);
        }



        ///// <summary>
        ///// Handles the OutputValueChanged event of the base class.<br/>
        ///// Updates the internal arrays holding the states of the LedWiz outputs. 
        ///// </summary>
        ///// <param name="sender">The source of the event (must be a LedWizOutput).</param>
        ///// <param name="e">The <see cref="OutputEventArgs" /> instance containing the event data.</param>
        ///// <exception cref="System.Exception">The OutputValueChanged event handler for LedWiz {0:00} has been called by a sender which is not a LedWizOutput. or LedWiz output numbers must be in the range of 1-32. The supplied output number {0} is out of range.</exception>
        //private void OutputValueChanged(object sender, OutputEventArgs e)
        //{

        //    if (!(e.Output is LedWizOutput))
        //    {
        //        throw new Exception("The OutputValueChanged event handler for LedWiz {0:00} has been called by a sender which is not a LedWizOutput.".Build(Number));
        //    }
        //    LedWizOutput LWO = (LedWizOutput)e.Output;

        //    if (!LWO.LedWizOutputNumber.IsBetween(1, 32))
        //    {
        //        throw new Exception("LedWiz output numbers must be in the range of 1-32. The supplied output number {0} is out of range.".Build(LWO.LedWizOutputNumber));
        //    }

        //    LedWizUnit S = LedWizUnits[this.Number];
        //    S.UpdateValue(LWO);
        //}
        #endregion



        #region LEDWIZ Static Private Methods & Properties

		// LedWiz device descriptor
		struct LWDEVICE
		{
			public LWDEVICE(int unitNo, String path)
			{
				this.unitNo = unitNo;
				this.path = path;
			}

			// nominal LedWiz unit number (1-16)
			public int unitNo;

			// Win32 file path to USB endpoint for the device.  This path can
			// can opened as an ordinary file to send commands to the LedWiz.
			public String path;
		}
		
		// LedWiz device list.  For each LedWiz device we find in the system,
		// we populate the array entry indexed by the unit number with an
		// LWDEVICE structure for the device.
		private static List<LWDEVICE> deviceList = new List<LWDEVICE>();

		// The LedWiz USB interface is designed so that each unit attached
		// to a given computer is distinguished from others in the same system
		// by its USB product ID code.  The manufacturer assigned a range of
		// 16 product IDs for this purpose, which means that the maximum
		// number of units on a single system is 16.  We don't actually
		// impose any limit of our own; this constant is just for reference.
        private const int LWZ_MAX_DEVICES = 16;

		// The LedWiz flash modes.  These modes can be used in place of
		// PWM brightness levels in PBA commands.  This enum is for reference
		// only, as we never use any of the flash modes in DOF; we instead
		// handle flashes and fades by setting PWM levels on a timer.  Note
		// that LedWiz devices aren't actually very reliable at executing
		// these modes properly, so it's good that we don't need them.
        private enum AutoPulseMode : int
        {
            RampUpRampDown = 129,				// /\/\
            OnOff = 130,						// _|-|_|-|
            OnRampDown = 131,					// -\|-\
            RampUpDown = 132					// /-|/-
        }

        private static int StartedUp = 0;
        private static object StartupLocker = new object();
		private static void StartupLedWiz()
		{
			lock (StartupLocker)
			{
				if (StartedUp == 0)
				{
					// clear the device list
					deviceList = new List<LWDEVICE>();

					try
					{
						// The LedWiz is uses the Windows generic HID driver, so we can find
						// all of the LedWiz devices in the system by enumerating HID devices
						// and filtering for the LedWiz vendor/product ID codes.  To enumerate
						// the HID devices, enumerate all devices matching the HID class GUID.
						Guid guid;
						HIDImports.HidD_GetHidGuid(out guid);
						IntPtr hdev = HIDImports.SetupDiGetClassDevs(
							ref guid, null, IntPtr.Zero, HIDImports.DIGCF_DEVICEINTERFACE);

						// set up the attribute structure buffer
						HIDImports.SP_DEVICE_INTERFACE_DATA diData = new HIDImports.SP_DEVICE_INTERFACE_DATA();
						diData.cbSize = Marshal.SizeOf(diData);

						// read the devices matching the HID class GUID
						for (uint i = 0;
							 HIDImports.SetupDiEnumDeviceInterfaces(hdev, IntPtr.Zero, ref guid, i, ref diData);
							 ++i)
						{
							// get the size of the detail data structure
							UInt32 size = 0;
							HIDImports.SetupDiGetDeviceInterfaceDetail(
								hdev, ref diData, IntPtr.Zero, 0, out size, IntPtr.Zero);

							// now that we know the structure size, set up a buffer and
							// and read the contents into it
							HIDImports.SP_DEVICE_INTERFACE_DETAIL_DATA diDetail = new HIDImports.SP_DEVICE_INTERFACE_DETAIL_DATA();
							diDetail.cbSize = (IntPtr.Size == 8) ? (uint)8 : (uint)5;
							if (HIDImports.SetupDiGetDeviceInterfaceDetail(hdev, ref diData, ref diDetail, size, out size, IntPtr.Zero))
							{
								// create a file handle to access the device
								IntPtr fp = HIDImports.CreateFile(
									diDetail.DevicePath, HIDImports.GENERIC_READ_WRITE, HIDImports.SHARE_READ_WRITE,
									IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);

								// make sure we opened the file
								if (fp != IntPtr.Zero && fp.ToInt32() != -1)
								{
									// read the attributes
									HIDImports.HIDD_ATTRIBUTES attrs = new HIDImports.HIDD_ATTRIBUTES();
									attrs.Size = Marshal.SizeOf(attrs);
									if (HIDImports.HidD_GetAttributes(fp, ref attrs))
									{
										// presume this is an LedWiz unit, then look for reasons it's not
										bool ok = true;

										// Check for the LedWiz vendor and product ID codes.  If they don't
										// match, it's not an LedWiz.
										ok &= ((ushort)attrs.VendorID == 0xFAFA);
										ok &= (attrs.ProductID >= 0x00F0 && attrs.ProductID < 0x00FF);

										// Infer the unit number from the product ID.  The product ID
										// is simply 0x00EF + Unit Number, using our internal convention
										// of starting the numbering at 1.
										int unitNo = attrs.ProductID - 0x00EF;

										// Check the HID output report descriptor.  The LedWiz interface
										// has an 8-byte output report (which actually looks like 9 bytes
										// on the Windows side, because Windows adds a mandatory prefix
										// byte for the report ID, even when the extra byte doesn't go 
										// across the wire, which it doesn't in this case).
										// 
										// To get the report descriptor information, we have to retrieve
										// the "capabilities" structure, which requires fetching the
										// "preparsed data" structure.
										IntPtr ppdata;
										if (ok && HIDImports.HidD_GetPreparsedData(fp, out ppdata))
										{
											// get the device caps
											HIDImports.HIDP_CAPS caps = new HIDImports.HIDP_CAPS();
											HIDImports.HidP_GetCaps(ppdata, ref caps);

											// Check that we have a single link collection, and that
											// we have the expected output report length.
											ok &= (caps.NumberLinkCollectionNodes == 1);
											ok &= (caps.OutputReportByteLength == 9);

											// done with the preparsed data
											HIDImports.HidD_FreePreparsedData(ppdata);
										}

										// if we passed all tests, add this device to the list
										if (ok)
											deviceList.Add(new LWDEVICE(unitNo, diDetail.DevicePath));
									}

									// done with the file handle
									HIDImports.CloseHandle(fp);
								}
							}
						}
					}
					catch (Exception ex)
					{
						Log.Exception("Could not init LedWiz", ex);
						throw new Exception("Could not init LedWiz", ex);
					}

				}
				StartedUp++;
			}
		}

        private static void TerminateLedWiz()
        {
            lock (StartupLocker)
            {

                if (StartedUp > 0)
                {
                    StartedUp--;
                    if (StartedUp == 0)
                    {
                        foreach (LedWizUnit S in LedWizUnits.Values)
                        {
                            S.ShutdownLighting();
                        }
                    }
                }
            }
        }
        #endregion


        /// <summary>
        /// Gets the numbers of the LedWiz controllers which are connected to the system.
        /// </summary>
        /// <returns></returns>
        public static List<int> GetLedwizNumbers()
        {
			// create an instance to ensure we populate the static unit list
			LedWiz LW = new LedWiz();

			// get a list of just the unit numbers
            List<int> lst = deviceList.Select(d => d.unitNo).ToList();

			// done with the temporary instance
			LW.Dispose();

			// return the list
			return lst;
        }



        /// <summary>
        /// Finalizes an instance of the <see cref="LedWiz"/> class.
        /// </summary>
        ~LedWiz()
        {

            Dispose(false);
        }


        #region Dispose

        /// <summary>
        /// Disposes the LedWiz object.
        /// </summary>
        public void Dispose()
        {

            Dispose(true);
            GC.SuppressFinalize(this); // remove this from gc finalizer list
        }
        
        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                try
                {
                    Log.Debug("Disposing LedWiz instance {0:00}.".Build(Number));
                }
                catch
                {
                }
                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if (disposing)
                {
                    // Dispose managed resources.

                }

                // Call the appropriate methods to clean up
                // unmanaged resources here.
                // If disposing is false,
                // only the following code is executed.

                TerminateLedWiz();


                // Note disposing has been done.
                disposed = true;

            }
        }
        private bool disposed = false;

        #endregion

        #region Constructor


        /// <summary>
        /// Initializes the <see cref="LedWiz"/> class.
        /// </summary>
        static LedWiz()
        {
            LedWizUnits = new Dictionary<int, LedWizUnit>();
            for (int i = 1; i <= 16; i++)
            {
                LedWizUnits.Add(i, new LedWizUnit(i));
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LedWiz"/> class.
        /// </summary>
        public LedWiz()
        {
            Log.Write("Opening " + (IntPtr.Size == 8 ? "64" : "32") + "-bit LedWiz driver...");
            StartupLedWiz();
            Outputs = new OutputList();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LedWiz"/> class.
        /// </summary>
        /// <param name="Number">The number of the LedWiz (1-16).</param>
        public LedWiz(int Number)
            : this()
        {
            this.Number = Number;
        }

        #endregion


        #region Internal class for LedWiz output states and update methods

        private static Dictionary<int, LedWizUnit> LedWizUnits = new Dictionary<int, LedWizUnit>();

        private class LedWizUnit
        {
            //  private Pinball Pinball;


            //Used to convert the 0-255 range of output values to LedWiz values in the range of 0-48.
            //            private static readonly byte[] ByteToLedWizValue = { 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 8, 8, 8, 8, 8, 9, 9, 9, 9, 9, 9, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11, 12, 12, 12, 12, 12, 13, 13, 13, 13, 13, 14, 14, 14, 14, 14, 14, 15, 15, 15, 15, 15, 16, 16, 16, 16, 16, 17, 17, 17, 17, 17, 18, 18, 18, 18, 18, 19, 19, 19, 19, 19, 19, 20, 20, 20, 20, 20, 21, 21, 21, 21, 21, 22, 22, 22, 22, 22, 23, 23, 23, 23, 23, 24, 24, 24, 24, 24, 24, 25, 25, 25, 25, 25, 26, 26, 26, 26, 26, 27, 27, 27, 27, 27, 28, 28, 28, 28, 28, 29, 29, 29, 29, 29, 29, 30, 30, 30, 30, 30, 31, 31, 31, 31, 31, 32, 32, 32, 32, 32, 33, 33, 33, 33, 33, 34, 34, 34, 34, 34, 34, 35, 35, 35, 35, 35, 36, 36, 36, 36, 36, 37, 37, 37, 37, 37, 38, 38, 38, 38, 38, 39, 39, 39, 39, 39, 39, 40, 40, 40, 40, 40, 41, 41, 41, 41, 41, 42, 42, 42, 42, 42, 43, 43, 43, 43, 43, 44, 44, 44, 44, 44, 44, 45, 45, 45, 45, 45, 46, 46, 46, 46, 46, 47, 47, 47, 47, 47, 48, 48, 48, 48, 48 };

            private static readonly byte[] ByteToLedWizValue = { 0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7, 8, 8, 8, 8, 8, 9, 9, 9, 9, 9, 10, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11, 12, 12, 12, 12, 12, 13, 13, 13, 13, 13, 13, 14, 14, 14, 14, 14, 15, 15, 15, 15, 15, 16, 16, 16, 16, 16, 16, 17, 17, 17, 17, 17, 18, 18, 18, 18, 18, 19, 19, 19, 19, 19, 20, 20, 20, 20, 20, 20, 21, 21, 21, 21, 21, 22, 22, 22, 22, 22, 23, 23, 23, 23, 23, 23, 24, 24, 24, 24, 24, 25, 25, 25, 25, 25, 26, 26, 26, 26, 26, 26, 27, 27, 27, 27, 27, 28, 28, 28, 28, 28, 29, 29, 29, 29, 29, 29, 30, 30, 30, 30, 30, 31, 31, 31, 31, 31, 32, 32, 32, 32, 32, 32, 33, 33, 33, 33, 33, 34, 34, 34, 34, 34, 35, 35, 35, 35, 35, 36, 36, 36, 36, 36, 36, 37, 37, 37, 37, 37, 38, 38, 38, 38, 38, 39, 39, 39, 39, 39, 39, 40, 40, 40, 40, 40, 41, 41, 41, 41, 41, 42, 42, 42, 42, 42, 42, 43, 43, 43, 43, 43, 44, 44, 44, 44, 44, 45, 45, 45, 45, 45, 45, 46, 46, 46, 46, 46, 47, 47, 47, 47, 47, 48, 48, 48, 48, 48, 49 };
            private const int MaxUpdateFailCount = 5;

			// our LedWiz unit number (1-16)
            public int Number { get; private set; }

			// Win32 file path to the USB HID endpoint for the device
			private String path;

			// Win32 file handle to the USB HID endpoint
			private IntPtr fp = IntPtr.Subtract(IntPtr.Zero, 1);

            public byte[] NewOutputValues = new byte[32] { 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49 };
            public byte[] CurrentOutputValues = new byte[32];
            public byte[] NewAfterValueSwitches = new byte[4];
            public byte[] NewBeforeValueSwitches = new byte[4];
            public byte[] CurrentAfterValueSwitches = new byte[4];
            public byte[] CurrentBeforeValueSwitches = new byte[4];
            public bool NewValueUpdateRequired = true;
            public bool NewSwitchUpdateBeforeValueUpdateRequired = true;
            public bool NewSwitchUpdateAfterValueUpdateRequired = true;

            public bool CurrentValueUpdateRequired = true;
            public bool CurrentSwitchUpdateBeforeValueUpdateRequired = true;
            public bool CurrentSwitchUpdateAfterValueUpdateRequired = true;
            public bool UpdateRequired = true;

            public object LedWizUpdateLocker = new object();
            public object ValueChangeLocker = new object();

            public Thread LedWizUpdater;
            public bool KeepLedWizUpdaterAlive = false;
            public object LedWizUpdaterThreadLocker = new object();

            public void Init(Cabinet Cabinet, int MinCommandIntervalMs)
            {
                //this.Pinball = Cabinet.Pinball;
                //if (!Pinball.TimeSpanStatistics.Contains("LedWiz {0:00} update calls".Build(Number)))
                //{
                //    UpdateTimeStatistics = new TimeSpanStatisticsItem() { Name = "LedWiz {0:00} update calls".Build(Number), GroupName = "OutputControllers - LedWiz" };
                //    Pinball.TimeSpanStatistics.Add(UpdateTimeStatistics);
                //}
                //else
                //{
                //    UpdateTimeStatistics = Pinball.TimeSpanStatistics["LedWiz {0:00} update calls".Build(Number)];
                //}
                //if (!Pinball.TimeSpanStatistics.Contains("LedWiz {0:00} PWM updates".Build(Number)))
                //{
                //    PWMUpdateTimeStatistics = new TimeSpanStatisticsItem() { Name = "LedWiz {0:00} PWM updates".Build(Number), GroupName = "OutputControllers - LedWiz" };
                //    Pinball.TimeSpanStatistics.Add(PWMUpdateTimeStatistics);
                //}
                //else
                //{
                //    PWMUpdateTimeStatistics = Pinball.TimeSpanStatistics["LedWiz {0:00} PWM updates".Build(Number)];
                //}
                //if (!Pinball.TimeSpanStatistics.Contains("LedWiz {0:00} OnOff updates".Build(Number)))
                //{
                //    OnOffUpdateTimeStatistics = new TimeSpanStatisticsItem() { Name = "LedWiz {0:00} OnOff updates".Build(Number), GroupName = "OutputControllers - LedWiz" };
                //    Pinball.TimeSpanStatistics.Add(OnOffUpdateTimeStatistics);
                //}
                //else
                //{
                //    OnOffUpdateTimeStatistics = Pinball.TimeSpanStatistics["LedWiz {0:00} OnOff updates".Build(Number)];
                //}

                this.MinCommandIntervalMs = MinCommandIntervalMs;
                StartLedWizUpdaterThread();
            }

            public void Finish()
            {
                TerminateLedWizUpdaterThread();
                ShutdownLighting();
                //this.Pinball = null;
            }

			/// <summary>
			/// Output controller in-use state.  This tells us if we've ever received a value update from the
			/// client, and if we've sent any commands yet to the connected device.  Initially, this is set
			/// to Startup.  On a value change event, if this is still startup, we set it to ValueChanged.
			/// The updater thread can use this to determine if it needs to send initialization commands 
			/// and/or value updates to the device.  The updater thread can simply take no action when the
			/// value is Startup, as this means that the unit hasn't yet been addressed by the client and
			/// thus might not be in use at all during this session.  When the state is ValueChanged, the
			/// updater thread should send any necessary initialization commands to the device and change
			/// the state to Running.  When the state is Running, the thread can simply send value updates
			/// as normal.
			/// </summary>
			protected enum InUseStates { Startup, ValueChanged, Running };

			/// <summary>
			/// Current in-use state for this controller.
			/// </summary>
			protected InUseStates InUseState = InUseStates.Startup;

            public void UpdateValue(LedWizOutput LedWizOutput)
            {
                byte V = ByteToLedWizValue[LedWizOutput.Value];
                bool S = (V != 0);

                int ZeroBasedOutputNumber = LedWizOutput.LedWizOutputNumber - 1;

                int ByteNr = ZeroBasedOutputNumber >> 3;
                int BitNr = ZeroBasedOutputNumber & 7;

                byte Mask = (byte)(1 << BitNr);

                lock (ValueChangeLocker)
                {
					// if in Startup state, transition to ValueChanged state
					if (InUseState == InUseStates.Startup)
						InUseState = InUseStates.ValueChanged;

                    if (S != ((NewAfterValueSwitches[ByteNr] & Mask) != 0))
                    {
                        //Neeed to adjust switches
                        if (S == false)
                        {
                            //Output will be turned off
                            Mask = (byte)~Mask;
                            NewAfterValueSwitches[ByteNr] &= Mask;
                            NewBeforeValueSwitches[ByteNr] &= Mask;
                            NewSwitchUpdateBeforeValueUpdateRequired = true;
                            UpdateRequired = true;
                        }
                        else
                        {
                            //Output will be turned on
                            if (V != NewOutputValues[ZeroBasedOutputNumber])
                            {
                                //Need to change value before turning on
                                NewOutputValues[ZeroBasedOutputNumber] = V;
                                NewAfterValueSwitches[ByteNr] |= Mask;
                                NewValueUpdateRequired = true;
                                NewSwitchUpdateAfterValueUpdateRequired = true;
                                UpdateRequired = true;
                            }
                            else
                            {
                                //Value is correct, only need to turn on switch
                                NewAfterValueSwitches[ByteNr] |= Mask;
                                NewBeforeValueSwitches[ByteNr] |= Mask;
                                NewSwitchUpdateBeforeValueUpdateRequired = true;
                                UpdateRequired = true;
                            }
                        }
                    }
                    else
                    {
                        if (S == true && V != NewOutputValues[ZeroBasedOutputNumber])
                        {
                            //Only need to adjust value
                            NewOutputValues[ZeroBasedOutputNumber] = V;
                            NewValueUpdateRequired = true;
                            UpdateRequired = true;
                        }

                    }
                }
            }
            public void CopyNewToCurrent()
            {
                lock (ValueChangeLocker)
                {

                    CurrentValueUpdateRequired = NewValueUpdateRequired;
                    CurrentSwitchUpdateBeforeValueUpdateRequired = NewSwitchUpdateBeforeValueUpdateRequired;
                    CurrentSwitchUpdateAfterValueUpdateRequired = NewSwitchUpdateAfterValueUpdateRequired;


                    if (NewValueUpdateRequired)
                    {
                        Array.Copy(NewOutputValues, CurrentOutputValues, NewOutputValues.Length);
                        NewValueUpdateRequired = false;
                    }
                    if (NewSwitchUpdateAfterValueUpdateRequired || NewSwitchUpdateBeforeValueUpdateRequired)
                    {
                        Array.Copy(NewAfterValueSwitches, CurrentAfterValueSwitches, NewAfterValueSwitches.Length);
                        Array.Copy(NewBeforeValueSwitches, CurrentBeforeValueSwitches, NewBeforeValueSwitches.Length);
                        Array.Copy(CurrentAfterValueSwitches, NewBeforeValueSwitches, NewAfterValueSwitches.Length);
                        NewSwitchUpdateAfterValueUpdateRequired = false;
                        NewSwitchUpdateBeforeValueUpdateRequired = false;
                    }



                }
            }

            public bool IsUpdaterThreadAlive
            {
                get
                {
                    if (LedWizUpdater != null)
                    {
                        return LedWizUpdater.IsAlive;
                    }
                    return false;
                }
            }

            public void StartLedWizUpdaterThread()
            {
                lock (LedWizUpdaterThreadLocker)
                {
                    if (!IsUpdaterThreadAlive)
                    {
                        KeepLedWizUpdaterAlive = true;
                        LedWizUpdater = new Thread(LedWizUpdaterDoIt);
                        LedWizUpdater.Name = "LedWiz {0:00} updater thread".Build(Number);
                        LedWizUpdater.Start();
                    }
                }
            }
            public void TerminateLedWizUpdaterThread()
            {
                //  lock (LedWizUpdaterThreadLocker)
                //  {
                if (LedWizUpdater != null)
                {
                    try
                    {
                        KeepLedWizUpdaterAlive = false;
                        TriggerLedWizUpdaterThread();
                        if (!LedWizUpdater.Join(1000))
                        {
                            LedWizUpdater.Abort();
                        }

                    }
                    catch (Exception E)
                    {
                        Log.Exception("A error occurd during termination of {0}.".Build(LedWizUpdater.Name), E);
                        throw new Exception("A error occurd during termination of {0}.".Build(LedWizUpdater.Name), E);
                    }
                    LedWizUpdater = null;
                }
                // }
            }

            bool TriggerUpdate = false;
            public void TriggerLedWizUpdaterThread()
            {
                TriggerUpdate = true;
                lock (LedWizUpdaterThreadLocker)
                {
                    Monitor.Pulse(LedWizUpdaterThreadLocker);
                }
            }

            //TODO: Check if thread should really terminate on failed updates
            private void LedWizUpdaterDoIt()
            {
                Log.Write("Updater thread for LedWiz {0:00} started.".Build(Number));
                //Pinball.ThreadInfoList.HeartBeat("LedWiz {0:00}".Build(Number));

                int FailCnt = 0;
                while (KeepLedWizUpdaterAlive)
                {
                    try
                    {
                        if (IsPresent)
                        {
							// If in ValueChanged state, initialize the LedWiz unit to the
							// base state, with all switches (SBA) OFF and all profile levels
							// (PBA) set to 100% (brightness level 49).
							if (InUseState == InUseStates.Startup)
							{
								// send the initialization commands
								SBA(new byte[] { 0, 0, 0, 0 });
								PBA(Enumerable.Repeat((byte)49, 32).ToArray());

								// switch to Running state
								InUseState = InUseStates.Running;
							}

							// if in Running state, send the update
							if (InUseState == InUseStates.Running)
								SendLedWizUpdate();
                        }
                        FailCnt = 0;
                    }
                    catch (Exception E)
                    {
                        Log.Exception("A error occured when updating LedWiz Nr. {0}".Build(Number), E);
                        //Pinball.ThreadInfoList.RecordException(E);
                        FailCnt++;

                        if (FailCnt > MaxUpdateFailCount)
                        {
                            Log.Exception("More than {0} consecutive updates failed for LedWiz Nr. {1}. Updater thread will terminate.".Build(MaxUpdateFailCount, Number));
                            KeepLedWizUpdaterAlive = false;
                        }
                    }

                    //Pinball.ThreadInfoList.HeartBeat();
                    if (KeepLedWizUpdaterAlive)
                    {
                        lock (LedWizUpdaterThreadLocker)
                        {
                            while (!TriggerUpdate && KeepLedWizUpdaterAlive)
                            {
                                Monitor.Wait(LedWizUpdaterThreadLocker, 50);  // Lock is released while we’re waiting
                                //Pinball.ThreadInfoList.HeartBeat();
                            }
						}
                    }

                    TriggerUpdate = false;
                }

                //Pinball.ThreadInfoList.ThreadTerminates();
                Log.Write("Updater thread for LedWiz {0:00} terminated.".Build(Number));
            }



            private DateTime LastUpdate = DateTime.Now;
            public int MinCommandIntervalMs = 1;
            private void UpdateDelay()
            {
                int Ms = (int)DateTime.Now.Subtract(LastUpdate).TotalMilliseconds;
                if (Ms < MinCommandIntervalMs)
                {
                    Thread.Sleep((MinCommandIntervalMs - Ms).Limit(0, MinCommandIntervalMs));
                }
                LastUpdate = DateTime.Now;
            }

            private void SendLedWizUpdate()
            {
                if (Number.IsBetween(1, 16))
                {

                    lock (LedWizUpdateLocker)
                    {
                        lock (ValueChangeLocker)
                        {
                            if (!UpdateRequired) return;


                            CopyNewToCurrent();

                            UpdateRequired = false;
                        }

						// If it's been too long since our last PBA, send a PBA.
						// The LedWiz seems to occasionally miss a PBA packet, so 
						// it's counterproductive to optimize *too* aggressively
						// to avoid sending them.
						//
						// If we do decide to send the PBA due to the time lapse,
						// send the full update with an SBA first and an SBA after.
						// The initial SBA helps ensures that the PBA counter on the
						// device gets reset - if it dropped a PBA previously, it 
						// would be out of sync as a result.  The post-SBA ensures
						// that the final switch values are in effect.
                        bool pbaTimeout = ((DateTime.Now - pbaTime).TotalMilliseconds > 1000);

                        if (CurrentValueUpdateRequired || pbaTimeout)
                        {
                            if (CurrentSwitchUpdateBeforeValueUpdateRequired || pbaTimeout)
                                SBA(CurrentBeforeValueSwitches);

                            PBA(CurrentOutputValues);
                        }

						if (CurrentSwitchUpdateAfterValueUpdateRequired
							|| (CurrentSwitchUpdateBeforeValueUpdateRequired && !CurrentValueUpdateRequired)
							|| pbaTimeout)
                            SBA(CurrentAfterValueSwitches);
                    }
                }
            }


            public void ShutdownLighting()
            {
				SBA(new byte[]{0, 0, 0, 0});
            }


			// Send an SBA command.  This command sets all 32 outputs to ON/OFF
			// values, leaving the brightness or flash mode unchanged.  The switch
			// values are packed into four bytes, one bit per output.
			private void SBA(byte[] b, byte globalFlashSpeed = 2)
			{
				// Set up the SBA command.  The first byte is the HID report ID 
				// (always 0 for the LedWiz, since there's only one report type).
				// The second byte of an SBA is always 64 (0x40), which identifies
				// the SBA command.  The next four bytes are the switch bits, and 
				// the next byte is the global flash speed.  The last two bytes are 
				// unused, so we set them to zero.
				WriteUSB(new byte[9]{ 0, 64, b[0], b[1], b[2], b[3], globalFlashSpeed, 0, 0 });
			}

			// Send a set of PBA updates.  This sends a sequence of four PBA
			// commands to the LedWiz to update all 32 outputs.  
			//
			// A PBA command simply consists of 8 bytes containing the brightness 
			// levels for 8 consecutive outputs.  An internal counter on the LedWiz
			// determines which block of 8 outputs is being set.  Each SBA command
			// resets the counter to the first port, so that the first PBA after
			// an SBA writes to outputs 1-8.  The LedWiz bumps up the counter by 8
			// on each PBA, so the second PBA after an SBA writes ports 9-16, etc.
			// So to update all 32 outputs, we write four PBA commands.  The series
			// of four PBAs leaves the counter where it started, since the counter 
			// rolls over to the first port after passing the last port.
			//
			// Note that we don't do anything here to reset the internal counter
			// before sending the series of PBA commands, because we assume it's
			// already pointing to the first port.  This is safe because the only 
			// commands we ever send are SBA commands (which implicitly reset the 
			// counter to the first port) and blocks of four consecutive PBA 
			// commands (which leave the counter where it started because of the 
			// rollover behavior).
			private DateTime pbaTime = new DateTime(0);
			private void PBA(byte[] b)
			{
				// Send the outputs 8 at a time
				for (int ofs = 0; ofs < 32; ofs += 8)
				{
					// send the PBA command for the next 8 outputs
					WriteUSB(new byte[9]{ 
						0, 
						b[ofs+0], b[ofs+1], b[ofs+2], b[ofs+3], 
						b[ofs+4], b[ofs+5], b[ofs+6], b[ofs+7] 
					});
				}

				// note the time of the last PBA
				pbaTime = DateTime.Now;
			}

			private System.Threading.NativeOverlapped ov = new System.Threading.NativeOverlapped();
			private int[] RetryWait = new int[] { 1, 1, 1, 2, 2, 2, 3, 3, 5, 5 };
			private void WriteUSB(byte[] buf)
			{
				// make sure the file handle is valid
				if (fp.ToInt32() != -1)
				{
					// Try a few times.  USB writes sometimes fail, but these failures
					// are usually temporary and will succeed on retry.  But don't keep
					// trying indefinitely; if we can't get through after a few tries,
					// there's probably an unrecoverable problem.
                    UpdateDelay();
                    for (int tries = 0; tries < RetryWait.Length; ++tries)
					{
						// try sending the command
						UInt32 actual;
						if (HIDImports.WriteFile(fp, buf, (UInt32)buf.Length, out actual, ref ov) != 0
							&& actual == buf.Length)
							return;

						// if that failed, give it a moment before trying again
						Thread.Sleep(RetryWait[tries]);
					}
					Log.Write(String.Format("LedWiz {0} WriteUSB failed after {1} retries", Number, RetryWait.Length));
				}
				else
					Log.Write(String.Format("LedWiz {0} WriteUSB has no file handle", Number));
			}

            private bool IsPresent
            {
                get
                {
                    if (!Number.IsBetween(1, 16)) return false;
                    return deviceList.Any(d => d.unitNo == Number);
                }
            }


            public LedWizUnit(int Number)
            {
				// make sure the device list is loaded
				StartupLedWiz();

				// remember my unit number
				this.Number = Number;

				// If this device exists in the system, open a Win32 file handle
				// to its USB HID endpoint.  This allows us to send LWZ commands
				// simply by writing to the file handle.
				LWDEVICE d = deviceList.FirstOrDefault(dd => dd.unitNo == Number);

				// if we have a path, open the file
                if ((path = d.path) != null)
                    fp = HIDImports.CreateFile(
                        path, HIDImports.GENERIC_READ_WRITE, HIDImports.SHARE_READ_WRITE,
                        IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
            }
        }


        #endregion




    }
}
