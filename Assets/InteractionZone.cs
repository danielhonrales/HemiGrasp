using UnityEngine;

public class InteractionZone : MonoBehaviour
{

    [Header("Interaction"), Space(10)]
    public GameObject targetObject;
    public bool isInteracting;
    public Vector3 positionOffset;
    public Vector3 rotationOffset;

    [Header("Interaction"), Space(10)]
    public Transform hand;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        hand = GameObject.Find("[BuildingBlock] Hand Tracking right").transform.Find("Bones").Find("XRHand_Wrist").Find("XRHand_MiddleMetacarpal").Find("XRHand_MiddleProximal");
    }

    // Update is called once per frame
    void Update()
    {
        if (isInteracting)
        {
            targetObject.transform.SetPositionAndRotation(hand.position + positionOffset, Quaternion.Euler(hand.rotation.eulerAngles + rotationOffset));
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Hand"))
        {
            isInteracting = true;
        }        
    }
}
