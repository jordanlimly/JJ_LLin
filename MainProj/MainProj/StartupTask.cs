using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

using System.Diagnostics;
using System.Threading.Tasks;
using GrovePi;
using GrovePi.Sensors;

namespace MainProj
{
    public sealed class StartupTask : IBackgroundTask
    {
        // State machine variables to control different mode of operation
        const int MODE_NORMAL = 1;
        const int MODE_ENTERING = 2;
        const int MODE_EXITING = 3;
        static int curMode; // stores current mode program is at
        const int RFIDMODE_NORMAL = 4;
        const int RFIDMODE_STARTGAME = 5;
        static int rfidMode;

        // =====FOR RFID=====
        private static SerialComms uartComms;
        private static string strRfidDetected = ""; //used to check for RFID

        private void Sleep(int NoOfMs)
        {
            Task.Delay(NoOfMs).Wait();
        }

        //this method is automatically called when there is card detected
        static void UartDataHandler(object sender, SerialComms.UartEventArgs e)
        {
            //strRfidDetected can be used anywhere in the program to check for card detected
            strRfidDetected = e.data;
            Debug.WriteLine("Card detected: " + strRfidDetected);
        }

        ////Must call this to initialise the Serial Comms
        private void StartUart()
        {
            uartComms = new SerialComms();
            uartComms.UartEvent += new SerialComms.UartEventDelegate(UartDataHandler);
        }

        // =====END OF RFID=====

        // =====FOR DISTANCE SENSOR=====
        IUltrasonicRangerSensor sensor = DeviceFactory.Build.UltraSonicSensor(Pin.DigitalPin8);

        private System.Threading.Semaphore sm = new System.Threading.Semaphore(1, 1);

        //used by sensor for internal processing
        private int distance = 400;

        //this is for main logic controller to check for distance
        int sensorDistance;

        private int getDistance()
        {
            // need to ensure you cover the correct FOV before distance is report correctly
            // better to cover with a big object like a file
            //will take some time to init before scanning
            sm.WaitOne();
            int distanceRead = sensor.MeasureInCentimeters();
            sm.Release();
            if (distanceRead < 400 && distanceRead > 0)
                distance = distanceRead;
            return distance;
        }
        // =====END OF DISTANCE SENSOR=====

        // =====FOR LIGHT SENSOR(2nd Distance Sensor)=====
        Pin lightPin = Pin.AnalogPin1;
        
        private int GetLightValue(Pin pin)
        {
            sm.WaitOne();
            int value = DeviceFactory.Build.GrovePi().AnalogRead(pin);
            sm.Release();
            return value;
        }


        int adcValue; //creating variable
        // =====END OF LIGHT SENSOR=====

        // for Data Comms
        DataComms dataComms;
        //this is used to check for data coming in from winform
        string strDataReceived = "";

        // this method is automatically called when data comes from Winform
        public void commsDataReceive(string dataReceived)
        {
            //can use strDataReceived anywhere in codes to check for any data coming in from Winform
            strDataReceived = dataReceived;
            Debug.WriteLine("Data Received: " + strDataReceived);
        }


        //use this method to send data out to Winforms
        private void sendDataToWindows(string direction) //direction refers to entering or exiting
        {
            try
            {
                dataComms.sendData(direction);
                Debug.WriteLine("Sending Msg : " + direction);
            }
            catch(Exception)
            {
                Debug.WriteLine("Error, Did you forget to initComms()?");
            }
        }

        //this is to setup the comms for data transfer with Winforms
        private void initComms()
        {
            dataComms = new DataComms();
            dataComms.dataReceiveEvent += new DataComms.DataReceivedDelegate(commsDataReceive);
        }


        private void handleModeNormal()
        {
            if (sensorDistance < 110)
            {
                //check if dist. sensor value goes back to normal values
                Sleep(300);
                int checkDistance = getDistance();
                if (checkDistance >= 110)
                {
                    // move to MODE_Entering
                    curMode = MODE_ENTERING;
                    Debug.WriteLine("===Entering MODE_ENTERING===");
                    sendDataToWindows("SENSORTRIGGERED=" + "Sensor1");
                }               
            }
            else if (adcValue < 50)
            {
                //check if light sensor value goes back to normal values
                Sleep(300);
                int checkAdcValue = GetLightValue(lightPin);
                if (checkAdcValue >= 100)
                {
                    //move to MODE_Exiting
                    curMode = MODE_EXITING;
                    Debug.WriteLine("===Entering MODE_EXITING===");
                    sendDataToWindows("SENSORTRIGGERED=" + "Sensor2");
                }
            }
        }

