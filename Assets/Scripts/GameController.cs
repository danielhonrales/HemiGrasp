using UnityEngine;
using System.IO.Ports;

public class GameController : MonoBehaviour
{

    public GameObject basketball;
    public GameObject basketballPrefab;

    SerialPort port;
    public string portName;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        port = new SerialPort(portName, 115200);
        port.ReadTimeout = 50;
        port.Open();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RespawnBall();
        }
    }

    public void RespawnBall()
    {
        Destroy(basketball);
        basketball = Instantiate(basketballPrefab);
    }

    public void WriteToSerial(string message)
    {
        if (port != null && port.IsOpen)
        {
            port.Write(message);
            Debug.Log("Sent: " + message);
        }
    }
}

public enum BallState
{
    Idle,
    Inflate,
    Hold,
    Dribble,
    Shoot
}
