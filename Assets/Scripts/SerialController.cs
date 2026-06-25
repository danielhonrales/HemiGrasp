using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using System.Collections;

public class SerialController : MonoBehaviour
{
    [Header("Serial Config")]
    public string firstHandPort = "COM6";
    public string secondHandPort = "COM14";
    public int baudRate = 115200;
    public int errorWarning = 50;

    [Header("Runtime")]
    public string activeMotor = "A";

    private SerialPort firstHandSerial;
    private SerialPort secondHandSerial;
    private System.Random rng = new System.Random();

    public DynamicController dynamicController;

    void Start()
    {
        firstHandSerial = new SerialPort(firstHandPort, baudRate);
        firstHandSerial.ReadTimeout = 10;
        firstHandSerial.NewLine = "\n";

        secondHandSerial = new SerialPort(secondHandPort, baudRate);
        secondHandSerial.ReadTimeout = 10;
        secondHandSerial.NewLine = "\n";

        try
        {
            firstHandSerial.Open();
            Debug.Log("First hand serial connected");
        }
        catch (Exception e)
        {
            Debug.LogError("First hand serial error: " + e.Message);
        }

        try {
            secondHandSerial.Open();
            Debug.Log("Second hand serial connected");
        } catch (Exception e) {
            Debug.LogError("Second hand serial error: " + e.Message);
        }

        // Arduino reset delay
        Thread.Sleep(2000);
    }

    void OnDestroy()
    {
        if (firstHandSerial != null && firstHandSerial.IsOpen)
            firstHandSerial.Close();

        if (secondHandSerial != null && secondHandSerial.IsOpen)
            secondHandSerial.Close();
    }

    // ---------------- SERIAL HELPERS ----------------

    void SendCmd(string cmd, bool secondHand)
    {
        if (!secondHand && firstHandSerial != null && firstHandSerial.IsOpen)
        {
            firstHandSerial.WriteLine(cmd);
        }
        else if (secondHandSerial != null && secondHandSerial.IsOpen)
        {
            secondHandSerial.WriteLine(cmd);
        }
    }

    bool GetPosAndError(
        int location, bool secondHand,
        out float m, out float t, out float l,
        out float mErr, out float tErr, out float lErr)
    {
        m = t = l = mErr = tErr = lErr = 0f;

        try
        {
            string line = secondHand 
                ? secondHandSerial.ReadLine() 
                : firstHandSerial.ReadLine();
            string[] values = line.Split(',');

            if (values.Length < 3) return false;

            m = float.Parse(values[0]);
            t = float.Parse(values[1]);
            l = float.Parse(values[2]);

            float target = location * 10f;

            mErr = Mathf.Abs(m - target);
            tErr = Mathf.Abs(t - target);
            lErr = Mathf.Abs(l - target);

            return true;
        }
        catch
        {
            return false;
        }
    }

    // ---------------- MAIN LOGIC ----------------

    public void EnablePID() {
        Debug.Log("ENABLING PID");
        SendCmd("START", false);
    }

    public void DisablePID() {
        Debug.Log("DISABLING PID");
        SendCmd("STOP", false);
    }

    public void DynamicT(int location, int speed)
    {
        Debug.Log($"[Dynamic T] Going to {location} with speed {speed}");
        SendCmd($"DT,{location},{speed}", false);
    }

    public void DynamicM(int location, int speed)
    {
        Debug.Log($"[Dynamic M] Going to {location} with speed {speed}");
        SendCmd($"DM,{location},{speed}", false);
    }

    public void DynamicL(int location, int speed)
    {
        Debug.Log($"[Dynamic L] Going to {location} with speed {speed}");
        SendCmd($"DL,{location},{speed}", false);
    }