        private void handleModeEntering()
        {
            //string startTime = DateTime.Now.ToString("ss");
            adcValue = GetLightValue(lightPin);
            if (adcValue < 50)
            {
                Sleep(300);
                int checkAdcValue = GetLightValue(lightPin);
                if (checkAdcValue > 150)
                {
                    sendDataToWindows("SENSORTRIGGERED=" + "Sensor2");
                    string direction = "In";
                    sendDataToWindows("DIRECTION=" + direction);
                    Debug.WriteLine("Someone entered");

                    curMode = MODE_NORMAL;
                    Debug.WriteLine("===Back to MODE_Normal===");
                }
                //else
                //{
                //    // this part not confirmed yet
                //    curMode = MODE_NORMAL;
                //    Debug.WriteLine("===Enter cancelled, back to MODE_Normal===");
                //}
            }

        }


        private void handleModeExiting()
        {
            sensorDistance = getDistance();
            if (sensorDistance < 110)
            {
                //check if dist. sensor value goes back to normal values
                Sleep(300);
                int checkDistance = getDistance();
                if (checkDistance >= 110)
                {
                    sendDataToWindows("SENSORTRIGGERED=" + "Sensor1");
                    string direction = "Out";
                    sendDataToWindows("DIRECTION=" + direction);
                    Debug.WriteLine("Someone left");

                    curMode = MODE_NORMAL;
                    Debug.WriteLine("===Back to MODE_Normal===");
                }
            }

        }

        // creating variable
        string detectedRFID;

        private void handleRfidModeNormal()
        {
            if (!strRfidDetected.Equals(""))  // this is true for any card detected      **6A003E1A3E70
            {
                detectedRFID = strRfidDetected;
                rfidMode = RFIDMODE_STARTGAME;
                Debug.WriteLine("===Entering Game mode===");
            }

            ////Important: Must always clear after you've processed the data
            strRfidDetected = "";
        }

        private void handleRfidModeStartGame()
        {
            Debug.WriteLine("rfid scanned is" + detectedRFID);
            sendDataToWindows("RFIDGAMESTART=" + detectedRFID);

            rfidMode = RFIDMODE_NORMAL;
            Debug.WriteLine("===Back to RFIDMODE_Normal===");
        }

        public void Run(IBackgroundTaskInstance taskInstance)
        {

            // 
            // TODO: Insert code to perform background work
            //
            // If you start any asynchronous methods here, prevent the task
            // from closing prematurely by using BackgroundTaskDeferral as
            // described in http://aka.ms/backgroundtaskdeferral

            initComms(); // Must start before data transfer can work

            //Must call this to init the Serial Comm before you can use it
            StartUart();

            string direction = "";

            //Init mode
            curMode = MODE_NORMAL;
            Debug.WriteLine("===Entering Mode_Normal===");
            rfidMode = RFIDMODE_NORMAL;
            Debug.WriteLine("===Entering RFIDMODE_NORMAL===");

            while (true)
            {
                //Sleep(200);

                // =====FOR DISTANCE SENSOR=====
                Sleep(300);
                sensorDistance = getDistance();
                adcValue = GetLightValue(lightPin);
                Debug.WriteLine("Sensor distance = " + sensorDistance);
                Debug.WriteLine("ADC value = " + adcValue);
                Debug.WriteLine("Current mode = " + Convert.ToString(curMode));

                //state machine
                if (curMode == MODE_NORMAL)
                    handleModeNormal();
                else if (curMode == MODE_ENTERING)
                    handleModeEntering();
                else if (curMode == MODE_EXITING)
                    handleModeExiting();
                else
                    Debug.WriteLine("Error: Invalid mode, check logic");

                // =====END OF DISTANCE SENSOR=====


                // =====FOR LIGHT SENSOR(2nd Distance Sensor)=====
                //Sleep(300);
                //adcValue1 = GetLightValue(lightPin);
                //Debug.WriteLine("Light ADC = " + adcValue);
                // =====END OF LIGHT SENSOR=====

                // =====FOR RFID=====
                if (rfidMode == RFIDMODE_NORMAL)
                    handleRfidModeNormal();
                else if (rfidMode == RFIDMODE_STARTGAME)
                    handleRfidModeStartGame();
                else
                    Debug.WriteLine("Error: Invalid mode, check logic");

                
                // =====END OF RFID=====

            }
        }
    }
}
