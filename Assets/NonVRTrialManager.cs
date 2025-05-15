// Full VR Trial Manager Script with Familiarization, Mic Recording, CSV Log, Start/End Screens, Pause, and Fixation Cross (Non-VR Test Version)
// Updated version to meet all new specifications.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using UnityEngine.Networking;
using System.Linq;

public class NonVRTrialManager : MonoBehaviour
{
    [Header("Trial Settings")]
    public Transform spawnPoint;
    public TextAsset trialCSV;
    public float stimulusDuration = 0.6f;
    public float interTrialInterval = 1.0f;
    public float familiarizationDuration = 2.0f;

    [Header("Prefabs")]
    public List<GameObject> objectPrefabs;
    public GameObject fixationCrossPrefab;

    [Header("UI Elements")]
    public Text instructionText;
    public GameObject startScreen;
    public GameObject endScreen;

    [Header("Audio Recording")]
    public string microphoneDevice;
    private AudioClip recording;
    private int trialCounter = 0;

    private class Trial
    {
        public string task;
        public string object_name;
        public string verb_ing;
        public string affordance;
        public string hand_condition;
    }

    private List<Trial> trials;
    private GameObject currentObject;
    private GameObject fixationCross;
    private string csvLogPath;

    void Start()
    {
        microphoneDevice = Microphone.devices[0];
        LoadCSV();
        csvLogPath = Path.Combine(Application.dataPath, "trial_log.csv");
        WriteCsvHeader();
        StartCoroutine(WaitForStartKey());
    }

