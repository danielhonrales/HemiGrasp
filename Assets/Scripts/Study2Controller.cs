using UnityEngine;
using System;
using System.IO;
using System.IO.Ports;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Oculus.Interaction.Input;
using Unity.VisualScripting;
using UnityEngine.InputSystem;
using TMPro;

public class Study2Controller : MonoBehaviour {

    // Keyboard controls info
    [Header("Keyboard Controls"), Space(10)]

    [Header("Space: \t Change visual and physical size")]
    [Header("V: \t\t Change visual size")]
    [Header("P: \t\t Change physical size")]
    [Header("R: \t\t Set visual and physical to 62mm")]
    [Header("F: \t\t Set visual and physical to 82mm")]
    [Header("C: \t\t Calibrate")]
    [Header("A: \t\t Toggle trial active")]
    [Header("T: \t\t Toggle tracking")]
    [Header("X: \t\t Toggle sphere visibility")]
    [Header("L: \t\t Load data from CSV")]
    [Header("Left: \t Load trial")]
    [Header("Right: \t Move to next trial")]
    [Header("Up: \t\t Record response \"YES\" (1)")]
    [Header("Down: \t Record response \"NO\" (0)")]

    [Space(20)]

    [Header("Current Status"), Space(10)]

    [SerializeField]
    private float visualRadius;

    [SerializeField]
    private float physicalRadius;

    [SerializeField]
    private bool trialActive;

    [Header("Manual Control"), Space(10)]

    [SerializeField]
    private float targetVisualRadius;

    [SerializeField]
    private float targetPhysicalRadius;

    [SerializeField]
    private float delayTime;

    [SerializeField]
    private bool twoHand;

    // Calibration
    [Header("Calibration"), Space(10)]

    [SerializeField]
    private Vector3 oneHandCalibOffset;

    [SerializeField]
    private Vector3 twoHandCalibOffset;

    [SerializeField]
    private bool tracking;

    [Header("Data Control"), Space(10)]

    [SerializeField]
    private int pid;

    [SerializeField]
    private int currentTrial;

    [SerializeField]
    private int totalTrials;

    [Header("Study Files"), Space(10)]

    [SerializeField]
    private string baseFolder;

    [SerializeField]
    private string csvPath;

    [Header("Serial Control"), Space(10)]

    [SerializeField]
    private string leftHandPort;
    
    [SerializeField]
    private string rightHandPort;

    [SerializeField]
    private int baudRate = 115200;

    [Header("References"), Space(10)]

    public GameObject sphere;
    public GameObject yesBin;
    public GameObject noBin;

    [Header("Misc. Settings"), Space(10)]

    [SerializeField]
    private float graspTolerance;

    // Non-serialized variables
    private Vector3 homePosition;

    private SerialPort leftHandSerial;
    private SerialPort rightHandSerial;
    
    private Transform leftHand;
    private Transform rightHand;

    private float originalOneHandOffsetY;
    private float originalTwoHandOffsetY;

    // In-memory CSV data storage
    private string[] header;
    private List<string[]> csvData = new List<string[]>();

    private readonly Dictionary<string, int> dataIndex = new() {
        { "pid", 0 },
        { "trial", 1 },
        { "visualSize", 2 },
        { "scenario", 3 },
        { "delayTime", 4 },
        { "direction", 5 },
        { "congruent", 6 }
    };

    void Start() {
        // Initialize serial connections
        leftHandSerial = new SerialPort(leftHandPort, baudRate);
        leftHandSerial.ReadTimeout = 10;
        leftHandSerial.NewLine = "\n";

        try {
            leftHandSerial.Open();
            Debug.Log("[HemiGrasp] Left hand serial connected");
        } catch (Exception e) {
            Debug.LogError($"[HemiGrasp] Left hand serial error: {e.Message}");
        }

        rightHandSerial = new SerialPort(rightHandPort, baudRate);
        rightHandSerial.ReadTimeout = 10;
        rightHandSerial.NewLine = "\n";

        try {
            rightHandSerial.Open();
            Debug.Log("[HemiGrasp] Right hand serial connected");
        } catch (Exception e) {
            Debug.LogError($"[HemiGrasp] Right hand serial error: {e.Message}");
        }

        // Find hand (middle finger) objects
        rightHand = GameObject.Find("[BuildingBlock] Hand Tracking right").transform
                              .Find("Bones")
                              .Find("XRHand_Wrist")
                              .Find("XRHand_MiddleMetacarpal")
                              .Find("XRHand_MiddleProximal");
                         
        leftHand = GameObject.Find("[BuildingBlock] Hand Tracking left").transform
                             .Find("Bones")
                             .Find("XRHand_Wrist")
                             .Find("XRHand_MiddleMetacarpal")
                             .Find("XRHand_MiddleProximal");
                         
        // Set initial offset value
        originalOneHandOffsetY = oneHandCalibOffset.y;
        originalTwoHandOffsetY = twoHandCalibOffset.y;

        // Set default home position
        homePosition = sphere.transform.position;

        // Arduino reset delayTime
        Debug.Log("[HemiGrasp] Sleeping for 2s for Arduino reset...");
        Thread.Sleep(2000);
        Debug.Log("[HemiGrasp] Done sleeping!");
    }

