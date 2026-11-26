using UnityEngine;

namespace CuteDivinity.Prototype
{
    /// <summary>Cámara de seguimiento mínima para el greybox (vista 3/4 fija).</summary>
    public class SimpleFollowCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 9f, -7f);
        [SerializeField] float smoothTime = 0.25f;
        [SerializeField] float lookHeight = 1f;

        Vector3 velocity;

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            transform.LookAt(target.position + Vector3.up * lookHeight);
        }
    }
}
