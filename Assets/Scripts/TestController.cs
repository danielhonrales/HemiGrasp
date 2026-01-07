using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction.Input;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class TestController : MonoBehaviour
{

    [Header("TestControl"), Space(10)]
    public int trialNumber;
    public HandMode mode;
    [Range(0f, 2f)]
    public float visualRadiusChange;
    [Range(0f, 2f)]
    public float physicalRadiusChange;

    [Header("Calibration"), Space(10)]
    public Vector3 calibOffsetOneHand;
    public Vector3 calibOffsetTwoHand;

    [Header("References"), Space(10)]
    public GameObject sphere;
    public SerialController serialController;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        visualRadiusChange = Mathf.Round(visualRadiusChange * 20f) / 20f;
        ScaleVisual();
        physicalRadiusChange = Mathf.Round(physicalRadiusChange * 20f) / 20f;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ScaleVisual();
            ScalePhysical();
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            CalibrateVisual();
        }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            visualRadiusChange = Math.Min(visualRadiusChange + 0.1f, 2.0f);
            ScaleVisual();
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            visualRadiusChange = Math.Max(visualRadiusChange - 0.1f, 0.0f);
            ScaleVisual();
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            physicalRadiusChange = Math.Min(physicalRadiusChange + 0.1f, 2.0f);
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            physicalRadiusChange = Math.Max(physicalRadiusChange - 0.1f, 0.0f);
        }
    }

    public void ScaleVisual()
    {
        float scaleVal = changeMapping[visualRadiusChange];
        sphere.transform.localScale = new Vector3(scaleVal, scaleVal, scaleVal);
    }

    public void CalibrateVisual()
    {
        if (mode == HandMode.OneHand)
        {

            Vector3 handPos = GameObject.Find("[BuildingBlock] Hand Tracking right").transform.Find("Bones").Find("XRHand_Wrist").Find("XRHand_Palm").transform.position;
            sphere.transform.position = handPos + calibOffsetOneHand;
        } else
        {
            Vector3 leftHandPos = GameObject.Find("[BuildingBlock] Hand Tracking left").transform.Find("Bones").Find("XRHand_Wrist").Find("XRHand_Palm").transform.position;
            Vector3 rightHandPos = GameObject.Find("[BuildingBlock] Hand Tracking right").transform.Find("Bones").Find("XRHand_Wrist").Find("XRHand_Palm").transform.position;
            sphere.transform.position = ((leftHandPos + rightHandPos) / 2) + calibOffsetTwoHand;
        }
    }

    public void ScalePhysical()
    {
        serialController.GoTo((int)(physicalRadiusChange / 2f * 100));
    }

    public void AlertEnd()
    {
        StartCoroutine(AlertEndHelper());
    }
    private IEnumerator AlertEndHelper()
    {
        Color originalColor = sphere.GetComponent<Renderer>().material.color;
        for (int i = 0; i < 3; i++)
        {
            sphere.GetComponent<Renderer>().material.color = Color.red;
            yield return new WaitForSeconds(0.3f);
            sphere.GetComponent<Renderer>().material.color = originalColor;
            yield return new WaitForSeconds(0.3f);
        }
    }

    public enum HandMode
    {
        OneHand,
        TwoHand
    }

    public Dictionary<float, float> changeMapping = new()
    {
        {0.00f, 0.1258f},
        {0.10f, 0.1278f},
        {0.20f, 0.1298f},
        {0.25f, 0.1308f},
        {0.30f, 0.1318f},
        {0.40f, 0.1338f},
        {0.50f, 0.1358f},
        {0.60f, 0.1378f},
        {0.70f, 0.1398f},
        {0.75f, 0.1408f},
        {0.80f, 0.1418f},
        {0.90f, 0.1438f},
        {1.00f, 0.1458f},
        {1.10f, 0.1478f},
        {1.20f, 0.1498f},
        {1.25f, 0.1508f},
        {1.30f, 0.1518f},
        {1.40f, 0.1538f},
        {1.50f, 0.1558f},
        {1.60f, 0.1578f},
        {1.70f, 0.1598f},
        {1.75f, 0.1608f},
        {1.80f, 0.1618f},
        {1.90f, 0.1638f},
        {2.00f, 0.1658f},
    };
}