    void Update() {
        // Round physical radius to nearest 20mm
        physicalRadius = Mathf.Round(physicalRadius * 20f) / 20f;

        // Check for keyboard input
        if (Input.GetKeyDown(KeyCode.Space))        { ScaleAll(targetVisualRadius, targetPhysicalRadius); }
        if (Input.GetKeyDown(KeyCode.V))            { ScaleVisual(targetVisualRadius); }
        if (Input.GetKeyDown(KeyCode.P))            { ScalePhysical(targetPhysicalRadius); }
        if (Input.GetKeyDown(KeyCode.R))            { ResetAll(62f, 62f); }
        if (Input.GetKeyDown(KeyCode.F))            { ResetAll(82f, 82f); }
        if (Input.GetKeyDown(KeyCode.C))            { CalibrateVisual(); }
        if (Input.GetKeyDown(KeyCode.A))            { ToggleActive(); }
        if (Input.GetKeyDown(KeyCode.T))            { ToggleTracking(); }
        if (Input.GetKeyDown(KeyCode.X))            { SetSphereVisibility(!sphere.activeSelf); }
        if (Input.GetKeyDown(KeyCode.L))            { LoadData(); }
        if (Input.GetKeyDown(KeyCode.LeftArrow))    { LoadTrial(); }
        if (Input.GetKeyDown(KeyCode.RightArrow))   { NextTrial(); }
        if (Input.GetKeyDown(KeyCode.UpArrow))      { RecordResponse(1); }
        if (Input.GetKeyDown(KeyCode.DownArrow))    { RecordResponse(0); }

        // Update sphere position if tracking is enabled
        if (!twoHand && tracking) {
            float offset = MMtoUnityUnits((physicalRadius - visualRadius) / 2f);
            sphere.transform.position = new Vector3(rightHand.position.x + oneHandCalibOffset.x,
                                                    rightHand.position.y + oneHandCalibOffset.y + offset,
                                                    rightHand.position.z + oneHandCalibOffset.z);
        }

        if (twoHand && tracking) {
            sphere.transform.position = new Vector3(((rightHand.position.x + leftHand.position.x) / 2f) + twoHandCalibOffset.x,
                                                    ((rightHand.position.y + leftHand.position.y) / 2f) + twoHandCalibOffset.y,
                                                    ((rightHand.position.z + leftHand.position.z) / 2f) + twoHandCalibOffset.z);
        }

        // If trial is active, check if object has been grabbed
        if (trialActive && !tracking && !twoHand) {
            if (Vector3.Distance(rightHand.transform.position, sphere.transform.position) < MMtoUnityUnits(visualRadius) * graspTolerance) {
                tracking = true;
                ScalePhysical(targetPhysicalRadius); 
            }
        } else if (trialActive && !tracking && twoHand) {
            if (Vector3.Distance(rightHand.transform.position, sphere.transform.position) < MMtoUnityUnits(visualRadius) * graspTolerance &&
                    Vector3.Distance(leftHand.transform.position, sphere.transform.position) < MMtoUnityUnits(visualRadius) * graspTolerance) {
                tracking = true;
                ScalePhysical(targetPhysicalRadius); 
            }
        }

        // Check if sphere has been put in a response bin
        if (trialActive && yesBin.GetComponent<BoxCollider>().bounds.Contains(sphere.transform.position)) {
            RecordResponse(1);
        }

        if (trialActive && noBin.GetComponent<BoxCollider>().bounds.Contains(sphere.transform.position)) {
            RecordResponse(0);
        }
    }

