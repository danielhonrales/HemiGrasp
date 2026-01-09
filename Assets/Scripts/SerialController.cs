using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using System.Collections;

public class SerialController : MonoBehaviour
{
    [Header("Serial Config")]
    public string portName = "COM6";
    public int baudRate = 115200;
    public int errorWarning = 50;

    [Header("Runtime")]
    public string activeMotor = "A";

    private SerialPort serial;
    private System.Random rng = new System.Random();

    void Start()
    {
        serial = new SerialPort(portName, baudRate);
        serial.ReadTimeout = 10;
        serial.NewLine = "\n";

        try
        {
            serial.Open();
            Debug.Log("Serial connected");
        }
        catch (Exception e)
        {
            Debug.LogError("Serial error: " + e.Message);
        }

        // Arduino reset delay
        Thread.Sleep(2000);
    }

    void OnDestroy()
    {
        if (serial != null && serial.IsOpen)
            serial.Close();
    }

    // ---------------- SERIAL HELPERS ----------------

    void SendCmd(string cmd)
    {
        if (serial != null && serial.IsOpen)
        {
            serial.WriteLine(cmd);
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
            string line = serial.ReadLine();
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

    public void GoTo(int location, bool calibration = true)
    {
        SendCmd("START");

        if (!calibration)
        {
            int randA = rng.Next(0, 101);
            int randB = rng.Next(0, 101);

            SendCmd($"{activeMotor},{randA}");
            Thread.Sleep(250);

            SendCmd($"{activeMotor},{randB}");
            Thread.Sleep(250);

            Debug.Log($"Rand pos: A {(randA * 10):0} | B {(randB * 10):0}");
        }

        SendCmd($"{activeMotor},{location}");
        //Thread.Sleep(1000);

        //SendCmd("STOP");
        //SendCmd("POS");
        StartCoroutine(Helper());

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

    public void StopPID() {
        SendCmd("STOP");
    }

    public IEnumerator Helper() {
        yield return new WaitForSeconds(1);
        SendCmd("STOP");
    }

    // ---------------- INPUT HELPERS ----------------

    public void SetActiveMotor(string motor)
    {
        activeMotor = motor.ToUpper();
        Debug.Log("Active motor: " + activeMotor);
    }

    public void Home()
    {
        GoTo(0);
    }

    public void Full()
    {
        GoTo(100);
    }

    public void Go25() => GoTo(25);
    public void Go50() => GoTo(50);
    public void Go75() => GoTo(75);
}
