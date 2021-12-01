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
        // =====FOR RFID=====
        //private static SerialComms uartComms;
        //private static string strRfidDetected = ""; //used to check for RFID

        private void Sleep(int NoOfMs)
        {
            Task.Delay(NoOfMs).Wait();
        }

        //this method is automatically called when there is card detected
        //static void UartDataHandler(object sender, SerialComms.UartEventArgs e)
        //{
        //    //strRfidDetected can be used anywhere in the program to check for card detected
        //    strRfidDetected = e.data;
        //    Debug.WriteLine("Card detected: " + strRfidDetected);
        //}

        ////Must call this to initialise the Serial Comms
        //private void StartUart()
        //{
        //    uartComms = new SerialComms();
        //    uartComms.UartEvent += new SerialComms.UartEventDelegate(UartDataHandler);
        //}

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
            //StartUart();
            int adcValue1 = 0;
            int adcValue2 = 0;
            string direction = "";

            while (true)
            {
                //Sleep(200);


                // =====FOR RFID=====
                //if (!strRfidDetected.Equals(""))  // this is true for any card detected
                //{
                //    if (strRfidDetected.Equals(""))  // check for specific card
                //    {
                //        Debug.WriteLine("its working");
                //    }
                //}

                ////Important: Must always clear after you've processed the data
                //strRfidDetected = "";
                // =====END OF RFID=====


                // =====FOR DISTANCE SENSOR=====
                Sleep(700);
                sensorDistance = getDistance();
                adcValue1 = GetLightValue(lightPin);
                Debug.WriteLine("Sensor distance = " + sensorDistance);
                Debug.WriteLine("ADC1 = " + adcValue1);
                if (sensorDistance < 110)
                {
                    Sleep(400);
                    // check light sensor to confirm if someone entered the arcade
                    adcValue2 = GetLightValue(lightPin);
                    // Debug.WriteLine("Light ADC = " + adcValue);
                    if (adcValue1 - adcValue2 >= 100)  // set difference threshold to 10 so that it will not trigger unnecessarily
                    {                     
                        direction = "In";
                        sendDataToWindows("DIRECTION=" + direction);
                        Debug.WriteLine("Someone entered");
                        continue;
                    }
                }
                else
                {
                    Sleep(700);
                    adcValue2 = GetLightValue(lightPin);
                    Debug.WriteLine("ADC2 = " + adcValue2);
                    if (adcValue1 - adcValue2 >= 100)
                    {
                        //check distance sensor to confirm if someone exit the arcade
                        Sleep(300);
                        sensorDistance = getDistance();
                        
                        if (sensorDistance < 110)
                        {
                            direction = "Out";
                            sendDataToWindows("DIRECTION=" + direction);
                            Debug.WriteLine("Someone left");
                            continue;
                        }
                    }
                }
                // =====END OF DISTANCE SENSOR=====


                // =====FOR LIGHT SENSOR(2nd Distance Sensor)=====
                //Sleep(300);
                //adcValue1 = GetLightValue(lightPin);
                //Debug.WriteLine("Light ADC = " + adcValue);
                // =====END OF LIGHT SENSOR=====

            }
        }
    }
}
