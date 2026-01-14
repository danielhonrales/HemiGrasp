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
        int location,
        out float m, out float t, out float l,
        out float mErr, out float tErr, out float lErr)
    {
        m = t = l = mErr = tErr = lErr = 0f;

        try
        {
            string line = firstHandSerial.ReadLine();
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

        SendCmd($"{activeMotor},{location}", secondHand);
        //Thread.Sleep(1000);

        //SendCmd("STOP");
        //SendCmd("POS");

        if (!intermediate)
            {
            StartCoroutine(Helper(secondHand));
        }

        if (GetPosAndError(location, out float m, out float t, out float l,
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

    public void StopPID(bool secondHand = false) {
        SendCmd("STOP", secondHand);
    }

    public IEnumerator Helper(bool secondHand = false) {
        yield return new WaitForSeconds(1);
        SendCmd("STOP", secondHand);
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
