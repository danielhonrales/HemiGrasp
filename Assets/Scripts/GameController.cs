using UnityEngine;

public class GameController : MonoBehaviour
{

    public GameObject basketball;
    public GameObject basketballPrefab;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
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
}

public enum BallState
{
    Idle,
    Inflate,
    Hold,
    Dribble,
    Shoot
}
