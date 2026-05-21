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

    public enum PhysicalObject {
        Sphere,
        Grapefruit,
        Basketball,
        Football,
        PiggyBank,
        Shell
    }

    public PhysicalObject activeObject = PhysicalObject.Sphere;
    private PhysicalObject previousObject = PhysicalObject.Sphere;

    [Header("TestControl"), Space(10)]
    public int trialNumber;
    [Range(10f, 240f)]
    public float visualRadius;
    [Range(0f, 2f)]
    public float physicalRadiusChange;
    [Range(20f, 120f)]
    public float physicalRadius;
    public bool isDynamic;
    public bool dynamicVisualForward;
    public bool dynamicPhysicalForward;
    public float visualSpeed;
    public float physicalSpeed;
    public bool limitDynamicTime;
    public float dynamicTime;
    public bool bothHands;

    [Header("Calibration"), Space(10)]
    public Vector3 calibOffsetOneHand;
    public Vector3 calibOffsetTwoHand;
    public Vector3 homePos;
    public bool tracking;
    public float dynamicStepDuration;
    public bool changeOffset;

    [Header("References"), Space(10)]
    public GameObject sphere;
    public SerialController serialController;
    //public CongruencyDataController dataController;

    public GameObject grapefruit;
    public GameObject basketball;
    public GameObject football;
    public GameObject piggyBank;
    public GameObject shell;

    public Vector3 grapefruitOffset;
    public Vector3 basketballOffset;
    public Vector3 footballOffset;
    public Vector3 piggyBankOffset;
    public Vector3 shellOffset;

    private float originalOffsety;
    private Transform hand;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        originalOffsety = calibOffsetOneHand.y;
        hand = GameObject.Find("[BuildingBlock] Hand Tracking right").transform.Find("Bones").Find("XRHand_Wrist").Find("XRHand_MiddleMetacarpal").Find("XRHand_MiddleProximal");

        grapefruit.SetActive(false);
        basketball.SetActive(false);
        football.SetActive(false);
        piggyBank.SetActive(false);
        shell.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (previousObject != activeObject) {
            previousObject = activeObject;

            sphere.SetActive(false);
            grapefruit.SetActive(false);
            basketball.SetActive(false);
            football.SetActive(false);
            piggyBank.SetActive(false);
            shell.SetActive(false);

            switch (activeObject) {
                case PhysicalObject.Sphere:
                    sphere.SetActive(true);
                    break;
                case PhysicalObject.Grapefruit:
                    grapefruit.SetActive(true);
                    break;
                case PhysicalObject.Basketball:
                    basketball.SetActive(true);
                    break;
                case PhysicalObject.Football:
                    football.SetActive(true);
                    break;
                case PhysicalObject.PiggyBank:
                    piggyBank.SetActive(true);
                    break;
                case PhysicalObject.Shell:
                    shell.SetActive(true);
                    break;
                default:
                    sphere.SetActive(true);
                    break;
            }
        }

        if (changeOffset) {
            grapefruit.transform.position = sphere.transform.position + grapefruitOffset;
            basketball.transform.position = sphere.transform.position + basketballOffset;
            football.transform.position = sphere.transform.position + footballOffset;
            piggyBank.transform.position = sphere.transform.position + piggyBankOffset;
            shell.transform.position = sphere.transform.position + shellOffset;
        }

        visualRadius = Mathf.Round(visualRadius * 20f) / 20f;
        physicalRadiusChange = Mathf.Round(physicalRadiusChange * 20f) / 20f;
        physicalRadius = Mathf.Round(physicalRadius * 20f) / 20f;

        if (!isDynamic)
        {
            ScaleVisual();
        }
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isDynamic)
            {
                DynamicAll();
            }
            else
            {
                ScaleAll(bothHands);
            }
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            CalibrateVisual();
        }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            visualRadius = Math.Min(visualRadius + 0.1f, 2.0f);
            ScaleVisual();
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            visualRadius = Math.Max(visualRadius - 0.1f, 0.0f);
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
        if (Input.GetKeyDown(KeyCode.X))
        {
            sphere.SetActive(true);
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) {
            serialController.ChangeShape(0);

            sphere.transform.position = homePos;
            grapefruit.transform.position = sphere.transform.position + grapefruitOffset;
            basketball.transform.position = sphere.transform.position + basketballOffset;
            football.transform.position = sphere.transform.position + footballOffset;
            piggyBank.transform.position = sphere.transform.position + piggyBankOffset;
            shell.transform.position = sphere.transform.position + shellOffset;

            float visualOffset = 0.0f;
            if (activeObject == PhysicalObject.Grapefruit) {
                visualOffset = 0.0f;
                grapefruit.transform.position = new Vector3(grapefruit.transform.position.x, grapefruit.transform.position.y + visualOffset, grapefruit.transform.position.z);
            } else if (activeObject == PhysicalObject.Sphere) {
                visualOffset = -0.01f;
                sphere.transform.position = new Vector3(sphere.transform.position.x, sphere.transform.position.y + visualOffset, sphere.transform.position.z);
            } else if (activeObject == PhysicalObject.Basketball) {
                visualOffset = -0.02f;
                basketball.transform.position = new Vector3(basketball.transform.position.x, basketball.transform.position.y + visualOffset, basketball.transform.position.z);
            } else if (activeObject == PhysicalObject.Football) {
                visualOffset = -0.02f;
                football.transform.position = new Vector3(football.transform.position.x, football.transform.position.y + visualOffset, football.transform.position.z);
            } else if (activeObject == PhysicalObject.PiggyBank) {
                visualOffset = 0.0f;
                piggyBank.transform.position = new Vector3(piggyBank.transform.position.x, piggyBank.transform.position.y + visualOffset, piggyBank.transform.position.z);
            } else if (activeObject == PhysicalObject.Shell) {
                visualOffset = -0.01f;
                shell.transform.position = new Vector3(shell.transform.position.x, shell.transform.position.y + visualOffset, shell.transform.position.z);
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha2)) {
            serialController.ChangeShape(1);
            
            sphere.transform.position = homePos;
            grapefruit.transform.position = sphere.transform.position + grapefruitOffset;
            basketball.transform.position = sphere.transform.position + basketballOffset;
            football.transform.position = sphere.transform.position + footballOffset;
            piggyBank.transform.position = sphere.transform.position + piggyBankOffset;
            shell.transform.position = sphere.transform.position + shellOffset;

            float visualOffset = 0.0f;
            if (activeObject == PhysicalObject.Grapefruit) {
                visualOffset = 0.01f;
                grapefruit.transform.position = new Vector3(grapefruit.transform.position.x, grapefruit.transform.position.y + visualOffset, grapefruit.transform.position.z);
            } else if (activeObject == PhysicalObject.Sphere) {
                visualOffset = 0.0f;
                sphere.transform.position = new Vector3(sphere.transform.position.x, sphere.transform.position.y + visualOffset, sphere.transform.position.z);
            } else if (activeObject == PhysicalObject.Basketball) {
                visualOffset = -0.01f;
                basketball.transform.position = new Vector3(basketball.transform.position.x, basketball.transform.position.y + visualOffset, basketball.transform.position.z);
            } else if (activeObject == PhysicalObject.Football) {
                visualOffset = -0.01f;
                football.transform.position = new Vector3(football.transform.position.x, football.transform.position.y + visualOffset, football.transform.position.z);
            } else if (activeObject == PhysicalObject.PiggyBank) {
                visualOffset = 0.01f;
                piggyBank.transform.position = new Vector3(piggyBank.transform.position.x, piggyBank.transform.position.y + visualOffset, piggyBank.transform.position.z);
            } else if (activeObject == PhysicalObject.Shell) {
                visualOffset = 0.0f;
                shell.transform.position = new Vector3(shell.transform.position.x, shell.transform.position.y + visualOffset, shell.transform.position.z);
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha3)) {
            serialController.ChangeShape(2);
            
            sphere.transform.position = homePos;
            grapefruit.transform.position = sphere.transform.position + grapefruitOffset;
            basketball.transform.position = sphere.transform.position + basketballOffset;
            football.transform.position = sphere.transform.position + footballOffset;
            piggyBank.transform.position = sphere.transform.position + piggyBankOffset;
            shell.transform.position = sphere.transform.position + shellOffset;
            
            float visualOffset = 0.0f;
            if (activeObject == PhysicalObject.Grapefruit) {
                visualOffset = 0.02f;
                grapefruit.transform.position = new Vector3(grapefruit.transform.position.x, grapefruit.transform.position.y + visualOffset, grapefruit.transform.position.z);
            } else if (activeObject == PhysicalObject.Sphere) {
                visualOffset = 0.01f;
                sphere.transform.position = new Vector3(sphere.transform.position.x, sphere.transform.position.y + visualOffset, sphere.transform.position.z);
            } else if (activeObject == PhysicalObject.Basketball) {
                visualOffset = 0.0f;
                basketball.transform.position = new Vector3(basketball.transform.position.x, basketball.transform.position.y + visualOffset, basketball.transform.position.z);
            } else if (activeObject == PhysicalObject.Football) {
                visualOffset = 0.0f;
                football.transform.position = new Vector3(football.transform.position.x, football.transform.position.y + visualOffset, football.transform.position.z);
            } else if (activeObject == PhysicalObject.PiggyBank) {
                visualOffset = 0.02f;
                piggyBank.transform.position = new Vector3(piggyBank.transform.position.x, piggyBank.transform.position.y + visualOffset, piggyBank.transform.position.z);
            } else if (activeObject == PhysicalObject.Shell) {
                visualOffset = 0.01f;
                shell.transform.position = new Vector3(shell.transform.position.x, shell.transform.position.y + visualOffset, shell.transform.position.z);
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha4)) {
            serialController.ChangeShape(3);

            sphere.transform.position = homePos;
            grapefruit.transform.position = sphere.transform.position + grapefruitOffset;
            basketball.transform.position = sphere.transform.position + basketballOffset;
            football.transform.position = sphere.transform.position + footballOffset;
            piggyBank.transform.position = sphere.transform.position + piggyBankOffset;
            shell.transform.position = sphere.transform.position + shellOffset;
            
            float visualOffset = 0.0f;
            if (activeObject == PhysicalObject.Grapefruit) {
                visualOffset = 0.02f;
                grapefruit.transform.position = new Vector3(grapefruit.transform.position.x, grapefruit.transform.position.y + visualOffset, grapefruit.transform.position.z);
            } else if (activeObject == PhysicalObject.Sphere) {
                visualOffset = 0.01f;
                sphere.transform.position = new Vector3(sphere.transform.position.x, sphere.transform.position.y + visualOffset, sphere.transform.position.z);
            } else if (activeObject == PhysicalObject.Basketball) {
                visualOffset = 0.0f;
                basketball.transform.position = new Vector3(basketball.transform.position.x, basketball.transform.position.y + visualOffset, basketball.transform.position.z);
            } else if (activeObject == PhysicalObject.Football) {
                visualOffset = 0.0f;
                football.transform.position = new Vector3(football.transform.position.x, football.transform.position.y + visualOffset, football.transform.position.z);
            } else if (activeObject == PhysicalObject.PiggyBank) {
                visualOffset = 0.02f;
                piggyBank.transform.position = new Vector3(piggyBank.transform.position.x, piggyBank.transform.position.y + visualOffset, piggyBank.transform.position.z);
            } else if (activeObject == PhysicalObject.Shell) {
                visualOffset = 0.01f;
                shell.transform.position = new Vector3(shell.transform.position.x, shell.transform.position.y + visualOffset, shell.transform.position.z);
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha5)) {
            serialController.ChangeShape(4);
            
            sphere.transform.position = homePos;
            grapefruit.transform.position = sphere.transform.position + grapefruitOffset;
            basketball.transform.position = sphere.transform.position + basketballOffset;
            football.transform.position = sphere.transform.position + footballOffset;
            piggyBank.transform.position = sphere.transform.position + piggyBankOffset;
            shell.transform.position = sphere.transform.position + shellOffset;
            

            float visualOffset = 0.0f;
            if (activeObject == PhysicalObject.Grapefruit) {
                visualOffset = 0.0f;
                grapefruit.transform.position = new Vector3(grapefruit.transform.position.x, grapefruit.transform.position.y + visualOffset, grapefruit.transform.position.z);
            } else if (activeObject == PhysicalObject.Sphere) {
                visualOffset = -0.01f;
                sphere.transform.position = new Vector3(sphere.transform.position.x, sphere.transform.position.y + visualOffset, sphere.transform.position.z);
            } else if (activeObject == PhysicalObject.Basketball) {
                visualOffset = -0.02f;
                basketball.transform.position = new Vector3(basketball.transform.position.x, basketball.transform.position.y + visualOffset, basketball.transform.position.z);
            } else if (activeObject == PhysicalObject.Football) {
                visualOffset = -0.02f;
                football.transform.position = new Vector3(football.transform.position.x, football.transform.position.y + visualOffset, football.transform.position.z);
            } else if (activeObject == PhysicalObject.PiggyBank) {
                visualOffset = 0.0f;
                piggyBank.transform.position = new Vector3(piggyBank.transform.position.x, piggyBank.transform.position.y + visualOffset, piggyBank.transform.position.z);
            } else if (activeObject == PhysicalObject.Shell) {
                visualOffset = -0.01f;
                shell.transform.position = new Vector3(shell.transform.position.x, shell.transform.position.y + visualOffset, shell.transform.position.z);
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha6)) {
            serialController.ChangeShape(5);
            
            sphere.transform.position = homePos;
            grapefruit.transform.position = sphere.transform.position + grapefruitOffset;
            basketball.transform.position = sphere.transform.position + basketballOffset;
            football.transform.position = sphere.transform.position + footballOffset;
            piggyBank.transform.position = sphere.transform.position + piggyBankOffset;
            shell.transform.position = sphere.transform.position + shellOffset;
            
            float visualOffset = 0.0f;
            if (activeObject == PhysicalObject.Grapefruit) {
                visualOffset = 0.01f;
                grapefruit.transform.position = new Vector3(grapefruit.transform.position.x, grapefruit.transform.position.y + visualOffset, grapefruit.transform.position.z);
            } else if (activeObject == PhysicalObject.Sphere) {
                visualOffset = 0.0f;
                sphere.transform.position = new Vector3(sphere.transform.position.x, sphere.transform.position.y + visualOffset, sphere.transform.position.z);
            } else if (activeObject == PhysicalObject.Basketball) {
                visualOffset = -0.01f;
                basketball.transform.position = new Vector3(basketball.transform.position.x, basketball.transform.position.y + visualOffset, basketball.transform.position.z);
            } else if (activeObject == PhysicalObject.Football) {
                visualOffset = -0.01f;
                football.transform.position = new Vector3(football.transform.position.x, football.transform.position.y + visualOffset, football.transform.position.z);
            } else if (activeObject == PhysicalObject.PiggyBank) {
                visualOffset = 0.01f;
                piggyBank.transform.position = new Vector3(piggyBank.transform.position.x, piggyBank.transform.position.y + visualOffset, piggyBank.transform.position.z);
            } else if (activeObject == PhysicalObject.Shell) {
                visualOffset = 0.0f;
                shell.transform.position = new Vector3(shell.transform.position.x, shell.transform.position.y + visualOffset, shell.transform.position.z);
            }
        }

        if (tracking) {
            sphere.transform.position = new Vector3(hand.position.x + calibOffsetOneHand.x, hand.position.y + calibOffsetOneHand.y, hand.position.z + calibOffsetOneHand.z);
            grapefruit.transform.position = sphere.transform.position + grapefruitOffset;
            basketball.transform.position = sphere.transform.position + basketballOffset;
            football.transform.position = sphere.transform.position + footballOffset;
            piggyBank.transform.position = sphere.transform.position + piggyBankOffset;
            shell.transform.position = sphere.transform.position + shellOffset;
        }
    }

    private void OnApplicationQuit() {
        serialController.GoTo(0, false);
        serialController.GoTo(0, true);
        Thread.Sleep(1000);
        serialController.StopPID();
    }

    public void ScaleAll(bool twoHand = false) {
        ScaleVisual();
        //ScalePhysical(twoHand);

        sphere.transform.position = homePos;
        //float visualOffset = (physicalRadiusChange - visualRadius) * 0.01f;
        float visualOffset = (60f - visualRadius) * 0.001f;
        sphere.transform.position = new Vector3(sphere.transform.position.x, sphere.transform.position.y + visualOffset, sphere.transform.position.z);
    }

    public void ScaleVisual()
    {
        //calibOffsetOneHand.y = originalOffsety;

        //float scaleVal = changeMapping[visualRadiusChange];
        float scaleVal = visualRadius * 0.002f;
        sphere.transform.localScale = new Vector3(scaleVal, scaleVal, scaleVal);
        //if (dataController.fixedFactor == CongruencyDataController.FixedFactor.fixedVolume) {
            //sphere.transform.localPosition = new Vector3(homePos.x, homePos.y - (changeMapping[visualRadiusChange] - 0.1258f) + 0.0150f, homePos.z);
        //calibOffsetOneHand.y = calibOffsetOneHand.y - (changeMapping[visualRadiusChange] - 0.1258f) + 0.0150f;
        //}
    }

    public void CalibrateVisual()
    {
        //if (dataController.technique == CongruencyDataController.Technique.oneHand)
        //{
        //    //sphere.transform.position = hand.position + calibOffsetOneHand;
        //}
        //else
        //{
        //    Vector3 leftHandPos = GameObject.Find("[BuildingBlock] Hand Tracking left").transform.Find("Bones").Find("XRHand_Wrist").Find("XRHand_MiddleMetacarpal").Find("XRHand_MiddleProximal").transform.position;
        //    Vector3 rightHandPos = GameObject.Find("[BuildingBlock] Hand Tracking right").transform.Find("Bones").Find("XRHand_Wrist").Find("XRHand_MiddleMetacarpal").Find("XRHand_MiddleProximal").position;
        //    sphere.transform.position = ((leftHandPos + rightHandPos) / 2) + calibOffsetTwoHand;
        //}
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

    public void DynamicAll() {
        if (dynamicVisualForward) {
            StartCoroutine(DynamicVisual(1));
        } else {
            StartCoroutine(DynamicVisual(0));
        }

        if (dynamicPhysicalForward) {
            StartCoroutine(DynamicPhysical(1));
        } else {
            StartCoroutine(DynamicPhysical(0));
        }
    }

    public IEnumerator DynamicPhysical(int direction)
    {
        float elapsed = 0f;
        float duration = 2.0f / physicalSpeed;

        if (limitDynamicTime)
        {
            duration = dynamicTime;
        }

        float startRadius = (direction == 1) ? 0.0f : 2.0f;
        float endRadius = (direction == 1) ? 2.0f : 0.0f;
        serialController.GoTo((int)(startRadius / 2f * 100), false, true, true);

        while (elapsed <= duration)
        {
            //float t = elapsed / duration;
            float t = elapsed / (2.0f / physicalSpeed);
            physicalRadiusChange = Mathf.Lerp(startRadius, endRadius, t);
            serialController.GoTo((int)(physicalRadiusChange / 2f * 100), false, true, true);

            elapsed += dynamicStepDuration;
            yield return new WaitForSeconds(dynamicStepDuration);
        }

        if (!limitDynamicTime) {
            physicalRadiusChange = endRadius;
            serialController.GoTo((int)(endRadius / 2f * 100), false);
        } else {
            float t = dynamicTime / (2.0f / physicalSpeed);
            physicalRadiusChange = Mathf.Lerp(startRadius, endRadius, t);
            serialController.GoTo((int)(physicalRadiusChange / 2f * 100), false);
        }
    }

    public IEnumerator DynamicVisual(int direction)
    {
        float elapsed = 0f;
        float duration = 2.0f / visualSpeed;
        // float duration = 2.0f / physicalSpeed;

        if (limitDynamicTime)
        {
            duration = dynamicTime;
        }

        float forwardEnd = (visualSpeed / physicalSpeed) * 2;
        float backwardEnd = (forwardEnd - 2) * -1;

        float forwardEndMapped = (forwardEnd * 0.02f) + 0.1258f;
        float backwardEndMapped = (backwardEnd * 0.02f) + 0.1258f;

        // float startScale = (direction == 1) ? changeMapping[0] : changeMapping[2];
        //float endScale = (direction == 1) ? changeMapping[2] : changeMapping[0];

        float startScale = sphere.transform.localScale.x;

        // float endScale;
        // if (direction == 1) {
        //     endScale = forwardEndMapped;
        // } else {
        //     endScale = backwardEndMapped;
        // }

        float endScale = visualRadius * 0.002f;

        sphere.transform.localScale = new Vector3(startScale, startScale, startScale);

        float delay = (1 / physicalSpeed) * dynamicStepDuration;

        yield return new WaitForSeconds((dynamicStepDuration * 1.5f) + delay);

        duration -= delay;

        while (elapsed <= duration)
        {
            //float t = elapsed / duration;
            float t = elapsed / (2.0f / visualSpeed);
            // float t = elapsed / (2.0f / physicalSpeed);

            sphere.transform.localScale = Vector3.Lerp(new Vector3(startScale, startScale, startScale), new Vector3(endScale, endScale, endScale), t);

            sphere.transform.position = homePos;
            // float visualOffset = (physicalRadiusChange - ((Mathf.Lerp(startScale, endScale, t) - 0.1258f) / 0.02f)) * 0.01f;
            // float visualOffset = physicalRadiusChange - (sphere.transform.localScale.y - 0.1258f) * 0.002f;
            float visualOffset = (physicalRadiusChange * 0.02f + 0.1258f) - sphere.transform.localScale.y;
            Debug.Log($"DEBUG! homePos: {homePos} | physicalRadiusChange: {physicalRadiusChange} | visualRadius: {((Mathf.Lerp(startScale, endScale, t) - 0.1258f) / 0.02f)} | visualOffset: {visualOffset}");
            sphere.transform.position = new Vector3(sphere.transform.position.x, sphere.transform.position.y + visualOffset, sphere.transform.position.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // sphere.transform.localScale = new Vector3(endScale, endScale, endScale);
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
        {-0.50f, 0.1158f},
        {-0.40f, 0.1178f},
        {-0.30f, 0.1198f},
        {-0.25f, 0.1208f},
        {-0.20f, 0.1218f},
        {-0.10f, 0.1238f},
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
        {2.50f, 0.1758f}
    };
}
