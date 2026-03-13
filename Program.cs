using Microsoft.FlightSimulator.SimConnect;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SimConnectBridge
{
    // Dummy enum required for GroupID (MSFS managed DLL does NOT include this)
    public enum GROUP_ID
    {
        GROUP0 = 0
    }

    // All events used by Switch, Multi, and Radio panels
    public enum EVENT_ID
    {
        // Switch Panel
        GEAR_TOGGLE, // keep for compatibility
        GEAR_UP,
        GEAR_DOWN,

        LANDING_LIGHTS_ON,
        LANDING_LIGHTS_OFF,

        BEACON_LIGHTS_ON,
        BEACON_LIGHTS_OFF,

        NAV_LIGHTS_ON,
        NAV_LIGHTS_OFF,

        PANEL_LIGHTS_ON,
        PANEL_LIGHTS_OFF,

        STROBES_ON,
        STROBES_OFF,

        TAXI_LIGHTS_ON,
        TAXI_LIGHTS_OFF,

        MAGNETO_OFF,
        MAGNETO_RIGHT,
        MAGNETO_LEFT,
        MAGNETO_BOTH,
        MAGNETO_START,

        MASTER_BATTERY_ON,
        MASTER_BATTERY_OFF,
        BATTERY1_SET,
        BATTERY2_SET,

        ALTERNATOR_ON,
        ALTERNATOR_OFF,
        ALTERNATOR1_SET,
        ALTERNATOR2_SET,

        AVIONICS_MASTER_1_ON,
        AVIONICS_MASTER_1_OFF,

        FUELSYSTEM_PUMP_ON,
        FUELSYSTEM_PUMP_OFF,

        ANTI_ICE_ON,
        ANTI_ICE_OFF,

        PITOT_HEAT_ON,
        PITOT_HEAT_OFF,

        COWLFLAP_CLOSE,
        COWLFLAP_OPEN,

        // Multi Panel Controls
        ELEV_TRIM_UP,
        ELEV_TRIM_DN,
        FLAPS_INCR,
        FLAPS_DECR,
        AP_MASTER,

        // Multi Panel Autopilot modes & rotary
        AP_HDG_HOLD,
        AP_NAV1_HOLD,
        AP_AIRSPEED_HOLD,
        AP_ALT_HOLD,
        AP_VS_HOLD,
        AP_APR_HOLD,
        AP_BACKCOURSE_HOLD,
        AUTO_THROTTLE_ARM,

        HEADING_BUG_INC,
        HEADING_BUG_DEC,
        VOR1_OBI_INC,
        VOR1_OBI_DEC,
        AP_SPD_VAR_INC,
        AP_SPD_VAR_DEC,
        AP_ALT_VAR_INC,
        AP_ALT_VAR_DEC,
        AP_VS_VAR_INC,
        AP_VS_VAR_DEC,

        // Radio Panel events
        COM1_WHOLE_INC,
        COM1_WHOLE_DEC,
        COM1_FRACT_INC,
        COM1_FRACT_DEC,
        COM1_STBY_SWAP,

        COM2_WHOLE_INC,
        COM2_WHOLE_DEC,
        COM2_FRACT_INC,
        COM2_FRACT_DEC,
        COM2_STBY_SWAP,

        NAV1_WHOLE_INC,
        NAV1_WHOLE_DEC,
        NAV1_FRACT_INC,
        NAV1_FRACT_DEC,
        NAV1_STBY_SWAP,

        NAV2_WHOLE_INC,
        NAV2_WHOLE_DEC,
        NAV2_FRACT_INC,
        NAV2_FRACT_DEC,
        NAV2_STBY_SWAP,

        //ADF_100_INC,
        //ADF_100_DEC,
        ADF_WHOLE_INC,
        ADF_WHOLE_DEC,
        ADF_FRACT_INC,
        ADF_FRACT_DEC,
        ADF_STBY_SWAP,

        XPDR_1000_INC,
        XPDR_1000_DEC,
        XPDR_100_INC,
        XPDR_100_DEC,
        XPDR_10_INC,
        XPDR_10_DEC,
        XPDR_1_INC,
        XPDR_1_DEC
    }

    // Data definition groups
    public enum DATA_DEFINITION
    {
        AircraftData,
        AutopilotData,
        RadioData,
        GearData
    }

    // Request IDs
    public enum REQUEST_ID
    {
        AircraftDataRequest,
        AutopilotDataRequest,
        RadioDataRequest,
        GearDataRequest
    }

    // Aircraft simvars
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct AircraftData
    {
        public double Altitude;      // feet
        public double Airspeed;      // knots
        public double Heading;       // degrees (true)
        public double VerticalSpeed; // feet per minute
    }
    // Gear display simvars
    public struct GearData
    {
        public double Left;
        public double Center;
        public double Right;
    }

    // Autopilot display simvars (extended with boolean flags for LEDs)
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct AutopilotData
    {
        public double AltitudeLock;     // AUTOPILOT ALTITUDE LOCK VAR (feet)
        public double VerticalSpeed;    // AUTOPILOT VERTICAL HOLD VAR (ft/min)
        public double AirspeedHold;     // AUTOPILOT AIRSPEED HOLD VAR (knots)
        public double HeadingLock;      // AUTOPILOT HEADING LOCK DIR (degrees)
        public double Course;           // NAV OBS:1 (degrees)

        // Existing AP master (0/1)
        public int ApMaster;

        // New boolean flags (0 = off, 1 = on)
        public int ApHdgHold;           // AUTOPILOT HEADING HOLD (bool)
        public int ApNav1Hold;          // AUTOPILOT NAV1 HOLD (bool)
        public int ApAltHold;           // AUTOPILOT ALTITUDE HOLD (bool)
        public int ApVsHold;            // AUTOPILOT VERTICAL SPEED HOLD (bool)
        public int ApAirspeedHoldFlag;  // AUTOPILOT FLIGHT LEVEL CHANGE (bool)
        public int ApAprHold;           // AUTOPILOT APPROACH HOLD (bool)
        public int ApBackcourseHold;    // AUTOPILOT BACKCOURSE HOLD (bool)
    }

    // Radio panel simvars
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct RadioData
    {
        public double Com1Active;
        public double Com1Standby;

        public double Com2Active;
        public double Com2Standby;

        public double Nav1Active;
        public double Nav1Standby;

        public double Nav2Active;
        public double Nav2Standby;

        public double AdfActive;
        public double AdfStandby;

        public double XpdrCode;

        public double Dme1Distance;
        public double Dme1Speed;
        public double Dme1Time;
    }

    // JSON command packet
    public class CommandPacket
    {
        public string? cmd { get; set; }
        public uint? data { get; set; }
    }

    class Program
    {
        private const int WM_USER_SIMCONNECT = 0x0402;
        private static SimConnect? simconnect;
        private static TcpCommandServer? server;

        // Mirror of MapClientEventToSimEvent for logging and lookup
        static readonly Dictionary<EVENT_ID, string> EventNameMap = new Dictionary<EVENT_ID, string>();

        static void PopulateEventNameMap()
        {
            // COM1
            EventNameMap[EVENT_ID.COM1_WHOLE_INC] = "COM_RADIO_WHOLE_INC";
            EventNameMap[EVENT_ID.COM1_WHOLE_DEC] = "COM_RADIO_WHOLE_DEC";
            EventNameMap[EVENT_ID.COM1_FRACT_INC] = "COM_RADIO_FRACT_INC";
            EventNameMap[EVENT_ID.COM1_FRACT_DEC] = "COM_RADIO_FRACT_DEC";
            EventNameMap[EVENT_ID.COM1_STBY_SWAP] = "COM_STBY_RADIO_SWAP";

            // COM2
            EventNameMap[EVENT_ID.COM2_WHOLE_INC] = "COM2_RADIO_WHOLE_INC";
            EventNameMap[EVENT_ID.COM2_WHOLE_DEC] = "COM2_RADIO_WHOLE_DEC";
            EventNameMap[EVENT_ID.COM2_FRACT_INC] = "COM2_RADIO_FRACT_INC";
            EventNameMap[EVENT_ID.COM2_FRACT_DEC] = "COM2_RADIO_FRACT_DEC";
            EventNameMap[EVENT_ID.COM2_STBY_SWAP] = "COM2_RADIO_SWAP";

            // NAV1
            EventNameMap[EVENT_ID.NAV1_WHOLE_INC] = "NAV1_RADIO_WHOLE_INC";
            EventNameMap[EVENT_ID.NAV1_WHOLE_DEC] = "NAV1_RADIO_WHOLE_DEC";
            EventNameMap[EVENT_ID.NAV1_FRACT_INC] = "NAV1_RADIO_FRACT_INC";
            EventNameMap[EVENT_ID.NAV1_FRACT_DEC] = "NAV1_RADIO_FRACT_DEC";
            EventNameMap[EVENT_ID.NAV1_STBY_SWAP] = "NAV1_RADIO_SWAP";

            // NAV2
            EventNameMap[EVENT_ID.NAV2_WHOLE_INC] = "NAV2_RADIO_WHOLE_INC";
            EventNameMap[EVENT_ID.NAV2_WHOLE_DEC] = "NAV2_RADIO_WHOLE_DEC";
            EventNameMap[EVENT_ID.NAV2_FRACT_INC] = "NAV2_RADIO_FRACT_INC";
            EventNameMap[EVENT_ID.NAV2_FRACT_DEC] = "NAV2_RADIO_FRACT_DEC";
            EventNameMap[EVENT_ID.NAV2_STBY_SWAP] = "NAV2_RADIO_SWAP";

            // ADF
            //EventNameMap[EVENT_ID.ADF_100_INC] = "ADF_100_INC";
            //EventNameMap[EVENT_ID.ADF_100_DEC] = "ADF_100_DEC";
            EventNameMap[EVENT_ID.ADF_WHOLE_INC] = "ADF_100_INC";
            EventNameMap[EVENT_ID.ADF_WHOLE_DEC] = "ADF_100_DEC";
            EventNameMap[EVENT_ID.ADF_FRACT_INC] = "ADF1_WHOLE_INC";
            EventNameMap[EVENT_ID.ADF_FRACT_DEC] = "ADF1_WHOLE_DEC";
            EventNameMap[EVENT_ID.ADF_STBY_SWAP] = "ADF1_RADIO_SWAP";

            // XPDR (fixed typos)
            EventNameMap[EVENT_ID.XPDR_1000_INC] = "XPNDR_1000_INC";
            EventNameMap[EVENT_ID.XPDR_1000_DEC] = "XPNDR_1000_DEC";
            EventNameMap[EVENT_ID.XPDR_100_INC] = "XPNDR_100_INC";
            EventNameMap[EVENT_ID.XPDR_100_DEC] = "XPNDR_100_DEC";
            EventNameMap[EVENT_ID.XPDR_10_INC] = "XPNDR_10_INC";
            EventNameMap[EVENT_ID.XPDR_10_DEC] = "XPNDR_10_DEC";
            EventNameMap[EVENT_ID.XPDR_1_INC] = "XPNDR_1_INC";
            EventNameMap[EVENT_ID.XPDR_1_DEC] = "XPNDR_1_DEC";

            // Add any other mappings you use (switch/multi panel events) if you want them logged too.
        }

        static void Main(string[] args)
        {
            Console.WriteLine("SimConnect Event + TCP + SimVar Bridge");
            Console.WriteLine("Connecting to MSFS...");

            IntPtr hwnd = IntPtr.Zero; // Proton-safe

            try
            {
                simconnect = new SimConnect(
                    "SimConnect TCP Bridge",
                    hwnd,
                    WM_USER_SIMCONNECT,
                    null,
                    0
                );

                simconnect.OnRecvOpen += OnRecvOpen;
                simconnect.OnRecvQuit += OnRecvQuit;
                simconnect.OnRecvException += OnRecvException;
                simconnect.OnRecvSimobjectData += OnRecvSimobjectData;

                // Message pump
                while (true)
                {
                    simconnect.ReceiveMessage();
                    System.Threading.Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Connection failed: " + ex.Message);
            }
        }

        private static void OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Console.WriteLine("Connected to MSFS!");

            // --- Map events to SimConnect names ---

            // Switch Panel
            sender.MapClientEventToSimEvent(EVENT_ID.GEAR_UP, "GEAR_UP");
            sender.MapClientEventToSimEvent(EVENT_ID.GEAR_DOWN, "GEAR_DOWN");

            sender.MapClientEventToSimEvent(EVENT_ID.LANDING_LIGHTS_ON, "LANDING_LIGHTS_ON");
            sender.MapClientEventToSimEvent(EVENT_ID.LANDING_LIGHTS_OFF, "LANDING_LIGHTS_OFF");

            sender.MapClientEventToSimEvent(EVENT_ID.BEACON_LIGHTS_ON, "BEACON_LIGHTS_ON");
            sender.MapClientEventToSimEvent(EVENT_ID.BEACON_LIGHTS_OFF, "BEACON_LIGHTS_OFF");

            sender.MapClientEventToSimEvent(EVENT_ID.NAV_LIGHTS_ON, "NAV_LIGHTS_ON");
            sender.MapClientEventToSimEvent(EVENT_ID.NAV_LIGHTS_OFF, "NAV_LIGHTS_OFF");

            sender.MapClientEventToSimEvent(EVENT_ID.PANEL_LIGHTS_ON, "PANEL_LIGHTS_ON");
            sender.MapClientEventToSimEvent(EVENT_ID.PANEL_LIGHTS_OFF, "PANEL_LIGHTS_OFF");

            sender.MapClientEventToSimEvent(EVENT_ID.STROBES_ON, "STROBES_ON");
            sender.MapClientEventToSimEvent(EVENT_ID.STROBES_OFF, "STROBES_OFF");

            sender.MapClientEventToSimEvent(EVENT_ID.TAXI_LIGHTS_ON, "TAXI_LIGHTS_ON");
            sender.MapClientEventToSimEvent(EVENT_ID.TAXI_LIGHTS_OFF, "TAXI_LIGHTS_OFF");

            sender.MapClientEventToSimEvent(EVENT_ID.MAGNETO_OFF, "MAGNETO_OFF");
            sender.MapClientEventToSimEvent(EVENT_ID.MAGNETO_RIGHT, "MAGNETO_RIGHT");
            sender.MapClientEventToSimEvent(EVENT_ID.MAGNETO_LEFT, "MAGNETO_LEFT");
            sender.MapClientEventToSimEvent(EVENT_ID.MAGNETO_BOTH, "MAGNETO_BOTH");
            sender.MapClientEventToSimEvent(EVENT_ID.MAGNETO_START, "MAGNETO_START");

            sender.MapClientEventToSimEvent(EVENT_ID.BATTERY1_SET, "BATTERY1_SET");
            sender.MapClientEventToSimEvent(EVENT_ID.BATTERY2_SET, "BATTERY2_SET");

            sender.MapClientEventToSimEvent(EVENT_ID.ALTERNATOR1_SET, "TOGGLE_ALTERNATOR1");
            sender.MapClientEventToSimEvent(EVENT_ID.ALTERNATOR2_SET, "TOGGLE_ALTERNATOR2");

            // Keep the original mappings for compatibility
            sender.MapClientEventToSimEvent(EVENT_ID.MASTER_BATTERY_ON, "MASTER_BATTERY_ON");
            sender.MapClientEventToSimEvent(EVENT_ID.MASTER_BATTERY_OFF, "MASTER_BATTERY_OFF");
            sender.MapClientEventToSimEvent(EVENT_ID.ALTERNATOR_ON, "TOGGLE_MASTER_ALTERNATOR");
            sender.MapClientEventToSimEvent(EVENT_ID.ALTERNATOR_OFF, "TOGGLE_MASTER_ALTERNATOR");

            sender.MapClientEventToSimEvent(EVENT_ID.AVIONICS_MASTER_1_ON, "AVIONICS_MASTER_1_ON");
            sender.MapClientEventToSimEvent(EVENT_ID.AVIONICS_MASTER_1_OFF, "AVIONICS_MASTER_1_OFF");

            sender.MapClientEventToSimEvent(EVENT_ID.FUELSYSTEM_PUMP_ON, "TOGGLE_ELECT_FUEL_PUMP1");
            sender.MapClientEventToSimEvent(EVENT_ID.FUELSYSTEM_PUMP_OFF, "TOGGLE_ELECT_FUEL_PUMP1");

            sender.MapClientEventToSimEvent(EVENT_ID.ANTI_ICE_ON, "TOGGLE_PROPELLER_DEICE");
            sender.MapClientEventToSimEvent(EVENT_ID.ANTI_ICE_OFF, "TOGGLE_PROPELLER_DEICE");

            sender.MapClientEventToSimEvent(EVENT_ID.PITOT_HEAT_ON, "PITOT_HEAT_ON");
            sender.MapClientEventToSimEvent(EVENT_ID.PITOT_HEAT_OFF, "PITOT_HEAT_OFF");

            sender.MapClientEventToSimEvent(EVENT_ID.COWLFLAP_CLOSE, "COWLFLAP1_SET");
            sender.MapClientEventToSimEvent(EVENT_ID.COWLFLAP_OPEN, "COWLFLAP1_SET");

            sender.MapClientEventToSimEvent(EVENT_ID.ELEV_TRIM_UP, "ELEV_TRIM_UP");
            sender.MapClientEventToSimEvent(EVENT_ID.ELEV_TRIM_DN, "ELEV_TRIM_DN");

            sender.MapClientEventToSimEvent(EVENT_ID.FLAPS_INCR, "FLAPS_INCR");
            sender.MapClientEventToSimEvent(EVENT_ID.FLAPS_DECR, "FLAPS_DECR");

            sender.MapClientEventToSimEvent(EVENT_ID.AP_MASTER, "AP_MASTER");

            // Multi Panel autopilot modes & rotary
            sender.MapClientEventToSimEvent(EVENT_ID.AP_HDG_HOLD, "AP_HDG_HOLD");
            sender.MapClientEventToSimEvent(EVENT_ID.AP_NAV1_HOLD, "AP_NAV1_HOLD");
            sender.MapClientEventToSimEvent(EVENT_ID.AP_AIRSPEED_HOLD, "FLIGHT_LEVEL_CHANGE");
            sender.MapClientEventToSimEvent(EVENT_ID.AP_ALT_HOLD, "AP_ALT_HOLD");
            sender.MapClientEventToSimEvent(EVENT_ID.AP_VS_HOLD, "AP_VS_HOLD");
            sender.MapClientEventToSimEvent(EVENT_ID.AP_APR_HOLD, "AP_APR_HOLD");
            sender.MapClientEventToSimEvent(EVENT_ID.AP_BACKCOURSE_HOLD, "AP_BC_HOLD");
            sender.MapClientEventToSimEvent(EVENT_ID.AUTO_THROTTLE_ARM, "AUTO_THROTTLE_ARM");

            sender.MapClientEventToSimEvent(EVENT_ID.HEADING_BUG_INC, "HEADING_BUG_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.HEADING_BUG_DEC, "HEADING_BUG_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.VOR1_OBI_INC, "VOR1_OBI_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.VOR1_OBI_DEC, "VOR1_OBI_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.AP_SPD_VAR_INC, "AP_SPD_VAR_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.AP_SPD_VAR_DEC, "AP_SPD_VAR_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.AP_ALT_VAR_INC, "AP_ALT_VAR_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.AP_ALT_VAR_DEC, "AP_ALT_VAR_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.AP_VS_VAR_INC, "AP_VS_VAR_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.AP_VS_VAR_DEC, "AP_VS_VAR_DEC");

            // Radio Panel mappings
            // COM1
            sender.MapClientEventToSimEvent(EVENT_ID.COM1_WHOLE_INC, "COM_RADIO_WHOLE_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.COM1_WHOLE_DEC, "COM_RADIO_WHOLE_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.COM1_FRACT_INC, "COM_RADIO_FRACT_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.COM1_FRACT_DEC, "COM_RADIO_FRACT_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.COM1_STBY_SWAP, "COM_STBY_RADIO_SWAP");

            // COM2
            sender.MapClientEventToSimEvent(EVENT_ID.COM2_WHOLE_INC, "COM2_RADIO_WHOLE_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.COM2_WHOLE_DEC, "COM2_RADIO_WHOLE_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.COM2_FRACT_INC, "COM2_RADIO_FRACT_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.COM2_FRACT_DEC, "COM2_RADIO_FRACT_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.COM2_STBY_SWAP, "COM2_RADIO_SWAP");

            // NAV1
            sender.MapClientEventToSimEvent(EVENT_ID.NAV1_WHOLE_INC, "NAV1_RADIO_WHOLE_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.NAV1_WHOLE_DEC, "NAV1_RADIO_WHOLE_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.NAV1_FRACT_INC, "NAV1_RADIO_FRACT_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.NAV1_FRACT_DEC, "NAV1_RADIO_FRACT_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.NAV1_STBY_SWAP, "NAV1_RADIO_SWAP");

            // NAV2
            sender.MapClientEventToSimEvent(EVENT_ID.NAV2_WHOLE_INC, "NAV2_RADIO_WHOLE_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.NAV2_WHOLE_DEC, "NAV2_RADIO_WHOLE_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.NAV2_FRACT_INC, "NAV2_RADIO_FRACT_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.NAV2_FRACT_DEC, "NAV2_RADIO_FRACT_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.NAV2_STBY_SWAP, "NAV2_RADIO_SWAP");

            // ADF
            // sender.MapClientEventToSimEvent(EVENT_ID.ADF_100_INC, "ADF_100_INC");
            // sender.MapClientEventToSimEvent(EVENT_ID.ADF_100_DEC, "ADF_100_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.ADF_WHOLE_INC, "ADF_100_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.ADF_WHOLE_DEC, "ADF_100_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.ADF_FRACT_INC, "ADF1_WHOLE_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.ADF_FRACT_DEC, "ADF1_WHOLE_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.ADF_STBY_SWAP, "ADF1_RADIO_SWAP");

            // XPDR
            sender.MapClientEventToSimEvent(EVENT_ID.XPDR_1000_INC, "XPNDR_1000_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.XPDR_1000_DEC, "XPNDR_1000_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.XPDR_100_INC, "XPNDR_100_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.XPDR_100_DEC, "XPNDR_100_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.XPDR_10_INC, "XPNDR_10_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.XPDR_10_DEC, "XPNDR_10_DEC");
            sender.MapClientEventToSimEvent(EVENT_ID.XPDR_1_INC, "XPNDR_1_INC");
            sender.MapClientEventToSimEvent(EVENT_ID.XPDR_1_DEC, "XPNDR_1_DEC");

            // after all sender.MapClientEventToSimEvent(...) calls
            PopulateEventNameMap();


            // Required for MSFS event routing
            sender.SetNotificationGroupPriority(GROUP_ID.GROUP0, 1);

            // --- Data definitions ---

            // AircraftData
            sender.AddToDataDefinition(
                DATA_DEFINITION.AircraftData,
                "PLANE ALTITUDE",
                "feet",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.AircraftData,
                "AIRSPEED INDICATED",
                "knots",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.AircraftData,
                "PLANE HEADING DEGREES MAGNETIC",
                "degrees",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.AircraftData,
                "VERTICAL SPEED",
                "feet per minute",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.RegisterDataDefineStruct<AircraftData>(DATA_DEFINITION.AircraftData);

            sender.AddToDataDefinition(
                DATA_DEFINITION.GearData,
                "GEAR LEFT POSITION",
                "percent",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.GearData,
                "GEAR CENTER POSITION",
                "percent",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.GearData,
                "GEAR RIGHT POSITION",
                "percent",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.RegisterDataDefineStruct<GearData>(DATA_DEFINITION.GearData);

            // AutopilotData (define all fields, including boolean flags, BEFORE registering the struct)
            sender.AddToDataDefinition(
                DATA_DEFINITION.AutopilotData,
                "AUTOPILOT ALTITUDE LOCK VAR",
                "feet",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.AutopilotData,
                "AUTOPILOT VERTICAL HOLD VAR",
                "feet per minute",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.AutopilotData,
                "AUTOPILOT AIRSPEED HOLD VAR",
                "knots",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.AutopilotData,
                "AUTOPILOT HEADING LOCK DIR",
                "degrees",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.AutopilotData,
                "NAV OBS:1",
                "degrees",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );

            // Existing AP master (0/1)
            sender.AddToDataDefinition(
                DATA_DEFINITION.AutopilotData,
                "AUTOPILOT MASTER",
                "Bool",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );

            // Additional autopilot boolean flags for panel LEDs
            sender.AddToDataDefinition(
                DATA_DEFINITION.AutopilotData,
                "AUTOPILOT HEADING LOCK",
                "Bool",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.AutopilotData,
                "AUTOPILOT NAV1 LOCK",
                "Bool",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.AutopilotData,
                "AUTOPILOT ALTITUDE LOCK",
                "Bool",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.AutopilotData,
                "AUTOPILOT VERTICAL HOLD",
                "Bool",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.AutopilotData,
                "AUTOPILOT FLIGHT LEVEL CHANGE",
                "Bool",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.AutopilotData,
                "AUTOPILOT APPROACH HOLD",
                "Bool",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );
            sender.AddToDataDefinition(
                DATA_DEFINITION.AutopilotData,
                "AUTOPILOT BACKCOURSE HOLD",
                "Bool",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );

            sender.RegisterDataDefineStruct<AutopilotData>(DATA_DEFINITION.AutopilotData);

            // RadioData
            sender.AddToDataDefinition(DATA_DEFINITION.RadioData, "COM ACTIVE FREQUENCY:1", "MHz", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            sender.AddToDataDefinition(DATA_DEFINITION.RadioData, "COM STANDBY FREQUENCY:1", "MHz", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

            sender.AddToDataDefinition(DATA_DEFINITION.RadioData, "COM ACTIVE FREQUENCY:2", "MHz", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            sender.AddToDataDefinition(DATA_DEFINITION.RadioData, "COM STANDBY FREQUENCY:2", "MHz", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

            sender.AddToDataDefinition(DATA_DEFINITION.RadioData, "NAV ACTIVE FREQUENCY:1", "MHz", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            sender.AddToDataDefinition(DATA_DEFINITION.RadioData, "NAV STANDBY FREQUENCY:1", "MHz", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

            sender.AddToDataDefinition(DATA_DEFINITION.RadioData, "NAV ACTIVE FREQUENCY:2", "MHz", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            sender.AddToDataDefinition(DATA_DEFINITION.RadioData, "NAV STANDBY FREQUENCY:2", "MHz", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

            sender.AddToDataDefinition(DATA_DEFINITION.RadioData, "ADF ACTIVE FREQUENCY:1", "kHz", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            sender.AddToDataDefinition(DATA_DEFINITION.RadioData, "ADF STANDBY FREQUENCY:1", "kHz", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

            sender.AddToDataDefinition(DATA_DEFINITION.RadioData, "TRANSPONDER CODE:1", "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

            sender.AddToDataDefinition(DATA_DEFINITION.RadioData, "NAV DME:1", "nautical miles", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            sender.AddToDataDefinition(DATA_DEFINITION.RadioData, "NAV DME SPEED:1", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            sender.AddToDataDefinition(DATA_DEFINITION.RadioData, "NAV DME TIME:1", "seconds", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

            sender.RegisterDataDefineStruct<RadioData>(DATA_DEFINITION.RadioData);

            // --- Request periodic data ---
            sender.RequestDataOnSimObject(
                REQUEST_ID.AircraftDataRequest,
                DATA_DEFINITION.AircraftData,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SECOND,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                0, 0, 0
            );

            sender.RequestDataOnSimObject(
                REQUEST_ID.GearDataRequest,
                DATA_DEFINITION.GearData,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SECOND,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                0, 0, 0
            );

            sender.RequestDataOnSimObject(
                REQUEST_ID.AutopilotDataRequest,
                DATA_DEFINITION.AutopilotData,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SIM_FRAME,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                0, 0, 0
            );

            sender.RequestDataOnSimObject(
                REQUEST_ID.RadioDataRequest,
                DATA_DEFINITION.RadioData,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SIM_FRAME,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                0, 0, 0
            );

            // Start TCP server
            server = new TcpCommandServer(5555, HandleJsonCommand);
            server.Start();
        }

        private static void OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Console.WriteLine("MSFS has exited.");
            Environment.Exit(0);
        }

        private static void OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            Console.WriteLine("SimConnect Exception: " + data.dwException);
        }

        private static void OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            if ((REQUEST_ID)data.dwRequestID == REQUEST_ID.AircraftDataRequest)
            {
                AircraftData ad = (AircraftData)data.dwData[0];

                var obj = new
                {
                    type = "simvars",
                    altitude = ad.Altitude,
                    airspeed = ad.Airspeed,
                    heading = ad.Heading,
                    vertical_speed = ad.VerticalSpeed
                };

                string json = JsonConvert.SerializeObject(obj);
                Console.WriteLine("SIMVARS: " + json);

                server?.Broadcast(json + "\n");
            }
            else if ((REQUEST_ID)data.dwRequestID == REQUEST_ID.GearDataRequest)
            {
                GearData g = (GearData)data.dwData[0];

                var obj = new
                {
                    type = "switch",
                    gear = new
                    {
                        left = g.Left,
                        center = g.Center,
                        right = g.Right
                    }
                };

                string json = JsonConvert.SerializeObject(obj);
                server?.Broadcast(json + "\n");
            }
            else if ((REQUEST_ID)data.dwRequestID == REQUEST_ID.AutopilotDataRequest)
            {
                AutopilotData ap = (AutopilotData)data.dwData[0];

                var obj = new
                {
                    type = "autopilot",
                    alt = ap.AltitudeLock,
                    vs = ap.VerticalSpeed,
                    ias = ap.AirspeedHold,
                    hdg = ap.HeadingLock,
                    crs = ap.Course,
                    ap_master = ap.ApMaster,

                    // New boolean flags (0/1)
                    ap_hdg_hold = ap.ApHdgHold,
                    ap_nav1_hold = ap.ApNav1Hold,
                    ap_alt_hold = ap.ApAltHold,
                    ap_vs_hold = ap.ApVsHold,
                    ap_airspeed_hold_flag = ap.ApAirspeedHoldFlag,
                    ap_approach_hold = ap.ApAprHold,
                    ap_backcourse_hold = ap.ApBackcourseHold
                };

                string json = JsonConvert.SerializeObject(obj);
                Console.WriteLine("AUTOPILOT: " + json);

                server?.Broadcast(json + "\n");
            }
            else if ((REQUEST_ID)data.dwRequestID == REQUEST_ID.RadioDataRequest)
            {
                RadioData r = (RadioData)data.dwData[0];

                // Helper function to check if value is valid
                bool IsValidDmeValue(double val)
                {
                    return !double.IsNaN(val) && !double.IsInfinity(val) &&
                    val > -1e10 && val < 1e10;  // Reasonable range
                }

                var obj = new
                {
                    type = "radio",
                    com1 = new { active = r.Com1Active, standby = r.Com1Standby },
                    com2 = new { active = r.Com2Active, standby = r.Com2Standby },
                    nav1 = new { active = r.Nav1Active, standby = r.Nav1Standby },
                    nav2 = new { active = r.Nav2Active, standby = r.Nav2Standby },
                    adf = new { active = r.AdfActive, standby = r.AdfStandby },
                    xpdr = r.XpdrCode,
                    dme = new
                    {
                        dist = IsValidDmeValue(r.Dme1Distance) ? r.Dme1Distance : 0.0,
                        speed = IsValidDmeValue(r.Dme1Speed) ? r.Dme1Speed : 0.0,
                        time = IsValidDmeValue(r.Dme1Time) ? r.Dme1Time : 0.0
                    }
                };

                string json = JsonConvert.SerializeObject(obj);
                server?.Broadcast(json + "\n");
            }
        }

        // Placeholder for JSON command handler - ensure this exists in your file
        // and that it parses incoming JSON and dispatches events using EventNameMap or Enum.TryParse.
        // If you want, I can add a small debug log in the handler to print incoming commands and resolved enums.
        private static void HandleJsonCommand(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    Console.WriteLine("BRIDGE: Empty JSON line");
                    return;
                }

                CommandPacket? pkt = null;

                try
                {
                    pkt = JsonConvert.DeserializeObject<CommandPacket>(json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"BRIDGE: JSON parse error: {ex.Message}");
                    return;
                }

                // Validate packet and cmd
                if (pkt == null || string.IsNullOrWhiteSpace(pkt.cmd))
                {
                    Console.WriteLine("BRIDGE: Invalid packet (missing cmd)");
                    return;
                }

                var cmdName = pkt!.cmd!.Trim();

                // Try to parse enum
                if (!Enum.TryParse<EVENT_ID>(cmdName, true, out var ev))
                {
                    Console.WriteLine($"BRIDGE: Unknown command '{cmdName}'");
                    return;
                }

                // Logging only — EventNameMap is optional
                string mappedName = EventNameMap.ContainsKey(ev)
                    ? EventNameMap[ev]
                    : "(no mapping)";

                Console.WriteLine($"BRIDGE: Received '{cmdName}' -> Enum.{ev} -> EventNameMap='{mappedName}'");

                // Dispatch to SimConnect
                try
                {
                    uint dataValue = pkt.data ?? 1;

                    // SPECIAL CASES: Commands that control BOTH systems
                    if (ev == EVENT_ID.ALTERNATOR_ON)
                    {
                        // Turn ON both alternators
                        simconnect?.TransmitClientEvent(
                            SimConnect.SIMCONNECT_OBJECT_ID_USER,
                            EVENT_ID.ALTERNATOR1_SET,
                            0,  // ON
                            GROUP_ID.GROUP0,
                            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
                        );

                        simconnect?.TransmitClientEvent(
                            SimConnect.SIMCONNECT_OBJECT_ID_USER,
                            EVENT_ID.ALTERNATOR2_SET,
                            0,  // ON
                            GROUP_ID.GROUP0,
                            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
                        );

                        Console.WriteLine("BRIDGE: Set BOTH alternators ON");
                    }
                    else if (ev == EVENT_ID.ALTERNATOR_OFF)
                    {
                        // Turn OFF both alternators
                        simconnect?.TransmitClientEvent(
                            SimConnect.SIMCONNECT_OBJECT_ID_USER,
                            EVENT_ID.ALTERNATOR1_SET,
                            0,  // OFF
                            GROUP_ID.GROUP0,
                            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
                        );

                        simconnect?.TransmitClientEvent(
                            SimConnect.SIMCONNECT_OBJECT_ID_USER,
                            EVENT_ID.ALTERNATOR2_SET,
                            0,  // OFF
                            GROUP_ID.GROUP0,
                            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
                        );

                        Console.WriteLine("BRIDGE: Set BOTH alternators OFF");
                    }
                    else if (ev == EVENT_ID.MASTER_BATTERY_ON)
                    {
                        // Turn ON both batteries
                        simconnect?.TransmitClientEvent(
                            SimConnect.SIMCONNECT_OBJECT_ID_USER,
                            EVENT_ID.BATTERY1_SET,
                            1,  // ON
                            GROUP_ID.GROUP0,
                            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
                        );

                        simconnect?.TransmitClientEvent(
                            SimConnect.SIMCONNECT_OBJECT_ID_USER,
                            EVENT_ID.BATTERY2_SET,
                            1,  // ON
                            GROUP_ID.GROUP0,
                            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
                        );

                        Console.WriteLine("BRIDGE: Set BOTH batteries ON");
                    }
                    else if (ev == EVENT_ID.MASTER_BATTERY_OFF)
                    {
                        // Turn OFF both batteries
                        simconnect?.TransmitClientEvent(
                            SimConnect.SIMCONNECT_OBJECT_ID_USER,
                            EVENT_ID.BATTERY1_SET,
                            0,  // OFF
                            GROUP_ID.GROUP0,
                            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
                        );

                        simconnect?.TransmitClientEvent(
                            SimConnect.SIMCONNECT_OBJECT_ID_USER,
                            EVENT_ID.BATTERY2_SET,
                            0,  // OFF
                            GROUP_ID.GROUP0,
                            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
                        );

                        Console.WriteLine("BRIDGE: Set BOTH batteries OFF");
                    }
                    else
                    {
                        // NORMAL CASE: Single event dispatch
                        simconnect?.TransmitClientEvent(
                            SimConnect.SIMCONNECT_OBJECT_ID_USER,
                            ev,
                            dataValue,
                            GROUP_ID.GROUP0,
                            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
                        );

                        Console.WriteLine($"BRIDGE: Dispatched {ev} (data={dataValue})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"BRIDGE: Error dispatching {ev}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                // Final safety net — TCP thread will never die
                Console.WriteLine($"BRIDGE: HandleJsonCommand fatal error: {ex}");
            }
        }

    }
}
