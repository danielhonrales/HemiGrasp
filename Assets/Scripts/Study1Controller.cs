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

public class Study1Controller : MonoBehaviour {

    // Keyboard controls info
    [Header("Keyboard Controls"), Space(10)]
    
    [Header("Space: \t Change visual and physical size (turn table)")]
    [Header("V: \t\t Change visual size")]
    [Header("P: \t\t Change physical size (turn table)")]
    [Header("H: \t\t Set current table position as home (20mm)")]
    [Header("C: \t\t Calibrate")]
    [Header("T: \t\t Toggle tracking")]
    [Header("X: \t\t Toggle sphere visibility")]
    [Header("L: \t\t Load data from CSV")]
    [Header("Left: \t Load trial")]
    [Header("Right: \t Move to next trial")]
    [Header("Up: \t\t Record response \"YES\" (1)")]
    [Header("Down: \t Record response \"NO\" (0)")]

    [Space(20)]

    // Manual control
    [Header("Manual Control"), Space(10)]

    [SerializeField, Range(10f, 240f)]
    private float visualRadius = 60f;

    [SerializeField, Range(20f, 120f)]
    private float physicalRadius = 60f;

    // Calibration
    [Header("Calibration"), Space(10)]

    [SerializeField]
    private Vector3 calibrationOffset;

    private Vector3 homePosition;

    [SerializeField]
    private bool tracking;

    // Study control
    [Header("Data Control"), Space(10)]

    [SerializeField]
    private int pid = 1;

    [SerializeField]
    private int currentTrial = 1;

    [SerializeField]
    private int totalTrials;

    // Study files
    [Header("Study Files"), Space(10)]

    [SerializeField]
    private string baseFolder = "Assets/data/congruency/p_sheets";

    [SerializeField]
    private string csvPath;

    // Serial control
    [Header("Serial Control"), Space(10)]

    [SerializeField]
    private string tablePort = "COM11";
    
    [SerializeField]
    private int baudRate = 115200;

    // References
    [Header("References"), Space(10)]

    public GameObject sphere;
    public TMP_Text instructionText;

    // Non-serialized variables

    private SerialPort tableSerial;
    
    private Transform hand;
    private float originalOffsetY;

    // In-memory CSV data storage
    private string[] header;
    private List<string[]> csvData = new List<string[]>();

    // Instruction text strings
    private string placeText = "Place right hand on the sphere";
    private string liftText = "Lift hand above the sphere";
    private string alignText = "Throughout the study, line up the white dots until they turn green";
    private string calibrationText = "Calibrating...";
    private string congruentText = "Do the visual and physical sizes match?\n[Yes/No]";

    private readonly Dictionary<string, int> dataIndex = new() {
        { "pid", 0 },
        { "trial", 1 },
        { "physicalSize", 2 },
        { "visualSize", 3 },
        { "congruent", 4 },
    };

    void Start() {
        instructionText.text = calibrationText;

        // Initialize serial connection
        tableSerial = new SerialPort(tablePort, baudRate);
        tableSerial.ReadTimeout = 10;
        tableSerial.NewLine = "\n";

        try {
            tableSerial.Open();
            Debug.Log("Table serial connected");
        } catch (Exception e) {
            Debug.LogError($"Table serial error: {e.Message}");
        }

        // Find hand (middle finger) object
        hand = GameObject.Find("[BuildingBlock] Hand Tracking right").transform
                         .Find("Bones")
                         .Find("XRHand_Wrist")
                         .Find("XRHand_MiddleMetacarpal")
                         .Find("XRHand_MiddleProximal");
                         
        // Set initial offset value
        originalOffsetY = calibrationOffset.y;

        // Arduino reset delay
        Debug.Log("Sleeping for 2s for Arduino reset...");
        Thread.Sleep(2000);
        Debug.Log("Done sleeping!");
    }

    void Update() {
        // Round physical radius to nearest 20mm
        physicalRadius = Mathf.Round(physicalRadius * 20f) / 20f;

        // Check for keyboard input
        if (Input.GetKeyDown(KeyCode.Space))        { ScaleAll(); }
        if (Input.GetKeyDown(KeyCode.V))            { ScaleVisual(); }
        if (Input.GetKeyDown(KeyCode.P))            { RotateTable(); }
        if (Input.GetKeyDown(KeyCode.H))            { SetTableHome(); }
        if (Input.GetKeyDown(KeyCode.C))            { CalibrateVisual(); }
        if (Input.GetKeyDown(KeyCode.T))            { ToggleTracking(); }
        if (Input.GetKeyDown(KeyCode.X))            { ToggleSphere(); }
        if (Input.GetKeyDown(KeyCode.L))            { LoadData(); }
        if (Input.GetKeyDown(KeyCode.LeftArrow))    { LoadTrial(); }
        if (Input.GetKeyDown(KeyCode.RightArrow))   { NextTrial(); }
        if (Input.GetKeyDown(KeyCode.UpArrow))      { RecordResponse(1); }
        if (Input.GetKeyDown(KeyCode.DownArrow))    { RecordResponse(0); }

        // Update sphere position if tracking is enabled
        if (tracking) {
            sphere.transform.position = new Vector3(hand.position.x + calibrationOffset.x,
                                                    hand.position.y + calibrationOffset.y,
                                                    hand.position.z + calibrationOffset.z);
        }
    }

