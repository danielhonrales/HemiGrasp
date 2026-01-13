using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Oculus.Interaction.Input;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class TestController : MonoBehaviour
{

    [Header("TestControl"), Space(10)]
    public int trialNumber;
    [Range(0f, 4f)]
    public float visualRadiusChange;
    [Range(0f, 2f)]
    public float physicalRadiusChange;
    public float visualSpeed;
    public float physicalSpeed;
    public bool bothHands;

    [Header("Calibration"), Space(10)]
    public Vector3 calibOffsetOneHand;
    public Vector3 calibOffsetTwoHand;
    public Vector3 homePos;
    public bool tracking;

    [Header("References"), Space(10)]
    public GameObject sphere;
    public SerialController serialController;
    public CongruencyDataController dataController;

    private float originalOffsety;
    private Transform hand;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        originalOffsety = calibOffsetOneHand.y;
        hand = GameObject.Find("[BuildingBlock] Hand Tracking right").transform.Find("Bones").Find("XRHand_Wrist").Find("XRHand_MiddleMetacarpal").Find("XRHand_MiddleProximal");
    }

    // Update is called once per frame
    void Update()
    {
        visualRadiusChange = Mathf.Round(visualRadiusChange * 20f) / 20f;
        ScaleVisual();
        physicalRadiusChange = Mathf.Round(physicalRadiusChange * 20f) / 20f;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ScaleAll(bothHands);
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

        if (tracking)
            sphere.transform.position = new Vector3(hand.position.x + calibOffsetOneHand.x, hand.position.y + calibOffsetOneHand.y, hand.position.z + calibOffsetOneHand.z);
    }

    private void OnApplicationQuit() {
        serialController.GoTo(0, false);
        serialController.GoTo(0, true);
        Thread.Sleep(1000);
        serialController.StopPID();
    }

    public void ScaleAll(bool twoHand = false) {
        ScaleVisual();
        ScalePhysical(twoHand);

        sphere.transform.position = homePos;
        float visualOffset = (physicalRadiusChange - visualRadiusChange) * 0.01f;
        sphere.transform.position = new Vector3(sphere.transform.position.x, sphere.transform.position.y + visualOffset, sphere.transform.position.z);
    }

    public void ScaleVisual()
    {
        //calibOffsetOneHand.y = originalOffsety;

        float scaleVal = changeMapping[visualRadiusChange];
        sphere.transform.localScale = new Vector3(scaleVal, scaleVal, scaleVal);
        //if (dataController.fixedFactor == CongruencyDataController.FixedFactor.fixedVolume) {
            //sphere.transform.localPosition = new Vector3(homePos.x, homePos.y - (changeMapping[visualRadiusChange] - 0.1258f) + 0.0150f, homePos.z);
        //calibOffsetOneHand.y = calibOffsetOneHand.y - (changeMapping[visualRadiusChange] - 0.1258f) + 0.0150f;
        //}
    }

    public void CalibrateVisual()
    {
        if (dataController.technique == CongruencyDataController.Technique.oneHand)
        {
            //sphere.transform.position = hand.position + calibOffsetOneHand;
        }
        else
        {
            Vector3 leftHandPos = GameObject.Find("[BuildingBlock] Hand Tracking left").transform.Find("Bones").Find("XRHand_Wrist").Find("XRHand_MiddleMetacarpal").Find("XRHand_MiddleProximal").transform.position;
            Vector3 rightHandPos = GameObject.Find("[BuildingBlock] Hand Tracking right").transform.Find("Bones").Find("XRHand_Wrist").Find("XRHand_MiddleMetacarpal").Find("XRHand_MiddleProximal").position;
            sphere.transform.position = ((leftHandPos + rightHandPos) / 2) + calibOffsetTwoHand;
        }
        homePos = sphere.transform.position;
    }

    public void ScalePhysical(bool twoHand = false)
    {
        serialController.GoTo(0, false);
        if (twoHand) { 
            serialController.GoTo(0, true);
        }
        StartCoroutine(PhysicalHelper(twoHand));
    }

    public IEnumerator PhysicalHelper(bool twoHand) {
        yield return new WaitForSeconds(1);
        serialController.GoTo((int)(physicalRadiusChange / 2f * 100), false);

        if (twoHand) {
            serialController.GoTo((int)(physicalRadiusChange / 2f * 100), true);
        }

        //if (dataController.fixedFactor == IDataController.FixedFactor.fixedVisual) {
        //sphere.transform.localPosition = new Vector3(homePos.x, homePos.y + (changeMapping[physicalRadiusChange] - 0.1258f) / 2, homePos.z);
        //}
    }

    public IEnumerator DynamicPhysical()
    {
       yield return null; 
    }

    public IEnumerator DynamicVisual(int direction)
    {
        float elapsed = 0f;
        float duration = 2.0f;

        float startScale = (direction == 1) ? changeMapping[0] : changeMapping[2];
        float endScale = (direction == 1) ? changeMapping[2] : changeMapping[0];
        transform.localScale = new Vector3(startScale, startScale, startScale);

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(new Vector3(startScale, startScale, startScale), new Vector3(endScale, endScale, endScale), t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = new Vector3(endScale, endScale, endScale);
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
        {2.10f, 0.1678f},
        {2.20f, 0.1698f},
        {2.25f, 0.1708f},
        {2.30f, 0.1718f},
        {2.40f, 0.1738f},
        {2.50f, 0.1758f},
        {2.60f, 0.1778f},
        {2.70f, 0.1798f},
        {2.75f, 0.1808f},
        {2.80f, 0.1818f},
        {2.90f, 0.1838f},
        {3.00f, 0.1858f},
        {3.10f, 0.1878f},
        {3.20f, 0.1898f},
        {3.25f, 0.1908f},
        {3.30f, 0.1918f},
        {3.40f, 0.1938f},
        {3.50f, 0.1958f},
        {3.60f, 0.1978f},
        {3.70f, 0.1998f},
        {3.75f, 0.2008f},
        {3.80f, 0.2018f},
        {3.90f, 0.2038f},
        {4.00f, 0.2058f},
    };
}
