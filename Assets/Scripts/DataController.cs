using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DataController : MonoBehaviour
{

    private string conditionFileLocation = "Assets/data/participant_sheets";
    public string fileName;

    public List<Study1Trial> study1Trials;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        study1Trials = new();

        string fullFileName = fileName + ".csv";
        string path = Path.Combine(conditionFileLocation, fullFileName);

        if (!File.Exists(path))
        {
            Debug.LogError("CSV not found: " + path);
            return;
        }

        string[] lines = File.ReadAllLines(path);

        // Optional: read header
        string[] headers = lines[0].Split(',');

        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = lines[i].Split(',');

            // Access by index
            int participantId = int.Parse(cols[0]);
            string techinque = cols[1];
            string fixedFactor = cols[2];
            string threshold = cols[3];
            int trial = int.Parse(cols[4]);

            Study1Trial study1Trial = new(i, participantId, techinque, fixedFactor, threshold, trial);
            study1Trials.Add(study1Trial);
            //Debug.Log($"Row {i}: {participantId}, {techinque}, {fixedFactor}, {threshold}, {trial}");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void LoadConditions(int participantId)
    {
        
    }

    public class Study1Trial {
        int row;
        int participantId;
        string technique;
        string fixedFactor;
        string threshold;
        int trial;

        public Study1Trial(int row, int participantId, string techinque, string fixedFactor, string threshold, int trial)
        {
            this.row = row;
            this.participantId = participantId;
            this.technique = techinque;
            this.fixedFactor = fixedFactor;
            this.threshold = threshold;
            this.trial = trial;
        }
    }
}
