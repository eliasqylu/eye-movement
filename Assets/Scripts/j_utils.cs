using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.XR;
using System.IO;

namespace j_utils {

    public static class myUnityFunctions{
        public static string GetGameObjectPath(GameObject obj)
        {
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }
            return path;
        }
    }
    public static class IListExtensions {
        /// <summary>
        /// Shuffles the element order of the specified list.
        /// </summary>
        public static void Shuffle<T>(this IList<T> ts) {
            var count = ts.Count;
            var last = count - 1;
            for (var i = 0; i < last; ++i) {
                var r = UnityEngine.Random.Range(i, count);
                var tmp = ts[i];
                ts[i] = ts[r];
                ts[r] = tmp;
            }
        }
    }

    public static class csv_utils {

        public static StreamWriter writer = null;
        public static bool isLogging = false;
        

        public static List<Vector3> CSVRead2Vector3List (List<Vector3> stimSP, StreamReader reader) {
            using (reader) {
                while (!reader.EndOfStream) {
                    var line = reader.ReadLine ();
                    var values = line.Split (',');

                    stimSP.Add (new Vector3 (
                        float.Parse (values[0], CultureInfo.InvariantCulture),
                        float.Parse (values[1], CultureInfo.InvariantCulture),
                        float.Parse (values[2], CultureInfo.InvariantCulture)));
                }
            }
            return stimSP;
        }

        public static void StartLogging (String[] ColumnNames, String recordPath=null){
            if (isLogging) {
                Debug.LogWarning ("Logging was on when StartLogging was called. No new log was started.");
                //gazePoint.GetComponent<Renderer>().material.SetColor("_Color", new Color(255, 0, 0, 255));
                return;
            }
            isLogging = true;

            string path = recordPath;
            if (path==null){
                //string logPath = Application.dataPath + "/Logs/";
                string logPath = Application.persistentDataPath + "/data/";
                Directory.CreateDirectory (logPath);

                DateTime now = DateTime.Now;
                //string fileName = string.Format ("{0}-{1:00}-{2:00}-{3:00}-{4:00}", now.Year, now.Month, now.Day, now.Hour, now.Minute);
                string fileName = string.Format ("dataLog_{0}-{1:00}-{2:00}-{3:00}-{4:00}", now.Year, now.Month, now.Day, now.Hour, now.Minute);

                path = logPath + fileName + ".csv";
            }

            Debug.Log ("Logging started at " + path);

            writer = new StreamWriter (path);

            Log(ColumnNames);
            
        }


        public static void Log (string[] values) {
            //Debug.Log("Log:");
            // Debug.Log("isLogging: " + isLogging.ToString());
            // Debug.Log("writer isNull: " + (writer == null).ToString());
            // Debug.Log(values.Length);
            // Debug.Log(values[0]);

            if (!isLogging || writer == null){
                //gazePoint.GetComponent<Renderer>().material.SetColor("_Color", new Color(0, 255, 0, 255));
                return;
            }

            string line = "";
            for (int i = 0; i < values.Length; ++i) {
                //Debug.Log ("value[i]: " + values[i].ToString ());
                values[i] = values[i].Replace ("\r", "").Replace ("\n", ""); // Remove new lines so they don't break csv
                line += values[i] + (i == (values.Length - 1) ? "" : ";"); // add semicolon to all but the last data string
            }
            writer.WriteLine (line);

        }

        public static void StopLogging () {
            // Debug.Log("StopLogging:");
            // Debug.Log("isLogging: " + isLogging.ToString());
            // Debug.Log("writer isNull: " + (writer == null).ToString());

            if (!isLogging){
                //gazePoint.GetComponent<Renderer>().material.SetColor("_Color", new Color(0, 0, 255, 255));
                return;
            }

            //Release ();
            if (writer != null) {
                writer.Flush ();
                writer.Close ();
                writer = null;
            }
            isLogging = false;
            Debug.Log ("Logging ended");
        }

        
    }

    
    public class controller_utils{
        public const InputDeviceCharacteristics kControllerRightCharacteristics = (
            InputDeviceCharacteristics.Right |
            InputDeviceCharacteristics.TrackedDevice |
            InputDeviceCharacteristics.Controller |
            InputDeviceCharacteristics.HeldInHand
        );

        public const InputDeviceCharacteristics kControllerLeftCharacteristics = (
            InputDeviceCharacteristics.Left |
            InputDeviceCharacteristics.TrackedDevice |
            InputDeviceCharacteristics.Controller |
            InputDeviceCharacteristics.HeldInHand
        );

        internal static List<InputDevice> m_InputDevices = new List<InputDevice>();

        public static bool GetRightControllerTriggerButton(out bool pressed)
        {
            pressed = false;

            InputDevices.GetDevices(m_InputDevices);
            if (m_InputDevices != null && m_InputDevices.Count > 0)
            {
                for (int i = 0; i < m_InputDevices.Count; i++)
                {
                    // The device is connected.
                    if (m_InputDevices[i].characteristics.Equals(kControllerRightCharacteristics))
                    {
                        if (m_InputDevices[i].TryGetFeatureValue(CommonUsages.triggerButton, out bool value))
                        {
                            pressed = value;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool GetLeftControllerMenuButton(out bool pressed)
        {
            pressed = false;

            InputDevices.GetDevices(m_InputDevices);
            if (m_InputDevices != null && m_InputDevices.Count > 0)
            {
                for (int i = 0; i < m_InputDevices.Count; i++)
                {
                    // The device is connected.
                    if (m_InputDevices[i].characteristics.Equals(kControllerLeftCharacteristics))
                    {
                        if (m_InputDevices[i].TryGetFeatureValue(CommonUsages.menuButton, out bool value))
                        {
                            pressed = value;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool GetLeftControllerPrimaryButton(out bool pressed)
        {
            pressed = false;

            InputDevices.GetDevices(m_InputDevices);
            if (m_InputDevices != null && m_InputDevices.Count > 0)
            {
                for (int i = 0; i < m_InputDevices.Count; i++)
                {
                    // The device is connected.
                    if (m_InputDevices[i].characteristics.Equals(kControllerLeftCharacteristics))
                    {
                        if (m_InputDevices[i].TryGetFeatureValue(CommonUsages.primaryButton, out bool value))
                        {
                            pressed = value;
                            return true;
                        }
                    }
                }
            }

            return false;
        }


        public static bool GetLeftControllerTrackPadClicked(out bool pressed)
        {
            pressed = false;

            InputDevices.GetDevices(m_InputDevices);
            if (m_InputDevices != null && m_InputDevices.Count > 0)
            {
                for (int i = 0; i < m_InputDevices.Count; i++)
                {
                    // The device is connected.
                    if (m_InputDevices[i].characteristics.Equals(kControllerLeftCharacteristics))
                    {
                        if (m_InputDevices[i].TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool value))
                        {
                            pressed = value;
                            return true;
                        }
                    }
                }
            }

            return false;
        }  


        public static bool GetRightControllerPrimaryButton(out bool pressed)
        {
            pressed = false;

            InputDevices.GetDevices(m_InputDevices);
            if (m_InputDevices != null && m_InputDevices.Count > 0)
            {
                
                for (int i = 0; i < m_InputDevices.Count; i++)
                {
                    // The device is connected.
                    if (m_InputDevices[i].characteristics.Equals(kControllerRightCharacteristics))
                    {
                        if (m_InputDevices[i].TryGetFeatureValue(CommonUsages.primaryButton, out bool value))
                        {
                            pressed = value;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool GetRightControllerTrackPadClicked(out bool pressed)
        {
            pressed = false;

            InputDevices.GetDevices(m_InputDevices);
            if (m_InputDevices != null && m_InputDevices.Count > 0)
            {
                for (int i = 0; i < m_InputDevices.Count; i++)
                {
                    // The device is connected.
                    if (m_InputDevices[i].characteristics.Equals(kControllerRightCharacteristics))
                    {
                        if (m_InputDevices[i].TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool value))
                        {
                            pressed = value;
                            return true;
                        }
                    }
                }
            }

            return false;
        }  

        public static bool GetRightControllerTrigger(out float pressure){
            pressure = 0.0f;
            InputDevices.GetDevices(m_InputDevices);
            if (m_InputDevices != null && m_InputDevices.Count > 0)
            {
                for (int i = 0; i < m_InputDevices.Count; i++)
                {
                    // The device is connected.
                    if (m_InputDevices[i].characteristics.Equals(kControllerRightCharacteristics))
                    {
                        if (m_InputDevices[i].TryGetFeatureValue(CommonUsages.trigger, out float value))
                        {
                            pressure = value;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool BuzzRightController(float amplitude, float duration){
            InputDevices.GetDevices(m_InputDevices);
            if (m_InputDevices != null && m_InputDevices.Count > 0)
            {
                for (int i = 0; i < m_InputDevices.Count; i++)
                {
                    // The device is connected.
                    if (m_InputDevices[i].characteristics.Equals(kControllerRightCharacteristics))
                    {
                        // Try to send a haptic impulse (vibration)
                        if (m_InputDevices[i].TryGetHapticCapabilities(out HapticCapabilities hapticCapabilities) && hapticCapabilities.supportsImpulse)
                        {
                            // Send haptic impulse (channel 0 is standard for most controllers)
                            m_InputDevices[i].SendHapticImpulse(0, amplitude, duration);
                            return true; // Successfully sent the buzz
                        }
                    }
                }
            }
             return false; // Failed to find or buzz the controller
        }
        
    }

    public class eyeHead_utils{
        public static Vector2 vec2ficks (float x, float y, float z) {
        float r = Mathf.Sqrt (Mathf.Pow (x, 2) + Mathf.Pow (y, 2) + Mathf.Pow (z, 2));
        float az = Mathf.Atan2 (x, z);
        float pol = Mathf.Acos (y / r);
        return new Vector2 (rad2deg (az), 90 - rad2deg (pol));
        }

        public static Vector3 ficks2vec(float az, float pol){
            // Per Baekgaard 2022
            // Calculate a unit vector from Ficks Angle coordinates, see vec2ficks for more comments
            var z = Mathf.Sin(deg2rad(90-pol)) * Mathf.Cos(deg2rad(az));
            var x = Mathf.Sin(deg2rad(90-pol)) * Mathf.Sin(deg2rad(az));
            var y = Mathf.Cos(deg2rad(90-pol));
            return new Vector3 (x,y,z);
        }


        public static Vector3 vis2vec(float az, float pol){
            var x = Mathf.Cos(deg2rad(90-az));
            var y = Mathf.Cos(deg2rad(90-pol));
            var z = Mathf.Sqrt(1-x*x- y*y);
            return new Vector3 (x,y,z);
        }


        public static float rad2deg (float rad) {
            return (180 / Mathf.PI) * rad;
        }

        public static float deg2rad (float deg) {
            return (Mathf.PI / 180) * deg;
        }

        public static float calc_isv (Vector3 X0, Vector3 X, float dt){
            
            var uPlusV = X + X0;
            var uMinusV = X-X0;
            var isa = 2 * Mathf.Atan2(Vector3.Magnitude(uMinusV), Vector3.Magnitude(uPlusV));
            float isv = rad2deg(isa) / dt;

            return isv;
        }

        public static float[] updateArrayWithNewSample(float[] _array, float newSample){
        //update last 5 samples
        for (int i = 0; i < _array.Length-1; i++) {
            _array[i] = _array[i + 1];
        }
        _array[_array.Length-1] = newSample;

        return _array;
        }

        public static float calc5pointVel(float[] _array, float dt){
            float vel = (-_array[0] - _array[1] + _array[3] + _array[4]) / (6 * dt);
            return vel;
        }

        public static float GetMedian(float[] sourceNumbers) {
            //Framework 2.0 version of this method. there is an easier way in F4        
            if (sourceNumbers == null || sourceNumbers.Length == 0)
                throw new System.Exception("Median of empty array not defined.");

            //make sure the list is sorted, but use a new array
            float[] sortedPNumbers = (float[])sourceNumbers.Clone();
            Array.Sort(sortedPNumbers);

            //get the median
            int size = sortedPNumbers.Length;
            int mid = size / 2;
            float median = (size % 2 != 0) ? (float)sortedPNumbers[mid] : ((float)sortedPNumbers[mid] + (float)sortedPNumbers[mid - 1]) / 2;
            return median;
        }

        public static Vector3  GetMeanOfQueue(Queue<Vector3> items){
            Vector3 sum = new Vector3(0f,0f,0f);
            foreach (Vector3 item in items)
            {
                sum += item;
            }
            Vector3 mean = sum / items.Count;
            return mean;
        }

        public static float Compute5ptVelocity(List<float> xs)
        {
            if (xs == null || xs.Count < 5) return 0f;
            int endInd = xs.Count-1;
            return (-xs[endInd] + 8 * xs[endInd-1] - 8 * xs[endInd-3] + xs[endInd-4]) / (12 * Time.deltaTime);
        }

        public static float Compute5ptAcceleration(List<float> xs)
        {
            if (xs == null || xs.Count < 5) return 0f;
            int endInd = xs.Count-1;
            return (-xs[endInd] + 16 * xs[endInd-1] - 30*xs[endInd-2] + 16 * xs[endInd-3] - xs[endInd-4]) / (12 * Time.deltaTime * Time.deltaTime);
        }

        public static float Compute5ptJolt(List<float> xs)
        {
            if (xs == null || xs.Count < 5) return 0f;
            int endInd = xs.Count-1;
            return (xs[endInd] -2 * xs[endInd-1] + 2* xs[endInd-3] - xs[endInd-4]) / (2 * Time.deltaTime * Time.deltaTime * Time.deltaTime);
        }

        public static float NormalizeAngle(float angle)
        {
            if (angle > 180f) return angle - 360f;
            else if (angle < -180f) return angle + 360f;
            return angle;
        }

        // Method to compute angular velocity using the 5-point method
        public static Vector3 ComputeAngularVelocity(Vector3[] rotations)
        {
            return new Vector3(
                (-rotations[4].x + 8 * rotations[3].x - 8 * rotations[1].x + rotations[0].x) / (12 * Time.deltaTime),
                (-rotations[4].y + 8 * rotations[3].y - 8 * rotations[1].y + rotations[0].y) / (12 * Time.deltaTime),
                (-rotations[4].z + 8 * rotations[3].z - 8 * rotations[1].z + rotations[0].z) / (12 * Time.deltaTime)
            );
        }

        // Method to compute angular acceleration using the 5-point method
        public static Vector3 ComputeAngularAcceleration(Vector3[] rotations)
        {
            return new Vector3(
                (-rotations[4].x + 16 * rotations[3].x - 30 * rotations[2].x + 16 * rotations[1].x - rotations[0].x) / (12 * Mathf.Pow(Time.deltaTime, 2)),
                (-rotations[4].y + 16 * rotations[3].y - 30 * rotations[2].y + 16 * rotations[1].y - rotations[0].y) / (12 * Mathf.Pow(Time.deltaTime, 2)),
                (-rotations[4].z + 16 * rotations[3].z - 30 * rotations[2].z + 16 * rotations[1].z - rotations[0].z) / (12 * Mathf.Pow(Time.deltaTime, 2))
            );
        }

        public static float RMS(List<float> x, List<float> y)
        {
            if (x.Count < 2) return 0f;

            float sumSquaredDiff = 0f;
            for (int i = 0; i < x.Count - 1; i++)
            {
                sumSquaredDiff += Mathf.Pow(x[i + 1] - x[i], 2);
                sumSquaredDiff += Mathf.Pow(y[i + 1] - y[i], 2);
            }

            float meanSquaredDiff = sumSquaredDiff / (x.Count - 1);
            return Mathf.Sqrt(meanSquaredDiff);
        }
    }

    public class IDTSession
    {
        private List<Vector2> angleHistory = new List<Vector2>();
        private float fixationStartTime = -1f;
        private bool isFixating = false;

        private float threshMulitplier;
        private float threshDur;
        private string dispersionType;
        // New: Current fixation centroid, updated continuously during fixation
        private Vector2 currentFixationCentroid = Vector2.zero;

        // New: List to store previous fixation centroids, updated when a fixation breaks
        private List<Vector2> previousFixationCentroids = new List<Vector2>();
        private const int MAX_PREVIOUS_FIXATIONS = 5;

        public IDTSession()
        {

        }

        public bool IDT(float currentXAngle, float currentYAngle, float thresh_disp, float thresh_dur, string dispersionType)
        {
            angleHistory.Add(new Vector2(currentXAngle, currentYAngle));

            if (angleHistory.Count < 7)
            {
                return false;
            }

            float dispersion = CalculateDispersion(dispersionType);
            // Debug.LogWarning("dispersion: " + dispersion);

            if (dispersion < thresh_disp)
            {
                if (!isFixating)
                {
                    fixationStartTime = Time.time;
                    isFixating = true;
                }
                else if (Time.time - fixationStartTime >= thresh_dur)
                {
                    UpdateCurrentFixationCentroid();
                    Debug.Log("Fixation Detected!");
                    return true;
                }
            }
            else
            {
                if (isFixating)
                {
                    UpdatePreviousFixationCentroids();
                    angleHistory = new List<Vector2> { new Vector2(currentXAngle, currentYAngle) };
                }
                else
                {
                    angleHistory.RemoveAt(0);
                }

                isFixating = false;
                fixationStartTime = -1f;
            }

            return false;
        }

        private float CalculateDispersion(string dispersionType)
        {
            if (dispersionType == "disp")
            {
                if (angleHistory.Count <= 1) return 0f;

                float maxX = float.MinValue, minX = float.MaxValue;
                float maxY = float.MinValue, minY = float.MaxValue;

                foreach (var angle in angleHistory)
                {
                    maxX = Mathf.Max(maxX, angle.x);
                    minX = Mathf.Min(minX, angle.x);
                    maxY = Mathf.Max(maxY, angle.y);
                    minY = Mathf.Min(minY, angle.y);
                }

                return (maxX - minX) + (maxY - minY);
            }
            else if (dispersionType == "var")
            {
                if (angleHistory.Count <= 1) return 0f;

                float meanX = 0f, meanY = 0f;
                foreach (var angle in angleHistory)
                {
                    meanX += angle.x;
                    meanY += angle.y;
                }

                meanX /= angleHistory.Count;
                meanY /= angleHistory.Count;

                float xVariance = 0f, yVariance = 0f;
                foreach (var angle in angleHistory)
                {
                    xVariance += Mathf.Pow(angle.x - meanX, 2);
                    yVariance += Mathf.Pow(angle.y - meanY, 2);
                }

                xVariance /= angleHistory.Count;
                yVariance /= angleHistory.Count;

                return Mathf.Sqrt(xVariance + yVariance);
            }
            else
            {
                Debug.LogError("Invalid dispersion type: " + dispersionType);
                return 0f;
            }
        }

        // New: Method to update the current fixation centroid
        private void UpdateCurrentFixationCentroid()
        {
            if (angleHistory.Count == 0)
            {
                currentFixationCentroid = Vector2.zero; // Or a sentinel value like Vector2.negativeInfinity
                return;
            }

            Vector2 centroid = Vector2.zero;
            foreach (var angle in angleHistory)
            {
                centroid += angle;
            }
            currentFixationCentroid = centroid / angleHistory.Count;
        }

        private void UpdatePreviousFixationCentroids()
        {
            // Add the last calculated currentFixationCentroid to the previous list
            previousFixationCentroids.Add(currentFixationCentroid);

            // Keep only the last MAX_PREVIOUS_FIXATIONS
            if (previousFixationCentroids.Count > MAX_PREVIOUS_FIXATIONS)
            {
                previousFixationCentroids.RemoveAt(0); // Remove the oldest fixation
            }
            currentFixationCentroid = Vector2.zero;
        }


        public void ResetSession()
        {
            angleHistory.Clear();
            fixationStartTime = -1f;
            isFixating = false;
        }

        public bool IsCurrentlyFixating()
        {
            return isFixating;
        }

        public List<Vector2> GetAngleHistory()
        {
            return new List<Vector2>(angleHistory); // return a copy
        }

        // Public method to get the current fixation centroid
        public Vector2 GetCurrentFixationCentroid()
        {
            return currentFixationCentroid;
        }
        // Public method to get the list of previous fixation centroids
        public List<Vector2> GetPreviousFixationCentroids()
        {
            // Return a copy to prevent external modification of the internal list
            return new List<Vector2>(previousFixationCentroids);
        }

    }
    
}