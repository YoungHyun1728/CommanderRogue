using UnityEngine;

// z축으로 회전해야하는 오브젝트에 어태치
public class Rotate : MonoBehaviour
{
    public float rotationSpeed = 100f;
    // Update is called once per frame
    void Update()
    {
        transform.Rotate(0f, 0f, -rotationSpeed * Time.deltaTime);
    }
}
