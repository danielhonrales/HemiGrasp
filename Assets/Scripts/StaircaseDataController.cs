using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class StaircaseDataController : MonoBehaviour
{
    // Base path to participant folders

    [Header("StudyControls"), Space(10)]
    // Participant number
    public int pid = 1;
    public Technique technique;
    public FixedFactor fixedFactor;
    public int currentTrial = 1;

    [Header("StaircaseInfo"), Space(10)]
    public Dictionary<string, int> staircaseDirections = new()
    {
        {"A", 0},
        {"B", 0}
    };

    [Header("FileStuff"), Space(10)]
    public string baseFolder = "Assets/data/p_sheets";
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
        {"staircase", 2},
        {"volumeSize", 3},
        {"visualSize", 4},
        {"response", 5},
        {"reversal", 6},
        {"step", 7},
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
        string csvFileName = $"p{pid}_{technique}_{fixedFactor}.csv";
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
        staircaseDirections["A"] = (pid % 2 == 1) ? 1 : 0;
        staircaseDirections["B"] = (pid % 2 == 1) ? 0 : 1;
        LoadTrial();
        
    }

    public void LoadTrial()
    {
        string[] trialData = csvData[currentTrial];
        Debug.Log($"Trial {trialData[dataIndex["trial"]]}: Staircase={trialData[dataIndex["staircase"]]}, VolumeSize={trialData[dataIndex["volumeSize"]]}, VisualSize={trialData[dataIndex["visualSize"]]}, Response={trialData[dataIndex["response"]]}, Reversal={trialData[dataIndex["reversal"]]}, Step={trialData[dataIndex["step"]]},");
    
        if (currentTrial > 1)
        {
            int prevTrial = GetPrevStaircaseTrial();
            string adaptiveFactor = (fixedFactor == FixedFactor.fixedVolume) ? "visualSize" : "volumeSize";
            Debug.Log($"{csvData[prevTrial][dataIndex[adaptiveFactor]]}, {csvData[prevTrial][dataIndex["step"]]}");
            csvData[currentTrial][dataIndex[adaptiveFactor]] = (float.Parse(csvData[prevTrial][dataIndex[adaptiveFactor]]) + float.Parse(csvData[prevTrial][dataIndex["step"]])).ToString();
        }

        testController.visualRadiusChange = float.Parse(csvData[currentTrial][dataIndex["visualSize"]]);
        testController.physicalRadiusChange = float.Parse(csvData[currentTrial][dataIndex["volumeSize"]]);

        testController.sphere.SetActive(true);
        testController.ScaleVisual();
        testController.ScalePhysical();
    }

    private int GetPrevStaircaseTrial()
    {
        string currentStaircase = csvData[currentTrial][dataIndex["staircase"]];
        int prevTrial = currentTrial;
        while (currentTrial > 0)
        {
            prevTrial--;
            string previousStaircase = csvData[prevTrial][dataIndex["staircase"]];
            if (currentStaircase == previousStaircase)
            {
                return prevTrial;
            }
        }

        return -1;
    }

    public void NextTrial()
    {
        currentTrial++;

        if (IsStaircaseDone())
        {
            if (staircaseDirections["A"] == -1 && staircaseDirections["B"] == -1)
            {
                EndSet();
            } 
            else
            {
                NextTrial();
            }
        } 
        else
        {
            LoadTrial();
        }
    }

    private bool IsStaircaseDone()
    {
        string currentStaircase = csvData[currentTrial][dataIndex["staircase"]];

        int reversalCount = 0;
        for (int i = 0; i < csvData.Count; i++)
        {
            if (csvData[i][dataIndex["staircase"]] == currentStaircase && csvData[i][dataIndex["reversal"]] == "1")
            {
                reversalCount++;
            }
        }

        if (reversalCount >= 10)
        {
            staircaseDirections[currentStaircase] = -1;
            return true;
        }
        return false;
    }

    public void RecordResponse(int response)
    {
        Debug.Log($"Recording {response} for trial {currentTrial}");
        csvData[currentTrial][dataIndex["response"]] = response.ToString();

        string currentStaircase = csvData[currentTrial][dataIndex["staircase"]];
        if (response == staircaseDirections[currentStaircase])
        {
            staircaseDirections[currentStaircase] = (staircaseDirections[currentStaircase] == 0) ? 1 : 0;
            csvData[currentTrial][dataIndex["reversal"]] = 1.ToString();
            csvData[currentTrial][dataIndex["step"]] = GetStepValue(staircaseDirections[currentStaircase]).ToString();
        } else
        {
            csvData[currentTrial][dataIndex["reversal"]] = 0.ToString();
            csvData[currentTrial][dataIndex["step"]] = GetStepValue(staircaseDirections[currentStaircase]).ToString();
        }

        SaveData();
        testController.sphere.SetActive(false);
    }
    
    private float GetStepValue(int staircaseDirection)
    {
        bool hasReversal = false;
        int reversalCol = dataIndex["reversal"];

        for (int i = 0; i < csvData.Count; i++)
        {
            if (csvData[i][reversalCol] == "1")
            {
                hasReversal = true;
                break;
            }
        }

        float stepMagnitude = hasReversal ? 0.1f : 0.2f;
        float signedStep = (staircaseDirection == 1) ? stepMagnitude : -stepMagnitude;
        return signedStep;
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
        testController.AlertEnd();
    }

    public enum Technique
    {
        oneHand,
        twoHand
    }

    public enum FixedFactor
    {
        fixedVolume,
        fixedVisual
    }
}
