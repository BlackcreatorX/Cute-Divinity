
using UnityEngine;

public class ScreamerTest : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ScreamerManager.Instance.PlayScreamer("1");//linea para invocar un screamer con ID "1" al presionar la tecla 1
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ScreamerManager.Instance.PlayScreamer("2");
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ScreamerManager.Instance.PlayScreamer("3");
        }
    }
}