    public void GoTo(int location, bool secondHand, bool calibration = true, bool intermediate = false)
    {
        Debug.Log($"GOING TO: {location}");
        SendCmd("START", secondHand);

        if (!calibration)
        {
            int randA = rng.Next(0, 101);
            int randB = rng.Next(0, 101);

            SendCmd($"{activeMotor},{randA}", secondHand);
            Thread.Sleep(250);

            SendCmd($"{activeMotor},{randB}", secondHand);
            Thread.Sleep(250);

            Debug.Log($"Rand pos: A {(randA * 10):0} | B {(randB * 10):0}");
        }

        if (dynamicController.currentShape == DynamicController.Shape.Sphere) {
            SendCmd($"{activeMotor},{location}", secondHand);
        } else if (dynamicController.currentShape == DynamicController.Shape.Tall) {
            SendCmd($"M,{location}", secondHand);
        } else if (dynamicController.currentShape == DynamicController.Shape.Wide) {
            SendCmd($"T,{location}", secondHand);
            SendCmd($"L,{location}", secondHand);
        }

        //Thread.Sleep(1000);

        //SendCmd("STOP");
        //SendCmd("POS");

        if (!intermediate)
            StartCoroutine(Helper(secondHand, location, 50f));

        if (GetPosAndError(location, secondHand, out float m, out float t, out float l,
                                         out float mErr, out float tErr, out float lErr))
        {
            string warning =
                (mErr >= errorWarning || tErr >= errorWarning || lErr >= errorWarning)
                ? " !!!"
                : "";

            Debug.Log($"Positions: M {m:0} | T {t:0} | L {l:0}");
            Debug.Log($"Error:     M {mErr:0} | T {tErr:0} | L {lErr:0}{warning}");
        }
        else
        {
            Debug.LogWarning("Failed to read position");
        }
    }

    public void ChangeShape(int shape, bool secondHand = false)
    {
        Debug.Log($"CHANGING TO: {shape}");
        SendCmd("START", secondHand);

        switch (shape) {
            case 0: // Small 1
                SendCmd("A,0", secondHand);
                break;
            case 1: // Medium 2
                SendCmd("A,50", secondHand);
                break;
            case 2: // Large 3
                SendCmd("A,100", secondHand);
                break;
            case 3: // Convex 4
                SendCmd("T,0", secondHand);
                SendCmd("M,100", secondHand);
                SendCmd("L,0", secondHand);
                break;
            case 4: // Concave 5
                SendCmd("T,100", secondHand);
                SendCmd("M,0", secondHand);
                SendCmd("L,100", secondHand);
                break;
            case 5: // Slope 6
                SendCmd("T,100", secondHand);
                SendCmd("M,50", secondHand);
                SendCmd("L,0", secondHand);
                break;
        }

        StartCoroutine(Helper(secondHand));
    }

    public void StopPID(bool secondHand = false) {
        SendCmd("STOP", secondHand);
    }

    public IEnumerator Helper(bool secondHand = false, int targetLocation = 0, float tolerance = 50f) {
        // float timeout = 10f;
        // float elapsed = 0f;

        // while (elapsed < timeout) {
        //     yield return new WaitForSeconds(0.1f);
        //     elapsed += 0.1f;

        //     if (GetPosAndError(targetLocation, secondHand, out _, out _, out _,
        //                     out float mErr, out float tErr, out float lErr)) {
        //         if (mErr < tolerance && tErr < tolerance && lErr < tolerance) {
        //             Debug.Log("Target reached");
        //             break;
        //         }
        //     }
        // }

        yield return new WaitForSeconds(1f);

        SendCmd("STOP", secondHand);
    }

    public void SpeedCommand(int position, int speed, bool secondHand = false) {
        SendCmd($"S,{position},{speed}", secondHand);
    }

    public void SetSpeedMode(bool speedMode, bool secondHand = false) {
        if (speedMode) {
            SendCmd("SPEED", secondHand);
        } else {
            SendCmd("PID", secondHand);
        }
    }

    public void DynamicCommand(int position, int speed, bool secondHand = false) {
        SendCmd("START", secondHand);

        if (dynamicController.currentShape == DynamicController.Shape.Sphere) {
            SendCmd($"D,{position},{speed}", secondHand);
        } else if (dynamicController.currentShape == DynamicController.Shape.Tall) {
            SendCmd($"DM,{position},{speed}", secondHand);
        } else if (dynamicController.currentShape == DynamicController.Shape.Wide) {
            SendCmd($"DT,{position},{speed}", secondHand);
            SendCmd($"DL,{position},{speed}", secondHand);
        }
    }

    // ---------------- INPUT HELPERS ----------------

    public void SetActiveMotor(string motor)
    {
        activeMotor = motor.ToUpper();
        Debug.Log("Active motor: " + activeMotor);
    }

    public void Home()
    {
        GoTo(0, false);
        GoTo(0, true);
    }

    public void Full()
    {
        GoTo(100, false);
        GoTo(100, true);
    }

    //public void Go25() => GoTo(25);
    //public void Go50() => GoTo(50);
    //public void Go75() => GoTo(75);
}