    // Automatically close serial connection
    void OnDestroy() {
        if (tableSerial != null && tableSerial.IsOpen) {
            tableSerial.Close();
        }
    }

    // Set the sphere scale and rotate the table to match the set visual and physical radii
    private void ScaleAll() {
        ScaleVisual();
        RotateTable();
    }

    // Set the sphere scale to match the set visual radius
    private void ScaleVisual() {
        float scaleVal = MMtoUnityUnits(visualRadius);
        sphere.transform.localScale = new Vector3(scaleVal, scaleVal, scaleVal);

        // Offset visual to match height
        sphere.transform.position = homePosition;
        float visualOffset = (60f - visualRadius) * 0.001f;
        sphere.transform.position = new Vector3(sphere.transform.position.x,
                                                sphere.transform.position.y + visualOffset,
                                                sphere.transform.position.z);
    }

    // Rotate the table to match the set physical radius
    private void RotateTable() {
        switch (physicalRadius) {
            case 20:
                tableSerial.WriteLine("MOVE,0");
                break;
            case 40:
                tableSerial.WriteLine("MOVE,4");
                break;
            case 60:
                tableSerial.WriteLine("MOVE,2");
                break;
            case 80:
                tableSerial.WriteLine("MOVE,3");
                break;
            case 100:
                tableSerial.WriteLine("MOVE,5");
                break;
            case 120:
                tableSerial.WriteLine("MOVE,1");
                break;
            default:
                Debug.LogError($"Physical radius {physicalRadius} is not valid!");
                break;
        }
    }

    // Set the current table position as its home position
    private void SetTableHome() {
        tableSerial.WriteLine("HOME");
    }

    // Save the current sphere location as its home position
    private void CalibrateVisual() {
        homePosition = sphere.transform.position;
        instructionText.text = alignText;
    }

    // Toggle if the sphere is tracked to the hand
    private void ToggleTracking() {
        tracking = !tracking;
    }

    // Toggle sphere visibility
    private void ToggleSphere() {
        sphere.SetActive(!sphere.activeSelf);

        if (sphere.activeSelf) {
            instructionText.text = placeText;
            StartCoroutine(WaitThenAsk());
        } else {
            instructionText.text = liftText;
        }
    }

    // Wait 3 seconds before displaying the congruency question text
    private IEnumerator WaitThenAsk() {
        yield return new WaitForSeconds(3.0f);
        instructionText.text = congruentText;
    }

    // Load trial data from given CSV file
    private void LoadData() {
        string participantFolder = Path.Combine(baseFolder, $"p{pid}");
        string csvFileName = $"p{pid}_conditions.csv";
        csvPath = Path.Combine(participantFolder, csvFileName);

        // Check for conditions CSV file
        if (!File.Exists(csvPath)) {
            Debug.LogError($"CSV file not found! Path: {csvPath}");
            return;
        }

        // Read CSV
        csvData.Clear();
        string[] lines = File.ReadAllLines(csvPath);

        // Log header (then skip)
        header = lines[0].Split(',');
        Debug.Log($"CSV Header: {string.Join(',', header)}");

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
        string physicalSize = trialData[dataIndex["physicalSize"]];
        string visualSize = trialData[dataIndex["visualSize"]];
        string congruent = trialData[dataIndex["congruent"]];

        Debug.Log($"Trial {trialNum}: physicalSize={physicalSize}, visualSize={visualSize}, congruent={congruent}");

        // Set physical and visual radii
        physicalRadius = float.Parse(physicalSize);
        visualRadius = physicalRadius * (float.Parse(visualSize) / 100f);
    }

    // Move to next trial
    private void NextTrial() {
        currentTrial++;

        // Check for last trial
        if (currentTrial >= totalTrials) {
            SaveData();
            Debug.Log("***** Set Finished! *****");
            StartCoroutine(AlertEnd());
        } else {
            LoadTrial();
        }
    }

    // Record user's response in the CSV
    private void RecordResponse(int response) {
        Debug.Log($"Recording {response} for trial {currentTrial}");
        csvData[currentTrial][dataIndex["congruent"]] = response.ToString();

        // Save updated CSV
        SaveData();

        // Disable sphere visibility
        ToggleSphere();
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
