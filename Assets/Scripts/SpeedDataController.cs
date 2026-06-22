using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SpeedDataController : MonoBehaviour
{
    // Base path to participant folders

    [Header("StudyControls"), Space(10)]
    // Participant number
    public int pid = 1;
    public int currentTrial = 1;
    public int totalTrials;

    public bool isGrowBlock;


    [Header("FileStuff"), Space(10)]
    public string baseFolder = "Assets/data/study2_speed/p_sheets";
    // CSV file to open
    public string csvPath;

    [Header("References"), Space(10)]
    public DynamicController dynamicController;

    // In-memory storage of CSV data
    private string[] header;
    private List<string[]> csvData = new List<string[]>();
    private readonly Dictionary<string, int> dataIndex = new()
    {
        {"pid", 0},
        {"trial", 1},
        {"physical", 2},
        {"visual", 3},
        {"direction", 4},
        {"congruency", 5},
    };

    void Start()
    {
        
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L)) { LoadData(); }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) { LoadTrial(); }
        if (Input.GetKeyDown(KeyCode.RightArrow)) { NextTrial(); }
        if (Input.GetKeyDown(KeyCode.UpArrow)) { RecordResponse(1); }
        if (Input.GetKeyDown(KeyCode.DownArrow)) { RecordResponse(0); }
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
        Debug.Log($"[DATA] Trial {trialData[dataIndex["trial"]]}: physical={trialData[dataIndex["physical"]]}, visual={trialData[dataIndex["visual"]]}, congruency={trialData[dataIndex["congruency"]]}");

        // if (trialData[dataIndex["direction"]] == "grow") {
        //     dynamicController.physicalSpeed = float.Parse(csvData[currentTrial][dataIndex["physical"]]);
        //     dynamicController.visualSpeed = dynamicController.physicalSpeed * (float.Parse(csvData[currentTrial][dataIndex["visual"]]) / 100f);
        // } else if (trialData[dataIndex["direction"]] == "shrink") {
        //     dynamicController.physicalSpeed = -1 * float.Parse(csvData[currentTrial][dataIndex["physical"]]);
        //     dynamicController.visualSpeed = dynamicController.physicalSpeed * (float.Parse(csvData[currentTrial][dataIndex["visual"]]) / 100f);
        // }

        if (isGrowBlock) {
            if (csvData[currentTrial][dataIndex["physical"]] == "0") {
                dynamicController.physicalSpeed = 40f;
                dynamicController.isStaticTrial = true;
            } else {
                dynamicController.sphere.SetActive(true);
                dynamicController.responseIndicator.GetComponent<Renderer>().material.color = Color.blue;
                dynamicController.physicalSpeed = float.Parse(csvData[currentTrial][dataIndex["physical"]]);
                dynamicController.isStaticTrial = false;

                if (dynamicController.physicalSpeed < 12f) {
                    dynamicController.physicalSpeed = 12f;
                }
            }
            dynamicController.visualSpeed = (float.Parse(csvData[currentTrial][dataIndex["visual"]]) - 60f) / (20f / dynamicController.physicalSpeed);
        } else {
            if (csvData[currentTrial][dataIndex["physical"]] == "0") {
                dynamicController.physicalSpeed = -40f;
                dynamicController.isStaticTrial = true;
            } else {
                dynamicController.sphere.SetActive(true);
                dynamicController.responseIndicator.GetComponent<Renderer>().material.color = Color.blue;
                dynamicController.physicalSpeed = float.Parse(csvData[currentTrial][dataIndex["physical"]]);
                dynamicController.isStaticTrial = false;

                if (dynamicController.physicalSpeed > -12f) {
                    dynamicController.physicalSpeed = -12f;
                }
            }
            dynamicController.visualSpeed = (80f - float.Parse(csvData[currentTrial][dataIndex["visual"]])) / (20f / dynamicController.physicalSpeed);
        }
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
        if (dynamicController.sphere.active) {
            Debug.Log($"[DATA] Wait for sphere to disappear before recording response!");
            return;
        }

        Debug.Log($"[DATA] Recording {response} for trial {currentTrial}");
        csvData[currentTrial][dataIndex["congruency"]] = response.ToString();

        SaveData();
        dynamicController.sphere.SetActive(false);
        dynamicController.responseIndicator.GetComponent<Renderer>().material.color = Color.red;

        if (isGrowBlock) {
            dynamicController.ManualReset(62f);
        } else {
            dynamicController.ManualReset(82f);
        }
        
        StartCoroutine(WaitThenNextTrial(0.5f));
    }

    private IEnumerator WaitThenNextTrial(float delay) {
        yield return new WaitForSeconds(delay);
        NextTrial();
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
        Debug.Log("[DATA] *****Set Finished*****");
    }

    public enum Technique
    {
        oneHand,
        twoHand
    }
}
