using UnityEngine;

namespace SongSurvival.Gameplay
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class Hazard : MonoBehaviour
    {
        private Vector3 direction;
        private float speed;
        private float lifeTime;

        public HazardType HazardType { get; private set; }

        public void Initialize(HazardType hazardType, Vector3 moveDirection, float moveSpeed, float duration, Vector3 scale, Color color)
        {
            HazardType = hazardType;
            direction = moveDirection.normalized;
            speed = moveSpeed;
            lifeTime = duration;
            transform.localScale = scale;

            GetComponent<SpriteRenderer>().color = color;
        }

        private void Update()
        {
            transform.position += direction * (speed * Time.deltaTime);
            lifeTime -= Time.deltaTime;
            if (lifeTime <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