    // Send raw command to Arduino
    private void SendCmd(string cmd, bool leftHand) {
        if (leftHand && leftHandSerial != null && leftHandSerial.IsOpen) {
            Debug.Log($"[HemiGrasp] Sending command \"{cmd}\" to left hand");
            leftHandSerial.WriteLine(cmd);
        } else if (!leftHand && rightHandSerial != null && rightHandSerial.IsOpen) {
            Debug.Log($"[HemiGrasp] Sending command \"{cmd}\" to right hand");
            rightHandSerial.WriteLine(cmd);
        }
    }

    // Return devices to home position on quit
    private void OnApplicationQuit() {
        Debug.Log("[HemiGrasp] Returning devices to home position");

        SendCmd("START", true);
        SendCmd("A,0", true);

        SendCmd("START", false);
        SendCmd("A,0", false);

        Thread.Sleep(500);

        SendCmd("STOP", true);
        SendCmd("STOP", false);
    }

    // Wait 0.5s then stop both PID controllers
    private IEnumerator WaitThenStop() {
        yield return new WaitForSeconds(0.5f);
        SendCmd("STOP", true);
        SendCmd("STOP", false);
    }

    // Automatically close serial connections
    void OnDestroy() {
        if (leftHandSerial != null && leftHandSerial.IsOpen) {
            Debug.Log("[HemiGrasp] Closing left hand serial");
            leftHandSerial.Close();
        }

        if (rightHandSerial != null && rightHandSerial.IsOpen) {
            Debug.Log("[HemiGrasp] Closing right hand serial");
            rightHandSerial.Close();
        }
    }

    // Scale the visual and physical to the target radii
    private void ScaleAll(float vRadius, float pRadius) {
        ScaleVisual(vRadius);
        ScalePhysical(pRadius);
    }

    // Reset the visual and physical to the target radii
    private void ResetAll(float vRadius, float pRadius) {
        ScaleVisual(vRadius);
        ResetPhysical(pRadius);
    }

    // Set the sphere scale to match the target visual radius
    private void ScaleVisual(float radius) {
        Debug.Log($"[HemiGrasp] Scaling visual to {radius}mm");

        visualRadius = radius;

        sphere.transform.localScale = new Vector3(MMtoUnityUnits(radius),
                                                  MMtoUnityUnits(radius),
                                                  MMtoUnityUnits(radius));
        
        sphere.transform.position = homePosition;

        // Offset visual to match height
        float offset = 0;
        if (!twoHand) {
            offset = MMtoUnityUnits((physicalRadius - visualRadius) / 2f);
        }

        sphere.transform.position = new Vector3(sphere.transform.position.x,
                                                sphere.transform.position.y + offset,
                                                sphere.transform.position.z);
    }

    // Set the device(s) scale to match the target physical radius
    private void ScalePhysical(float radius) {
        Debug.Log($"[HemiGrasp] Scaling physical to {radius}mm");

        physicalRadius = radius;

        int position = MMtoDevice(radius);
        int speed = (int)(20f / delayTime);

        if (speed < 10) {
            speed = 10;
        } else if (speed > 40) {
            speed = 40;
        }

        SendCmd("START", false);
        SendCmd($"D,{position},{speed}", false);

        if (twoHand) {
            SendCmd("START", true);
            SendCmd($"D,{position},{speed}", true);
        }

        StartCoroutine(WaitThenStop());
    }

    private void ResetPhysical(float radius) {
        Debug.Log($"[HemiGrasp] Resetting physical to {radius}mm");

        physicalRadius = radius;

        int position = MMtoDevice(radius);

        SendCmd("START", false);
        SendCmd($"A,{position}", false);

        if (twoHand) {
            SendCmd("START", true);
            SendCmd($"A,{position}", true);
        }

        StartCoroutine(WaitThenStop());
    }

    // Save the current sphere location as its home position
    private void CalibrateVisual() {
        Debug.Log("[HemiGrasp] Setting current sphere location as home position");
        homePosition = sphere.transform.position;
    }

    // Toggle if the trial is active
    private void ToggleActive() {
        trialActive = !trialActive;
    }

    // Toggle if the sphere is tracked to the hand
    private void ToggleTracking() {
        tracking = !tracking;
    }

    // Toggle sphere visibility
    private void SetSphereVisibility(bool on) {
        sphere.SetActive(on);
    }

