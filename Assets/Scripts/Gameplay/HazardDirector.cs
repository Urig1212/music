using System.Collections.Generic;
using SongSurvival.Core;
using UnityEngine;

namespace SongSurvival.Gameplay
{
    public sealed class HazardDirector
    {
        private readonly Transform root;
        private readonly Sprite sharedSprite;
        private readonly List<GameObject> spawnedObjects = new List<GameObject>();
        private float bassTimer;
        private float sparkTimer;
        private float laneBias;
        private float lastBassX = 10f;

        public HazardDirector(Transform root, Sprite sharedSprite)
        {
            this.root = root;
            this.sharedSprite = sharedSprite;
        }

        public void Tick(DifficultySnapshot difficulty)
        {
            CompactDestroyedHazards();
            bassTimer -= Time.deltaTime;
            sparkTimer -= Time.deltaTime;

            float bassInterval = Mathf.Lerp(1.8f, 0.48f, difficulty.BassPressure);
            float sparkInterval = Mathf.Lerp(0.72f, 0.12f, difficulty.SparkPressure);

            if (bassTimer <= 0f)
            {
                SpawnBassWave(difficulty);
                bassTimer = bassInterval;
            }

            if (sparkTimer <= 0f)
            {
                SpawnSparkFragment(difficulty);
                sparkTimer = sparkInterval;
            }

            if (difficulty.ShockReady)
            {
                SpawnShockRing(difficulty);
            }
        }

        public void Clear()
        {
            foreach (GameObject spawnedObject in spawnedObjects)
            {
                if (spawnedObject != null)
                {
                    Object.Destroy(spawnedObject);
                }
            }

            spawnedObjects.Clear();
            bassTimer = 0f;
            sparkTimer = 0f;
            laneBias = 0f;
            lastBassX = 10f;
        }

        private void SpawnBassWave(DifficultySnapshot difficulty)
        {
            float width = Mathf.Lerp(1.8f, 5.2f, difficulty.BassPressure);
            float x = Mathf.Lerp(-2.8f, 2.8f, laneBias);
            if (Mathf.Abs(x - lastBassX) < 1.6f)
            {
                x += x >= 0f ? -1.8f : 1.8f;
                x = Mathf.Clamp(x, -3.1f, 3.1f);
            }

            laneBias = Mathf.Repeat(laneBias + Random.Range(0.23f, 0.41f), 1f);
            lastBassX = x;

            CreateHazard(
                HazardType.BassWave,
                new Vector3(x, 9.5f, 0f),
                Vector3.down,
                difficulty.WorldSpeed * 0.78f,
                4.5f,
                new Vector3(width, 0.75f, 1f),
                new Color(0.42f, 0.81f, 1f, 0.92f));
        }

        private void SpawnSparkFragment(DifficultySnapshot difficulty)
        {
            float x = Random.Range(-4.1f, 4.1f);
            float y = Random.Range(8.4f, 10.4f);
            float width = Mathf.Lerp(0.35f, 0.7f, difficulty.SparkPressure);
            float height = Mathf.Lerp(0.35f, 1.3f, difficulty.SparkPressure);
            float drift = Random.Range(-0.3f, 0.3f);

            CreateHazard(
                HazardType.SparkFragment,
                new Vector3(x, y, 0f),
                new Vector3(drift, -1f, 0f),
                difficulty.WorldSpeed * 1.24f,
                3.6f,
                new Vector3(width, height, 1f),
                new Color(1f, 0.89f, 0.42f, 0.92f));
        }

        private void SpawnShockRing(DifficultySnapshot difficulty)
        {
            CreateHazard(
                HazardType.ShockRing,
                new Vector3(0f, 8.4f, 0f),
                Vector3.down,
                difficulty.WorldSpeed * 0.92f,
                4.2f,
                new Vector3(8.6f, 0.45f, 1f),
                new Color(1f, 0.44f, 0.62f, 0.95f));
        }

        private void CreateHazard(HazardType hazardType, Vector3 position, Vector3 direction, float speed, float duration, Vector3 scale, Color color)
        {
            GameObject go = new GameObject(hazardType.ToString());
            go.transform.SetParent(root, false);
            go.transform.position = position;

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sharedSprite;
            renderer.sortingOrder = 2;

            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            Hazard hazard = go.AddComponent<Hazard>();
            hazard.Initialize(hazardType, direction, speed, duration, scale, color);

            spawnedObjects.Add(go);
        }

        private void CompactDestroyedHazards()
        {
            for (int i = spawnedObjects.Count - 1; i >= 0; i--)
            {
                if (spawnedObjects[i] == null)
                {
                    spawnedObjects.RemoveAt(i);
                }
            }
        }
    }
}
