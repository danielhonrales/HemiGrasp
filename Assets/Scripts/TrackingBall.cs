using UnityEngine;

public class TrackingBall : MonoBehaviour
{

    public bool trackHand;
    public Transform hand;
    public Vector3 trackOffset;
    public GameObject sphere;
    public GameObject otherBall;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        hand = GameObject.Find("[BuildingBlock] Hand Tracking right").transform.Find("Bones").Find("XRHand_Wrist").Find("XRHand_MiddleMetacarpal").Find("XRHand_MiddleProximal");
    }

    // Update is called once per frame
    void Update()
    {
        if (trackHand)
        {
            transform.position = hand.position + trackOffset;
        } else {
            transform.position = new Vector3(sphere.transform.position.x, sphere.transform.position.y + (sphere.transform.localScale.y / 2), sphere.transform.position.z);
        }
        

        if (Vector3.Distance(transform.position, otherBall.transform.position) < 0.01)
        {
            gameObject.GetComponent<Renderer>().material.color = Color.green;
        } else
        {
            gameObject.GetComponent<Renderer>().material.color = Color.white;
        }
    }
}
