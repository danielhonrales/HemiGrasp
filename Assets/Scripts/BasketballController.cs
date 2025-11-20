using System;
using System.Collections.Generic;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Rendering;

public class BasketballController : MonoBehaviour
{

    public BallState state;
    public Rigidbody rb;
    public GameController gameController;


    private float pushDownForce = 8f;
    private float bounceForce = 7f;
    private float shootForce = 12f;
    private bool dribbling;
    private float lastImpactVelocity;
    private float maxScale;
    private float xChange;
    private float yChange;
    private float zChange;
    private GameObject leftHand;
    private GameObject rightHand;
    private HashSet<GameObject> handsColliding = new HashSet<GameObject>();

    private int motorSpeed = 200;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        int changeStepCount = 100;
        maxScale = 0.6f;
        xChange = (maxScale - transform.localScale.x) / changeStepCount;
        yChange = (maxScale - transform.localScale.y) / changeStepCount;
        zChange = (maxScale - transform.localScale.z) / changeStepCount;

        leftHand = GameObject.Find("LeftHandAnchor");
        rightHand = GameObject.Find("RightHandAnchor");

        state = BallState.Idle;
        gameController = GameObject.Find("GameController").GetComponent<GameController>();
    }

    // Update is called once per frame
    void Update()
    {
        switch (state)
        {
            case BallState.Idle:
                if (handsColliding.Count == 2) Inflate();
                break;
            case BallState.Hold:
                Vector3 holdPos = GetHoldPos(leftHand.transform.position, rightHand.transform.position);
                if (!dribbling) {
                    if (handsColliding.Count >= 1) {
                        transform.position = holdPos;
                        rb.constraints = RigidbodyConstraints.FreezeAll;
                    }
                } else {
                    transform.position = new Vector3(holdPos.x, transform.position.y, holdPos.z);
                }

                if (Input.GetKeyDown(KeyCode.Space))
                {
                    if (!dribbling) Dribble();
                }

                if (Input.GetKeyDown(KeyCode.S))
                {
                    if (!dribbling) Shoot(leftHand.transform.position, rightHand.transform.position);
                }
                break;
            default:
                Debug.Log("unknown ball state");
                break;
        }
        
    }

    private Vector3 GetHoldPos(Vector3 leftPos, Vector3 rightPos)
    {
        Vector3 handsMidpoint = (leftPos + rightPos) / 2;
        Vector3 rightVector = rightPos - leftPos;           // left→right
        Vector3 forward = Vector3.Cross(rightVector, Vector3.up).normalized;
        return handsMidpoint + forward * 0.1f;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Hand"))
        {
            handsColliding.Add(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("Floor"))
        {
            rb.AddForce(Vector3.up * bounceForce, ForceMode.VelocityChange);
            dribbling = false;
            gameController.WriteToSerial(string.Format("T,{0},{1}", 100, motorSpeed));
            gameController.WriteToSerial(string.Format("M,{0},{1}", 100, motorSpeed));
            gameController.WriteToSerial(string.Format("L,{0},{1}", 100, motorSpeed));
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Hand"))
        {
            handsColliding.Remove(collision.gameObject);
        }
    }

    public void Inflate()
    {
        Vector3 change = new();
        if (transform.localScale.x < maxScale) change.x = xChange;
        if (transform.localScale.y < maxScale) change.y = yChange;
        if (transform.localScale.z < maxScale) change.z = zChange;
        transform.localScale = new Vector3(transform.localScale.x + change.x, transform.localScale.y + change.y, transform.localScale.z + change.z);

        if (transform.localScale.x >= maxScale && transform.localScale.y >= maxScale && transform.localScale.z >= maxScale)
        {
            state = BallState.Hold;
        }

        gameController.WriteToSerial(string.Format("T,{0},{1}", (int)Math.Round(transform.localScale.y / maxScale * 100), motorSpeed));
        gameController.WriteToSerial(string.Format("M,{0},{1}", (int)Math.Round(transform.localScale.x / maxScale * 100), motorSpeed));
        gameController.WriteToSerial(string.Format("L,{0},{1}", (int)Math.Round(transform.localScale.y / maxScale * 100), motorSpeed));
    }   

    public void Dribble()
    {
        dribbling = true;
        transform.position = new Vector3(transform.position.x, transform.position.y - 0.2f, transform.position.z);
        rb.constraints = RigidbodyConstraints.None;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(Vector3.down * pushDownForce, ForceMode.VelocityChange);

        gameController.WriteToSerial(string.Format("T,{0},{1}", 0, motorSpeed));
        gameController.WriteToSerial(string.Format("M,{0},{1}", 0, motorSpeed));
        gameController.WriteToSerial(string.Format("L,{0},{1}", 0, motorSpeed));
    }

    public void Shoot(Vector3 leftPos, Vector3 rightPos)
    {
        Vector3 rightVector = rightPos - leftPos;
        Vector3 forward = Vector3.Cross(rightVector, Vector3.up).normalized;
        Vector3 shootDir = Vector3.Slerp(forward, Vector3.up, 75f / 90f).normalized;

        transform.position = new Vector3(transform.position.x, transform.position.y + 0.2f, transform.position.z);
        rb.constraints = RigidbodyConstraints.None;
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(shootDir * pushDownForce, ForceMode.VelocityChange);

        gameController.WriteToSerial(string.Format("T,{0},{1}", 0, motorSpeed));
        gameController.WriteToSerial(string.Format("M,{0},{1}", 0, motorSpeed));
        gameController.WriteToSerial(string.Format("L,{0},{1}", 0, motorSpeed));
    }
}
