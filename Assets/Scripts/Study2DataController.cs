using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Study2DataController : MonoBehaviour
{
    // Base path to participant folders

    [Header("StudyControls"), Space(10)]
    // Participant number
    public int pid = 1;
    public Technique technique;
    public int currentTrial = 1;
    public int totalTrials;


    [Header("FileStuff"), Space(10)]
    public string baseFolder = "Assets/data/study2/p_sheets";
    // CSV file to open
    public string csvPath;

    [Header("References"), Space(10)]
    public TestController testController;

    // In-memory storage of CSV data
    private string[] header;
    private List<string[]> csvData = new List<string[]>();
    private readonly Dictionary<string, int> dataIndex = new()
    {
        {"pid", 0},
        {"trial", 1},
        {"physicalSpeed", 2},
        {"visualSpeed", 3},
        {"congruent", 4},
    };

    void Start()
    {
        
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            LoadData();
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            LoadTrial();
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            NextTrial();
        }
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            RecordResponse(1);
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            RecordResponse(0);
        }
    }

    public void LoadData()
    {
        string participantFolder = Path.Combine(baseFolder, $"p{pid}");
        string csvFileName = $"p{pid}_conditions.csv";
        csvPath = Path.Combine(participantFolder, csvFileName);

        if (!File.Exists(csvPath))
        {
            Debug.LogError("CSV file not found: " + csvPath);
            return;
        }

        // -------------------------------
        // READ CSV
        // -------------------------------
        csvData.Clear();
        string[] lines = File.ReadAllLines(csvPath);

        // Optional: skip header
        header = lines[0].Split(',');
        Debug.Log("CSV Header: " + string.Join(", ", header));

        for (int i = 1; i < lines.Length; i++)
        {
            string[] cells = lines[i].Split(',');
            csvData.Add(cells);
        }

        currentTrial = 0;
        totalTrials = lines.Length - 1;
        LoadTrial();
    }

    public void LoadTrial()
    {
        string[] trialData = csvData[currentTrial];
        Debug.Log($"Trial {trialData[dataIndex["trial"]]}: physicalVelocity={trialData[dataIndex["physicalVelocity"]]}, visualMultiplier={trialData[dataIndex["visualMultiplier"]]}, Congruent={trialData[dataIndex["congruent"]]}");

        testController.sphere.SetActive(true);

        float physicalVal = float.Parse(csvData[currentTrial][dataIndex["physicalVelocity"]]);
        testController.physicalSpeed = Math.Abs(physicalVal);
        testController.dynamicForward = Math.Sign(physicalVal) >= 0;
        testController.visualSpeed = float.Parse(csvData[currentTrial][dataIndex["visualMultiplier"]]) * testController.physicalSpeed;

        testController.DynamicAll();

        //testController.ScaleVisual();
        //testController.ScalePhysical();
    }

    public void NextTrial()
    {
        currentTrial++;

        if (currentTrial >= totalTrials)
        {
            EndSet();
        } 
        else
        {
            LoadTrial();
        }
    }

    public void RecordResponse(int response)
    {
        Debug.Log($"Recording {response} for trial {currentTrial}");
        csvData[currentTrial][dataIndex["congruent"]] = response.ToString();

        SaveData();
        testController.sphere.SetActive(false);

        if (currentTrial + 1 < totalTrials)
        {
            float physicalVal = float.Parse(csvData[currentTrial + 1][dataIndex["physicalVelocity"]]);
            testController.physicalRadiusChange = (Math.Sign(physicalVal) >= 0) ? 0 : 2;
            testController.ScalePhysical();
        }
    }
    
    public void SaveData()
    {
        using (StreamWriter writer = new StreamWriter(csvPath))
        {
            // Write header first
            writer.WriteLine(string.Join(",", header));

            // Write all rows
            foreach (var row in csvData)
            {
                writer.WriteLine(string.Join(",", row));
            }
        }

        Debug.Log("CSV file updated successfully: " + csvPath);
    }

    public void EndSet()
    {
        SaveData();
        Debug.Log("*****Set Finised*****");
        testController.sphere.SetActive(true);
        testController.AlertEnd();
    }

    public enum Technique
    {
        oneHand,
        twoHand
    }
}
