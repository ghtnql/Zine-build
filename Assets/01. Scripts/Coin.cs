using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Coin : MonoBehaviour
{
    int coinCnt = 0;

    private void OnTriggerEnter(Collider other)
    {
        switch (other.tag)
        {
            case "Player":
                GetComponent<AudioSource>().Play();
                float newX;
                float newZ;

                // 코인 갯수에 따라 등장위치 넓이 확장
                if (coinCnt <= 5)
                {
                    newX = Random.Range(-4, 4.0f);
                    newZ = Random.Range(-4, 4.0f);
                }
                else if (coinCnt > 5 && coinCnt <= 15)
                {
                    newX = Random.Range(-9, 9.0f);
                    newZ = Random.Range(-9, 9.0f);
                }
                else
                {
                    newX = Random.Range(-14, 14.0f);
                    newZ = Random.Range(-14, 14.0f);
                }

                if(Mathf.Abs(newX) + Mathf.Abs(newZ) > 27)
                {
                    newX = Random.Range(-13, 13.0f);
                    newZ = Random.Range(-13, 13.0f);
                }

                transform.position = new Vector3(newX, 0, newZ);
                coinCnt++;

                other.SendMessage("AddBody", SendMessageOptions.DontRequireReceiver); // Snake/AddBody 호출
                break;
        }
    }
}