    // Load trial data from given CSV file
    private void LoadData() {
        string participantFolder = Path.Combine(baseFolder, $"p{pid}");
        string csvFileName = $"p{pid}_conditions.csv";
        csvPath = Path.Combine(participantFolder, csvFileName);

        // Check for conditions CSV file
        if (!File.Exists(csvPath)) {
            Debug.LogError($"[HemiGrasp] CSV file not found! Path: {csvPath}");
            return;
        }

        // Read CSV
        csvData.Clear();
        string[] lines = File.ReadAllLines(csvPath);

        // Log header (then skip)
        header = lines[0].Split(',');
        Debug.Log($"[HemiGrasp] CSV Header: {string.Join(',', header)}");

        // Load CSV data
        for (int i = 1; i < lines.Length; i++) {
            string[] cells = lines[i].Split(',');
            csvData.Add(cells);
        }

        // Reset and load first trial
        currentTrial = 0;
        totalTrials = lines.Length - 1;
        LoadTrial();
    }

    // Load current trial data
    private void LoadTrial() {
        string[] trialData = csvData[currentTrial];

        string trialNum = trialData[dataIndex["trial"]];
        string visualSize = trialData[dataIndex["visualSize"]];
        string scenario = trialData[dataIndex["scenario"]];
        string delay = trialData[dataIndex["delayTime"]];
        string direction = trialData[dataIndex["direction"]];

        Debug.Log($"[HemiGrasp] Loading trial {trialNum}: visualSize={visualSize}, scenario={scenario}, delay={delay}, direction={direction}");

        // Set target visual radius
        targetVisualRadius = float.Parse(visualSize);

        // Set scenario
        if (scenario == "one-hand") {
            twoHand = false;
        } else {
            twoHand = true;
        }

        // Set delay time
        delayTime = float.Parse(delay);

        // Set target physical radius
        if (direction == "expand") {
            targetPhysicalRadius = 82f;
        } else {
            targetPhysicalRadius = 62f;
        }

        // Scale visual, reset sphere position, and enable sphere visibility
        ScaleVisual(targetVisualRadius);
        sphere.transform.position = homePosition;
        SetSphereVisibility(true);

        // Set trial as active after 1 second
        StartCoroutine(WaitThenActive());
    }

    // Set trial as active after 1 second
    private IEnumerator WaitThenActive() {
        yield return new WaitForSeconds(1f);
        trialActive = true;
    }

    // Move to next trial
    private void NextTrial() {
        currentTrial++;

        // Check for last trial
        if (currentTrial >= totalTrials) {
            SaveData();
            Debug.Log("[HemiGrasp] ***** Set Finished! *****");
            StartCoroutine(AlertEnd());
        } else {
            // Reset device(s)
            if (csvData[currentTrial][dataIndex["direction"]] == "expand") {
                ResetPhysical(62f);
            } else {
                ResetPhysical(82f);
            }

            StartCoroutine(WaitThenLoadTrial());
        }
    }

    // Wait 1s to load next trial
    private IEnumerator WaitThenLoadTrial() {
        yield return new WaitForSeconds(1f);
        LoadTrial();
    }

    // Record user's response in the CSV
    private void RecordResponse(int response) {
        // Set trial as no longer active
        trialActive = false;
        tracking = false;

        Debug.Log($"[HemiGrasp] Recording {response} for trial {currentTrial}");
        csvData[currentTrial][dataIndex["congruent"]] = response.ToString();

        // Save updated CSV
        SaveData();

        // Disable sphere visibility and load next trial
        SetSphereVisibility(false);
        sphere.transform.position = homePosition;
        NextTrial();
    }

    // Save data to CSV file
    private void SaveData() {
        using (StreamWriter writer = new StreamWriter(csvPath)) {
            // Write header
            writer.WriteLine(string.Join(',', header));

            // Write all rows
            foreach (var row in csvData) {
                writer.WriteLine(string.Join(',', row));
            }
        }

        Debug.Log($"CSV file updated successfully! Path: {csvPath}");
    }

    // Convert from millimeters to Unity units
    private float MMtoUnityUnits(float mm) {
        return mm * 0.002f;
    }

    // Convert from millimeters to device 0-100
    private int MMtoDevice(float mm) {
        return (int)(Mathf.InverseLerp(62f, 82f, mm) * 100);
    }

    // Alert that the set has ended
    private IEnumerator AlertEnd() {
        Color originalColor = sphere.GetComponent<Renderer>().material.color;

        // Make the sphere flash red 3 times
        for (int i = 0; i < 3; i++) {
            sphere.GetComponent<Renderer>().material.color = Color.red;
            yield return new WaitForSeconds(0.3f);
            sphere.GetComponent<Renderer>().material.color = originalColor;
            yield return new WaitForSeconds(0.3f);
        }
    }
}
