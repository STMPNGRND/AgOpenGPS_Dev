﻿//Please, if you use this, share the improvements

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using SharpGL;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    //the main form object
    public partial class FormGPS : Form
    {
        #region // Class Props and instances
        //maximum sections available
        private const int MAXSECTIONS = 9;
        public const int MAXFUNCTIONS = 5;

        //some test variables
        //double testDouble = 0;
        //bool testBool = false;
        //int testInt = 0;

        //current directory of field;
        public string currentFieldDirectory = "", workingDirectory = "", vehiclefileName = "";

        //colors for sections and field background
        private byte redSections,grnSections,bluSections;
        public byte redField,grnField,bluField;

        //polygon mode for section drawing
        private bool isDrawPolygons;

        //flag for free drive window to control autosteer
        public bool isInFreeDriveMode;

        //Flag stuff
        public byte flagColor = 0;
        private bool leftMouseDownOnOpenGL = false; //mousedown event in opengl window
        private int flagNumberPicked = 0;

        //Is it in 2D or 3D, metric or imperial, display lightbar, display grid
        public bool isIn3D = true, isMetric = true, isLightbarOn = true, isGridOn, isSideGuideLines = true;
        public bool isPureDisplayOn = true;

        //bool for whether or not a job is active
        public bool isJobStarted = false, isAreaOnRight = true, isAutoSteerBtnOn = false;

        //master Manual and Auto, 3 states possible
        public enum btnStates {Off,Auto,On}
        public btnStates manualBtnState = btnStates.Off;
        public btnStates autoBtnState = btnStates.Off;

        //section button states
        public enum manBtn { Off, Auto, On }

        //if we are saving a file
        public bool isSavingFile = false, isLogNMEA = false;

        //Zoom variables
        public double gridZoom;
        public double zoomValue = 15;
        public double triangleResolution = 1.0;
        private double previousZoom = 25;
        public uint[] texture = new uint[3];

        //create the scene camera
        public CCamera camera = new CCamera();

        //create world grid
        public CWorldGrid worldGrid;

        //create instance of a stopwatch for timing of frames and NMEA hz determination
        private readonly Stopwatch swFrame = new Stopwatch();

        //Time to do fix position update and draw routine
        private double frameTime = 0;

        //For field saving in background
        private int saveCounter = 1;

        //used to update the screen status bar etc
        private int statusUpdateCounter = 1;
        private int fiveSecondCounter = 0;

        /// <summary>
        /// The NMEA class that decodes it
        /// </summary>
        public CNMEA pn;

        /// <summary>
        /// an array of sections, so far only 8 section + 1 fullWidth Section
        /// </summary>
        public CSection[] section = new CSection[MAXSECTIONS];

        /// <summary>
        /// AB Line object
        /// </summary>
        public CABLine ABLine;

        /// <summary>
        /// Contour Mode Instance
        /// </summary>
        public CContour ct;

        //Auto Headland Instance
        /// <summary>
        /// Auto Headland YouTurn
        /// </summary>
        public CYouTurn yt;

        /// <summary>
        /// Rate control Object
        /// </summary>
        public CRate rc;

        /// <summary>
        /// Our vehicle including the tool
        /// </summary>
        public CVehicle vehicle;

        /// <summary>
        /// All the structs for recv and send of information out ports
        /// </summary>
        public CModuleComm mc;

        /// <summary>
        /// perimeter object for area calc
        /// </summary>     
        public CPerimeter periArea;

        /// <summary>
        /// The outer boundary of the field
        /// </summary>
        public CBoundary boundz;

        /// <summary>
        /// The headland created
        /// </summary>
        public CHeadland hl;

        /// <summary>
        /// The entry and exit sequences, functions, actions
        /// </summary>
        public CSequence seq;

        /// <summary>
        /// The internal simulator
        /// </summary>
        public CSim sim;

        #endregion

        // Constructor, Initializes a new instance of the "FormGPS" class.
        public FormGPS()
        {
            //winform initialization
            InitializeComponent();

            //create a new section and set left and right positions
            //created whether used or not, saves restarting program
            for (int j = 0; j < MAXSECTIONS; j++) section[j] = new CSection(this);

            //  Get the OpenGL object.
            OpenGL gl = openGLControl.OpenGL;
            OpenGL glBack = openGLControlBack.OpenGL;

            //create the world grid
            worldGrid = new CWorldGrid(gl, this);

            //our vehicle made with gl object and pointer of mainform
            vehicle = new CVehicle(gl, this);

            //our NMEA parser
            pn = new CNMEA(this);

            //create the ABLine instance
            ABLine = new CABLine(gl, this);

            //new instance of contour mode
            ct = new CContour(gl, this);

            //new instance of auto headland turn
            yt = new CYouTurn(gl, this);

            //module communication
            mc = new CModuleComm(this);

            //perimeter list object
            periArea = new CPerimeter(gl);

            //boundary object
            boundz = new CBoundary(gl, glBack, this);

            //headland object
            hl = new CHeadland(gl, this);

            //rate object
            rc = new CRate(this);

            seq = new CSequence(this);

            sim = new CSim(this);

            //start the stopwatch
            swFrame.Start();
        }

        //keystrokes for easy and quick startup
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            //speed up
            if (keyData == Keys.K)
            {
                sim.stepDistance += 0.05;
                if (sim.stepDistance > 6) sim.stepDistance = 6;
                return true;
            }

            //slow down
            if (keyData == Keys.J)
            {
                sim.stepDistance -= 0.05;
                if (sim.stepDistance < 0) sim.stepDistance = 0;
                return true;
            }

            //turn right
            if (keyData == Keys.M)
            {
                sim.steerAngle += 0.5;
                if (sim.steerAngle > 30) sim.steerAngle = 30;
                sim.steerAngleScrollBar = sim.steerAngle;
                lblSteerAngle.Text = sim.steerAngle.ToString();
                tbarSteerAngle.Value = (int)(10 * sim.steerAngle);
                return true;
            }

            //turn left
            if (keyData == Keys.N)
            {
                sim.steerAngle -= 0.5;
                if (sim.steerAngle < -30) sim.steerAngle = -30;
                sim.steerAngleScrollBar = sim.steerAngle;
                lblSteerAngle.Text = sim.steerAngle.ToString();
                tbarSteerAngle.Value = (int)(10 * sim.steerAngle);
                return true;
            }

            if (keyData == Keys.Up)
            {
                sim.steerAngle = 0;
                sim.steerAngleScrollBar = sim.steerAngle;
                lblSteerAngle.Text = sim.steerAngle.ToString();
                tbarSteerAngle.Value = (int)(10 * sim.steerAngle);
                return true;
            }

            if (keyData == (Keys.F))
            {
                JobNewOpenResume();
                return true;    // indicate that you handled this keystroke
            }

            if (keyData == (Keys.NumPad1))
            {
                Form form = new FormGPSData(this);
                form.Show();
                return true;    // indicate that you handled this keystroke
            }

            if (keyData == (Keys.S))
            {
                SettingsPageOpen(0);
                return true;    // indicate that you handled this keystroke
            }

            if (keyData == (Keys.C))
            {
                SettingsCommunications();
                return true;    // indicate that you handled this keystroke
            }

            if (keyData == (Keys.A))
            {
                btnAutoSteer.PerformClick();
                return true;    // indicate that you handled this keystroke
            }

            if (keyData == (Keys.D))
            {
                btnSectionOffAutoOn.PerformClick();
                return true;    // indicate that you handled this keystroke
            }
            // Call the base class
            return base.ProcessCmdKey(ref msg, keyData);
        }

        //Initialize items before the form Loads or is visible
        private void FormGPS_Load(object sender, EventArgs e)
        {
            //tooltips of controls
            ToolTip ToolTip1 = new ToolTip();
            ToolTip1.SetToolTip(btnABLine, "Set and configure\n an ABLine");

            //get the working directory, if not exist, create
            workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                + "\\AgOpenGPS\\Fields\\";
            string dir = Path.GetDirectoryName(workingDirectory);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            { Directory.CreateDirectory(dir); }

            //set baud and port from last time run
            baudRateGPS = Settings.Default.setPort_baudRate;
            portNameGPS = Settings.Default.setPort_portNameGPS;

            //try and open
            SerialPortOpenGPS();

            //same for SectionRelay port
            portNameRelaySection = Settings.Default.setPort_portNameRateRelay;
            wasRateRelayConnectedLastRun = Settings.Default.setPort_wasRateRelayConnected;
            if (wasRateRelayConnectedLastRun) SerialPortRateRelayOpen();

            //same for AutoSteer port
            portNameAutoSteer = Settings.Default.setPort_portNameAutoSteer;
            wasAutoSteerConnectedLastRun = Settings.Default.setPort_wasAutoSteerConnected;
            if (wasAutoSteerConnectedLastRun) SerialPortAutoSteerOpen();

            //Set width of section and positions for each section
            SectionSetPosition();

            //Calculate total width and each section width
            SectionCalcWidths();

            //start udp server
            StartUDPServer();

            //set the correct zoom and grid
            camera.camSetDistance = zoomValue * zoomValue * -1;
            SetZoom();

            //which cam source is being used
            isAtanCam = Settings.Default.setCam_isAtanCam;

            //triangle resolution is how far to next triangle point trigger distance
            triangleResolution = Settings.Default.setDisplay_triangleResolution;

            //remembered window position
            if (Settings.Default.setWindow_Maximized)
            {
                WindowState = FormWindowState.Maximized;
                Location = Settings.Default.setWindow_Location;
                Size = Settings.Default.setWindow_Size;
            }
            else if (Settings.Default.setWindow_Minimized)
            {
                //WindowState = FormWindowState.Minimized;
                Location = Settings.Default.setWindow_Location;
                Size = Settings.Default.setWindow_Size;
            }
            else
            {
                Location = Settings.Default.setWindow_Location;
                Size = Settings.Default.setWindow_Size;
            }

            //don;t draw the back opengl to GDI - it still works tho
            openGLControlBack.Visible = false;

            //set previous job directory
            currentFieldDirectory = Settings.Default.setF_CurrentDir;
            vehiclefileName = Vehicle.Default.setVehicle_Name;

            //clear the flags
            flagPts.Clear();
            btnFlag.Enabled = false;

            //workswitch stuff
            mc.isWorkSwitchEnabled = Settings.Default.setF_IsWorkSwitchEnabled;
            mc.isWorkSwitchActiveLow = Settings.Default.setF_IsWorkSwitchActiveLow;

            minFixStepDist = Settings.Default.setF_minFixStep;

            pitchZero = Settings.Default.setIMU_pitchZero;
            rollZero = Settings.Default.setIMU_rollZero;

            totalUserSquareMeters = Settings.Default.setF_UserTotalArea;
            userSquareMetersAlarm = Settings.Default.setF_UserTripAlarm;

            //space between points while recording a boundary
            boundaryTriggerDistance = Settings.Default.setF_boundaryTriggerDistance;

            //load the last used auto turn shape
            string fileAndDir = @".\YouTurnShapes\" + Properties.Settings.Default.setAS_youTurnShape;
            yt.LoadYouTurnShapeFromFile(fileAndDir);

            sim.latitude = Settings.Default.setSim_lastLat;
            sim.longitude = Settings.Default.setSim_lastLong;

            // load all the gui elements in gui.designer.cs
            LoadGUI();
        }

        //form is closing so tidy up and save settings
        private void FormGPS_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Save, return, cancel save
            if (isJobStarted)
            {
                int choice = SaveOrNot();

                switch (choice)
                {
                    //OK
                    case 0:
                        isSendConnected = false;
                        //sendSocket.Shutdown(SocketShutdown.Both);
                        //recvSocket.Shutdown(SocketShutdown.Both);

                        Settings.Default.setSim_lastLong = pn.longitude;
                        Settings.Default.setSim_lastLat = pn.latitude;
                        Settings.Default.setF_CurrentDir = currentFieldDirectory;
                        Settings.Default.Save();

                        FileSaveEverythingBeforeClosingField();

                        //shutdown and reset all module data
                        mc.ResetAllModuleCommValues();

                        //sendSocket.Disconnect(true);
                        //recvSocket.Disconnect(true);

                        break;

                    //Ignore and return
                    case 1:
                        e.Cancel = true;
                        break;
                }
            }

            //save window settings
             if (WindowState == FormWindowState.Maximized)
            {
                Settings.Default.setWindow_Location = RestoreBounds.Location;
                Settings.Default.setWindow_Size = RestoreBounds.Size;
                Settings.Default.setWindow_Maximized = true;
                Settings.Default.setWindow_Minimized = false;
            }
            else if (WindowState == FormWindowState.Normal)
            {
                Settings.Default.setWindow_Location = Location;
                Settings.Default.setWindow_Size = Size;
                Settings.Default.setWindow_Maximized = false;
                Settings.Default.setWindow_Minimized = false;
            }
            else
            {
                Settings.Default.setWindow_Location = RestoreBounds.Location;
                Settings.Default.setWindow_Size = RestoreBounds.Size;
                Settings.Default.setWindow_Maximized = false;
                Settings.Default.setWindow_Minimized = true;
            }

            Settings.Default.setCam_pitch = camera.camPitch;
            Settings.Default.setF_UserTotalArea = totalUserSquareMeters;
            Settings.Default.setF_UserTripAlarm = userSquareMetersAlarm;

            Settings.Default.Save();
       }

        //called everytime window is resized, clean up button positions
        private void FormGPS_Resize(object sender, EventArgs e)
        {
            LineUpManualBtns();
        }

        // Procedures and Functions ---------------------------------------
        public uint LoadGLTextures()
        {
            OpenGL gl = openGLControl.OpenGL;
            //try
            //{
            //    particleTexture = new Texture();
            //    particleTexture.Create(gl, @".\Dependencies\Compass.png");
            //    texture[0] = particleTexture.TextureName;
            //}

            //catch (System.Exception excep)
            //{
            //    MessageBox.Show("Texture File Compass.png is Missing",excep.Message);
            //}

            try
            {
                //  Background
                ParticleTexture = new Texture();
                ParticleTexture.Create(gl, @".\Dependencies\landscape.png");
                texture[0] = ParticleTexture.TextureName;
            }
            catch (Exception e)
            {
                WriteErrorLog("Loading Landscape Textures" + e);
                MessageBox.Show("Texture File LANDSCAPE.PNG is Missing", e.Message);
            }

            try
            {
                //  Floor
                ParticleTexture = new Texture();
                ParticleTexture.Create(gl, @".\Dependencies\Floor.png");
                texture[1] = ParticleTexture.TextureName;
            }
            catch (Exception e)
            {
                WriteErrorLog("Loading Floor Texture" + e);
               MessageBox.Show("Texture File FLOOR.PNG is Missing", e.Message);
             }

            return texture[0];
        }// Load Bitmaps And Convert To Textures

        //start the UDP server
        private void StartUDPServer()
        {
            try
            {
                // Initialise the delegate which updates the message received
                updateRecvMessageDelegate = UpdateRecvMessage;

                // Initialise the socket
                sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                recvSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                // Initialise the IPEndPoint for the server and listen on port 9999
                IPEndPoint recv = new IPEndPoint(IPAddress.Any, Properties.Settings.Default.setIP_thisPort);

                // Associate the socket with this IP address and port
                recvSocket.Bind(recv);

                // Initialise the send socket
                sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                // Initialise the IPEndPoint for the server to send on port 9998
                IPEndPoint server = new IPEndPoint(IPAddress.Any, 9998);
                sendSocket.Bind(server);

                //IP address and port of Auto Steer server
                IPAddress epIP = IPAddress.Parse(Properties.Settings.Default.setIP_autoSteerIP);
                epAutoSteer = new IPEndPoint(epIP, Properties.Settings.Default.setIP_autoSteerPort);

                // Initialise the IPEndPoint for the client - async listner client only!
                EndPoint client = new IPEndPoint(IPAddress.Any, 0);

                // Start listening for incoming data
                recvSocket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None,
                                                ref client, ReceiveData, recvSocket);
                isSendConnected = true;
            }
            catch (Exception e)
            {
                WriteErrorLog("UDP Server" + e);
                MessageBox.Show("Load Error: " + e.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //dialog for requesting user to save or cancel
        public int SaveOrNot()
        {
            using (var form = new FormSaveOrNot())
            {
                var result = form.ShowDialog();

                if (result == DialogResult.OK) return 0;
                if (result == DialogResult.Ignore) return 1;
                if (result == DialogResult.Cancel) return 2;
                return 3;
            }
        }

        //show the communications window
        private void SettingsCommunications()
        {
            using (var form = new FormCommSet(this))
            {
                var result = form.ShowDialog();
                if (result == DialogResult.OK)
                {
                    fixUpdateTime = 1 / (double)fixUpdateHz;
                }
            }

            AutoSteerSettingsOutToPort();
        }

        private void timerSim_Tick(object sender, EventArgs e)
        {
            //if a GPS is connected disable sim
            if (!sp.IsOpen)
            {
                if (isAutoSteerBtnOn) sim.DoSimTick(guidanceLineSteerAngle / 10.0);
                else sim.DoSimTick(sim.steerAngleScrollBar);
            }
        }

        private void tbarStepDistance_Scroll(object sender, EventArgs e)
        {
            sim.stepDistance = ((double)(tbarStepDistance.Value)) / 10.0 / (double)fixUpdateHz;
        }

        private void tbarSteerAngle_Scroll(object sender, EventArgs e)
        {
            sim.steerAngleScrollBar = (tbarSteerAngle.Value) * 0.1;
            lblSteerAngle.Text = sim.steerAngleScrollBar.ToString("N1");
        }

        private void btnResetSteerAngle_Click(object sender, EventArgs e)
        {
            sim.steerAngleScrollBar = 0;
            tbarSteerAngle.Value = 0;
            lblSteerAngle.Text = sim.steerAngleScrollBar.ToString("N1");
        }

        private void btnResetSim_Click(object sender, EventArgs e)
        {
            sim.latitude = 53.436026;
            sim.longitude = -111.160047;
        }

        //show the UDP ethernet settings page
        private void SettingsUDP()
        {
            using (var form = new FormUDP(this))
            {
                var result = form.ShowDialog();
                if (result == DialogResult.OK)
                {
                    //Clicked Save
                }
                else
                {
                    //Clicked X - No Save
                }
            }
        }

        //function to set section positions
        public void SectionSetPosition()
        {
            section[0].positionLeft = (double)Vehicle.Default.setSection_position1 + Vehicle.Default.setVehicle_toolOffset;
            section[0].positionRight = (double)Vehicle.Default.setSection_position2 + Vehicle.Default.setVehicle_toolOffset;

            section[1].positionLeft = (double)Vehicle.Default.setSection_position2 + Vehicle.Default.setVehicle_toolOffset;
            section[1].positionRight = (double)Vehicle.Default.setSection_position3 + Vehicle.Default.setVehicle_toolOffset;

            section[2].positionLeft = (double)Vehicle.Default.setSection_position3 + Vehicle.Default.setVehicle_toolOffset;
            section[2].positionRight = (double)Vehicle.Default.setSection_position4 + Vehicle.Default.setVehicle_toolOffset;

            section[3].positionLeft = (double)Vehicle.Default.setSection_position4 + Vehicle.Default.setVehicle_toolOffset;
            section[3].positionRight = (double)Vehicle.Default.setSection_position5 + Vehicle.Default.setVehicle_toolOffset;

            section[4].positionLeft = (double)Vehicle.Default.setSection_position5 + Vehicle.Default.setVehicle_toolOffset;
            section[4].positionRight = (double)Vehicle.Default.setSection_position6 + Vehicle.Default.setVehicle_toolOffset;

            section[5].positionLeft = (double)Vehicle.Default.setSection_position6 + Vehicle.Default.setVehicle_toolOffset;
            section[5].positionRight = (double)Vehicle.Default.setSection_position7 + Vehicle.Default.setVehicle_toolOffset;

            section[6].positionLeft = (double)Vehicle.Default.setSection_position7 + Vehicle.Default.setVehicle_toolOffset;
            section[6].positionRight = (double)Vehicle.Default.setSection_position8 + Vehicle.Default.setVehicle_toolOffset;

            section[7].positionLeft = (double)Vehicle.Default.setSection_position8 + Vehicle.Default.setVehicle_toolOffset;
            section[7].positionRight = (double)Vehicle.Default.setSection_position9 + Vehicle.Default.setVehicle_toolOffset;
        }

        //function to calculate the width of each section and update
        public void SectionCalcWidths()
        {
            for (int j = 0; j < MAXSECTIONS; j++)
            {
                section[j].sectionWidth = (section[j].positionRight - section[j].positionLeft);
                section[j].rpSectionPosition = 200 + (int)(Math.Round(section[j].positionLeft * 10, 0, MidpointRounding.AwayFromZero));
                section[j].rpSectionWidth = (int)(Math.Round(section[j].sectionWidth * 10, 0, MidpointRounding.AwayFromZero));
            }

            //calculate tool width based on extreme right and left values
            vehicle.toolWidth = Math.Abs(section[0].positionLeft) + Math.Abs(section[vehicle.numOfSections - 1].positionRight);

            //left and right tool position
            vehicle.toolFarLeftPosition = section[0].positionLeft;
            vehicle.toolFarRightPosition = section[vehicle.numOfSections - 1].positionRight;

            //now do the full width section
            section[vehicle.numOfSections].sectionWidth = vehicle.toolWidth;
            section[vehicle.numOfSections].positionLeft = vehicle.toolFarLeftPosition;
            section[vehicle.numOfSections].positionRight = vehicle.toolFarRightPosition;

            //find the right side pixel position
            vehicle.rpXPosition = 200 + (int)(Math.Round(vehicle.toolFarLeftPosition * 10, 0, MidpointRounding.AwayFromZero));
            vehicle.rpWidth = (int)(Math.Round(vehicle.toolWidth * 10, 0, MidpointRounding.AwayFromZero));
        }

        //request a new job
        public void JobNew()
        {
            AutoSteerSettingsOutToPort();
            isJobStarted = true;
            startCounter = 0;

            btnManualOffOn.Enabled = true;
            manualBtnState = btnStates.Off;
            btnManualOffOn.Image = Properties.Resources.ManualOff;

            btnSectionOffAutoOn.Enabled = true;
            autoBtnState = btnStates.Off;
            btnSectionOffAutoOn.Image = Properties.Resources.SectionMasterOff;

            btnABLine.Enabled = true;
            btnContour.Enabled = true;
            btnAutoSteer.Enabled = true;
            btnSnap.Enabled = true;
            ABLine.abHeading = 0.00;

            btnRightYouTurn.Enabled = false;
            btnLeftYouTurn.Enabled = false;
            btnFlag.Enabled = true;

            LineUpManualBtns();

            //update the menu
            fieldToolStripMenuItem.Text = "Close Field";
        }

        //close the current job
        public void JobClose()
        {
            //turn auto button off
            autoBtnState = btnStates.Off;
            btnSectionOffAutoOn.Image = Properties.Resources.SectionMasterOff;

            //turn section buttons all OFF and zero square meters
            for (int j = 0; j < MAXSECTIONS; j++)
            {
                section[j].isAllowedOn = false;
                section[j].manBtnState = manBtn.On;
             }

            //turn manual button off
            manualBtnState = btnStates.Off;
            btnManualOffOn.Image = Properties.Resources.ManualOff;

            //Update the button colors and text
            ManualAllBtnsUpdate();

            //enable disable manual buttons
            LineUpManualBtns();

            btnSection1Man.Enabled = false;
            btnSection2Man.Enabled = false;
            btnSection3Man.Enabled = false;
            btnSection4Man.Enabled = false;
            btnSection5Man.Enabled = false;
            btnSection6Man.Enabled = false;
            btnSection7Man.Enabled = false;
            btnSection8Man.Enabled = false;

            //job is closed
            isJobStarted = false;

            //clear the section lists
            for (int j = 0; j < MAXSECTIONS; j++)
            {
                //clean out the lists
                section[j].patchList.Clear();
#pragma warning disable RCS1146 // Use conditional access.
                if (section[j].triangleList != null) section[j].triangleList.Clear();
#pragma warning restore RCS1146 // Use conditional access.
            }

            //clear out the contour Lists
            ct.ResetContour();

            //clear the flags
            flagPts.Clear();
            btnFlag.Enabled = false;

            //reset the buttons
            btnABLine.Enabled = false;
            btnContour.Enabled = false;
            btnAutoSteer.Enabled = false;
            btnSnap.Enabled = false;
            isAutoSteerBtnOn = false;

            ct.isContourBtnOn = false;
            ct.isContourOn = false;

            //change images to reflect on off
            btnABLine.Image = Properties.Resources.ABLineOff;
            btnRightYouTurn.Visible = false;
            btnLeftYouTurn.Visible = false;
            btnContour.Image = Properties.Resources.ContourOff;
            btnAutoSteer.Image = Properties.Resources.AutoSteerOff;

            //fix ManualOffOnAuto buttons
            btnManualOffOn.Enabled = false;
            manualBtnState = btnStates.Off;
            btnManualOffOn.Image = Properties.Resources.ManualOff;

            //fix auto button
            btnSectionOffAutoOn.Enabled = false;
            autoBtnState = btnStates.Off;
            btnSectionOffAutoOn.Image = Properties.Resources.SectionMasterOff;

            //reset all the ABLine stuff
            ABLine.ResetABLine();

            //reset acre and distance counters
            totalSquareMeters = 0;

            //reset boundary
            boundz.ResetBoundary();

            //reset headland
            hl.ResetHeadland();

            //update the menu
            fieldToolStripMenuItem.Text = "Start Field";

            //rate control buttons
            btnRate1Select.Visible = false;
            btnRate2Select.Visible = false;
            btnRate.Image = Properties.Resources.RateControlOff;
            rc.ShutdownRateControl();

            //turn off top level buttons
            btnRightYouTurn.Enabled = false;
            btnLeftYouTurn.Enabled = false;

            //auto YouTurn shutdown
            yt.isYouTurnBtnOn = false;
            yt.ResetYouTurnAndSequenceEvents();
            youTurnProgressBar = 0;

            //turn off youturn...
            btnEnableAutoYouTurn.Enabled = false;
            yt.isYouTurnBtnOn = false;
            btnEnableAutoYouTurn.Image = Properties.Resources.YouTurnNo;

            //reset all Port Module values
            mc.ResetAllModuleCommValues();
        }

        //bring up field dialog for new/open/resume
        private void JobNewOpenResume()
        {
            //bring up dialog if no job active, close job if one is

            if (!isJobStarted)
            {
                if (stripOnlineGPS.Value == 1)
                {
                    var form = new FormTimedMessage(3000, "No GPS", "Is your GPS source off?");
                    form.Show();
                    return;
                }

                using (var form = new FormJob(this))
                {
                    var result = form.ShowDialog();
                    if (result == DialogResult.Yes)
                    {
                        //ask for a directory name
                        using (var form2 = new FormFieldDir(this))
                        { form2.ShowDialog(); }
                    }
                }

                Text = "AgOpenGPS - " + currentFieldDirectory;
            }

            //close the current job and ask how to or if to save
            else
            {
                int choice = SaveOrNot();
                switch (choice)
                {
                    //OK
                    case 0:
                        Settings.Default.setF_CurrentDir = currentFieldDirectory;
                        Settings.Default.Save();
                        FileSaveEverythingBeforeClosingField();
                        break;
                    //Ignore and return
                    case 1:
                        break;
                }
            }
        }

        //Does the logic to process section on off requests
        private void ProcessSectionOnOffRequests()
        {
            //if (pn.speed > 0.2)
            {
                for (int j = 0; j < vehicle.numOfSections+1; j++)
                {
                    //Turn ON
                    //if requested to be on, set the timer to Max 10 (1 seconds) = 10 frames per second
                    if (section[j].sectionOnRequest && !section[j].sectionOnOffCycle)
                    {
                        section[j].sectionOnTimer = (int)(pn.speed * vehicle.toolLookAhead)+1;
                        if (section[j].sectionOnTimer > fixUpdateHz+3) section[j].sectionOnTimer = fixUpdateHz+3;
                        section[j].sectionOnOffCycle = true;
                    }

                    //reset the ON request
                    section[j].sectionOnRequest = false;

                    //decrement the timer if not zero
                    if (section[j].sectionOnTimer > 0)
                    {
                        //turn the section ON if not and decrement timer
                        section[j].sectionOnTimer--;
                        if (!section[j].isSectionOn) section[j].TurnSectionOn();

                        //keep resetting the section OFF timer while the ON is active
                        section[j].sectionOffTimer = (int)(fixUpdateHz * vehicle.toolTurnOffDelay);
                    }

                    if (!section[j].sectionOffRequest) section[j].sectionOffTimer = (int)(fixUpdateHz * vehicle.toolTurnOffDelay);

                    //decrement the off timer
                    if (section[j].sectionOffTimer > 0) section[j].sectionOffTimer--;

                    //Turn OFF
                    //if Off section timer is zero, turn off the section
                    if (section[j].sectionOffTimer == 0 && section[j].sectionOnTimer == 0 && section[j].sectionOffRequest)
                    {
                        if (section[j].isSectionOn) section[j].TurnSectionOff();
                        section[j].sectionOnOffCycle = false;
                        section[j].sectionOffRequest = false;
                    }
                }
            }
        }

        //take the distance from object and convert to camera data
        private void SetZoom()
        {
            //match grid to cam distance and redo perspective
            if (camera.camSetDistance <= -20000) gridZoom = 2000;
            if (camera.camSetDistance >= -20000 && camera.camSetDistance < -10000) gridZoom =   2000;
            if (camera.camSetDistance >= -10000 && camera.camSetDistance < -5000) gridZoom =    1000;
            if (camera.camSetDistance >= -5000 && camera.camSetDistance < -2000) gridZoom =     503;
            if (camera.camSetDistance >= -2000 && camera.camSetDistance < -1000) gridZoom =     201.2;
            if (camera.camSetDistance >= -1000 && camera.camSetDistance < -500) gridZoom =      100.6;
            if (camera.camSetDistance >= -500 && camera.camSetDistance < -250) gridZoom =       50.3;
            if (camera.camSetDistance >= -250 && camera.camSetDistance < -150) gridZoom =       25.15;
            if (camera.camSetDistance >= -150 && camera.camSetDistance < -50) gridZoom =         10.06;
            if (camera.camSetDistance >= -50 && camera.camSetDistance < -1) gridZoom = 5.03;
            //1.216 2.532

            //  Get the OpenGL object.
            OpenGL gl = openGLControl.OpenGL;

            //  Set the projection matrix.
            gl.MatrixMode(OpenGL.GL_PROJECTION);

            //  Load the identity.
            gl.LoadIdentity();

            //  Create a perspective transformation.
            gl.Perspective(fovy, openGLControl.Width / (double)openGLControl.Height, 1, camDistanceFactor * camera.camSetDistance);

            //  Set the modelview matrix.
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
        }

        //All the files that need to be saved when closing field or app
        private void FileSaveEverythingBeforeClosingField()
        {
            //turn off contour line if on
            if (ct.isContourOn) ct.StopContourLine();

            //turn off all the sections
            for (int j = 0; j < vehicle.numOfSections + 1; j++)
            {
                if (section[j].isSectionOn) section[j].TurnSectionOff();
                section[j].sectionOnOffCycle = false;
                section[j].sectionOffRequest = false;
            }

            FileSaveHeadlandYouTurn();
            FileSaveOuterBoundary();
            FileSaveField();
            FileSaveContour();
            FileSaveFlagsKML();

            JobClose();
            Text = "AgOpenGPS";
        }

        //function called by menu items to delete a selected flag
        private void DeleteSelectedFlag()
        {
            //delete selected flag and set selected to none
            flagPts.RemoveAt(flagNumberPicked - 1);
            flagNumberPicked = 0;

            // re-sort the id's based on how many flags left
            int flagCnt = flagPts.Count;
            if (flagCnt > 0)
            {
                for (int i = 0; i < flagCnt; i++) flagPts[i].ID = i + 1;
            }
        }

        //an error log called by all try catches
        public void WriteErrorLog(string strErrorText)
        {
            try
            {
                //set up file and folder if it doesn't exist
                const string strFileName = "Error Log.txt";
                string strPath = Application.StartupPath;

                //Write out the error appending to existing
                File.AppendAllText(strPath + "\\" + strFileName, strErrorText + " - " +
                    DateTime.Now.ToString() + "\r\n\r\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error in WriteErrorLog: " + ex.Message, "Error Logging", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        //called by you turn class to do the actual sequence events
        public void DoYouTurnSequenceEvent(int function, int action)
        {
            switch (function)
            {
                case 0: //should not be here - it means no function at all
                    TimedMessageBox(2000, "ID 0 ??????", "YouTurn fucked up");
                    break;

                case 1: //Manual button
                    if (action == 0) //turn auto off
                    {
                        if (manualBtnState != btnStates.Off)
                        {
                            btnManualOffOn.PerformClick();
                        }
                    }
                    else
                    {
                        if (manualBtnState != btnStates.On)
                        {
                            btnManualOffOn.PerformClick();
                        }
                    }
                    break;

                case 2: //Auto Button
                    if (action == 0) //turn auto off
                    {
                        if (autoBtnState != btnStates.Off)
                        {
                            btnSectionOffAutoOn.PerformClick();
                        }
                    }
                    else
                    {
                        if (autoBtnState != btnStates.Auto)
                        {
                            btnSectionOffAutoOn.PerformClick();
                        }
                    }
                    break;

                case 3: //Relay 1
                    if (action == 0)
                    {
                        TimedMessageBox(1000, "Relay 1 ", "Turn Off");
                    }
                    else
                    {
                        TimedMessageBox(1000, "Relay 1 ", "Turn On");
                    }
                    break;

                case 4: //Relay 2
                    if (action == 0)
                    {
                        TimedMessageBox(1000, "Relay 2 ", "Turn Off");
                    }
                    else
                    {
                        TimedMessageBox(1000, "Relay 2 ", "Turn On");
                    }
                    break;

                case 5: //thingy
                    if (action == 0)
                    {
                        TimedMessageBox(1000, "Thingy ", "Turn Off");
                    }
                    else
                    {
                        TimedMessageBox(1000, "Thingy ", "Turn On");
                    }
                    break;

                default:
                    break;
            }
        }

        public void TimedMessageBox(int timeout, string s1, string s2)
        {
            var form = new FormTimedMessage(timeout, s1, s2);
            form.Show();
        }
   }//class FormGPS
}//namespace AgOpenGPS

        /*The order is:
         * 
         * The watchdog timer times out and runs this function tmrWatchdog_tick().
         * 50 times per second so statusUpdateCounter counts to 25 and updates strip menu etc at 2 hz
         * it also makes sure there is new sentences showing up otherwise it shows **** No GGA....
         * saveCounter ticks 2 x per second, used at end of draw routine every minute to save a backup of field
         * then ScanForNMEA function checks for a complete sentence if contained in pn.rawbuffer
         * if not it comes right back and waits for next watchdog trigger and starts all over
         * if a new sentence is there, UpdateFix() is called
         * Right away CalculateLookAhead(), no skips, is called to determine lookaheads and trigger distances to save triangles plotted
         * Then UpdateFix() continues. 
         * Hitch, pivot, antenna locations etc and directions are figured out if trigDistance is triggered
         * When that is done, DoRender() is called on the visible OpenGL screen and its draw routine _draw is run
         * before triangles are drawn, frustum cull figures out how many of the triangles should be drawn 
         * When its all the way thru, it triggers the sectioncontrol Draw, its frustum cull, and determines if sections should be on
         * ProcessSectionOnOffRequests() runs and that does the section on off magic
         * SectionControlToArduino() runs and spits out the port relay control based on sections on or off
         * If field needs saving (1.5 minute since last time) field is saved
         * Now the program is "Done" and waits for the next watchdog trigger, determines if a new sentence is available etc
         * and starts all over from the top. 
         */



