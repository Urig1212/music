using System;
using UnityEngine;

namespace SongSurvival.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float minX = -4f;
        [SerializeField] private float maxX = 4f;
        [SerializeField] private float fixedY = -6.2f;

        private Camera cachedCamera;
        private bool isActive;

        public event Action Hit;

        public void Initialize(Camera sceneCamera, float leftBound, float rightBound, float yPosition)
        {
            cachedCamera = sceneCamera;
            minX = leftBound;
            maxX = rightBound;
            fixedY = yPosition;
            transform.position = new Vector3(0f, fixedY, 0f);
        }

        public void SetActive(bool active)
        {
            isActive = active;
            gameObject.SetActive(active);
        }

        private void Update()
        {
            if (!isActive || cachedCamera == null)
            {
                return;
            }

            if (Input.touchCount > 0)
            {
                MoveToScreenPoint(Input.GetTouch(0).position);
            }
            else if (Input.GetMouseButton(0))
            {
                MoveToScreenPoint(Input.mousePosition);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isActive || !other.TryGetComponent(out Hazard _))
            {
                return;
            }

            Hit?.Invoke();
        }

        private void MoveToScreenPoint(Vector3 screenPoint)
        {
            Vector3 world = cachedCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, Mathf.Abs(cachedCamera.transform.position.z)));
            float targetX = Mathf.Clamp(world.x, minX, maxX);
            transform.position = new Vector3(targetX, fixedY, 0f);
        }
    }
}