    void LoadCSV()
    {
        trials = new List<Trial>();
        var lines = trialCSV.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Trim().Split(',');
            if (parts.Length < 5) continue;
            trials.Add(new Trial {
                task = parts[0],
                object_name = parts[1],
                verb_ing = parts[2],
                affordance = parts[3],
                hand_condition = parts[4]
            });
        }
    }

    IEnumerator WaitForStartKey()
    {
        startScreen.SetActive(true);
        instructionText.text = "Press SPACE to begin the familiarization phase.";
        while (!Input.GetKeyDown(KeyCode.Space))
        {
            yield return null;
        }
        startScreen.SetActive(false);
        StartCoroutine(FamiliarizationPhase());
    }

    IEnumerator FamiliarizationPhase()
    {
        instructionText.text = "Familiarization Phase: Please observe the objects and their associated names/verbs.";

        HashSet<string> seenObjects = new HashSet<string>();
        foreach (var trial in trials)
        {
            if (seenObjects.Contains(trial.object_name)) continue;

            GameObject prefab = objectPrefabs.Find(o => o.name.ToLower().Contains(trial.object_name.ToLower()));
            if (prefab == null)
            {
                Debug.LogWarning("Prefab not found for familiarization: " + trial.object_name);
                continue;
            }

            yield return ShowObject(prefab, trial.object_name);
            yield return ShowObject(prefab, "Look at the object.");
            yield return ShowObject(prefab, trial.verb_ing);
            yield return ShowObject(prefab, "Look at the object.");

            seenObjects.Add(trial.object_name);
        }

        instructionText.text = "Familiarization Complete. Press SPACE to start the trials.";
        while (!Input.GetKeyDown(KeyCode.Space))
        {
            yield return null;
        }
        StartCoroutine(RunTrials());
    }

    IEnumerator ShowObject(GameObject prefab, string instruction)
    {
        instructionText.text = instruction;
        currentObject = Instantiate(prefab, spawnPoint.position, Quaternion.identity);
        yield return new WaitForSeconds(familiarizationDuration);
        Destroy(currentObject);
        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator RunTrials()
    {
        for (int i = 0; i < trials.Count; i++)
        {
            if (i == trials.Count / 2)
            {
                instructionText.text = "You are halfway through. Press SPACE to continue.";
                while (!Input.GetKeyDown(KeyCode.Space))
                {
                    yield return null;
                }
            }

            var trial = trials[i];
            instructionText.text = "Prepare...";

            fixationCross = Instantiate(fixationCrossPrefab, spawnPoint.position + new Vector3(0, 1, 0), Quaternion.identity);
            yield return new WaitForSeconds(1.0f);
            Destroy(fixationCross);

            GameObject prefab = objectPrefabs.Find(o => o.name.ToLower().Contains(trial.object_name.ToLower()));
            if (prefab == null)
            {
                Debug.LogWarning("Prefab not found: " + trial.object_name);
                continue;
            }

            currentObject = Instantiate(prefab, spawnPoint.position, Quaternion.Euler(RandomRotationForAffordance(trial.affordance)));
            string audioFileName = $"trial_{trialCounter}_{trial.task}_{trial.object_name}_{trial.affordance}_{trial.hand_condition}.wav";
            recording = Microphone.Start(microphoneDevice, false, 5, 44100);

            yield return new WaitForSeconds(stimulusDuration);

            Destroy(currentObject);
            yield return new WaitForSeconds(2.0f);
            Microphone.End(microphoneDevice);
            SaveRecordingToFile(audioFileName);

            LogTrialData(trial, audioFileName);

            trialCounter++;
            yield return new WaitForSeconds(interTrialInterval);
        }

        instructionText.text = "Experiment Complete. Thank you!";
        endScreen.SetActive(true);
        Debug.Log("Experiment Complete");
    }

    Vector3 RandomRotationForAffordance(string affordance)
    {
        switch (affordance)
        {
            case "good": return Vector3.zero;
            case "bad1": return new Vector3(0, 90, 0);
            case "bad2": return new Vector3(0, 180, 0);
            case "bad3": return new Vector3(90, 0, 0);
            default: return Vector3.zero;
        }
    }

    void SaveRecordingToFile(string filename)
    {
        string filepath = Path.Combine(Application.dataPath, filename);
        Directory.CreateDirectory(Path.GetDirectoryName(filepath));

        var samples = new float[recording.samples * recording.channels];
        recording.GetData(samples, 0);

        using (FileStream fileStream = new FileStream(filepath, FileMode.Create))
        {
            int sampleCount = samples.Length;
            int headerSize = 44;

            fileStream.Position = 0;

            // RIFF header
            fileStream.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"), 0, 4);
            fileStream.Write(System.BitConverter.GetBytes(headerSize + sampleCount * 2 - 8), 0, 4);
            fileStream.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"), 0, 4);

            // fmt chunk
            fileStream.Write(System.Text.Encoding.UTF8.GetBytes("fmt "), 0, 4);
            fileStream.Write(System.BitConverter.GetBytes(16), 0, 4); // Subchunk1Size
            fileStream.Write(System.BitConverter.GetBytes((ushort)1), 0, 2); // AudioFormat
            fileStream.Write(System.BitConverter.GetBytes((ushort)recording.channels), 0, 2); // NumChannels
            fileStream.Write(System.BitConverter.GetBytes(recording.frequency), 0, 4); // SampleRate
            fileStream.Write(System.BitConverter.GetBytes(recording.frequency * recording.channels * 2), 0, 4); // ByteRate
            fileStream.Write(System.BitConverter.GetBytes((ushort)(recording.channels * 2)), 0, 2); // BlockAlign
            fileStream.Write(System.BitConverter.GetBytes((ushort)16), 0, 2); // BitsPerSample

            // data chunk
            fileStream.Write(System.Text.Encoding.UTF8.GetBytes("data"), 0, 4);
            fileStream.Write(System.BitConverter.GetBytes(sampleCount * 2), 0, 4);

            // Write sample data
            foreach (var sample in samples)
            {
                short intData = (short)(sample * short.MaxValue);
                byte[] bytesData = System.BitConverter.GetBytes(intData);
                fileStream.Write(bytesData, 0, bytesData.Length);
            }
        }

        Debug.Log("Saved: " + filepath);
    }


    void WriteCsvHeader()
    {
        File.WriteAllText(csvLogPath, "Trial,Task,Object,Affordance,HandCondition,AudioFilename\n");
    }

    void LogTrialData(Trial trial, string audioFileName)
    {
        string line = $"{trialCounter},{trial.task},{trial.object_name},{trial.affordance},{trial.hand_condition},{audioFileName}\n";
        File.AppendAllText(csvLogPath, line);
    }
}
